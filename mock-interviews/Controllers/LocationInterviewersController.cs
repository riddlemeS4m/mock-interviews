using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.LocationInterviewersController;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public class LocationInterviewersController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LocationInterviewersController(MockInterviewsDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: LocationInterviewers
        public async Task<IActionResult> Index()
        {
            var locationInterviewers = await _context.InterviewerLocations
                .Include(v => v.Location)
                .Include(v => v.Event)
                .Where(v => v.Event != null && v.Event.IsActive)
                .ToListAsync();

            var interviewerIds = locationInterviewers
                .Select(v => v.InterviewerId)
                .Distinct()
                .ToList();

            var interviewers = await _userManager.Users
                .Where(u => interviewerIds.Contains(u.Id))
                .Select(x => new { x.FirstName, x.LastName, x.Id })
                .ToListAsync();

            var query = from locationInterviewer in locationInterviewers
                        join interviewer in interviewers on locationInterviewer.InterviewerId equals interviewer.Id
                        select new LocationInterviewerWithName
                        {
                            LocationInterviewer = locationInterviewer,
                            InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
                            InterviewerPreference = locationInterviewer.Preference
                        };

            var locationInterviewersWithNames = query.ToList();
            var locations = await _context.Locations
                .OrderBy(u => u.Room)
                .ToListAsync();

            var viewModel = new LocationInterviewerViewModel
            {
                Locations = locations,
                LocationInterviewerWithNames = locationInterviewersWithNames
            };
            return View(viewModel);
        }

        // GET: LocationInterviewers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.InterviewerLocations == null)
            {
                return NotFound();
            }

            var locationInterviewer = await _context.InterviewerLocations
                .Include(l => l.Location)
                .Include(l => l.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (locationInterviewer == null)
            {
                return NotFound();
            }

            var interviewer = await _userManager.FindByIdAsync(locationInterviewer.InterviewerId);
            if (interviewer is null)
            {
                return NotFound();
            }

            var locationInterviewerWithName = new LocationInterviewerWithName
            {
                LocationInterviewer = locationInterviewer,
                InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
            };

            return View(locationInterviewerWithName);
        }

        // GET: LocationInterviewers/Create
        public async Task<IActionResult> Create()
        {
            return View(await BuildEditorAsync(new InterviewerLocation()));
        }

        // POST: LocationInterviewers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,InterviewerId,LocationId,EventId")] InterviewerLocation locationInterviewer, bool InPerson)
        {
            locationInterviewer.Preference = InPerson
                ? InterviewLocationConstants.InPerson
                : InterviewLocationConstants.IsVirtual;

            if (await IsValidAssignmentAsync(locationInterviewer))
            {
                _context.Add(locationInterviewer);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = "Room assignment created.";
                return RedirectToAction(nameof(Index));
            }

            return View(await BuildEditorAsync(locationInterviewer));
        }

        // GET: LocationInterviewers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.InterviewerLocations == null)
            {
                return NotFound();
            }

            var locationInterviewer = await _context.InterviewerLocations.FindAsync(id);
            if (locationInterviewer == null)
            {
                return NotFound();
            }

            var interviewer = await _userManager.FindByIdAsync(locationInterviewer.InterviewerId);
            if (interviewer is null)
            {
                return NotFound();
            }

            var viewModel = await BuildEditorAsync(locationInterviewer);
            viewModel.InterviewerName = interviewer.FirstName + " " + interviewer.LastName;
            return View(viewModel);
        }

        // POST: LocationInterviewers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,InterviewerId,LocationId,EventId,Preference")] InterviewerLocation locationInterviewer)
        {
            if (id != locationInterviewer.Id)
            {
                return NotFound();
            }

            var existingAssignment = await _context.InterviewerLocations.FindAsync(id);
            if (existingAssignment is null)
            {
                return NotFound();
            }

            existingAssignment.LocationId = locationInterviewer.LocationId;
            if (await IsValidAssignmentAsync(existingAssignment))
            {
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = "Room assignment updated.";
                return RedirectToAction(nameof(Index));
            }

            locationInterviewer.InterviewerId = existingAssignment.InterviewerId;
            locationInterviewer.EventId = existingAssignment.EventId;
            locationInterviewer.Preference = existingAssignment.Preference;
            return View(await BuildEditorAsync(locationInterviewer));
        }

        // GET: LocationInterviewers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.InterviewerLocations == null)
            {
                return NotFound();
            }

            var locationInterviewer = await _context.InterviewerLocations
                .Include(l => l.Location)
                .Include(e => e.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (locationInterviewer == null)
            {
                return NotFound();
            }

            var interviewer = await _userManager.FindByIdAsync(locationInterviewer.InterviewerId);
            if (interviewer is null)
            {
                return NotFound();
            }

            var locationInterviewerWithName = new LocationInterviewerWithName
            {
                LocationInterviewer = locationInterviewer,
                InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
            };

            return View(locationInterviewerWithName);
        }

        // POST: LocationInterviewers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var locationInterviewer = await _context.InterviewerLocations.FindAsync(id);
            if (locationInterviewer != null)
            {
                _context.InterviewerLocations.Remove(locationInterviewer);
            }

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Room assignment deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<LocationInterviewerCreateViewModel> BuildEditorAsync(InterviewerLocation assignment)
        {
            var interviewerRows = await _context.InterviewerTimeslots
                .Where(timeslot => timeslot.Timeslot.Event.IsActive)
                .Select(timeslot => new
                {
                    timeslot.InterviewerSignup.InterviewerId,
                    timeslot.InterviewerSignup.FirstName,
                    timeslot.InterviewerSignup.LastName
                })
                .ToListAsync();

            return new LocationInterviewerCreateViewModel
            {
                LocationInterviewer = assignment,
                InterviewerNames = interviewerRows
                    .GroupBy(interviewer => interviewer.InterviewerId)
                    .Select(group => new SelectListItem
                    {
                        Value = group.Key,
                        Text = $"{group.First().FirstName} {group.First().LastName}"
                    })
                    .OrderBy(interviewer => interviewer.Text)
                    .ToList(),
                Locations = await _context.Locations
                    .OrderBy(location => location.Room)
                    .Select(location => new SelectListItem { Value = location.Id.ToString(), Text = location.Room })
                    .ToListAsync(),
                Dates = await _context.Events
                    .Where(@event => @event.IsActive)
                    .OrderBy(@event => @event.Date)
                    .Select(@event => new SelectListItem { Value = @event.Id.ToString(), Text = $"{@event.Name} ({@event.Date:d})" })
                    .ToListAsync()
            };
        }

        private async Task<bool> IsValidAssignmentAsync(InterviewerLocation assignment)
        {
            if (string.IsNullOrWhiteSpace(assignment.InterviewerId)
                || assignment.EventId is null
                || !await _context.Events.AnyAsync(@event => @event.Id == assignment.EventId && @event.IsActive)
                || !await _context.InterviewerTimeslots.AnyAsync(timeslot =>
                    timeslot.InterviewerSignup.InterviewerId == assignment.InterviewerId
                    && timeslot.Timeslot.Event.IsActive))
            {
                ModelState.AddModelError(string.Empty, "Choose an interviewer with active availability and an active event.");
                return false;
            }

            if (assignment.LocationId is not null
                && !await _context.Locations.AnyAsync(location => location.Id == assignment.LocationId))
            {
                ModelState.AddModelError(nameof(assignment.LocationId), "Choose an existing room or location.");
                return false;
            }

            return true;
        }

        private bool LocationInterviewerExists(int id)
        {
            return (_context.InterviewerLocations?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
