using MockInterviews.Data.Constants;
using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class VolunteerCancellationNotification : ASendAnEmail
    {
        public VolunteerCancellationNotification()
        {
            FilePath += "volunteer-cancellation-notification.html";
        }
        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{adminName}", SuperUser.FirstName);
            HTMLContent = HTMLContent.Replace("{name}", ToName);
            HTMLContent = HTMLContent.Replace("{times}", Times);
        }
    }
}
