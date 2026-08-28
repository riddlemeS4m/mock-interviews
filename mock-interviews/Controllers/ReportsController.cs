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
    [Authorize(Roles = RolesConstants.AdminRole)]
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
            return View();
        }

        public async Task<IActionResult> EventStatistics()
        {
            var events = await _eventService.GetAllAsync();

            var participantCounts = new List<ParticipantCountPerDateViewModel>();

            foreach (var @event in events)
            {
                var studentCount = await _context.Interviews
                    .Where(e => e.Timeslot.EventId == @event.Id)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .CountAsync();

                var interviewerCount = await _context.InterviewerTimeslots
                    .Where(s => s.Timeslot.EventId == @event.Id)
                    .Select(s => s.InterviewerSignup.InterviewerId)
                    .Distinct()
                    .CountAsync();

                var volunteerCount = await _context.VolunteerTimeslots
                    .Where(v => v.Timeslot.EventId == @event.Id)
                    .Select(v => v.StudentId)
                    .Distinct()
                    .CountAsync();

                var countViewModel = new ParticipantCountPerDateViewModel
                {
                    EventDate = @event,
                    StudentCount = studentCount,
                    InterviewerCount = interviewerCount,
                    VolunteerCount = volunteerCount
                };

                participantCounts.Add(countViewModel);
            }

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

            var countlist = new List<ParticipantCountViewModel>();
            foreach (Timeslot timeslot in timeslots)
            {
                var studentCount = await _context.Interviews.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                var volunteerCount = await _context.VolunteerTimeslots.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                var interviewerCount = await _context.InterviewerTimeslots.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                countlist.Add(new ParticipantCountViewModel
                {
                    Timeslot = timeslot,
                    StudentCount = studentCount,
                    InterviewerCount = interviewerCount,
                    VolunteerCount = volunteerCount
                });
            }

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

            var countlist = new List<ParticipantCountViewModel>();
            foreach (Timeslot timeslot in timeslots)
            {
                var studentCount = await _context.Interviews.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                var volunteerCount = await _context.VolunteerTimeslots.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                var interviewerCount = await _context.InterviewerTimeslots.Where(x => x.TimeslotId == timeslot.Id).CountAsync();
                countlist.Add(new ParticipantCountViewModel
                {
                    Timeslot = timeslot,
                    StudentCount = studentCount,
                    InterviewerCount = interviewerCount,
                    VolunteerCount = volunteerCount,
                    Difference = studentCount - interviewerCount
                });
            }

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
    }
}
