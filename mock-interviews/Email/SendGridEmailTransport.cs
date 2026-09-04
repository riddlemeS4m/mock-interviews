using SendGrid;
using SendGrid.Helpers.Mail;

namespace MockInterviews.Email;

public sealed class SendGridEmailTransport(ISendGridClient client, ILogger<SendGridEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sendGridMessage = MailHelper.CreateSingleEmail(
            new SendGrid.Helpers.Mail.EmailAddress(message.From.Address, message.From.DisplayName),
            new SendGrid.Helpers.Mail.EmailAddress(message.To.Address, message.To.DisplayName),
            message.Subject,
            message.PlainTextBody,
            message.HtmlBody);

        foreach (var attachment in message.Attachments)
        {
            sendGridMessage.AddAttachment(
                attachment.FileName,
                Convert.ToBase64String(attachment.Content),
                attachment.ContentType);
        }

        try
        {
            var response = await client.SendEmailAsync(sendGridMessage, cancellationToken);
            if ((int)response.StatusCode is >= 200 and < 300)
            {
                logger.LogInformation("Email delivered through SendGrid to {Recipient} with status {StatusCode}", message.To.Address, response.StatusCode);
                return;
            }

            logger.LogError("SendGrid rejected email to {Recipient} with status {StatusCode}", message.To.Address, response.StatusCode);
            throw new EmailDeliveryException("SendGrid was unable to deliver the email.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not EmailDeliveryException)
        {
            logger.LogError(exception, "SendGrid was unable to deliver email to {Recipient}", message.To.Address);
            throw new EmailDeliveryException("SendGrid was unable to deliver the email.", exception);
        }
    }
}
