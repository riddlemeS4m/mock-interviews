using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels
{
    public class SignupInterviewerViewModel
    {
        public InterviewerSignup SignupInterviewer { get; set; } = null!; // Assigned when the controller composes the signup page.
    }
}
