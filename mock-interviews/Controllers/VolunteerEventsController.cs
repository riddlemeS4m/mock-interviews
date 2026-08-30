using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Data.Access.Reports;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.Shared;
using MockInterviews.Models.ViewModels.VolunteerEventsController;
using MockInterviews.Options;
using SendGrid;


namespace MockInterviews.Controllers
{
    public class VolunteerEventsController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISendGridClient _sendGridClient;
        private readonly ILogger<VolunteerEventsController> _logger;
        private readonly string _superUserEmail;

        public VolunteerEventsController(MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager,
            ISendGridClient sendGridClient,
            IOptions<SuperUserOptions> superUserOptions,
            ILogger<VolunteerEventsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _sendGridClient = sendGridClient;
            _logger = logger;
            _superUserEmail = superUserOptions.Value.Email;
        }

        // GET: VolunteerEvents
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Index()
        {
            var volunteerEvents = await _context.VolunteerTimeslots
                .Include(v => v.Timeslot)
                .ThenInclude(y => y.Event)
                .Where(y => y.Timeslot.Event.IsActive == true)
                .OrderBy(ve => ve.StudentId)
                .ThenBy(x => x.TimeslotId)
                .ToListAsync();

            var timeRanges = new ControlBreakVolunteer(_userManager);
            var groupedEvents = await timeRanges.ToTimeRanges(volunteerEvents);

            return View(groupedEvents);
        }

