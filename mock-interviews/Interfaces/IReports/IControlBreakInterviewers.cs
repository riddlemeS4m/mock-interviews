using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels;

namespace MockInterviews.Interfaces.IReports
{
    public interface IControlBreakInterviewers
    {
        public Task<List<TimeRangeViewModel>> ToTimeRanges(List<InterviewerTimeslot> signupInterviewerTimeslots);
    }
}
