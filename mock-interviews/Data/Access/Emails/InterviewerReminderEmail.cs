using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class InterviewerReminderEmail : ASendAnEmail
    {
        public InterviewerReminderEmail()
        {
            FilePath += "interviewer-reminder-email.html";
        }

        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{firstName}", ToName);
            HTMLContent = HTMLContent.Replace("{interviews}", Times);
        }
    }
}
