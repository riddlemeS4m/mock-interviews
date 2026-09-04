using MockInterviews.Options;

namespace MockInterviews.Extensions;

public static class ApplicationConfigurationExtensions
{
    public static readonly string[] RequiredConfigurationKeys =
    [
        "ConnectionString:DefaultConnection",
        "Email:Provider",
        "SuperUser:Email",
        "SeededAdminPwd"
    ];

    public static void LoadDevelopmentEnvironment(string? workingDirectory = null)
    {
        if (!IsDevelopmentEnvironment())
        {
            return;
        }

        LoadEnvironmentFile(workingDirectory);
    }

    public static void LoadEnvironmentFile(string? workingDirectory = null)
    {
        var environmentFile = FindNearestEnvironmentFile(workingDirectory ?? Directory.GetCurrentDirectory());
        if (environmentFile is not null)
        {
            DotNetEnv.Env.NoClobber().Load(environmentFile);
        }
    }

    public static string? FindNearestEnvironmentFile(string workingDirectory)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(workingDirectory)); directory is not null; directory = directory.Parent)
        {
            var environmentFile = Path.Combine(directory.FullName, ".env");
            if (File.Exists(environmentFile))
            {
                return environmentFile;
            }
        }

        return null;
    }

    public static void ValidateRequiredConfiguration(this IConfiguration configuration)
    {
        var missingKeys = RequiredConfigurationKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();

        var provider = configuration["Email:Provider"];
        if (string.Equals(provider, EmailOptions.SendGridProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration[$"{SendGridOptions.SectionName}:ApiKey"]))
            {
                missingKeys.Add($"{SendGridOptions.SectionName}:ApiKey");
            }
        }
        else if (string.Equals(provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration[$"{SmtpEmailOptions.SectionName}:Host"]))
            {
                missingKeys.Add($"{SmtpEmailOptions.SectionName}:Host");
            }

            if (!int.TryParse(configuration[$"{SmtpEmailOptions.SectionName}:Port"], out var port) || port is < 1 or > 65535)
            {
                missingKeys.Add($"{SmtpEmailOptions.SectionName}:Port");
            }

            var hasUsername = !string.IsNullOrWhiteSpace(configuration[$"{SmtpEmailOptions.SectionName}:Username"]);
            var hasPassword = !string.IsNullOrWhiteSpace(configuration[$"{SmtpEmailOptions.SectionName}:Password"]);
            if (hasUsername != hasPassword)
            {
                missingKeys.Add($"{SmtpEmailOptions.SectionName}:Username and {SmtpEmailOptions.SectionName}:Password (must be configured together)");
            }
        }
        else if (!string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                $"Unsupported Email:Provider '{provider}'. Supported values are {EmailOptions.SendGridProvider} and {EmailOptions.SmtpProvider}.");
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required configuration values: {string.Join(", ", missingKeys)}.");
        }
    }

    private static bool IsDevelopmentEnvironment()
    {
        return string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
    }
}
