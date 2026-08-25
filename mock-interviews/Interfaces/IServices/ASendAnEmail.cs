using SendGrid.Helpers.Mail;
using SendGrid;
using MockInterviews.Data.Constants;
using System.Text;
using MockInterviews.Data.Seeds;

namespace MockInterviews.Interfaces.IServices
{
    public abstract class ASendAnEmail
    {
        public EmailAddress FromEmail { get; set; }
        public string Subject { get; set; }
        public string PlainTextContent { get; set; }
        public EmailAddress ToEmail { get; set; }
        public string HTMLContent { get; set; }
        public string ToName { get; set; }
        public string Times { get; set; }
        public string FilePath { get; set; }

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
