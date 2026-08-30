using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using MockInterviews.Models.Identity;

namespace MockInterviews.Services;

public sealed class AccountInvitationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AccountInvitationService> _logger;

    public AccountInvitationService(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AccountInvitationService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IdentityResult> CreateAndInviteAsync(ApplicationUser user, string role)
    {
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return createResult;
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return roleResult;
        }

        try
        {
            await SendInvitationAsync(user);
            return IdentityResult.Success;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to deliver an account invitation to {Email}.", user.Email);
            await _userManager.DeleteAsync(user);
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvitationDeliveryFailed",
                Description = "The account invitation could not be sent. Please try again."
            });
        }
    }

    public async Task SendInvitationAsync(ApplicationUser user)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("An HTTP request is required to create an account invitation.");
        var emailCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
            await _userManager.GenerateEmailConfirmationTokenAsync(user)));
        var passwordCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
            await _userManager.GeneratePasswordResetTokenAsync(user)));
        var callbackUrl = _linkGenerator.GetUriByPage(
            httpContext,
            "/Account/AcceptInvitation",
            values: new { area = "Identity", userId = user.Id, emailCode, passwordCode });

        if (callbackUrl is null)
        {
            throw new InvalidOperationException("Unable to generate the account invitation URL.");
        }

        await _emailSender.SendEmailAsync(
            user.Email ?? throw new InvalidOperationException("An email address is required for an account invitation."),
            "Set up your Mock Interviews account",
            $"You have been invited to Mock Interviews. <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Confirm your email and choose a password</a>.");
    }
}
