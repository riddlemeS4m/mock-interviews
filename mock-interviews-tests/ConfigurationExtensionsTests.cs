using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Controllers;
using MockInterviews.Data.Contexts;
using MockInterviews.Extensions;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;

namespace MockInterviews.Tests;

public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void LoadDevelopmentEnvironment_LoadsNearestAncestorFile()
    {
        var variableName = $"CONFIG_TEST_{Guid.NewGuid():N}";
        var root = CreateTemporaryDirectory();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(root, "project"));
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory.FullName, "src"));
        File.WriteAllText(Path.Combine(root, ".env"), $"{variableName}=parent");
        File.WriteAllText(Path.Combine(projectDirectory.FullName, ".env"), $"{variableName}=nearest");

        using var environment = new EnvironmentVariablesScope(
            ("DOTNET_ENVIRONMENT", null),
            ("ASPNETCORE_ENVIRONMENT", "Development"),
            (variableName, null));
        try
        {
            ApplicationConfigurationExtensions.LoadDevelopmentEnvironment(nestedDirectory.FullName);

            Assert.Equal("nearest", Environment.GetEnvironmentVariable(variableName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadDevelopmentEnvironment_IgnoresFilesOutsideDevelopment()
    {
        var variableName = $"CONFIG_TEST_{Guid.NewGuid():N}";
        var root = CreateTemporaryDirectory();
        File.WriteAllText(Path.Combine(root, ".env"), $"{variableName}=file-value");

        using var environment = new EnvironmentVariablesScope(
            ("DOTNET_ENVIRONMENT", "Production"),
            ("ASPNETCORE_ENVIRONMENT", "Production"),
            (variableName, null));
        try
        {
            ApplicationConfigurationExtensions.LoadDevelopmentEnvironment(root);

            Assert.Null(Environment.GetEnvironmentVariable(variableName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadDevelopmentEnvironment_DoesNotOverrideProcessVariables_AndMapsEscapedNewlines()
    {
        var overrideVariable = $"CONFIG_TEST_{Guid.NewGuid():N}";
        var multilineVariable = $"CONFIG_TEST_MULTILINE_{Guid.NewGuid():N}";
        var root = CreateTemporaryDirectory();
        File.WriteAllText(
            Path.Combine(root, ".env"),
            $"{overrideVariable}=file-value{Environment.NewLine}{multilineVariable}=\"first\\nsecond\\n\"");

        using var environment = new EnvironmentVariablesScope(
            ("DOTNET_ENVIRONMENT", "Development"),
            (overrideVariable, "process-value"),
            (multilineVariable, null));
        try
        {
            ApplicationConfigurationExtensions.LoadDevelopmentEnvironment(root);
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            Assert.Equal("process-value", Environment.GetEnvironmentVariable(overrideVariable));
            Assert.Equal("first\nsecond\n", configuration[multilineVariable]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ValidateRequiredConfiguration_AcceptsCompleteConfiguration()
    {
        var configuration = BuildConfiguration(ApplicationConfigurationExtensions.RequiredConfigurationKeys
            .ToDictionary(key => key, _ => "configured"));

        configuration.ValidateRequiredConfiguration();
    }

    [Fact]
    public void RequiredConfiguration_ContainsOnlyActiveIntegrations()
    {
        Assert.Equal(
            [
                "ConnectionString:DefaultConnection",
                "SendGrid:ApiKey",
                "SeededAdminPwd"
            ],
            ApplicationConfigurationExtensions.RequiredConfigurationKeys);
    }

    [Fact]
    public void ValidateRequiredConfiguration_ReportsAllMissingAndWhitespaceKeysWithoutValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionString:DefaultConnection"] = "   "
        });

        var exception = Assert.Throws<InvalidOperationException>(configuration.ValidateRequiredConfiguration);

        Assert.Contains("ConnectionString:DefaultConnection", exception.Message);
        Assert.Contains("SendGrid:ApiKey", exception.Message);
        Assert.DoesNotContain("secret-value-that-must-not-appear", exception.Message);
        Assert.DoesNotContain("Postgres:development", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddIdentityAndAuth_UsesIdentityCookiesWithoutAnExternalProvider()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndAuth();

        using var serviceProvider = services.BuildServiceProvider();
        var schemes = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(IdentityConstants.ApplicationScheme));
        Assert.Equal(IdentityConstants.ApplicationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
        Assert.Null(await schemes.GetSchemeAsync("Microsoft"));
    }

    [Fact]
    public void AttemptLogin_RedirectsAnonymousUsersToTheIdentityLoginPage()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())
        };
        var controller = new HomeController(null!, null!, null!, NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = Assert.IsType<RedirectToPageResult>(controller.AttemptLogin());

        Assert.Equal("/Account/Login", result.PageName);
        Assert.Equal("Identity", result.RouteValues!["area"]);
    }

    [Fact]
    public void CombinedContext_ContainsIdentityAndDomainEntities()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(ApplicationUser)));
        Assert.NotNull(context.Model.FindEntityType(typeof(Interview)));
    }

    [Theory]
    [InlineData(typeof(Interview), nameof(Interview.StudentId))]
    [InlineData(typeof(VolunteerTimeslot), nameof(VolunteerTimeslot.StudentId))]
    [InlineData(typeof(InterviewerSignup), nameof(InterviewerSignup.InterviewerId))]
    [InlineData(typeof(InterviewerLocation), nameof(InterviewerLocation.InterviewerId))]
    public void UserForeignKeys_UseRestrictedDeletion(Type dependentType, string foreignKeyProperty)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(dependentType)!;
        var foreignKey = entityType.GetForeignKeys().Single(key =>
            key.PrincipalEntityType.ClrType == typeof(ApplicationUser)
            && key.Properties.Single().Name == foreignKeyProperty);

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static MockInterviewsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MockInterviewsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_tests;Username=postgres;Password=postgres")
            .Options;
        return new MockInterviewsDbContext(options);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mockinterviews-configuration-tests", Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path).FullName;
    }

    private sealed class EnvironmentVariablesScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentVariablesScope(params (string Name, string? Value)[] values)
        {
            foreach (var (name, value) in values)
            {
                _originalValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
