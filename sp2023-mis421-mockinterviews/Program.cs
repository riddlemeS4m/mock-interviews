using Serilog;
using sp2023_mis421_mockinterviews.Extensions;

namespace sp2023_mis421_mockinterviews
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ApplicationConfigurationExtensions.LoadDevelopmentEnvironment();
            var builder = WebApplication.CreateBuilder(args);

            builder.AddSerilogLogging();
            builder.Configuration.ValidateRequiredConfiguration();

            // Add services
            builder.Services.AddForwardedHeaders();
            builder.Services.AddDatabases(builder.Configuration);
            builder.Services.AddIdentityAndAuth(builder.Configuration);
            builder.Services.AddSendGrid(builder.Configuration);
            builder.Services.AddGoogleDrive(builder.Configuration);
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
