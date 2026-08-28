using System.Text;
using MockInterviews.Data.Constants;
using SendGrid;
using SendGrid.Helpers.Mail;

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
            ISendGridClient sendGridClient,
            string senderEmail,
            string subject,
            string emailto,
            string emailname,
            string times,
            List<string>? base64CalendarContent)
        {
            FromEmail = new EmailAddress(senderEmail, "UA MIS " + SuperUser.FirstName + " " + SuperUser.LastName);
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

            var msg = MailHelper.CreateSingleEmail(FromEmail, ToEmail, Subject, PlainTextContent, HTMLContent);

            if (base64CalendarContent != null)
            {
                foreach (string Event in base64CalendarContent)
                {
                    msg.AddAttachment("MockInterviews.ics", Event, "text/calendar");
                }
            }

            await sendGridClient.SendEmailAsync(msg);
        }
    }
}
