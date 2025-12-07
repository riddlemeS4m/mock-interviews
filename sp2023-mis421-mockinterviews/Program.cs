using Serilog;
using sp2023_mis421_mockinterviews.Extensions;

namespace sp2023_mis421_mockinterviews
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddSerilogLogging();

            // Configuration setup
            await builder.AddHashiCorpVaultAsync();
            builder.AddUserSecrets();

            // Add services
            builder.Services.AddForwardedHeaders();
            builder.Services.AddDatabases(builder.Configuration, builder.Environment);
            builder.Services.AddIdentityAndAuth(builder.Configuration, builder.Environment);
            builder.Services.AddSendGrid(builder.Configuration);
            builder.Services.AddGoogleDrive(builder.Configuration, builder.Environment);
            builder.Services.AddApplicationServices();
            builder.Services.AddExternalIntegrations(builder.Configuration);

            var app = builder.Build();

            // Configure pipeline
            app.UseStandardPipeline();

            // Run startup tasks
            await app.UseStartupTasksAsync();

            app.Run();
        }
    }
}
