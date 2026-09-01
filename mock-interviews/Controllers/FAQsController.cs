using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Email;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.FAQsController;
using MockInterviews.Options;

namespace MockInterviews.Controllers;

public class FAQsController(
    MockInterviewsDbContext context,
    UserManager<ApplicationUser> userManager,
    IEmailTransport emailTransport,
    ILogger<FAQsController> logger,
    IOptions<SuperUserOptions> superUserOptions) : Controller
{
    private readonly string _superUserEmail = superUserOptions.Value.Email;

    // GET: FAQs
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> Index() => View(await BuildIndexViewModelAsync());

    public async Task<IActionResult> Resources()
    {
        var settings = await context.Settings.AsNoTracking()
            .Where(setting => setting.Name == SettingsConstants.MockInterviewManual.Name || setting.Name == SettingsConstants.GuestParkingPass.Name)
            .ToDictionaryAsync(setting => setting.Name, setting => setting.Value);
        var questions = await context.Questions.AsNoTracking().Where(question => question.A != null)
            .OrderBy(question => question.Id).ToListAsync();
        return View(new ResourcesViewModel(
            questions,
            GetPublicAssetUrl(settings.GetValueOrDefault(SettingsConstants.MockInterviewManual.Name)),
            GetPublicAssetUrl(settings.GetValueOrDefault(SettingsConstants.GuestParkingPass.Name))));
    }

    // GET: FAQs/Details/5
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> Details(int? id)
    {
        var question = id is null ? null : await context.Questions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return question is null ? NotFound() : View(question);
    }

    // GET: FAQs/Create
    [Authorize(Roles = RolesConstants.ParticipantOrAdministrationRoles)]
    public IActionResult Create() => View(new Question());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesConstants.ParticipantOrAdministrationRoles)]
    public async Task<IActionResult> Create([Bind("Q,A")] Question question)
    {
        var isAdministrator = User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.SystemAdminRole);
        if (!isAdministrator)
        {
            question.A = null;
        }
        if (string.IsNullOrWhiteSpace(question.Q))
        {
            ModelState.AddModelError(nameof(question.Q), "A question is required.");
        }
        if (!ModelState.IsValid)
        {
            return View(question);
        }

        question.Q = question.Q.Trim();
        question.A = string.IsNullOrWhiteSpace(question.A) ? null : question.A.Trim();
        context.Questions.Add(question);
        await context.SaveChangesAsync();

        if (isAdministrator)
        {
            TempData["StatusMessage"] = "FAQ was added.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        try
        {
            ASendAnEmail emailer = new NewFAQSubmitted();
            await emailer.SendEmailAsync(emailTransport, _superUserEmail, "A Required: Participant Submitted New Question", _superUserEmail,
                user is null ? "Deleted user" : $"{user.FirstName} {user.LastName}", question.Q, null);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to send FAQ notification for question {QuestionId}", question.Id);
            TempData["ErrorMessage"] = "Your question was saved, but the administrator notification could not be delivered.";
        }

        TempData["StatusMessage"] ??= "Your question was submitted for review.";
        return RedirectToAction(nameof(Resources));
    }

    // GET: FAQs/Edit/5
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> Edit(int? id)
    {
        var question = id is null ? null : await context.Questions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return question is null ? NotFound() : View(question);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Q,A")] Question input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(input.Q))
        {
            ModelState.AddModelError(nameof(input.Q), "A question is required.");
        }

        var question = await context.Questions.SingleOrDefaultAsync(item => item.Id == id);
        if (question is null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        question.Q = input.Q.Trim();
        question.A = string.IsNullOrWhiteSpace(input.A) ? null : input.A.Trim();
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "FAQ was updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: FAQs/Delete/5
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> Delete(int? id)
    {
        var question = id is null ? null : await context.Questions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return question is null ? NotFound() : View(question);
    }

    // POST: FAQs/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var question = await context.Questions.SingleOrDefaultAsync(item => item.Id == id);
        if (question is null)
        {
            TempData["ErrorMessage"] = "That FAQ no longer exists.";
            return RedirectToAction(nameof(Index));
        }
        context.Questions.Remove(question);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "FAQ was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<FaqIndexViewModel> BuildIndexViewModelAsync(Question? editor = null, string? activeDialog = null)
        => new(await context.Questions.AsNoTracking().OrderBy(question => question.Id).ToListAsync(), editor ?? new Question(), activeDialog);

    private static string? GetPublicAssetUrl(string? assetUrl)
    {
        if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }
        return uri.AbsoluteUri;
    }
}
