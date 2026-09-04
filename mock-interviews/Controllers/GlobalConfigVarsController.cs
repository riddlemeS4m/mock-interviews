using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.ViewModels.GlobalConfigVarsController;

namespace MockInterviews.Controllers;

[Authorize(Roles = RolesConstants.AdministrationRoles)]
public class GlobalConfigVarsController(MockInterviewsDbContext context) : Controller
{
    // GET: GlobalConfigVars
    public async Task<IActionResult> Index() => View(await BuildIndexViewModelAsync());

    // GET: GlobalConfigVars/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        var setting = id is null ? null : await context.Settings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return setting is null ? NotFound() : View(setting);
    }

    // GET: GlobalConfigVars/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        var setting = id is null ? null : await context.Settings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return setting is null ? NotFound() : View(ConfigurationEditViewModel.FromSetting(setting));
    }

    // POST: GlobalConfigVars/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ConfigurationEditViewModel input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        var setting = await context.Settings.SingleOrDefaultAsync(item => item.Id == id);
        if (setting is null)
        {
            return NotFound();
        }

        if (!IsValidValue(setting.Name, input.Value, out var error))
        {
            ModelState.AddModelError(nameof(input.Value), error);
        }

        if (!ModelState.IsValid)
        {
            input.Name = setting.Name;
            return View(nameof(Index), await BuildIndexViewModelAsync(input, "configuration-edit-dialog"));
        }

        setting.Value = input.Value.Trim();
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = $"{setting.Name} was updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ConfigurationIndexViewModel> BuildIndexViewModelAsync(
        ConfigurationEditViewModel? editor = null,
        string? activeDialog = null)
    {
        var settings = await context.Settings.AsNoTracking().OrderBy(setting => setting.Name).ToListAsync();
        return new ConfigurationIndexViewModel(settings, editor ?? new ConfigurationEditViewModel(), activeDialog);
    }

    private static bool IsValidValue(string name, string value, out string error)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed)
            && name is SettingsConstants.MockInterviewManual.Name or SettingsConstants.GuestParkingPass.Name)
        {
            error = string.Empty;
            return true;
        }
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "A value is required.";
            return false;
        }

        if (name is SettingsConstants.ZoomLink.Name or SettingsConstants.MockInterviewManual.Name or SettingsConstants.GuestParkingPass.Name)
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "Enter an absolute HTTP(S) URL.";
                return false;
            }
        }
        else if (name is SettingsConstants.ZoomLinkVisible.Name or SettingsConstants.DisruptionBanner.Name or SettingsConstants.AutomaticallyReleaseTimeslots.Name)
        {
            if (trimmed is not "0" and not "1")
            {
                error = "Enter 0 to disable or 1 to enable this setting.";
                return false;
            }
        }
        else if (name is SettingsConstants.InterviewIndexHours.Name or SettingsConstants.MaximumTimeslotSignups.Name)
        {
            if (!int.TryParse(trimmed, out var number) || number <= 0)
            {
                error = "Enter a whole number greater than zero.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
