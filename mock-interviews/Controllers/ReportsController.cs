using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.ReportsController;
using MockInterviews.Models.ViewModels.TimeslotsController;
using MockInterviews.Services;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public class ReportsController : Controller
    {
        private readonly EventService _eventService;
        private readonly MockInterviewsDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(EventService eventService,
            MockInterviewsDbContext context,
            ILogger<ReportsController> logger)
        {
            _eventService = eventService;
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(EventStatistics));
        }

        public async Task<IActionResult> EventStatistics()
        {
            var events = (await _eventService.GetAllAsync()).OrderBy(@event => @event.Date).ToList();
            var eventIds = events.Select(@event => @event.Id).ToArray();
            var studentCounts = await _context.Interviews
                .Where(interview => eventIds.Contains(interview.Timeslot.EventId))
                .GroupBy(interview => interview.Timeslot.EventId)
                .Select(group => new { EventId = group.Key, Count = group.Select(interview => interview.StudentId).Distinct().Count() })
                .ToDictionaryAsync(item => item.EventId, item => item.Count);
            var interviewerCounts = await _context.InterviewerTimeslots
                .Where(timeslot => eventIds.Contains(timeslot.Timeslot.EventId))
                .GroupBy(timeslot => timeslot.Timeslot.EventId)
                .Select(group => new { EventId = group.Key, Count = group.Select(timeslot => timeslot.InterviewerSignup.InterviewerId).Distinct().Count() })
                .ToDictionaryAsync(item => item.EventId, item => item.Count);
            var volunteerCounts = await _context.VolunteerTimeslots
                .Where(timeslot => eventIds.Contains(timeslot.Timeslot.EventId))
                .GroupBy(timeslot => timeslot.Timeslot.EventId)
                .Select(group => new { EventId = group.Key, Count = group.Select(timeslot => timeslot.StudentId).Distinct().Count() })
                .ToDictionaryAsync(item => item.EventId, item => item.Count);

            var participantCounts = events.Select(@event => new ParticipantCountPerDateViewModel
            {
                EventDate = @event,
                StudentCount = studentCounts.GetValueOrDefault(@event.Id),
                InterviewerCount = interviewerCounts.GetValueOrDefault(@event.Id),
                VolunteerCount = volunteerCounts.GetValueOrDefault(@event.Id)
            }).ToList();

            var uniqueStudentCount = await _context.Interviews
                .Where(x => x.Timeslot.Event.IsActive)
                .Select(e => e.StudentId)
                .Distinct()
                .CountAsync();

            var uniqueInterviewerCount = await _context.InterviewerTimeslots
                .Where(s => s.Timeslot.Event.IsActive)
                .Select(s => s.InterviewerSignup.InterviewerId)
                .Distinct()
                .CountAsync();

            var uniqueVolunteerCount = await _context.VolunteerTimeslots
                .Where(v => v.Timeslot.Event.IsActive)
                .Select(v => v.StudentId)
                .Distinct()
                .CountAsync();


            var eventStatisticsVM = new EventStatisticsViewModel
            {
                EventStatistics = participantCounts,
                TotalStudents = uniqueStudentCount,
                TotalInterviewers = uniqueInterviewerCount,
                TotalVolunteers = uniqueVolunteerCount
            };

            return View("EventStatistics", eventStatisticsVM);
        }

        public async Task<IActionResult> SignupReport()
        {
            var timeslots = await _context.Timeslots
                .Include(t => t.Event)
                .Where(x => x.Event.IsActive)
                .ToListAsync();
            var eventdates = await _context.Events
                .Where(x => x.IsActive)
                .ToListAsync();

            var countlist = await BuildParticipantCountsAsync(timeslots);

            var viewModel = new TimeslotViewModel()
            {
                Timeslots = countlist,
                EventDates = eventdates
            };

            return View("SignupReport", viewModel);
        }

        public async Task<IActionResult> AllocationReport()
        {
            var timeslots = await _context.Timeslots
                .Include(t => t.Event)
                .Where(x => x.Event.For221 == For221.n &&
                    x.IsInterviewer &&
                    x.Event.IsActive)
                .ToListAsync();

            var countlist = await BuildParticipantCountsAsync(timeslots, includeDifference: true);

            var top10underserved = countlist
                .OrderByDescending(x => x.Difference)
                .Take(10)
                .ToList();

            var top10available = countlist
                .OrderByDescending(x => x.Difference)
                .TakeLast(10)
                .ToList();

            var viewModel = new AllocationReportViewModel()
            {
                Top10Available = top10available,
                Top10Underserved = top10underserved,
            };

            return View("AllocationReport", viewModel);
        }

        private async Task<List<ParticipantCountViewModel>> BuildParticipantCountsAsync(
            List<Timeslot> timeslots,
            bool includeDifference = false)
        {
            var timeslotIds = timeslots.Select(timeslot => timeslot.Id).ToArray();
            var studentCounts = await _context.Interviews
                .Where(interview => timeslotIds.Contains(interview.TimeslotId))
                .GroupBy(interview => interview.TimeslotId)
                .Select(group => new { TimeslotId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TimeslotId, item => item.Count);
            var interviewerCounts = await _context.InterviewerTimeslots
                .Where(interviewerTimeslot => timeslotIds.Contains(interviewerTimeslot.TimeslotId))
                .GroupBy(interviewerTimeslot => interviewerTimeslot.TimeslotId)
                .Select(group => new { TimeslotId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TimeslotId, item => item.Count);
            var volunteerCounts = await _context.VolunteerTimeslots
                .Where(volunteerTimeslot => timeslotIds.Contains(volunteerTimeslot.TimeslotId))
                .GroupBy(volunteerTimeslot => volunteerTimeslot.TimeslotId)
                .Select(group => new { TimeslotId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.TimeslotId, item => item.Count);

            return timeslots.Select(timeslot =>
            {
                var studentCount = studentCounts.GetValueOrDefault(timeslot.Id);
                var interviewerCount = interviewerCounts.GetValueOrDefault(timeslot.Id);
                return new ParticipantCountViewModel
                {
                    Timeslot = timeslot,
                    StudentCount = studentCount,
                    InterviewerCount = interviewerCount,
                    VolunteerCount = volunteerCounts.GetValueOrDefault(timeslot.Id),
                    Difference = includeDifference ? studentCount - interviewerCount : null
                };
            }).ToList();
        }
    }
}
