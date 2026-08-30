using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using MockInterviews.Models.Identity;
using MockInterviews.Services;

namespace MockInterviews.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class AcceptInvitationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AccountRoleProvisioner _roleProvisioner;
    private readonly UserProfileCompletionService _profileCompletionService;

    public AcceptInvitationModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AccountRoleProvisioner roleProvisioner,
        UserProfileCompletionService profileCompletionService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleProvisioner = roleProvisioner;
        _profileCompletionService = profileCompletionService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string Email { get; private set; } = string.Empty;

    public sealed class InputModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string EmailCode { get; set; } = string.Empty;

        [Required]
        public string PasswordCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string? userId, string? emailCode, string? passwordCode)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(emailCode) || string.IsNullOrWhiteSpace(passwordCode))
        {
            return BadRequest("This invitation link is incomplete.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (await _userManager.HasPasswordAsync(user))
        {
            return RedirectToPage("./Login");
        }

        Email = user.Email ?? string.Empty;
        Input = new InputModel { UserId = userId, EmailCode = emailCode, PasswordCode = passwordCode };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.FindByIdAsync(Input.UserId);
        if (user is null)
        {
            return NotFound();
        }

        Email = user.Email ?? string.Empty;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string emailCode;
        string passwordCode;
        try
        {
            emailCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.EmailCode));
            passwordCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.PasswordCode));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "This invitation link is invalid. Request a new invitation.");
            return Page();
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            var confirmationResult = await _userManager.ConfirmEmailAsync(user, emailCode);
            if (!confirmationResult.Succeeded)
            {
                AddErrors(confirmationResult);
                return Page();
            }
        }

        var passwordResult = await _userManager.ResetPasswordAsync(user, passwordCode, Input.Password);
        if (!passwordResult.Succeeded)
        {
            AddErrors(passwordResult);
            return Page();
        }

        await _roleProvisioner.ProvisionStudentRoleAsync(user);
        await _signInManager.SignInAsync(user, isPersistent: false);
        if (await _profileCompletionService.IsRequiredAsync(user))
        {
            return RedirectToPage("/Account/Manage/ProfileEdit", new { ReturnUrl = Url.Content("~/") });
        }

        return RedirectToAction("Landing", "Home", new { area = "" });
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
