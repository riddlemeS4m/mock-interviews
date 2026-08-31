using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MockInterviews.Email;
using MockInterviews.Options;

namespace MockInterviews.Services;

public sealed class IdentityEmailSender(
    IEmailTransport emailTransport,
    IOptions<SuperUserOptions> superUserOptions)
    : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new EmailMessage(
            new EmailAddress(superUserOptions.Value.Email, "Mock Interviews"),
            new EmailAddress(email),
            subject,
            "Open this message in an HTML-capable email client to use the included account link.",
            htmlMessage);
        await emailTransport.SendAsync(message);
    }
}
