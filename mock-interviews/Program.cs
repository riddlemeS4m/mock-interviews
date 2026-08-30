using MockInterviews.Extensions;

namespace MockInterviews
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
            builder.Services.AddIdentityAndAuth(builder.Environment);
            builder.Services.AddOptionalMicrosoftAuthentication(builder.Configuration);
            builder.Services.AddSendGrid(builder.Configuration);
            builder.Services.AddSuperUserOptions(builder.Configuration);
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
