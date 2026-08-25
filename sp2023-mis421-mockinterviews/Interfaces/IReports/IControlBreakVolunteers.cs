using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.ViewModels;

namespace sp2023_mis421_mockinterviews.Interfaces.IReports
{
    public interface IControlBreakVolunteers
    {
        public Task<List<TimeRangeViewModel>> ToTimeRanges(List<VolunteerTimeslot> volunteerEvents);
    }
}
