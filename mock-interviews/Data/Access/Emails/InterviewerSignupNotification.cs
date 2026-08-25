using MockInterviews.Data.Constants;
using MockInterviews.Data.Seeds;
using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class InterviewerSignupNotification : ASendAnEmail
    {
        public InterviewerSignupNotification()
        {
            FilePath += "interviewer-signup-notification.html";
        }
        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{adminName}", SuperUser.FirstName);
            HTMLContent = HTMLContent.Replace("{name}", ToName);
            HTMLContent = HTMLContent.Replace("{times}", Times);
        }
    }
}