        // GET: VolunteerEvents/Details/
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.VolunteerTimeslots == null)
            {
                return NotFound();
            }

            var volunteerEvent = await _context.VolunteerTimeslots
                .Include(v => v.Timeslot)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (volunteerEvent?.Timeslot is null)
            {
                return NotFound();
            }

            var specificTimeslot = await _context.Timeslots
                .Include(v => v.Event)
                .FirstOrDefaultAsync(m => m.Id == volunteerEvent.Timeslot.Id);
            if (specificTimeslot is null)
            {
                return NotFound();
            }

            volunteerEvent.Timeslot = specificTimeslot;

            return View(volunteerEvent);
        }

        // GET: VolunteerEvents/Create
        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var timeslots = await _context.Timeslots
                .Where(x => x.IsVolunteer == true)
                .Include(y => y.Event)
                .Where(x => !_context.VolunteerTimeslots.Any(y => y.TimeslotId == x.Id && y.StudentId == userId))
                .Where(x => !_context.Interviews.Any(y => y.TimeslotId == x.Id && y.StudentId == userId))
                .Where(x => x.Event.For221 == For221.n)
                .Where(x => x.Event.IsActive)
                .ToListAsync();

            var eventdates = timeslots
                .Select(x => x.Event)
                .Distinct()
                .OrderBy(x => x.Id)
                .ToList();

            VolunteerEventSignupViewModel volunteerEventsViewModel = new()
            {
                Timeslots = timeslots,
                EventDates = eventdates
            };

            if (timeslots.Count == 0)
            {
                volunteerEventsViewModel.SignedUp = true;
            }
            else
            {
                volunteerEventsViewModel.SignedUp = false;
            }

            return View(volunteerEventsViewModel);
        }

        // POST: VolunteerEvents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int[] SelectedEventIds1, int[] SelectedEventIds2)
        {
            int[] SelectedEventIds = (SelectedEventIds1 ?? [])
                .Concat(SelectedEventIds2 ?? [])
                .Distinct()
                .ToArray();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Conflict("The current account no longer exists.");
            }

            List<VolunteerTimeslot> volEvents = new();

            //var allVolunteerEvents = await _context.VolunteerEvent
            //    .Include(v => v.Timeslot)
            //    .ThenInclude(y => y.EventDate)
            //    .ToListAsync();

            //var studentIds = allVolunteerEvents
            //    .Select(v => v.StudentId)
            //    .Distinct()
            //    .ToList();

            //handle bad input
            var timeslots = await _context.Timeslots
                .Where(x => x.IsVolunteer == true)
                .Include(y => y.Event)
                .Where(x => !_context.VolunteerTimeslots.Any(y => y.TimeslotId == x.Id && y.StudentId == userId))
                .Where(x => !_context.Interviews.Any(y => y.TimeslotId == x.Id && y.StudentId == userId))
                .Where(x => x.Event.For221 == For221.n)
                .Where(x => x.Event.IsActive)
                .ToListAsync();

            var eventdates = timeslots
                .Select(x => x.Event)
                .Distinct()
                .OrderBy(x => x.Id)
                .ToList();

            VolunteerEventSignupViewModel volunteerEventsViewModel = new()
            {
                Timeslots = timeslots,
                EventDates = eventdates
            };

            if (SelectedEventIds.Length == 0)
            {
                ModelState.AddModelError("SelectedEventIds1", "Please select at least one checkbox");
                return View(volunteerEventsViewModel);
            }

            if (SelectedEventIds.Any(id => timeslots.All(timeslot => timeslot.Id != id)))
            {
                ModelState.AddModelError("SelectedEventIds1", "One or more selected timeslots are no longer available. Refresh the page and try again.");
                return View(volunteerEventsViewModel);
            }

            _context.VolunteerTimeslots.AddRange(SelectedEventIds.Select(id => new VolunteerTimeslot
            {
                TimeslotId = id,
                StudentId = userId
            }));
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict("The selected volunteer timeslot is no longer available.");
            }

            volEvents = await _context.VolunteerTimeslots
                .Include(volunteerTimeslot => volunteerTimeslot.Timeslot)
                .ThenInclude(timeslot => timeslot.Event)
                .Where(volunteerTimeslot => volunteerTimeslot.StudentId == userId &&
                    SelectedEventIds.Contains(volunteerTimeslot.TimeslotId))
                .ToListAsync();

            var sortedEvents = volEvents
                .OrderBy(ve => ve.TimeslotId)
                .ToList();

            await ComposeEmail(user, sortedEvents);

            return RedirectToAction("Index", "Home");
        }

        // GET: VolunteerEvents/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.VolunteerTimeslots == null)
            {
                return NotFound();
            }

            var volunteerEvent = await _context.VolunteerTimeslots.FindAsync(id);
            if (volunteerEvent == null)
            {
                return NotFound();
            }
            ViewData["TimeslotId"] = new SelectList(_context.Timeslots, "Id", "Id", volunteerEvent.TimeslotId);
            return View(volunteerEvent);
        }

        // POST: VolunteerEvents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,TimeslotId")] VolunteerTimeslot volunteerEvent)
        {
            if (id != volunteerEvent.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(volunteerEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VolunteerEventExists(volunteerEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["TimeslotId"] = new SelectList(_context.Timeslots, "Id", "Id", volunteerEvent.TimeslotId);
            return View(volunteerEvent);
        }

        // GET: VolunteerEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.VolunteerTimeslots == null)
            {
                return NotFound();
            }

            var volunteerEvent = await _context.VolunteerTimeslots
                .Include(v => v.Timeslot).ThenInclude(y => y.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (volunteerEvent == null)
            {
                return NotFound();
            }

            return View(volunteerEvent);
        }

        // POST: VolunteerEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Challenge();
            }

            var user = await _userManager.FindByIdAsync(userId);

            var volunteerEvent = await _context.VolunteerTimeslots.FindAsync(id);
            if (volunteerEvent != null)
            {
                var fullName = user is null ? "Deleted user" : $"{user.FirstName} {user.LastName}";

                ASendAnEmail emailNotification = new VolunteerCancellationNotification();
                await emailNotification.SendEmailAsync(_sendGridClient, _superUserEmail, "Volunteer Cancellation Notification: " + fullName, _superUserEmail, fullName, volunteerEvent.ToString(), null);

                _context.VolunteerTimeslots.Remove(volunteerEvent);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        private bool VolunteerEventExists(int id)
        {
            return (_context.VolunteerTimeslots?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private async Task<ActionResult> GetUserId()
        {
            var user = await _userManager.GetUserAsync(User);
            return user is null ? Challenge() : Content(user.Id);
        }

        private async Task ComposeEmail(ApplicationUser user, List<VolunteerTimeslot> emailTimes)
        {
            var timeRanges = new ControlBreakVolunteer(_userManager);
            var groupedEvents = await timeRanges.ToTimeRanges(emailTimes);
            List<string> calendarEvents = new List<string>();

            var times = "";
            foreach (TimeRangeViewModel interview in groupedEvents)
            {
                DateTime combinedStart = CombineDateWithTimeString(interview.Date, interview.StartTime);
                DateTime combinedEnd = CombineDateWithTimeString(interview.Date, interview.EndTime);
                var plainBytes = Encoding.UTF8.GetBytes(CreateCalendarEvent(combinedStart, combinedEnd));
                string newEvent = Convert.ToBase64String(plainBytes);
                calendarEvents.Add(newEvent);
                times += interview.StartTime + " - " + interview.EndTime + " on " + interview.Date.ToString(@"M/dd/yyyy") + "<br>";
            }

            ASendAnEmail emailer = new VolunteerSignupConfirmation();
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning("Skipping volunteer confirmation for uncontactable user {UserId}.", user.Id);
                return;
            }

            await emailer.SendEmailAsync(_sendGridClient, _superUserEmail, "Volunteer Sign-Up Confirmation", user.Email, user.FirstName ?? "Deleted user", times, calendarEvents);

            string fullName = user.FirstName + " " + user.LastName;

            ASendAnEmail emailNotification = new VolunteerSignupNotification();
            await emailNotification.SendEmailAsync(_sendGridClient, _superUserEmail, "Volunteer Sign-Up Notification: " + fullName, _superUserEmail, fullName, times, null);
        }

        [Authorize(Roles = RolesConstants.StudentRole + "," + RolesConstants.AdminRole)]
        public async Task<IActionResult> UserDeleteRange(int[] timeslots)
        {
            // Check if the timeslotIds array is empty or null
            if (timeslots == null || timeslots.Length == 0)
            {
                return NotFound();
            }

            // Get the timeslots to delete
            var timeslotsToDelete = await _context.VolunteerTimeslots
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(t => timeslots.Contains(t.Id))
                .ToListAsync();

            // Check if any of the timeslots to delete are null
            if (timeslotsToDelete == null || timeslotsToDelete.Count == 0)
            {
                return NotFound();
            }

            var date = timeslotsToDelete.First().Timeslot.Event.Date;
            if (timeslotsToDelete.Any(t => t.Timeslot.Event.Date != date))
            {
                return NotFound();
            }

            if (!timeslotsToDelete.All(e => e.StudentId == User.FindFirstValue(ClaimTypes.NameIdentifier)) && !User.IsInRole(RolesConstants.AdminRole))
            {
                return NotFound();
            }

            var timeslotslist = timeslots.ToList();

            var viewModel = new TimeRangeViewModel
            {
                Date = date,
                StartTime = timeslotsToDelete.First().Timeslot.Time.ToString(@"h\:mm tt"),
                EndTime = timeslotsToDelete.Last().Timeslot.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                TimeslotIds = timeslotslist
            };


            return View(viewModel);
        }

        [Authorize(Roles = RolesConstants.StudentRole + "," + RolesConstants.AdminRole)]
        public async Task<IActionResult> UserDeleteRangeConfirmed(int[] timeslots)
        {

            // Get the timeslots to delete
            var timeslotsToDelete = await _context.VolunteerTimeslots

                .Where(t => timeslots.Contains(t.Id))
                .ToListAsync();

            if (!timeslotsToDelete.All(e => e.StudentId == User.FindFirstValue(ClaimTypes.NameIdentifier)) && !User.IsInRole(RolesConstants.AdminRole))
            {
                return NotFound();
            }

            // Delete the timeslots
            _context.VolunteerTimeslots.RemoveRange(timeslotsToDelete);
            await _context.SaveChangesAsync();

            if (User.IsInRole(RolesConstants.AdminRole))
            {
                return RedirectToAction("Index", "VolunteerEvents");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> DeleteRange(int[] timeslots)
        {
            // Check if the timeslotIds array is empty or null
            if (timeslots == null || timeslots.Length == 0)
            {
                return NotFound();
            }

            // Get the timeslots to delete
            var timeslotsToDelete = await _context.VolunteerTimeslots
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(t => timeslots.Contains(t.Id))
                .ToListAsync();

            // Check if any of the timeslots to delete are null
            if (timeslotsToDelete == null || timeslotsToDelete.Count == 0)
            {
                return NotFound();
            }

            var date = timeslotsToDelete.First().Timeslot.Event.Date;
            if (timeslotsToDelete.Any(t => t.Timeslot.Event.Date != date))
            {
                return NotFound();
            }

            var timeslotslist = timeslots.ToList();

            var viewModel = new TimeRangeViewModel
            {
                Date = date,
                StartTime = timeslotsToDelete.First().Timeslot.Time.ToString(@"h\:mm tt"),
                EndTime = timeslotsToDelete.Last().Timeslot.Time.AddMinutes(30).ToString(@"h\:mm tt"),
                TimeslotIds = timeslotslist
            };


            return View(viewModel);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> DeleteRangeConfirmed(int[] timeslots)
        {

            // Get the timeslots to delete
            var timeslotsToDelete = await _context.VolunteerTimeslots

                .Where(t => timeslots.Contains(t.Id))
                .ToListAsync();

            // Delete the timeslots
            _context.VolunteerTimeslots.RemoveRange(timeslotsToDelete);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "VolunteerEvents");
        }
        private static DateTime CombineDateWithTimeString(DateTime date, string timeString)
        {
            DateTime dateTime = DateTime.ParseExact(timeString, "h:mm tt", CultureInfo.InvariantCulture);
            TimeSpan timeSpan = dateTime.TimeOfDay;
            return date.Date + timeSpan;
        }

        private string CreateCalendarEvent(DateTime start, DateTime end)
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
    }
}
