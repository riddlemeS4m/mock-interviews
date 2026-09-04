using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Interfaces.IReports;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Data.Access.Reports
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
                var userIds = volunteerEvents.Select(volunteerEvent => volunteerEvent.StudentId).Distinct().ToArray();
                var usersById = await _userManager.Users
                    .Where(user => userIds.Contains(user.Id))
                    .ToDictionaryAsync(user => user.Id);
                var ints = new List<int>();
                var currentStart = volunteerEvents.First().Timeslot;
                var currentEnd = volunteerEvents.First().Timeslot;
                var studentid = volunteerEvents.First().StudentId;
                ints.Add(volunteerEvents.First().Id);

                for (int i = 1; i < volunteerEvents.Count; i++)
                {
                    var nextEvent = volunteerEvents[i].Timeslot;

                    if (currentEnd.EventId == nextEvent.EventId
                        && currentEnd.Time.AddMinutes(30) == nextEvent.Time
                        && volunteerEvents[i].StudentId == studentid)
                    {
                        currentEnd = nextEvent;
                        ints.Add(volunteerEvents[i].Id);
                    }
                    else
                    {
                        usersById.TryGetValue(volunteerEvents[i - 1].StudentId, out var name);
                        groupedEvents.Add(new TimeRangeViewModel
                        {
                            Date = currentStart.Event.Date,
                            EndTime = currentEnd.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                            StartTime = currentStart.Time.ToString(@"h\:mm tt"),
                            Name = name is null ? "Deleted user" : $"{name.FirstName} {name.LastName}",
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

                usersById.TryGetValue(studentid, out var user);
                groupedEvents.Add(new TimeRangeViewModel
                {
                    Date = currentStart.Event.Date,
                    EndTime = currentEnd.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                    StartTime = currentStart.Time.ToString(@"h\:mm tt"),
                    Name = user is null ? "Deleted user" : $"{user.FirstName} {user.LastName}",
                    TimeslotIds = ints
                });
            }

            return groupedEvents;
        }
    }
}
