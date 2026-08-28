using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class InterviewerSignupConfirmation : ASendAnEmail
    {
        public InterviewerSignupConfirmation()
        {
            FilePath += "interviewer-signup-confirmation.html";
        }
        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{name}", ToName);
            HTMLContent = HTMLContent.Replace("{times}", Times);
        }
    }
}
