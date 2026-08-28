using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class VolunteerSignupConfirmation : ASendAnEmail
    {
        public VolunteerSignupConfirmation()
        {
            FilePath += "volunteer-signup-confirmation.html";
        }
        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{name}", ToName);
            HTMLContent = HTMLContent.Replace("{times}", Times);
        }
    }
}
