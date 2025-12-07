using Microsoft.AspNetCore.Identity;
using sp2023_mis421_mockinterviews.Data.Seeds;
using sp2023_mis421_mockinterviews.Interfaces.IDbContext;
using sp2023_mis421_mockinterviews.Models.UserDb;
using sp2023_mis421_mockinterviews.Services.GoogleDrive;
using sp2023_mis421_mockinterviews.Services.SignupDb;

namespace sp2023_mis421_mockinterviews.Data;

public static class StartupTasks
{
    public static async Task RunStartupTasksAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            // Db contexts + managers
            var signupDb = scope.ServiceProvider.GetRequiredService<ISignupDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // App services
            var drive = scope.ServiceProvider.GetRequiredService<GoogleDriveSiteContentService>();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var timeslots = scope.ServiceProvider.GetRequiredService<TimeslotService>();
            var eventsSvc = scope.ServiceProvider.GetRequiredService<EventService>();

            await UserDbContextSeed.SeedRolesAsync(roleManager);
            await UserDbContextSeed.SeedSuperAdminAsync(userManager, app.Configuration["SeededAdminPwd"]!);
            await TimeslotSeed.SeedTimeslots(eventsSvc, timeslots);
            await SettingsSeed.SeedSettings(settings);

            try
            {
                await new GoogleDriveServiceSeed(drive, signupDb).Test();
                logger.LogInformation("Google Drive connectivity verified.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Google Drive connectivity check failed - continuing without Google Drive features.");
            }

            logger.LogInformation("Startup tasks completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup tasks failed.");
            // choose: rethrow to fail fast in prod, or continue in dev
            if (!env.IsDevelopment()) throw;
        }
    }
}