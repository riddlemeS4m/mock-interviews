using MockInterviews.Models.ViewModels.InterviewEventsController;

namespace MockInterviews.Interfaces.IServices
{
    public interface IManageInterviews
    {
        public Task AssignStudentsToInterviewers(Dictionary<int, string> keyValuePairs);
        public Task<List<InterviewEventManageViewModel>> ListOfAssignedStudents();
    }
}
