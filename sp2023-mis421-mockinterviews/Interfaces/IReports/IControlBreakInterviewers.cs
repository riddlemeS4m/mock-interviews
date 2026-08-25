using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.ViewModels;

namespace sp2023_mis421_mockinterviews.Interfaces.IReports
{
    public interface IControlBreakInterviewers
    {
        public Task<List<TimeRangeViewModel>> ToTimeRanges(List<InterviewerTimeslot> signupInterviewerTimeslots);
    }
}
