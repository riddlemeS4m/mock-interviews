using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.UsersController;
using MockInterviews.Services;

namespace MockInterviews.Controllers
{
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MockInterviewsDbContext _context;
        private readonly AccountInvitationService _accountInvitationService;
        private readonly IEmailSender _emailSender;
        private readonly LinkGenerator _linkGenerator;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            MockInterviewsDbContext context,
            AccountInvitationService accountInvitationService,
            IEmailSender emailSender,
            LinkGenerator linkGenerator)
        {
            _userManager = userManager;
            _context = context;
            _accountInvitationService = accountInvitationService;
            _emailSender = emailSender;
            _linkGenerator = linkGenerator;
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> Index()
        {
            return RedirectToAction("Index", "UserRoles");
        }

        [Authorize(Roles = RolesConstants.InterviewerRole)]
        public async Task<IActionResult> ExternalUserProfileView(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return NotFound();
            }

            var viewModel = new ExternalUserProfileViewModel
            {
                FirstName = user.FirstName ?? "Deleted user",
                LastName = user.LastName ?? string.Empty,
                Class = ClassConstants.GetClassText((Classes)user.Class)
            };

            return View(viewModel);
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (userId == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return View("DeleteUser", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "The account could not be found.";
                return RedirectToAction("Index", "UserRoles");
            }

            if (string.Equals(user.Id, _userManager.GetUserId(User), StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account from administration.";
                return RedirectToAction("Index", "UserRoles");
            }

            var targetRoles = await _userManager.GetRolesAsync(user);
            if (targetRoles.Contains(RolesConstants.SystemAdminRole, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "System Admin accounts cannot be deleted from the people workspace.";
                return RedirectToAction("Index", "UserRoles");
            }

            if (!User.IsInRole(RolesConstants.SystemAdminRole)
                && targetRoles.Contains(RolesConstants.AdminRole, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Only a System Admin can delete an Admin account.";
                return RedirectToAction("Index", "UserRoles");
            }

            if (await HasRelatedDomainRecordsAsync(user.Id))
            {
                TempData["ErrorMessage"] = "This account cannot be deleted because it has related interview or signup records.";
                return RedirectToAction("Index", "UserRoles");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(error => error.Description));
                return RedirectToAction("Index", "UserRoles");
            }

            TempData["StatusMessage"] = "Account deleted.";
            return RedirectToAction("Index", "UserRoles");
        }

        private async Task<bool> HasRelatedDomainRecordsAsync(string userId)
        {
            return await _context.Interviews.AnyAsync(interview => interview.StudentId == userId)
                || await _context.VolunteerTimeslots.AnyAsync(timeslot => timeslot.StudentId == userId)
                || await _context.InterviewerSignups.AnyAsync(signup => signup.InterviewerId == userId)
                || await _context.InterviewerLocations.AnyAsync(location => location.InterviewerId == userId);
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public IActionResult CreateProvisionaryUser()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> CreateProvisionaryUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    UserName = model.Email
                };
                var result = await _accountInvitationService.CreateAndInviteAsync(user, RolesConstants.InterviewerRole);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "UserRoles");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // If model state is not valid or user creation fails, return to the creation page with errors
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> ResetUserPassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            var model = new ResetPasswordViewModel { UserId = userId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> ResetUserPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ViewBag.ErrorMessage = "User not found.";
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ModelState.AddModelError(string.Empty, "This account does not have an email address for password reset delivery.");
                return View(model);
            }

            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _userManager.GeneratePasswordResetTokenAsync(user)));
            var callbackUrl = _linkGenerator.GetUriByPage(HttpContext, "/Account/ResetPassword", values: new { area = "Identity", code });
            if (callbackUrl is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to create a password reset link.");
                return View(model);
            }
            await _emailSender.SendEmailAsync(user.Email, "Reset your Mock Interviews password", $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Reset your password</a>.");
            TempData["StatusMessage"] = "Password reset email sent.";
            return RedirectToAction("Index", "UserRoles");
        }
    }
}
