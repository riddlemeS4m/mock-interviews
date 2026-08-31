using System.Text;
using MockInterviews.Data.Constants;
using MockInterviews.Email;

namespace MockInterviews.Interfaces.IServices
{
    public abstract class ASendAnEmail
    {
        public EmailAddress FromEmail { get; set; } = null!; // Assigned by SendEmailAsync before a message is created.
        public string Subject { get; set; } = null!; // Assigned by SendEmailAsync before a message is created.
        public string PlainTextContent { get; set; } = null!; // Assigned by SendEmailAsync before a message is created.
        public EmailAddress ToEmail { get; set; } = null!; // Assigned by SendEmailAsync before a message is created.
        public string HTMLContent { get; set; } = null!; // Assigned by SendEmailAsync before derived templates consume it.
        public string ToName { get; set; } = null!; // Assigned by SendEmailAsync before a message is created.
        public string Times { get; set; } = null!; // Assigned by SendEmailAsync before derived templates consume it.
        public string FilePath { get; set; } = string.Empty;

        public abstract void InjectHTMLContent();

        public async Task SendEmailAsync(
            IEmailTransport emailTransport,
            string senderEmail,
            string subject,
            string emailto,
            string emailname,
            string times,
            List<string>? base64CalendarContent)
        {
            FromEmail = new EmailAddress(senderEmail, SuperUser.FirstName + " " + SuperUser.LastName);
            Subject = subject;
            PlainTextContent = "";
            ToName = emailname;
            Times = times;
            ToEmail = new EmailAddress(emailto);
            StringBuilder stringBuilder = new(FilePath);
            stringBuilder.Insert(0, "./Content/Emails/");
            FilePath = stringBuilder.ToString();
            HTMLContent = await File.ReadAllTextAsync(FilePath);

            InjectHTMLContent();

            var attachments = base64CalendarContent?.Select(calendar =>
                new EmailAttachment("MockInterviews.ics", "text/calendar", Convert.FromBase64String(calendar)));
            var message = new EmailMessage(FromEmail, ToEmail, Subject, PlainTextContent, HTMLContent, attachments);
            await emailTransport.SendAsync(message);
        }
    }
}
