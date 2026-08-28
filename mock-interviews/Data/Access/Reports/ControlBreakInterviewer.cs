using Microsoft.AspNetCore.Identity;
using MockInterviews.Data.Constants;
using MockInterviews.Interfaces.IReports;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Data.Access.Reports
{
    public class ControlBreakInterviewer : IControlBreakInterviewers
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public ControlBreakInterviewer(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<TimeRangeViewModel>> ToTimeRanges(List<InterviewerTimeslot> signupInterviewTimeslots)
        {
            signupInterviewTimeslots = signupInterviewTimeslots.OrderBy(x => x.InterviewerSignupId).ThenBy(x => x.TimeslotId).ToList();
            var groupedEvents = new List<TimeRangeViewModel>();
            var name = new ApplicationUser();

            if (signupInterviewTimeslots != null && signupInterviewTimeslots.Count != 0)
            {
                var ints = new List<int>();
                var currentSI = signupInterviewTimeslots.First().InterviewerSignup;
                var currentEvent = signupInterviewTimeslots.First().Timeslot;
                var startAt = signupInterviewTimeslots.First().Timeslot.Time;

                ints.Add(signupInterviewTimeslots.First().Id);

                for (int i = 1; i < signupInterviewTimeslots.Count; i++)
                {
                    var nextSI = signupInterviewTimeslots[i].InterviewerSignup;
                    var nextEvent = signupInterviewTimeslots[i].Timeslot;

                    if (signupInterviewTimeslots[i].InterviewerSignupId == currentSI.Id
                        && signupInterviewTimeslots[i].TimeslotId == currentEvent.Id + 1)
                    {
                        currentEvent = nextEvent;
                        ints.Add(signupInterviewTimeslots[i].Id);
                    }
                    else
                    {
                        name = await _userManager.FindByIdAsync(currentSI.InterviewerId);
                        groupedEvents.Add(new TimeRangeViewModel
                        {
                            Date = currentEvent.Event.Date,
                            EndTime = currentEvent.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                            StartTime = startAt.ToString(@"h\:mm tt"),
                            Location = GetLocation(currentSI.InPerson),
                            Name = name is null ? "Deleted user" : $"{name.FirstName} {name.LastName}",
                            InterviewType = currentSI.Type,
                            TimeslotIds = ints,
                            WantsLunch = currentSI.Lunch,
                            SignupInterviewerId = currentSI.Id
                        });

                        ints = new List<int>
                        {
                            signupInterviewTimeslots[i].Id
                        };
                        currentSI = nextSI;
                        currentEvent = nextEvent;
                        startAt = nextEvent.Time;

                    }
                }

                name = await _userManager.FindByIdAsync(currentSI.InterviewerId);
                groupedEvents.Add(new TimeRangeViewModel
                {
                    Date = currentEvent.Event.Date,
                    EndTime = currentEvent.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                    StartTime = startAt.ToString(@"h\:mm tt"),
                    Location = GetLocation(currentSI.InPerson),
                    Name = name is null ? "Deleted user" : $"{name.FirstName} {name.LastName}",
                    InterviewType = currentSI.Type,
                    TimeslotIds = ints,
                    WantsLunch = currentSI.Lunch,
                    SignupInterviewerId = currentSI.Id
                });
            }

            return groupedEvents;
        }

        private static string GetLocation(bool loc)
        {
            if (loc)
            {
                return InterviewLocationConstants.InPerson;
            }
            return InterviewLocationConstants.IsVirtual;
        }
    }
}
