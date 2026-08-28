using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using MockInterviews.Data.Seeds;
using MockInterviews.Extensions;
using Moq;
using Npgsql;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MockInterviews.IntegrationTests.Infrastructure;

public sealed class MockInterviewsWebApplicationFactory : WebApplicationFactory<MockInterviews.Program>, IAsyncLifetime
{
    private const string IntegrationDatabaseName = "mock_interviews_test_db";
    private readonly string _connectionString;
    private readonly List<SendGridMessage> _sentEmails = [];

    public MockInterviewsWebApplicationFactory()
    {
        // The fixture is constructed before Program.Main runs, so load the local
        // environment file explicitly rather than relying on application startup.
        ApplicationConfigurationExtensions.LoadEnvironmentFile();
        _connectionString = GetIntegrationConnectionString();

        // Program validates configuration before WebApplicationFactory's deferred
        // ConfigureAppConfiguration callback runs. Test-process environment values
        // make that early validation deterministic locally and in CI.
        Environment.SetEnvironmentVariable("ConnectionString__DefaultConnection", _connectionString);
        Environment.SetEnvironmentVariable("SendGrid__ApiKey", "integration-test-key");
        Environment.SetEnvironmentVariable("SuperUser__Email", "admin@example.test");
        Environment.SetEnvironmentVariable("SeededAdminPwd", "Integration123!");
    }

    public IReadOnlyList<SendGridMessage> SentEmails => _sentEmails;

    public async Task InitializeAsync()
    {
        await MigrateDatabaseAsync();

        // The application starts only after migrations have completed successfully.
        using var scope = Services.CreateScope();
        await ResetDatabaseAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });

    public HttpClient CreateAuthenticatedClient(string userId, params string[] roles)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        ValidateIntegrationDatabase(_connectionString);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MockInterviewsDbContext>();
        var tableNames = await context.Database.SqlQueryRaw<string>("""
            SELECT quote_ident(tablename)
            FROM pg_tables
            WHERE schemaname = 'public'
              AND tablename <> '__EFMigrationsHistory'
            """).ToListAsync();

        if (tableNames.Count > 0)
        {
            var truncateSql = "TRUNCATE TABLE " + string.Join(", ", tableNames) + " RESTART IDENTITY CASCADE;";
            await context.Database.ExecuteSqlRawAsync(truncateSql);
        }

        _sentEmails.Clear();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await IdentitySeed.SeedRolesAsync(roleManager);
        var settings = scope.ServiceProvider.GetRequiredService<MockInterviews.Services.SettingsService>();
        await SettingsSeed.SeedSettings(settings);
    }

    public async Task<T> InDatabaseScopeAsync<T>(Func<MockInterviewsDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<MockInterviewsDbContext>());
    }

    public async Task InDatabaseScopeAsync(Func<MockInterviewsDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<MockInterviewsDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionString:DefaultConnection"] = _connectionString,
            ["SendGrid:ApiKey"] = "integration-test-key",
            ["SuperUser:Email"] = "admin@example.test",
            ["SeededAdminPwd"] = "Integration123!"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISendGridClient>();
            var emailClient = new Mock<ISendGridClient>();
            emailClient
                .Setup(client => client.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
                .Callback<SendGridMessage, CancellationToken>((message, _) => _sentEmails.Add(message))
                .ReturnsAsync(new Response(HttpStatusCode.Accepted, null, null));
            services.AddSingleton(emailClient.Object);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                options.DefaultScheme = TestAuthenticationHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationScheme,
                _ => { });
        });
    }

    private async Task MigrateDatabaseAsync()
    {
        ValidateIntegrationDatabase(_connectionString);
        var options = new DbContextOptionsBuilder<MockInterviewsDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        await using var context = new MockInterviewsDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.CloseConnectionAsync();
        await context.Database.MigrateAsync();
    }

    private static string GetIntegrationConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("IntegrationTests__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set IntegrationTests__ConnectionString to a dedicated PostgreSQL integration-test database.");
        }

        ValidateIntegrationDatabase(connectionString);
        return connectionString;
    }

    internal static void ValidateIntegrationDatabase(string connectionString)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.Database, IntegrationDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration test cleanup requires the dedicated '{IntegrationDatabaseName}' database.");
        }

        var host = connection.Host?.Trim().Trim('[', ']') ?? string.Empty;
        var isLoopback = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address));
        if (!isLoopback)
        {
            throw new InvalidOperationException(
                "Integration test cleanup requires a loopback PostgreSQL host (localhost, 127.0.0.1, or ::1).");
        }
    }
}
