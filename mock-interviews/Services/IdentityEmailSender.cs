using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MockInterviews.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MockInterviews.Services;

public sealed class IdentityEmailSender(
    ISendGridClient sendGridClient,
    IOptions<SuperUserOptions> superUserOptions,
    ILogger<IdentityEmailSender> logger)
    : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(superUserOptions.Value.Email, "Mock Interviews"),
            new EmailAddress(email),
            subject,
            "Open this message in an HTML-capable email client to use the included account link.",
            htmlMessage);

        var response = await sendGridClient.SendEmailAsync(message);
        if ((int)response.StatusCode is >= 200 and < 300)
        {
            return;
        }

        logger.LogError(
            "SendGrid rejected an Identity email to {Email} with status {StatusCode}",
            email,
            response.StatusCode);
        throw new InvalidOperationException("Unable to send the account email.");
    }
}
