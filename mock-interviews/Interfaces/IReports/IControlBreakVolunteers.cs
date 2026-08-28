using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Interfaces.IReports
{
    public interface IControlBreakVolunteers
    {
        public Task<List<TimeRangeViewModel>> ToTimeRanges(List<VolunteerTimeslot> volunteerEvents);
    }
}
