using Microsoft.AspNetCore.Identity;
using sp2023_mis421_mockinterviews.Interfaces.IReports;
using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.Identity;
using sp2023_mis421_mockinterviews.Models.ViewModels;

namespace sp2023_mis421_mockinterviews.Data.Access.Reports
{
    public class ControlBreakVolunteer : IControlBreakVolunteers
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public ControlBreakVolunteer(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }
        public async Task<List<TimeRangeViewModel>> ToTimeRanges(List<VolunteerTimeslot> volunteerEvents)
        {
            var groupedEvents = new List<TimeRangeViewModel>();

            if (volunteerEvents != null && volunteerEvents.Count != 0)
            {
                var ints = new List<int>();
                var currentStart = volunteerEvents.First().Timeslot;
                var currentEnd = volunteerEvents.First().Timeslot;
                var studentid = volunteerEvents.First().StudentId;
                ints.Add(volunteerEvents.First().Id);

                for (int i = 1; i < volunteerEvents.Count; i++)
                {
                    var nextEvent = volunteerEvents[i].Timeslot;

                    if (currentEnd.Id + 1 == nextEvent.Id
                        && currentEnd.Event.Date == nextEvent.Event.Date
                        && volunteerEvents[i].StudentId == studentid)
                    {
                        currentEnd = nextEvent;
                        ints.Add(volunteerEvents[i].Id);
                    }
                    else
                    {
                        var name = await _userManager.FindByIdAsync(volunteerEvents[i - 1].StudentId);
                        groupedEvents.Add(new TimeRangeViewModel
                        {
                            Date = currentStart.Event.Date,
                            EndTime = currentEnd.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                            StartTime = currentStart.Time.ToString(@"h\:mm tt"),
                            Name = name.FirstName + " " + name.LastName,
                            TimeslotIds = ints
                        });

                        currentStart = nextEvent;
                        currentEnd = nextEvent;
                        ints = new List<int>
                        {
                            volunteerEvents[i].Id
                        };
                        studentid = volunteerEvents[i].StudentId;
                    }
                }

                var user = await _userManager.FindByIdAsync(studentid);
                groupedEvents.Add(new TimeRangeViewModel
                {
                    Date = currentStart.Event.Date,
                    EndTime = currentEnd.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                    StartTime = currentStart.Time.ToString(@"h\:mm tt"),
                    Name = user.FirstName + " " + user.LastName,
                    TimeslotIds = ints
                });
            }

            return groupedEvents;
        }
    }
}
