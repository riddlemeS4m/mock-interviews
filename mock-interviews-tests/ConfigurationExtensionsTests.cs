using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MockInterviews.Areas.SystemArea.Controllers;
using MockInterviews.Controllers;
using MockInterviews.Data;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Extensions;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Options;

namespace MockInterviews.Tests;

public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void RolesConstants_IncludesAndMapsSystemAdminRole()
    {
        var role = Assert.Single(RolesConstants.GetRoleOptions(), option => option.Value == RolesConstants.SystemAdminRole);

        Assert.Equal(RolesConstants.SystemAdminRole, role.Text);
        Assert.Equal(RolesConstants.SystemAdminRole, RolesConstants.GetRoleText(Roles.systemadmin));
    }

    [Fact]
    public void SystemController_IsInSystemAreaAndRequiresSystemAdminRole()
    {
        var controllerType = typeof(SystemController);
        var area = Assert.Single(controllerType.GetCustomAttributes(typeof(AreaAttribute), inherit: true).Cast<AreaAttribute>());
        var authorization = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());

        Assert.Equal("System", area.RouteValue);
        Assert.Equal(RolesConstants.SystemAdminRole, authorization.Roles);
    }

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
    public void DesignTimeFactory_UsesConnectionStringFromEnvironment()
    {
        const string connectionString = "Host=localhost;Database=factory_test;Username=factory_user;Password=factory_password";

        using var environment = new EnvironmentVariablesScope(
            ("ConnectionString__DefaultConnection", connectionString));
        using var context = new MockInterviewsDbContextFactory().CreateDbContext([]);

        Assert.Equal(connectionString, context.Database.GetDbConnection().ConnectionString);
    }

    [Fact]
    public void ValidateRequiredConfiguration_AcceptsCompleteConfiguration()
    {
        var configuration = BuildConfiguration(ApplicationConfigurationExtensions.RequiredConfigurationKeys
            .ToDictionary(key => key, _ => (string?)"configured"));

        configuration.ValidateRequiredConfiguration();
    }

    [Fact]
    public void RequiredConfiguration_ContainsOnlyActiveIntegrations()
    {
        Assert.Equal(
            [
                "ConnectionString:DefaultConnection",
                "SendGrid:ApiKey",
                "SuperUser:Email",
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
        Assert.Contains("SuperUser:Email", exception.Message);
        Assert.Contains("SeededAdminPwd", exception.Message);
        Assert.DoesNotContain("secret-value-that-must-not-appear", exception.Message);
        Assert.DoesNotContain("Postgres:development", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public void TimeslotBackfill_RunsOnlyInDevelopment(string environmentName, bool expected)
    {
        var environment = new TestHostEnvironment { EnvironmentName = environmentName };

        Assert.Equal(expected, StartupTasks.ShouldRunTimeslotBackfill(environment));
    }

    [Fact]
    public void AddSuperUserOptions_RejectsInvalidEmail()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["SuperUser:Email"] = "not-an-email"
        });
        var services = new ServiceCollection();
        services.AddSuperUserOptions(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<IOptions<SuperUserOptions>>().Value);
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

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public void AddIdentityAndAuth_RelaxesPasswordPolicyOnlyInDevelopment(
        string environmentName,
        bool requiresComplexPassword)
    {
        var services = new ServiceCollection();
        services.AddIdentityAndAuth(new TestHostEnvironment { EnvironmentName = environmentName });

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(requiresComplexPassword, options.Password.RequireDigit);
        Assert.Equal(requiresComplexPassword, options.Password.RequireLowercase);
        Assert.Equal(requiresComplexPassword, options.Password.RequireUppercase);
        Assert.Equal(requiresComplexPassword, options.Password.RequireNonAlphanumeric);
        Assert.Equal(6, options.Password.RequiredLength);
    }

    [Fact]
    public void AttemptLogin_RedirectsAnonymousUsersToTheIdentityLoginPage()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())
        };
        var controller = new HomeController(
            null!,
            null!,
            null!,
            NullLogger<HomeController>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new SuperUserOptions { Email = "admin@example.com" }))
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(ConfigurationExtensionsTests);
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
