using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Data.Access.Reports;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.HomeController;
using MockInterviews.Models.ViewModels.InterviewEventsController;
using MockInterviews.Models.ViewModels.Shared;
using MockInterviews.Options;
using SendGrid;

namespace MockInterviews.Controllers
{
    public class HomeController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISendGridClient _sendGridClient;
        private readonly ILogger<HomeController> _logger;
        private readonly string _superUserEmail;


        public HomeController(
            MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager,
            ISendGridClient sendGridClient,
            ILogger<HomeController> logger,
            IOptions<SuperUserOptions> superUserOptions)
        {
            _context = context;
            _userManager = userManager;
            _sendGridClient = sendGridClient;
            _logger = logger;
            _superUserEmail = superUserOptions.Value.Email;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Calling {method} method...", nameof(Index));

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ApplicationUser? userFull = null;

            if (userId is not null)
            {
                userFull = await _userManager.FindByIdAsync(userId);
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                return Challenge();
            }

            if (User.Identity?.IsAuthenticated == true && userFull is null)
            {
                return Challenge();
            }

            IndexViewModel model = new()
            {
                DisruptionBanner = await GetDisruptionBanner()
            };

            if (userFull is not null)
            {
                model.Name = GetDisplayName(userFull);

                model.ZoomLink = await GetZoomLink();
                model.ZoomLinkVisible = await GetZoomLinkVisible();
            }

            model.VolunteerEventViewModels = new List<VolunteerEventViewModel>();
            model.TimeRangeViewModels = new List<TimeRangeViewModel>();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.StudentRole))
            {
                var volunteerEvents = await _context.VolunteerTimeslots
                    .Include(v => v.Timeslot)
                    .ThenInclude(y => y.Event)
                    .OrderBy(ve => ve.TimeslotId)
                    .Where(v => v.StudentId == userId && v.Timeslot.Event.IsActive)
                    .ToListAsync();

                var timeRanges = new ControlBreakVolunteer(_userManager);
                var groupedEvents = await timeRanges.ToTimeRanges(volunteerEvents);

                model.TimeRangeViewModels = groupedEvents;
            }

            model.InterviewerScheduledInterviews = new List<InterviewEventViewModel>();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.InterviewerRole))
            {
                var interviewEvents = await _context.Interviews
                    .Include(v => v.InterviewerTimeslot)
                    // EF Core parses Include expressions without dereferencing optional navigations.
                    .ThenInclude(v => v!.InterviewerSignup)
                    .Include(v => v.Location)
                    .Include(v => v.Timeslot)
                    .ThenInclude(v => v.Event)
                    .Where(v => v.InterviewerTimeslot != null && v.InterviewerTimeslot.InterviewerSignup.InterviewerId == userId
                        && v.Timeslot.Event.IsActive
                        && (v.Status == StatusConstants.Ongoing
                        || v.Status == StatusConstants.Completed
                        || v.Status == StatusConstants.CheckedIn))
                    .ToListAsync();

                if (interviewEvents != null && interviewEvents.Count != 0)
                {
                    foreach (Interview interviewEvent in interviewEvents)
                    {

                        if (interviewEvent.InterviewerTimeslot != null)
                        {
                            var student = await _userManager.FindByIdAsync(interviewEvent.StudentId);

                            model.InterviewerScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = GetDisplayName(student),
                                InterviewerName = GetDisplayName(userFull),
                                Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class)
                            });
                        }
                        else
                        {
                            model.InterviewerScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = "Not Assigned",
                                InterviewerName = GetDisplayName(userFull)
                            });
                        }
                    }
                }
            }

            model.SignupInterviewerTimeslots = new List<InterviewerTimeslot>();
            model.InterviewerRangeViewModels = new List<TimeRangeViewModel>();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.InterviewerRole))
            {
                var signupInterviewTimeslots = await _context.InterviewerTimeslots
                    .Include(s => s.InterviewerSignup)
                    .Include(v => v.Timeslot)
                    .ThenInclude(v => v.Event)
                    .Include(v => v.InterviewerSignup)
                    .OrderBy(ve => ve.TimeslotId)
                    .Where(v => v.InterviewerSignup.InterviewerId == userId
                        && v.Timeslot.Event.IsActive)
                    .ToListAsync();


                if (signupInterviewTimeslots.Count > 0)
                {
                    var si = signupInterviewTimeslots
                        .Select(x => x.InterviewerSignupId)
                        .Distinct()
                        .ToList();

                    model.SignupInterviewerId1 = si[0];

                    if (si.Count == 2)
                    {
                        model.SignupInterviewerId2 = si[1];
                    }
                }

                var timeRanges = new ControlBreakInterviewer(_userManager);
                var groupedEvents = await timeRanges.ToTimeRanges(signupInterviewTimeslots);

                model.InterviewerRangeViewModels = groupedEvents;
            }

            model.StudentScheduledInterviews = new List<InterviewEventViewModel>();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.StudentRole))
            {
                var interviewEvents = await _context.Interviews
                    .Include(v => v.InterviewerTimeslot)
                    .ThenInclude(v => v!.InterviewerSignup)
                    .Include(v => v.Location)
                    .Include(v => v.Timeslot)
                    .ThenInclude(v => v.Event)
                    .Where(v => v.StudentId == userId
                        && v.Timeslot.Event.IsActive)
                    .ToListAsync();

                if (interviewEvents != null && interviewEvents.Count != 0)
                {
                    foreach (Interview interviewEvent in interviewEvents)
                    {
                        if (interviewEvent.InterviewerTimeslot != null)
                        {
                            var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);

                            model.StudentScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = GetDisplayName(userFull),
                                InterviewerName = GetDisplayName(interviewer),
                                Class = ClassConstants.GetClassText(userFull?.Class ?? default)
                            });
                        }
                        else
                        {
                            model.StudentScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = GetDisplayName(userFull),
                                Class = ClassConstants.GetClassText(userFull?.Class ?? default),
                                InterviewerName = "Not Assigned"
                            });
                        }
                    }
                }
            }

            model.CompletedInterviews = new();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.InterviewerRole))
            {
                var interviewEvents = await _context.Interviews
                    .Include(v => v.InterviewerTimeslot)
                    .ThenInclude(v => v!.InterviewerSignup)
                    .Include(v => v.Location)
                    .Include(v => v.Timeslot)
                    .ThenInclude(v => v.Event)
                    .Where(v => v.InterviewerTimeslot != null && v.InterviewerTimeslot.InterviewerSignup.InterviewerId == userId
                        && v.InterviewerTimeslot.Timeslot.Event.IsActive
                        && v.Status == StatusConstants.Completed)
                    .ToListAsync();

                if (interviewEvents != null && interviewEvents.Count != 0)
                {
                    foreach (Interview interviewEvent in interviewEvents)
                    {

                        if (interviewEvent.InterviewerTimeslot != null)
                        {
                            var student = await _userManager.FindByIdAsync(interviewEvent.StudentId);

                            model.CompletedInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = GetDisplayName(student),
                                InterviewerName = GetDisplayName(userFull),
                                Class = student is null ? string.Empty : ClassConstants.GetClassText(student.Class)
                            });
                        }
                        else
                        {
                            model.CompletedInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = "Not Assigned",
                                InterviewerName = GetDisplayName(userFull)
                            });
                        }
                    }
                }
            }

            return View(model);
        }

        private async Task<string> GetZoomLink()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link");

            return banner?.Value ?? throw new InvalidOperationException("Setting 'zoom_link' does not exist.");
        }

        private async Task<string> GetDisruptionBanner()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "disruption_banner");

            return int.TryParse(banner?.Value, out var value)
                ? value == 0 ? "none" : "block"
                : throw new InvalidOperationException("Setting 'disruption_banner' does not exist, or it is not an integer.");
        }

        private async Task<string> GetZoomLinkVisible()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link_visible");

            return int.TryParse(banner?.Value, out var value)
                ? value == 0 ? "none" : "block"
                : throw new InvalidOperationException("Setting 'zoom_link_visible' does not exist, or it is not an integer.");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            // Log the error with additional context
            var exceptionFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exceptionFeature != null)
            {
                _logger.LogError(exceptionFeature.Error,
                    "Unhandled exception occurred. RequestId: {RequestId}, Path: {Path}",
                    requestId, exceptionFeature.Path);
            }
            else
            {
                _logger.LogWarning("Error page accessed without exception context. RequestId: {RequestId}", requestId);
            }

            return View(new ErrorViewModel { RequestId = requestId });
        }

        public async Task<IActionResult> EmailStudents()
        {
            var uniqueUsers = await _context.Interviews
                .Select(x => x.StudentId)
                .Distinct()
                .ToListAsync();

            foreach (var user in uniqueUsers)
            {
                var userFull = await _userManager.FindByIdAsync(user);
                if (userFull?.Email is null)
                {
                    _logger.LogWarning("Skipping student reminder for deleted or uncontactable user {UserId}.", user);
                    continue;
                }
                var interviews = await _context.Interviews
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.StudentId == user)
                    .ToListAsync();

                var times = "";
                foreach (var interview in interviews)
                {
                    times += interview.ToString();
                }

                ASendAnEmail emailer = new StudentReminderEmail();
                await emailer.SendEmailAsync(_sendGridClient, _superUserEmail, "Mock Interviews Reminder", userFull.Email, GetDisplayName(userFull), times, null);
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> EmailInterviewers()
        {
            var uniqueUsers = await _context.InterviewerSignups
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToListAsync();

            foreach (var user in uniqueUsers)
            {
                var userFull = await _userManager.FindByIdAsync(user);
                if (userFull?.Email is null)
                {
                    _logger.LogWarning("Skipping interviewer reminder for deleted or uncontactable user {UserId}.", user);
                    continue;
                }
                var interviews = await _context.InterviewerTimeslots
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Include(x => x.InterviewerSignup)
                    .OrderBy(x => x.Timeslot.Event.Date)
                    .ThenBy(x => x.Timeslot.Time)
                    .Where(x => x.InterviewerSignup.InterviewerId == user)
                    .ToListAsync();

                var timeRanges = new ControlBreakInterviewer(_userManager);
                var groupedEvents = await timeRanges.ToTimeRanges(interviews);

                var times = "";
                foreach (TimeRangeViewModel interview in groupedEvents)
                {
                    times += interview.StartTime + " - " + interview.EndTime + " on " + interview.Date.ToString(@"M/dd/yyyy") + "<br>";
                }

                ASendAnEmail emailer = new InterviewerReminderEmail();
                await emailer.SendEmailAsync(_sendGridClient, _superUserEmail, "UA MIS Mock Interviews Reminder", userFull.Email, GetDisplayName(userFull), times, null);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult AttemptLogin()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        private static string GetDisplayName(ApplicationUser? user) => user is null
            ? "Deleted user"
            : $"{user.FirstName} {user.LastName}";

        public IActionResult AttemptLogout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return View("LogoutPage");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
