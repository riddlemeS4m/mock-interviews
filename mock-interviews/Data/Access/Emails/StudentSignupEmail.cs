using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Data.Access.Emails
{
    public class StudentSignupEmail : ASendAnEmail
    {

        public StudentSignupEmail()
        {
            FilePath += "student-signup-email.html";
        }

        public override void InjectHTMLContent()
        {
            HTMLContent = HTMLContent.Replace("{firstName}", ToName);
            HTMLContent = HTMLContent.Replace("{interviewList}", Times);
        }
    }
}
