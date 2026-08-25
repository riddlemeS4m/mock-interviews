using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Constants;
using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Services;

namespace sp2023_mis421_mockinterviews.Data.Seeds
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
