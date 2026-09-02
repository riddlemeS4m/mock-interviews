using System.Globalization;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Email;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.HomeController;
using MockInterviews.Models.ViewModels.InterviewEventsController;
using MockInterviews.Models.ViewModels.Shared;
using MockInterviews.Options;
using MockInterviews.Services;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Controllers
{
    public class InterviewEventsController : Controller
    {
        private readonly IManageInterviews _manager;
        private readonly MockInterviewsDbContext _context;
        private readonly ParticipantSchedulingService _participantSchedulingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserService _userService;
        private readonly IEmailTransport _emailTransport;
        private readonly IHubContext<AssignInterviewsHub> _hubContext;
        private readonly IHubContext<AvailableInterviewersHub> _hubContextInterviewer;
        private readonly ILogger<InterviewEventsController> _logger;
        private readonly string _superUserEmail;

        public InterviewEventsController(IManageInterviews manager,
            MockInterviewsDbContext context,
            ParticipantSchedulingService participantSchedulingService,
            UserManager<ApplicationUser> userManager,
            UserService userService,
            IEmailTransport emailTransport,
            IHubContext<AssignInterviewsHub> hubContext,
            IHubContext<AvailableInterviewersHub> hubContextInterviewer,
            ILogger<InterviewEventsController> logger,
            IOptions<SuperUserOptions> superUserOptions)
        {
            _manager = manager;
            _context = context;
            _participantSchedulingService = participantSchedulingService;
            _userManager = userManager;
            _userService = userService;
            _emailTransport = emailTransport;
            _hubContext = hubContext;
            _hubContextInterviewer = hubContextInterviewer;
            _logger = logger;
            _superUserEmail = superUserOptions.Value.Email;
        }
        // adding a dummy comment bc I feel like it
        //--Dalton Wright, Fall 2023

        // GET: InterviewEvents
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Index()
        {
            //var interviewers = new List<AvailableInterviewer>();
            var busyInterviewers = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                // EF Core parses Include expressions without dereferencing an optional navigation.
                .ThenInclude(x => x!.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing && x.InterviewerTimeslot != null)
                .Select(x => x.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new AvailableInterviewer
                {
                    InterviewerId = x.InterviewerId,
                    InterviewType = x.Type ?? string.Empty,
                })
                .ToListAsync();

            foreach (var iv in interviewers)
            {
                iv.Name = await _userManager.Users
                    .Where(x => x.Id == iv.InterviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync() ?? "Deleted user";

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 8);

                iv.Room = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == iv.InterviewerId &&
                        x.Event != null && x.Event.Date.Date == date)
                    .Select(x => x.Location == null ? "Not Assigned" : x.Location.Room)
                    .FirstOrDefaultAsync() ?? "Not Assigned";
            }

            interviewers.Sort((x, y) => string.Compare(x.Name, y.Name));

            var hoursInAdvanceStr = _context.Settings
                .Where(x => x.Name == "interview_index_hours")
                .Select(x => x.Value)
                .FirstOrDefault();

            var hoursInAdvance = int.TryParse(hoursInAdvanceStr, out var configuredHours)
                ? configuredHours
                : throw new InvalidOperationException("Setting 'interview_index_hours' is not an integer.");

            var timeslot = await _context.Timeslots
                .OrderByDescending(x => x.MaxSignUps)
                .FirstOrDefaultAsync();
            var maxsignups = (timeslot?.MaxSignUps ?? 0) * hoursInAdvance * 2; //* 2 because there are two interviews per hour
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i!.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => i.Status != StatusConstants.Completed &&
                    i.Status != StatusConstants.NoShow &&
                    i.Status != StatusConstants.Excused &&
                    i.Timeslot.Event.IsActive)
                .OrderBy(i => i.TimeslotId)
                .ThenBy(i => i.Id)
                .Take(maxsignups)
                .ToListAsync();

            // 1. Collect unique StudentIds
            var studentIds = interviewEvents
                .Select(ie => ie.StudentId)
                .Distinct()
                .ToList();

            // 2. Fetch student names for all unique StudentIds
            var students = await _userManager.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}", u.Class })
                .ToListAsync();

            var studentNames = students
                .Select(u => new { u.Id, u.FullName })
                .ToDictionary(u => u.Id, u => u.FullName);

            var studentClasses = students
                .Select(u => new { u.Id, Class = ClassConstants.GetClassText(u.Class) })
                .ToDictionary(u => u.Id, u => u.Class);

            //3. Collect unique InterviewerIds
            var interviewerIds = await _context.InterviewerSignups
                .Select(ie => ie.InterviewerId)
                .Distinct()
                .ToListAsync();

            // 4. Fetch interviewer names for all unique InterviewerIds
            var interviewerNames = await _userManager.Users
                .Where(u => interviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}" })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            var model = new InterviewEventIndexViewModel();
            var eventslist = new List<InterviewEventViewModel>();

            foreach (Interview interviewEvent in interviewEvents)
            {
                var interviewEventViewModel = new InterviewEventViewModel();

                if (studentNames.TryGetValue(interviewEvent.StudentId, out var studentName))
                {
                    interviewEventViewModel.StudentName = studentName;
                }

                if (studentClasses.TryGetValue(interviewEvent.StudentId, out var studentClass))
                {
                    interviewEventViewModel.Class = studentClass;
                }

                if (interviewEvent.InterviewerTimeslot != null)
                {
                    if (interviewerNames.TryGetValue(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId, out var interviewerName))
                    {
                        interviewEventViewModel.InterviewerName = interviewerName;
                    }

                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
                else
                {
                    interviewEventViewModel.InterviewerName = "Not Assigned";
                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
            }

            model.Interviews = eventslist;
            model.AvailableInterviewers = interviewers;

            model.TechnicalInterviewers = new();
            model.BehavioralInterviewers = new();

            return View(model);
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> AttendanceReport()
        {
            //can't find my other attendance report method for some reason
            var uniqueStudentIds = await _context.Interviews
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            var students = await _userManager.Users
                .Where(u => uniqueStudentIds.Contains(u.Id))
                .Select(u => new
                {
                    u.FirstName,
                    u.LastName,
                    u.Class // Replace with the actual property name
                })
                .ToListAsync();

            var classReports = students
                .GroupBy(x => x.Class)
                .Select(g => new ClassReport
                {
                    ClassName = ClassConstants.GetClassText((Classes)g.Key),
                    StudentCount = g.Count()
                })
                .Where(r => r.StudentCount > 0)
                .ToList();

            var total = classReports
                .Select(x => x.StudentCount)
                .Sum();

            var signedup221 = classReports
                .Where(x => x.ClassName == ClassConstants.GetClassText(Classes.FirstSem))
                .Select(x => x.StudentCount)
                .FirstOrDefault();

            var summaries = new List<ClassReport>
            {
                new ClassReport
                {
                    ClassName = "Total",
                    StudentCount = total
                }
            };

            var entireProgram = await _context.RosteredStudents.CountAsync();
            var entire221 = await _context.RosteredStudents.Where(x => x.In221 == true).CountAsync();

            double percentEntireProgram = entireProgram == 0 ? 0 : (double)total / entireProgram;
            double percentEntire221 = entire221 == 0 ? 0 : (double)signedup221 / entire221;

            // Round to two decimal places
            percentEntireProgram = Math.Round(percentEntireProgram, 2) * 100;
            percentEntire221 = Math.Round(percentEntire221, 2) * 100;

            summaries.Add(new ClassReport
            {
                ClassName = "Total % Signed Up",
                StudentCount = (int)percentEntireProgram
            });
            summaries.Add(new ClassReport
            {
                ClassName = "221 % Signed Up",
                StudentCount = (int)percentEntire221
            });


            var viewModel = new AttendanceReportViewModel()
            {
                ClassReports = classReports,
                SummaryStats = summaries
            };

            return View("AttendanceReport", viewModel);
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> AssessFeedback()
        {
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i!.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(interview => interview.InterviewerRating != null)
                .ToListAsync();

            var userIds = interviewEvents.Select(interview => interview.StudentId)
                .Concat(interviewEvents.Where(interview => interview.InterviewerTimeslot is not null)
                    .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId))
                .Distinct()
                .ToArray();
            var users = await _userManager.Users
                .Where(user => userIds.Contains(user.Id))
                .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName, user.Class })
                .ToDictionaryAsync(user => user.Id);
            var model = interviewEvents.Select(interview => new InterviewEventViewModel
            {
                InterviewEvent = interview,
                StudentName = users.GetValueOrDefault(interview.StudentId)?.Name ?? "Deleted user",
                Class = users.TryGetValue(interview.StudentId, out var student)
                    ? ClassConstants.GetClassText(student.Class)
                    : string.Empty,
                InterviewerName = interview.InterviewerTimeslot is { } interviewerTimeslot
                    ? users.GetValueOrDefault(interviewerTimeslot.InterviewerSignup.InterviewerId)?.Name ?? "Deleted user"
                    : "Not assigned"
            }).ToList();

            return View("Feedback", model);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> FeedbackIndex()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var interviews = await _context.Interviews
                .Include(v => v.InterviewerTimeslot)
                .ThenInclude(v => v!.InterviewerSignup)
                .Include(v => v.Timeslot)
                .ThenInclude(v => v.Event)
                .Where(v => v.StudentId == userId && v.Status == StatusConstants.Completed)
                .OrderBy(v => v.Timeslot.Event.Date)
                .ThenBy(v => v.Timeslot.Time)
                .ToListAsync();

            var interviewerIds = interviews
                .Where(interview => interview.InterviewerTimeslot is not null)
                .Select(interview => interview.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();
            var interviewerNames = await _userManager.Users
                .Where(user => interviewerIds.Contains(user.Id))
                .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
                .ToDictionaryAsync(user => user.Id, user => user.Name);

            var model = new FeedbackListViewModel(interviews.Select(interview => new FeedbackListItemViewModel(
                interview.Id,
                interview.Timeslot.Event.Date,
                interview.Timeslot.Time,
                interview.InterviewerTimeslot is null
                    ? "Not assigned"
                    : interviewerNames.GetValueOrDefault(interview.InterviewerTimeslot.InterviewerSignup.InterviewerId, "Deleted user"),
                interview.Type ?? "Not specified",
                interview.InterviewerRating,
                interview.InterviewerFeedback,
                interview.ProcessFeedback)).ToList());

            return View(model);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> ProvideFeedback(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x!.InterviewerSignup)
                .Include(x => x.Location)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == userId && x.Status == StatusConstants.Completed);
            if (interviewEvent is null)
            {
                return NotFound();
            }

            return View(await BuildFeedbackFormAsync(interviewEvent));
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProvideFeedback(int id, [Bind("Id,InterviewerRating,InterviewerFeedback,ProcessFeedback")] FeedbackFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var interviewEventActual = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x!.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == userId && x.Status == StatusConstants.Completed);
            if (interviewEventActual is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var feedbackForm = await BuildFeedbackFormAsync(interviewEventActual);
                feedbackForm.InterviewerRating = model.InterviewerRating;
                feedbackForm.InterviewerFeedback = model.InterviewerFeedback;
                feedbackForm.ProcessFeedback = model.ProcessFeedback;
                return View(feedbackForm);
            }

            interviewEventActual.InterviewerRating = model.InterviewerRating;
            interviewEventActual.InterviewerFeedback = model.InterviewerFeedback;
            interviewEventActual.ProcessFeedback = model.ProcessFeedback;
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Your feedback was saved.";
            return RedirectToAction("FeedbackIndex", "InterviewEvents");

        }

        private async Task<FeedbackFormViewModel> BuildFeedbackFormAsync(Interview interviewEvent)
        {
            var interviewerName = "Not assigned";
            if (interviewEvent.InterviewerTimeslot is not null)
            {
                var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);
                interviewerName = interviewer is null ? "Deleted user" : GetDisplayName(interviewer);
            }

            return new FeedbackFormViewModel
            {
                Id = interviewEvent.Id,
                Date = interviewEvent.Timeslot.Event.Date,
                Time = interviewEvent.Timeslot.Time,
                InterviewerName = interviewerName,
                InterviewType = interviewEvent.Type ?? "Not specified",
                InterviewerRating = interviewEvent.InterviewerRating,
                InterviewerFeedback = interviewEvent.InterviewerFeedback,
                ProcessFeedback = interviewEvent.ProcessFeedback
            };
        }


        // GET: InterviewEvents/Details/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Details(int? id)
        {
            if (_context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i!.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            var student = await _userManager.Users
                .Where(x => x.Id == interviewEvent.StudentId)
                .Select(x => new { x.FirstName, x.LastName, x.Class })
                .FirstOrDefaultAsync();

            if (interviewEvent.InterviewerTimeslot == null)
            {
                var viewModel = new InterviewEventViewModel
                {
                    InterviewEvent = interviewEvent,
                    InterviewerName = "Not Assigned",
                    Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class),
                    StudentName = student is null ? "Deleted user" : student.FirstName + " " + student.LastName
                };

                return View(viewModel);
            }


            var interviewer = await _userManager.Users
                .Where(x => x.Id == interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Select(x => new { x.FirstName, x.LastName })
                .FirstOrDefaultAsync();

            var secondViewModel = new InterviewEventViewModel
            {
                InterviewEvent = interviewEvent,
                InterviewerName = interviewer is null ? "Deleted user" : interviewer.FirstName + " " + interviewer.LastName,
                StudentName = student is null ? "Deleted user" : student.FirstName + " " + student.LastName,
                Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class)
            };

            return View(secondViewModel);
        }

        // GET: InterviewEvents/Create
        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var user = await _userManager.Users
                .Where(x => x.Id == userId)
                .FirstOrDefaultAsync();
            if (user is null)
            {
                return Challenge();
            }

            return View(await BuildStudentSignupViewModelAsync(user));
        }

        // POST: InterviewEvents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int[] SelectedTimeslotIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Challenge();
            }

            if (SelectedTimeslotIds is null || SelectedTimeslotIds.Length != 1)
            {
                ModelState.AddModelError(nameof(SelectedTimeslotIds), "Select one interview start time.");
                return View(await BuildStudentSignupViewModelAsync(user, SelectedTimeslotIds));
            }

            var isFirstSemesterStudent = user.Class == Classes.NotYetMIS || user.Class == Classes.FirstSem;
            var selectedTimeslot = await _context.Timeslots
                .Include(timeslot => timeslot.Event)
                .SingleOrDefaultAsync(timeslot => timeslot.Id == SelectedTimeslotIds[0]);
            var pairedTimeslot = selectedTimeslot is null
                ? null
                : await _participantSchedulingService.FindAdjacentStudentInterviewTimeslotAsync(selectedTimeslot);

            var isEligible = selectedTimeslot is not null &&
                pairedTimeslot is not null &&
                selectedTimeslot.IsStudent &&
                selectedTimeslot.IsActive &&
                pairedTimeslot.IsActive &&
                selectedTimeslot.Event.IsActive &&
                (isFirstSemesterStudent
                    ? selectedTimeslot.Event.For221 != For221.n
                    : selectedTimeslot.Event.For221 != For221.y) &&
                await _context.Interviews.CountAsync(interview => interview.TimeslotId == selectedTimeslot.Id) < selectedTimeslot.MaxSignUps &&
                await _context.Interviews.CountAsync(interview => interview.TimeslotId == pairedTimeslot.Id) < pairedTimeslot.MaxSignUps &&
                !await _context.Interviews
                    .Include(interview => interview.Timeslot)
                    .ThenInclude(timeslot => timeslot.Event)
                    .AnyAsync(interview => interview.StudentId == userId && interview.Timeslot.Event.IsActive);

            if (!isEligible)
            {
                ModelState.AddModelError(nameof(SelectedTimeslotIds), "The requested interview timeslot is not available. Refresh the page and try again.");
                return View(await BuildStudentSignupViewModelAsync(user, SelectedTimeslotIds));
            }

            var interviewTypeTwo = isFirstSemesterStudent
                ? InterviewTypeConstants.Behavioral
                : InterviewTypeConstants.Technical;

            var interviewEvents = new List<Interview>
            {
                new Interview
                {
                    TimeslotId = selectedTimeslot!.Id,
                    StudentId = userId,
                    Status = StatusConstants.Default,
                    Type = InterviewTypeConstants.Behavioral
                },
                new Interview
                {
                    TimeslotId = pairedTimeslot!.Id,
                    StudentId = userId,
                    Status = StatusConstants.Default,
                    Type= interviewTypeTwo
                }
            };

            if (ModelState.IsValid)
            {
                _context.AddRange(interviewEvents);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(nameof(SelectedTimeslotIds), "The requested interview timeslot is no longer available. Refresh the page and try again.");
                    return View(await BuildStudentSignupViewModelAsync(user, SelectedTimeslotIds));
                }

                var emailTimes = new List<Interview>();
                List<string> calendarEvents = new();

                var newEvent = await _context.Interviews
                    .Include(v => v.Timeslot)
                    .ThenInclude(y => y.Event)
                    .Where(v => v.Id == interviewEvents[0].Id)
                    .FirstOrDefaultAsync();
                if (newEvent is not null)
                {
                    emailTimes.Add(newEvent);
                }
                newEvent = await _context.Interviews
                    .Include(v => v.Timeslot)
                    .ThenInclude(y => y.Event)
                    .Where(v => v.Id == interviewEvents[1].Id)
                    .FirstOrDefaultAsync();
                if (newEvent is not null)
                {
                    emailTimes.Add(newEvent);
                }

                string interviewDetails = "";
                foreach (var interview in emailTimes)
                {
                    var plainBytes = Encoding.UTF8.GetBytes(CreateCalendarEvent(interview.Timeslot.Time, interview.Timeslot.Time.AddMinutes(30)));
                    string tempEvent = Convert.ToBase64String(plainBytes);
                    calendarEvents.Add(tempEvent);
                    interviewDetails += interview.ToString();
                }

                ASendAnEmail emailer = new StudentSignupEmail();
                if (user.Email is { Length: > 0 } emailAddress)
                {
                    await emailer.SendEmailAsync(_emailTransport, _superUserEmail, "Mock Interview Sign-Up Confirmation", emailAddress, user.FirstName ?? "Deleted user", interviewDetails, calendarEvents);
                }

                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        private async Task<InterviewEventSignupViewModel> BuildStudentSignupViewModelAsync(
            ApplicationUser user,
            IEnumerable<int>? selectedTimeslotIds = null)
        {
            var isFirstSemesterStudent = user.Class == Classes.NotYetMIS || user.Class == Classes.FirstSem;
            var timeslots = await _context.Timeslots
                .Where(timeslot => timeslot.IsStudent && timeslot.IsActive)
                .Include(timeslot => timeslot.Event)
                .Where(timeslot => _context.Interviews.Count(interview => interview.TimeslotId == timeslot.Id) < timeslot.MaxSignUps)
                .Where(timeslot => timeslot.Event.IsActive &&
                    (isFirstSemesterStudent ? timeslot.Event.For221 != For221.n : timeslot.Event.For221 != For221.y))
                .ToListAsync();
            var signedUp = await _context.Interviews
                .Include(interview => interview.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
                .AnyAsync(interview => interview.StudentId == user.Id && interview.Timeslot.Event.IsActive);

            return new InterviewEventSignupViewModel
            {
                EventDays = _participantSchedulingService.ComposeEventDays(timeslots, selectedTimeslotIds),
                StudentClass = user.Class,
                SignedUp = signedUp,
                SelectedTimeslotIds = selectedTimeslotIds?.ToArray() ?? []
            };
        }

        // GET: InterviewEvents/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (_context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound();
            }

            var all = await OutsourceQuery2024(interviewEvent);
            var behavioralInterviewers = OutsourceQueryBehavioral2024(all);
            var technicalInterviewers = OutsourceQueryTechnical2024(all);

            var requestedInterviewers = new List<SelectListItem>();
            if (interviewEvent.Type == InterviewTypeConstants.Technical)
            {
                requestedInterviewers = technicalInterviewers;
            }
            else
            {
                requestedInterviewers = behavioralInterviewers;
            }

            var studentname = await _userManager.Users
                .Where(x => x.Id == interviewEvent.StudentId)
                .Select(x => x.FirstName + " " + x.LastName)
                .FirstOrDefaultAsync();

            var interviewEventManageViewModel = new InterviewEventManageViewModel
            {
                InterviewEvent = interviewEvent,
                BehavioralInterviewers = behavioralInterviewers,
                TechnicalInterviewers = technicalInterviewers,
                RequestedInterviewers = requestedInterviewers,
                StudentName = studentname ?? "Deleted user"
            };

            return View(interviewEventManageViewModel);
        }

        // POST: InterviewEvents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,LocationId,TimeslotId,Type,Status,InterviewerTimeslotId")] Interview interviewEvent, string InterviewerId)
        {
            if (id != interviewEvent.Id)
            {
                return NotFound();
            }

            //if(InterviewerId == "0")
            //{
            //    interviewEvent.SignupInterviewerTimeslot = null;
            //}

            if (ModelState.IsValid)
            {
                if (InterviewerId == "0")
                {
                    interviewEvent.InterviewerTimeslot = null;
                    interviewEvent.InterviewerTimeslotId = null;
                    interviewEvent.Location = null;
                    interviewEvent.LocationId = null;
                }
                else
                {
                    var signupInterviewTimeslot = await _context.InterviewerTimeslots
                        .Include(x => x.InterviewerSignup)
                        .Include(x => x.Timeslot)
                        .ThenInclude(x => x.Event)
                        .Where(x => x.InterviewerSignup.InterviewerId == InterviewerId)
                        .FirstOrDefaultAsync();

                    if (signupInterviewTimeslot is null)
                    {
                        interviewEvent.InterviewerTimeslot = null;
                        interviewEvent.InterviewerTimeslotId = null;
                        interviewEvent.Location = null;
                        interviewEvent.LocationId = null;
                    }
                    else
                    {
                        if (interviewEvent.Status == StatusConstants.CheckedIn)
                        {
                            interviewEvent.Status = StatusConstants.Ongoing;
                        }

                        var interviewerPreference = "";
                        if (signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.IsVirtual)
                        {
                            interviewerPreference = InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson;
                        }

                        var location = await _context.InterviewerLocations
                        .Include(x => x.Location)
                        .Where(x => x.InterviewerId == InterviewerId &&
                            x.Preference == interviewerPreference &&
                            x.EventId == signupInterviewTimeslot.Timeslot.EventId &&
                            x.LocationId != null)
                        .FirstOrDefaultAsync();

                        if (location?.Location is null)
                        {
                            interviewEvent.Location = null;
                            interviewEvent.LocationId = null;
                        }
                        else
                        {
                            interviewEvent.Location = location.Location;
                            interviewEvent.LocationId = location.Location.Id;
                        }

                        interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                        interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;
                    }
                }

                try
                {
                    _context.Update(interviewEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterviewEventExists(interviewEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                if (User.IsInRole(RolesConstants.AdminRole))
                {
                    await UpdateHub(id);
                    await UpdateHub();

                    return RedirectToAction(nameof(Index));
                }
                return RedirectToAction("Index", "Home");
            }

            return View(interviewEvent);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Override(int? id)
        {
            if (_context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();
            if (interviewEvent is null)
            {
                return NotFound();
            }

            //var selectedInterviewers = await _userManager.GetUsersInRoleAsync(RolesConstants.InterviewerRole);
            //var interviewers = selectedInterviewers.ToList();

            var selectedInterviewers = await _context.InterviewerTimeslots
                .Include(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Timeslot.EventId == interviewEvent.Timeslot.EventId &&
                    x.Timeslot.Event.IsActive)
                .Select(x => x.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var selectedInterviewersNames = new List<SelectListItem>();
            if (selectedInterviewers.Count == 0)
            {
                if (interviewEvent.InterviewerTimeslot != null)
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId,
                        Text = interviewEvent.InterviewerTimeslot.InterviewerSignup.FirstName + " " + interviewEvent.InterviewerTimeslot.InterviewerSignup.LastName
                    });
                }
                else
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = "0",
                        Text = "--Unassigned--"
                    });
                }
            }
            else
            {
                if (interviewEvent.InterviewerTimeslot != null)
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId,
                        Text = interviewEvent.InterviewerTimeslot.InterviewerSignup.FirstName + " " + interviewEvent.InterviewerTimeslot.InterviewerSignup.LastName
                    });
                }
                foreach (string sit in selectedInterviewers)
                {
                    var user = await _userManager.Users
                        .Where(u => u.Id == sit)
                        .Select(u => new { u.Id, u.FirstName, u.LastName })
                        .FirstOrDefaultAsync();
                    if (interviewEvent is null)
                    {
                        return NotFound();
                    }

                    if (user != null)
                    {
                        selectedInterviewersNames.Add(new SelectListItem
                        {
                            Value = user.Id,
                            Text = user.FirstName + " " + user.LastName
                        });
                    }
                }
                selectedInterviewersNames = selectedInterviewersNames.OrderBy(item => item.Text).ToList();
                selectedInterviewersNames.Insert(0, new SelectListItem
                {
                    Value = "0",
                    Text = "--Unassigned--"
                });

            }


            var interviewEventManageViewModel = new InterviewEventManageViewModel
            {
                InterviewEvent = interviewEvent,
                RequestedInterviewers = selectedInterviewersNames,
                BehavioralInterviewers = selectedInterviewersNames,
                TechnicalInterviewers = selectedInterviewersNames
            };

            return View(interviewEventManageViewModel);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Override(int id, [Bind("Id,StudentId,LocationId,TimeslotId,Type,Status,InterviewerTimeslotId")] Interview interviewEvent, string InterviewerId)
        {
            if (id != interviewEvent.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (InterviewerId == "0")
                {
                    interviewEvent.InterviewerTimeslot = null;
                }
                else
                {
                    var signupInterviewTimeslot = await _context.InterviewerTimeslots
                        .Include(x => x.InterviewerSignup)
                        .Include(x => x.Timeslot)
                        .ThenInclude(x => x.Event)
                        .Where(x => x.TimeslotId == interviewEvent.TimeslotId && x.InterviewerSignup.InterviewerId == InterviewerId)
                        .FirstOrDefaultAsync();

                    if (signupInterviewTimeslot != null)
                    {
                        var interviewerPreference = "";
                        if (signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.IsVirtual)
                        {
                            interviewerPreference = InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson;
                        }

                        var location = await _context.InterviewerLocations
                            .Include(x => x.Location)
                            .Where(x => x.InterviewerId == InterviewerId && x.Preference == interviewerPreference && x.EventId == signupInterviewTimeslot.Timeslot.EventId && x.LocationId != null)
                            .FirstOrDefaultAsync();

                        interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                        interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;

                        if (location != null)
                        {
                            interviewEvent.LocationId = location.Location?.Id;
                            interviewEvent.Location = location.Location;
                        }

                        if (interviewEvent.Status == StatusConstants.Ongoing)
                        {
                            interviewEvent.StartedAt = DateTime.UtcNow;
                        }
                        else if (interviewEvent.Status == StatusConstants.CheckedIn)
                        {
                            interviewEvent.CheckedInAt = DateTime.UtcNow;
                        }
                        else if (interviewEvent.Status == StatusConstants.Completed)
                        {
                            interviewEvent.EndedAt = DateTime.UtcNow;
                        }
                    }
                }

                try
                {
                    _context.Update(interviewEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterviewEventExists(interviewEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                var newInterviewEvent = await _context.Interviews
                    .Include(x => x.Location)
                    .Include(x => x.InterviewerTimeslot)
                    .ThenInclude(x => x!.InterviewerSignup)
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (newInterviewEvent is null)
                {
                    return NotFound();
                }

                var student = await _userManager.Users
                    .Where(x => x.Id == newInterviewEvent.StudentId)
                    .FirstOrDefaultAsync();

                var interviewername = "Not Assigned";
                var interviewerId = "0";

                if (newInterviewEvent.InterviewerTimeslot != null)
                {
                    interviewerId = newInterviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId;
                    interviewername = await _userManager.Users
                        .Where(x => x.Id == interviewerId)
                        .Select(x => x.FirstName + " " + x.LastName)
                        .FirstOrDefaultAsync() ?? "Deleted user";
                }

                if (newInterviewEvent.Location == null)
                {
                    newInterviewEvent.Location = new Location()
                    {
                        Room = "Not Assigned"
                    };
                }

                var time = $"{newInterviewEvent.Timeslot.Time:hh:mm tt}";
                var date = $"{newInterviewEvent.Timeslot.Event.Date:M/d/yyyy}";

                await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", newInterviewEvent, GetDisplayName(student), student?.GetClass() ?? string.Empty, interviewerId, interviewername, time, date);
                await UpdateHub();

                return RedirectToAction(nameof(Index));
            }

            return View(interviewEvent);
        }

        // GET: InterviewEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (_context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i!.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            var student = await _userManager.FindByIdAsync(interviewEvent.StudentId);

            if (interviewEvent.InterviewerTimeslot == null)
            {
                var viewModel = new InterviewEventViewModel
                {
                    InterviewEvent = interviewEvent,
                    InterviewerName = "Not Assigned",
                    StudentName = GetDisplayName(student),
                    Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class)
                };

                return View(viewModel);
            }


            var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);

            var secondViewModel = new InterviewEventViewModel
            {
                InterviewEvent = interviewEvent,
                InterviewerName = GetDisplayName(interviewer),
                StudentName = student is null ? "Deleted user" : student.FirstName + " " + student.LastName,
                Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class)
            };

            await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", new Interview { Id = id ?? 0 }, "delete", "0", "", "", "");

            return View(secondViewModel);
        }

        // POST: InterviewEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent != null)
            {
                _context.Interviews.Remove(interviewEvent);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        private bool InterviewEventExists(int id)
        {
            return (_context.Interviews?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private async Task<List<string>> OutsourceQuery(Interview interviewEvent)
        {
            // Get the timeslot of the current user's interview
            var timeslot = _context.Interviews
                .Include(i => i.Timeslot.Event)
                .Where(i => i.StudentId == interviewEvent.StudentId && i.TimeslotId == interviewEvent.TimeslotId)
                .Select(i => i.Timeslot)
                .FirstOrDefault();
            if (timeslot is null)
            {
                return [];
            }

            // Get the SignupInterviewerTimeslots for the same timeslot and event date as the user's interview
            var signupInterviewerTimeslots = _context.InterviewerTimeslots
                .Include(s => s.InterviewerSignup)
                .Where(s => s.TimeslotId == timeslot.Id &&
                    s.Timeslot.Event.Date == timeslot.Event.Date &&
                    s.Timeslot.Event.IsActive == true)
                .ToList();

            var interviewers = _context.InterviewerSignups
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have signed up to deliver the same type of interview as the student needs
            var selectedTypes = _context.InterviewerSignups
                .Where(u => ((interviewEvent.Type == InterviewTypeConstants.Technical && u.IsTechnical) ||
                             (interviewEvent.Type == InterviewTypeConstants.Behavioral && u.IsBehavioral)))
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have a timeslot that matches the student's timeslot
            var selectedTimeslot = signupInterviewerTimeslots
                .Select(u => u.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            //get list of all distinct interviewers that are in an interview event with a status of checked in or ongoing
            var selectedStatusNot = _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x!.Timeslot)
                .ThenInclude(x => x.Event)
                .Include(x => x.InterviewerTimeslot!.InterviewerSignup)
                .Where(x => x.InterviewerTimeslot != null && x.InterviewerTimeslot.Timeslot.Event.IsActive)
                .Select(x => x.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            var selectedStatus = interviewers
                .Except(selectedStatusNot)
                .ToList();

            //Get list of all distinct interviewers that are assigned to a location
            var selectedLocation = _context.InterviewerLocations
                .Where(x => x.LocationId != null && x.Event != null &&
                    x.Event == timeslot.Event && x.Event.IsActive)
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have interviewed this student
            var haveInterviewed = _context.Interviews
                .Where(x => x.StudentId == interviewEvent.StudentId && x.InterviewerTimeslot != null)
                .Select(x => x.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            var haveNotInterviewed = interviewers
                .Except(haveInterviewed)
                .ToList();

            //Get list of all distinct interviewers that are not the student
            var notStudent = interviewers
                .Where(x => x != interviewEvent.StudentId)
                .Distinct()
                .ToList();

            //combine the previous four query results
            var selectedInterviewers = selectedTypes
                .Intersect(selectedTimeslot)
                .Intersect(selectedStatus)
                .Intersect(selectedLocation)
                .Intersect(haveNotInterviewed)
                .Intersect(notStudent)
                .ToList();

            return selectedInterviewers;
        }

        private async Task<List<InterviewerOptionViewModel>> OutsourceQuery2024(Interview ie)
        {
            var allInterviewers = await _context.InterviewerTimeslots
                .Include(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x =>
                    x.Timeslot.Event.IsActive &&
                    x.Timeslot.Event.Date == ie.Timeslot.Event.Date &&
                    x.InterviewerSignup.CheckedIn &&
                    !_context.Interviews
                        .Where(i => i.Status == "Ongoing" && i.InterviewerTimeslot != null)
                        .Select(i => i.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                        .Contains(x.InterviewerSignup.InterviewerId))
                .Select(x => new InterviewerOptionViewModel
                {
                    Name = x.InterviewerSignup.FirstName + " " + x.InterviewerSignup.LastName,
                    Id = x.InterviewerSignup.InterviewerId.ToString(),
                    Technical = x.InterviewerSignup.IsTechnical,
                    Behavioral = x.InterviewerSignup.IsBehavioral
                })
                .ToListAsync();

            return allInterviewers;
        }

        private static List<SelectListItem> OutsourceQueryTechnical2024(List<InterviewerOptionViewModel> all)
        {
            var allInterviewers = all
                .Distinct(new InterviewerOptionViewModelComparer())
                .OrderBy(x => x.Name)
                .ToList();

            var technicalInterviewers = allInterviewers
                .Where(x => x.Technical)
                .Select(x => new SelectListItem
                {
                    Value = x.Id,
                    Text = x.Name
                })
                .ToList();

            technicalInterviewers.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "--Unassigned--"
            });

            return technicalInterviewers;
        }

        private static List<SelectListItem> OutsourceQueryBehavioral2024(List<InterviewerOptionViewModel> all)
        {
            var allInterviewers = all
                .Distinct(new InterviewerOptionViewModelComparer())
                .OrderBy(x => x.Name)
                .ToList();

            var behavioralInterviewers = allInterviewers
                .Where(x => x.Behavioral)
                .Select(x => new SelectListItem
                {
                    Value = x.Id,
                    Text = x.Name
                })
                .ToList();

            behavioralInterviewers.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "--Unassigned--"
            });

            return behavioralInterviewers;
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> UserDelete(int? id)
        {
            if (_context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews.Include(i => i.Location).Include(i => i.InterviewerTimeslot)
                .Include(i => i.Timeslot)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interviewEvent == null)
            {
                return NotFound();
            }

            if (interviewEvent.StudentId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                return NotFound();
            }

            return View(interviewEvent);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserDeleteConfirmed(int id)
        {
            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent != null && interviewEvent.StudentId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                _context.Interviews.Remove(interviewEvent);
                TempData["StatusMessage"] = "Your interview was cancelled.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> CreateForStudent()
        {
            return View(await BuildAdminStudentSignupViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> CreateForStudent(int[] SelectedTimeslotIds, string StudentId)
        {
            SelectedTimeslotIds ??= [];
            if (SelectedTimeslotIds.Length != 1)
            {
                ModelState.AddModelError(nameof(SelectedTimeslotIds), "Select one interview start time.");
            }

            if (string.IsNullOrEmpty(StudentId))
            {
                ModelState.AddModelError("StudentId", "Please select a student");
            }

            var user = string.IsNullOrEmpty(StudentId)
                ? null
                : await _userManager.FindByIdAsync(StudentId);
            if (user is not null && !await _userManager.IsInRoleAsync(user, RolesConstants.StudentRole))
            {
                ModelState.AddModelError(nameof(StudentId), "The selected account is not an active student.");
            }

            if (!ModelState.IsValid || user is null)
            {
                return View(await BuildAdminStudentSignupViewModelAsync(StudentId, SelectedTimeslotIds));
            }

            var selectedTimeslot = await _context.Timeslots
                .Include(timeslot => timeslot.Event)
                .SingleOrDefaultAsync(timeslot => timeslot.Id == SelectedTimeslotIds[0]);
            var pairedTimeslot = selectedTimeslot is null
                ? null
                : await _participantSchedulingService.FindAdjacentStudentInterviewTimeslotAsync(selectedTimeslot);

            var isFirstSemesterStudent = user.Class == Classes.NotYetMIS || user.Class == Classes.FirstSem;
            var isEligible = selectedTimeslot is not null &&
                pairedTimeslot is not null &&
                selectedTimeslot.IsStudent &&
                selectedTimeslot.IsActive &&
                pairedTimeslot.IsActive &&
                selectedTimeslot.Event.IsActive &&
                (isFirstSemesterStudent
                    ? selectedTimeslot.Event.For221 != For221.n
                    : selectedTimeslot.Event.For221 != For221.y) &&
                await _context.Interviews.CountAsync(interview => interview.TimeslotId == selectedTimeslot.Id) < selectedTimeslot.MaxSignUps &&
                await _context.Interviews.CountAsync(interview => interview.TimeslotId == pairedTimeslot.Id) < pairedTimeslot.MaxSignUps &&
                !await _context.Interviews
                    .Include(interview => interview.Timeslot)
                    .ThenInclude(timeslot => timeslot.Event)
                    .AnyAsync(interview => interview.StudentId == StudentId && interview.Timeslot.Event.IsActive);

            if (!isEligible)
            {
                ModelState.AddModelError(nameof(SelectedTimeslotIds), "The requested interview timeslot is not available. Refresh the page and try again.");
                return View(await BuildAdminStudentSignupViewModelAsync(StudentId, SelectedTimeslotIds));
            }

            var interviewTypeTwo = isFirstSemesterStudent ? InterviewTypeConstants.Behavioral : InterviewTypeConstants.Technical;

            var interviewEvents = new List<Interview>
            {
                new()
                {
                    TimeslotId = selectedTimeslot!.Id,
                    StudentId = StudentId,
                    Status = StatusConstants.Default,
                    Type = InterviewTypeConstants.Behavioral
                },
                new()
                {
                    TimeslotId = pairedTimeslot!.Id,
                    StudentId = StudentId,
                    Status = StatusConstants.Default,
                    Type= interviewTypeTwo
                }
            };

            _context.Interviews.AddRange(interviewEvents);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(SelectedTimeslotIds), "The requested interview timeslot is no longer available. Refresh the page and try again.");
                return View(await BuildAdminStudentSignupViewModelAsync(StudentId, SelectedTimeslotIds));
            }

            return RedirectToAction("Index", "InterviewEvents");
        }

        private async Task<InterviewSignupByAdminViewModel> BuildAdminStudentSignupViewModelAsync(
            string? studentId = null,
            IEnumerable<int>? selectedTimeslotIds = null)
        {
            var students = await _userService.GetUsersByRole(RolesConstants.StudentRole);
            var activeTimeslots = await _context.Timeslots
                .Include(timeslot => timeslot.Event)
                .Where(timeslot => timeslot.IsActive && timeslot.Event.IsActive)
                .OrderBy(timeslot => timeslot.Event.Date)
                .ThenBy(timeslot => timeslot.Time)
                .ToListAsync();
            var signupCounts = await _context.Interviews
                .GroupBy(interview => interview.TimeslotId)
                .ToDictionaryAsync(group => group.Key, group => group.Count());

            var availableStarts = new List<Timeslot>();
            foreach (var timeslot in activeTimeslots.Where(timeslot => timeslot.IsStudent &&
                         signupCounts.GetValueOrDefault(timeslot.Id) < timeslot.MaxSignUps))
            {
                var adjacent = activeTimeslots.SingleOrDefault(candidate =>
                    candidate.EventId == timeslot.EventId &&
                    candidate.Time == timeslot.Time.AddMinutes(30));
                if (adjacent is not null && signupCounts.GetValueOrDefault(adjacent.Id) < adjacent.MaxSignUps)
                {
                    availableStarts.Add(timeslot);
                }
            }

            return new InterviewSignupByAdminViewModel
            {
                Students = students
                    .Select(student => new SelectListItem
                    {
                        Value = student.Id,
                        Text = $"{student.FirstName} {student.LastName}",
                        Selected = student.Id == studentId
                    })
                    .OrderBy(student => student.Text)
                    .ToList(),
                EventDays = _participantSchedulingService.ComposeEventDays(availableStarts, selectedTimeslotIds),
                SelectedTimeslotIds = selectedTimeslotIds?.ToArray() ?? [],
                StudentId = studentId ?? string.Empty
            };
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> StudentCheckIn(int id)
        {
            _logger.LogInformation("Interviewee checked in. Interview Id: {id}", id);

            if (_context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            interviewEvent.Status = StatusConstants.CheckedIn;
            interviewEvent.CheckedInAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> StudentComplete(int id)
        {
            _logger.LogInformation($"Interview marked completed. Id: {id}");

            if (_context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x!.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole(RolesConstants.AdminRole) &&
                (interviewEvent.InterviewerTimeslot?.InterviewerSignup.InterviewerId != userId))
            {
                return Forbid();
            }

            interviewEvent.Status = StatusConstants.Completed;
            interviewEvent.EndedAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> CompleteAssignedInterview(int id)
        {
            var result = await StudentComplete(id);
            return result is NoContentResult
                ? RedirectToAction("Index", "Home")
                : result;
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> StudentNoShow(int id)
        {
            _logger.LogInformation($"Interview marked no-show. Id: {id}");

            if (_context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            interviewEvent.Status = StatusConstants.NoShow;
            interviewEvent.EndedAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> EditInline(int Id, string Status, string InterviewerId, string Type)
        {
            _logger.LogInformation($"Id: {Id}, Status: {Status}, InterviewerId: {InterviewerId}, Type: {Type}");

            if (Id == 0 || Status == null || Type == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == Id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound();
            }

            interviewEvent.Status = Status;
            interviewEvent.Type = Type;

            if (InterviewerId == "0" || InterviewerId == "" || InterviewerId == null)
            {
                interviewEvent.InterviewerTimeslot = null;
                interviewEvent.InterviewerTimeslotId = null;
                interviewEvent.Location = null;
                interviewEvent.LocationId = null;
            }
            else
            {
                var signupInterviewTimeslot = await _context.InterviewerTimeslots
                    .Include(x => x.InterviewerSignup)
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.InterviewerSignup.InterviewerId == InterviewerId &&
                        x.TimeslotId == interviewEvent.TimeslotId)
                    .FirstOrDefaultAsync();

                if (signupInterviewTimeslot is null)
                {
                    return BadRequest("The selected interviewer is not available for this timeslot.");
                }
                else
                {
                    if (interviewEvent.Status == StatusConstants.CheckedIn)
                    {
                        interviewEvent.Status = StatusConstants.Ongoing;
                        interviewEvent.StartedAt = DateTime.UtcNow;
                    }

                    var interviewerPreference = "";
                    if (signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson)
                    {
                        interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                    }
                    else if (signupInterviewTimeslot.InterviewerSignup.IsVirtual)
                    {
                        interviewerPreference = InterviewLocationConstants.IsVirtual;
                    }
                    else if (signupInterviewTimeslot.InterviewerSignup.InPerson)
                    {
                        interviewerPreference = InterviewLocationConstants.InPerson;
                    }

                    var location = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Where(x => x.InterviewerId == InterviewerId &&
                        x.Preference == interviewerPreference &&
                        x.EventId == signupInterviewTimeslot.Timeslot.EventId &&
                        x.LocationId != null)
                    .FirstOrDefaultAsync();

                    if (location?.Location is null)
                    {
                        interviewEvent.Location = null;
                        interviewEvent.LocationId = null;
                    }
                    else
                    {
                        interviewEvent.Location = location.Location;
                        interviewEvent.LocationId = location.Location.Id;
                    }

                    interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                    interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;
                }
            }

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();

                await UpdateHub(Id);
                await UpdateHub();

                var student = await _userManager.Users
                    .Where(x => x.Id == interviewEvent.StudentId)
                    .Select(x => new
                    {
                        StudentName = x.FirstName + " " + x.LastName,
                    })
                    .FirstOrDefaultAsync();

                var response = new EditInterviewResponse
                {
                    StudentName = student?.StudentName ?? "Deleted user",
                    InterviewType = interviewEvent.Type, // Replace with actual values
                    InterviewerName = interviewEvent.InterviewerTimeslot?.InterviewerSignup.GetInterviewerName() ?? "Not Assigned",
                    Location = interviewEvent.Location?.Room ?? "Not Assigned",
                };

                await UpdateHub(Id);

                return Ok(response);

            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InterviewEventExists(interviewEvent.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<ActionResult<AvailableInterviewersViewModel>> GetAvailableInterviewers(int id)
        {
            if (id == 0 || !_context.Interviews.Any(x => x.Id == id))
            {
                return BadRequest("Invalid ID or Interview Event not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (interviewEvent == null)
            {
                return NotFound("Interview Event not found.");
            }

            var vm = new AvailableInterviewersViewModel();

            var all = await OutsourceQuery2024(interviewEvent);
            vm.BehavioralInterviewers = OutsourceQueryBehavioral2024(all);
            vm.TechnicalInterviewers = OutsourceQueryTechnical2024(all);

            return vm;
        }

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        public async Task<IActionResult> GetCompletedInterviews()
        {
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                // EF Core parses Include expressions without dereferencing an optional navigation.
                .ThenInclude(i => i!.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => (i.Status == StatusConstants.Completed ||
                    i.Status == StatusConstants.NoShow ||
                    i.Status == StatusConstants.Excused) &&
                    i.Timeslot.Event.IsActive)
                .ToListAsync();

            // 1. Collect unique StudentIds
            var studentIds = interviewEvents
                .Select(ie => ie.StudentId)
                .Distinct()
                .ToList();

            // 2. Fetch student names for all unique StudentIds
            var students = await _userManager.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}", u.Class })
                .ToListAsync();

            var studentNames = students
                .Select(u => new { u.Id, u.FullName })
                .ToDictionary(u => u.Id, u => u.FullName);

            var studentClasses = students
                .Select(u => new { u.Id, Class = ClassConstants.GetClassText(u.Class) })
                .ToDictionary(u => u.Id, u => u.Class);

            //3. Collect unique InterviewerIds
            var interviewerIds = await _context.InterviewerSignups
                .Select(ie => ie.InterviewerId)
                .Distinct()
                .ToListAsync();

            // 4. Fetch interviewer names for all unique InterviewerIds
            var interviewerNames = await _userManager.Users
                .Where(u => interviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}" })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            var eventslist = new List<InterviewEventViewModel>();

            foreach (Interview interviewEvent in interviewEvents)
            {
                var interviewEventViewModel = new InterviewEventViewModel();

                if (studentNames.TryGetValue(interviewEvent.StudentId, out var studentName))
                {
                    interviewEventViewModel.StudentName = studentName;
                }

                if (studentClasses.TryGetValue(interviewEvent.StudentId, out var studentClass))
                {
                    interviewEventViewModel.Class = studentClass;
                }

                if (interviewEvent.InterviewerTimeslot != null)
                {
                    if (interviewerNames.TryGetValue(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId, out var interviewerName))
                    {
                        interviewEventViewModel.InterviewerName = interviewerName;
                    }

                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
                else
                {
                    interviewEventViewModel.InterviewerName = "Not Assigned";
                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
            }

            return View(eventslist);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> StudentSelfCheckIn()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var alreadyCheckedIn = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .AnyAsync(x => x.StudentId == userId && x.Timeslot.Event.IsActive && x.Status == StatusConstants.CheckedIn);
            var hasEligibleInterview = !alreadyCheckedIn && await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .AnyAsync(x => x.StudentId == userId && x.Timeslot.Event.IsActive && x.Status == StatusConstants.Default);

            return View("SelfCheckIn", new SelfCheckInViewModel
            {
                IsCheckedIn = alreadyCheckedIn,
                CheckInMessage = hasEligibleInterview
                    ? "Confirm when you are ready to check in for your interview."
                    : alreadyCheckedIn
                        ? "You are already checked in. Please take a seat until event staff calls you."
                        : "There is no interview ready for check-in. Please alert event staff if you need help."
            });
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentSelfCheckInConfirmed()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var alreadyCheckedIn = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .AnyAsync(x => x.StudentId == userId && x.Timeslot.Event.IsActive && x.Status == StatusConstants.CheckedIn);
            if (alreadyCheckedIn)
            {
                return View("SelfCheckIn", new SelfCheckInViewModel
                {
                    IsCheckedIn = true,
                    CheckInMessage = "You are already checked in. Please take a seat until event staff calls you."
                });
            }

            var interview = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.StudentId == userId && x.Timeslot.Event.IsActive && x.Status == StatusConstants.Default);
            if (interview is null)
            {
                return View("SelfCheckIn", new SelfCheckInViewModel
                {
                    IsCheckedIn = false,
                    CheckInMessage = "There is no interview ready for check-in. Please alert event staff if you need help."
                });
            }

            interview.Status = StatusConstants.CheckedIn;
            interview.CheckedInAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await UpdateHub(interview.Id);

            return View("SelfCheckIn", new SelfCheckInViewModel
            {
                IsCheckedIn = true,
                CheckInMessage = "You are checked in. Please take a seat until event staff calls you."
            });
        }

        [Authorize]
        public async Task<IActionResult> InterviewerSelfCheckIn()
        {
            if (User.IsInRole(RolesConstants.AdminRole))
            {
                var sits = await _context.InterviewerTimeslots
                                .Include(x => x.Timeslot)
                                .ThenInclude(x => x.Event)
                                .Where(x => x.Timeslot.Event.IsActive)
                                .Select(x => x.InterviewerSignupId)
                                .Distinct()
                                .ToListAsync();

                var interviewers = await _context.InterviewerSignups
                    .Where(x => sits.Contains(x.Id))
                    .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                    .OrderBy(x => x.Text)
                    .ToListAsync();

                var vm = new InterviewerCheckInViewModel
                {
                    Interviewers = interviewers,
                    CheckedIn = false
                };

                return View("InterviewerCheckIn", vm);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> InterviewerSelfCheckIn(string InterviewerId)
        {
            string id = InterviewerId;

            if (id == null || id == "0" || id == "")
            {
                return BadRequest("No interviewer was selected.");
            }

            int newId = 0;
            try
            {
                newId = int.Parse(id);
            }
            catch
            {
                return BadRequest("InterviewerSignup ID was invalid.");
            }

            var interviewer = await _context.InterviewerSignups
                .Where(x => x.Id == newId)
                .FirstOrDefaultAsync();

            if (interviewer == null)
            {
                return BadRequest("Interviewer not signed up.");
            }

            interviewer.CheckedIn = !interviewer.CheckedIn;

            _context.Update(interviewer);
            await _context.SaveChangesAsync();

            var sits = await _context.InterviewerTimeslots
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.Timeslot.Event.IsActive)
                    .Select(x => x.InterviewerSignupId)
                    .Distinct()
                    .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => sits.Contains(x.Id))
                .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                .OrderBy(x => x.Text)
                .ToListAsync();

            var room = "";

            if (interviewer.CheckedIn)
            {
                string interviewerId = interviewer.InterviewerId;

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 3);

                var li = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == interviewerId &&
                        x.Event != null && x.Event.Date.Date == date)
                    .FirstOrDefaultAsync();

                if (li != null)
                {
                    room = li.Location?.Room ?? "Not Assigned";
                }
                else
                {
                    room = "Not Assigned";
                }
            }

            var vm = new InterviewerCheckInViewModel
            {
                Interviewers = interviewers,
                CheckedIn = interviewer.CheckedIn,
                Name = interviewer.FirstName + " " + interviewer.LastName,
                Room = room
            };

            await UpdateHub();

            return View("InterviewerCheckIn", vm);
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> PreAssignInterviews()
        {
            var vm = await _manager.ListOfAssignedStudents();

            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> PreAssignInterviews([FromBody] List<PreAssignInterviewRequestViewModel> requests)
        {
            _logger.LogInformation("Preassignment requested...");

            if (requests == null || requests.Count == 0)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Invalid data. No interviews to assign." });
            }

            var dictionary = requests.ToDictionary(x => int.Parse(x.InterviewEventId), x => x.SelectedValue);

            try
            {
                await _manager.AssignStudentsToInterviewers(dictionary);
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to assign students to interviewers" });
            }

            return StatusCode(StatusCodes.Status200OK, new { message = "Success!" });
        }

        private static DateTime CombineDateWithTimeString(DateTime date, string timeString)
        {
            DateTime dateTime = DateTime.ParseExact(timeString, "h:mm tt", CultureInfo.InvariantCulture);
            TimeSpan timeSpan = dateTime.TimeOfDay;
            return date.Date + timeSpan;
        }

        private static string CreateCalendarEvent(DateTime start, DateTime end)
        {

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("BEGIN:VCALENDAR");
            stringBuilder.AppendLine("VERSION:2.0");
            stringBuilder.AppendLine("PRODID:-//YourCompany//YourProduct//EN"); // Optional identifier
            stringBuilder.AppendLine("BEGIN:VTIMEZONE");
            stringBuilder.AppendLine("TZID:America/Chicago");
            stringBuilder.AppendLine("BEGIN:DAYLIGHT");
            stringBuilder.AppendLine("TZOFFSETFROM:-0600");
            stringBuilder.AppendLine("TZOFFSETTO:-0500");
            stringBuilder.AppendLine("TZNAME:CDT");
            stringBuilder.AppendLine("DTSTART:19700308T020000");
            stringBuilder.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU");
            stringBuilder.AppendLine("END:DAYLIGHT");
            stringBuilder.AppendLine("BEGIN:STANDARD");
            stringBuilder.AppendLine("TZOFFSETFROM:-0500");
            stringBuilder.AppendLine("TZOFFSETTO:-0600");
            stringBuilder.AppendLine("TZNAME:CST");
            stringBuilder.AppendLine("DTSTART:19701101T020000");
            stringBuilder.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU");
            stringBuilder.AppendLine("END:STANDARD");
            stringBuilder.AppendLine("END:VTIMEZONE");
            stringBuilder.AppendLine("BEGIN:VEVENT");
            stringBuilder.AppendLine("UID:" + Guid.NewGuid());
            stringBuilder.AppendFormat("DTSTAMP:{0:yyyyMMddTHHmmssZ}\r\n", DateTime.UtcNow); // Added DTSTAMP
            stringBuilder.AppendLine("SEQUENCE:0"); // Added SEQUENCE for indicating the version of the event
            stringBuilder.AppendFormat("DTSTART;TZID=America/Chicago:{0:yyyyMMddTHHmmss}\r\n", start);
            stringBuilder.AppendFormat("DTEND;TZID=America/Chicago:{0:yyyyMMddTHHmmss}\r\n", end);
            stringBuilder.AppendLine("SUMMARY:Mock Interviews");
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-P14D"); // 14 days before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            // Add a reminder for 3 days before the event
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-P3D"); // 3 days before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            // Add a reminder for 30 minutes before the event
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-PT30M"); // 30 minutes before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            stringBuilder.AppendLine("END:VEVENT");
            stringBuilder.AppendLine("END:VCALENDAR");


            return stringBuilder.ToString();
        }

        private async Task UpdateHub(int id)
        {
            var newInterviewEvent = await _context.Interviews
                .Include(x => x.Location)
                .Include(x => x.InterviewerTimeslot)
                // EF Core parses Include expressions without dereferencing an optional navigation.
                .ThenInclude(x => x!.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (newInterviewEvent is null)
            {
                return;
            }

            var student = await _userManager.Users
                .Where(x => x.Id == newInterviewEvent.StudentId)
                .FirstOrDefaultAsync();

            var studentName = "";
            if (newInterviewEvent.Status == StatusConstants.Completed || newInterviewEvent.Status == StatusConstants.NoShow || newInterviewEvent.Status == StatusConstants.Excused)
            {
                studentName = "delete";
            }
            else
            {
                studentName = student?.GetFullName() ?? "Deleted user";
            }

            var interviewername = "Not Assigned";
            var interviewerId = "0";

            if (newInterviewEvent.InterviewerTimeslot != null)
            {
                interviewerId = newInterviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId;
                interviewername = await _userManager.Users
                    .Where(x => x.Id == interviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync() ?? "Deleted user";
            }

            if (newInterviewEvent.Location == null)
            {
                newInterviewEvent.Location = new Location()
                {
                    Room = "Not Assigned"
                };
            }

            var time = $"{newInterviewEvent.Timeslot.Time:hh:mm tt}";
            var date = $"{newInterviewEvent.Timeslot.Event.Date:M/d/yyyy}";

            _logger.LogInformation("Requesting all connected clients to update.");
            await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", newInterviewEvent, studentName, student?.GetClass() ?? string.Empty, interviewerId, interviewername, time, date);
            _logger.LogInformation("Requested.");
        }

        private async Task UpdateHub()
        {
            var busyInterviewers = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x!.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing && x.InterviewerTimeslot != null)
                .Select(x => x.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new AvailableInterviewer
                {
                    InterviewerId = x.InterviewerId,
                    InterviewType = x.Type ?? string.Empty,
                })
            .ToListAsync();

            foreach (var iv in interviewers)
            {
                iv.Name = await _userManager.Users
                    .Where(x => x.Id == iv.InterviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync() ?? "Deleted user";

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 8);

                iv.Room = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == iv.InterviewerId &&
                        x.Event != null && x.Event.Date == date && x.Location != null)
                    .Select(x => x.Location!.Room)
                    .FirstOrDefaultAsync() ?? "Not Assigned";
            }

            interviewers.Sort((x, y) => string.Compare(x.Name, y.Name));

            _logger.LogInformation("Requesting all clients to update their available interviewers lists...");
            await _hubContextInterviewer.Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
            _logger.LogInformation("Requested.");
        }

        private static string GetDisplayName(ApplicationUser? user) => user is null
            ? "Deleted user"
            : $"{user.FirstName} {user.LastName}";
    }
}
