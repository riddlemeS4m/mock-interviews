using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace sp2023_mis421_mockinterviews.Extensions;

public static class ConfigurationExtensions
{
    public static async Task AddHashiCorpVaultAsync(this WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        if (!(env.IsProduction() || env.IsStaging())) return;

        try
        {
            var tokenFile = Environment.GetEnvironmentVariable("VAULT_TOKEN_FILE")
                ?? "/vault/tokens/app-token";

            string vaultToken;
            if (File.Exists(tokenFile))
            {
                vaultToken = File.ReadAllText(tokenFile).Trim();
            }
            else
            {
                vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN")
                    ?? throw new InvalidOperationException("VAULT_TOKEN environment variable not found.");
            }

            var vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL")
                ?? "http://vault:8200";

            var authMethod = new TokenAuthMethodInfo(vaultToken);
            var vaultClientSettings = new VaultClientSettings(vaultUrl, authMethod);
            var vaultClient = new VaultClient(vaultClientSettings);

            var path = $"mockinterviews/{env.EnvironmentName.ToLowerInvariant()}";
            var secrets = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: path, mountPoint: "secret");

            var vaultConfig = new Dictionary<string, string?>();
            foreach (var kvp in secrets.Data.Data)
            {
                vaultConfig[kvp.Key] = kvp.Value?.ToString();
            }

            builder.Configuration.AddInMemoryCollection(vaultConfig);
        }
        catch (Exception ex)
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger>();
            logger?.LogError(ex, "Failed to load configuration from Vault: {Message}", ex.Message);

            throw;
        }
    }

    public static void AddUserSecrets(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>();
        }
    }
}