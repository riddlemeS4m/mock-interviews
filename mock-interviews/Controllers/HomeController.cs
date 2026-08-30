using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
using MockInterviews.Models.ViewModels.Shared;
using MockInterviews.Options;
using MockInterviews.Services;
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
        private readonly UserLandingPageResolver _landingPageResolver;
        private readonly DashboardService _dashboardService;


        public HomeController(
            MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager,
            ISendGridClient sendGridClient,
            ILogger<HomeController> logger,
            IOptions<SuperUserOptions> superUserOptions,
            UserLandingPageResolver landingPageResolver,
            DashboardService dashboardService)
        {
            _context = context;
            _userManager = userManager;
            _sendGridClient = sendGridClient;
            _logger = logger;
            _superUserEmail = superUserOptions.Value.Email;
            _landingPageResolver = landingPageResolver;
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Landing));
            }

            return View(await _dashboardService.BuildPublicHomeAsync());
        }

        [Authorize]
        public IActionResult Landing()
        {
            var destination = _landingPageResolver.Resolve(User);
            return RedirectToAction(
                destination.Action,
                destination.Controller,
                new { area = destination.Area });
        }

        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.SystemAdminRole)]
        public IActionResult Admin()
            => View();

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> Student()
            => await RenderDashboardAsync();

        [Authorize(Roles = RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Interviewer()
            => await RenderDashboardAsync();

        [Authorize(Roles = RolesConstants.StudentRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Participant()
            => await RenderDashboardAsync();

        [Authorize]
        public IActionResult AccessPending()
            => View();

        private async Task<IActionResult> RenderDashboardAsync()
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

            var model = await _dashboardService.BuildDashboardAsync(
                user,
                User.IsInRole(RolesConstants.StudentRole),
                User.IsInRole(RolesConstants.InterviewerRole));
            return View("Dashboard", model);
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
                await emailer.SendEmailAsync(_sendGridClient, _superUserEmail, "Mock Interviews Reminder", userFull.Email, GetDisplayName(userFull), times, null);
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
