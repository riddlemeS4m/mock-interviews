using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class StudentReminderEmail : ASendAnEmail
    {
        public StudentReminderEmail()
        {
            FilePath += "student-reminder-email.html";
        }

        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{firstName}", ToName);
            HTMLContent = HTMLContent.Replace("{interviews}", Times);
        }
    }
}
