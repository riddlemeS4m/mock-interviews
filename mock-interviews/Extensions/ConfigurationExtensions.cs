using Microsoft.Extensions.Configuration;

namespace MockInterviews.Extensions;

public static class ApplicationConfigurationExtensions
{
    public static readonly string[] RequiredConfigurationKeys =
    [
        "ConnectionString:DefaultConnection",
        "SendGrid:ApiKey",
        "SuperUser:Email",
        "SeededAdminPwd"
    ];

    public static void LoadDevelopmentEnvironment(string? workingDirectory = null)
    {
        if (!IsDevelopmentEnvironment())
        {
            return;
        }

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
            .ToArray();

        if (missingKeys.Length > 0)
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
