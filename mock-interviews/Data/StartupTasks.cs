using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Seeds;
using MockInterviews.Models.Identity;
using MockInterviews.Options;
using MockInterviews.Services;

namespace MockInterviews.Data;

public static class StartupTasks
{
    public static async Task RunStartupTasksAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            // Managers
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var superUser = scope.ServiceProvider.GetRequiredService<IOptions<SuperUserOptions>>().Value;

            // App services
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();

            await IdentitySeed.SeedRolesAsync(roleManager);
            await IdentitySeed.SeedSuperAdminAsync(
                userManager,
                superUser.Email,
                app.Configuration["SeededAdminPwd"]!);
            await SettingsSeed.SeedSettings(settings);

            if (ShouldRunTimeslotBackfill(env))
            {
                var timeslots = scope.ServiceProvider.GetRequiredService<TimeslotService>();
                var eventsSvc = scope.ServiceProvider.GetRequiredService<EventService>();
                await TimeslotSeed.SeedTimeslots(eventsSvc, timeslots);
            }
            else
            {
                logger.LogInformation(
                    "Skipping timeslot backfill in {EnvironmentName}.",
                    env.EnvironmentName);
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

    public static bool ShouldRunTimeslotBackfill(IHostEnvironment environment) => environment.IsDevelopment();
}
