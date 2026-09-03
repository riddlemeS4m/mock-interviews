using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MockInterviews.Options;

namespace MockInterviews.Email;

public sealed class SmtpEmailTransport(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var smtpOptions = options.Value;
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(message.From.DisplayName, message.From.Address));
        mimeMessage.To.Add(new MailboxAddress(message.To.DisplayName, message.To.Address));
        mimeMessage.Subject = message.Subject;

        var body = new BodyBuilder { TextBody = message.PlainTextBody, HtmlBody = message.HtmlBody };
        foreach (var attachment in message.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        mimeMessage.Body = body.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var security = smtpOptions.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(smtpOptions.Host, smtpOptions.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(smtpOptions.Username))
            {
                await client.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password!, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Email delivered through SMTP to {Recipient}", message.To.Address);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "SMTP was unable to deliver email to {Recipient}", message.To.Address);
            throw new EmailDeliveryException("SMTP was unable to deliver the email.", exception);
        }
    }
}
