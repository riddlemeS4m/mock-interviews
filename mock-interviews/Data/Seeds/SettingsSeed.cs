using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Models.Entities;
using MockInterviews.Services;

namespace MockInterviews.Data.Seeds
{
    public class SettingsSeed
    {
        public static async Task SeedSettings(SettingsService settingsService)
        {
            var list = new List<Setting>();

            var existingSettings = await settingsService.GetAllAsync();

            foreach (var setting in SettingsConstants.GetSettings())
            {
                if (!existingSettings.Any(x => x.Name == setting.Name))
                {
                    list.Add(setting);
                }
            }

            await settingsService.AddRange(list);
        }
    }
}
