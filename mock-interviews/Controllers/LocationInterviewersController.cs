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
    [Authorize(Roles = RolesConstants.AdminRole)]
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
            // Get a list of all Interviewers
            var availableInterviewers = await _context.InterviewerTimeslots
                .Include(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(i => i.Event)
                .Where(i => i.Timeslot.Event.IsActive == true)
                .Select(i => new SelectListItem
                {
                    Value = i.InterviewerSignup.InterviewerId,
                    Text = $"{i.InterviewerSignup.FirstName} {i.InterviewerSignup.LastName}"
                })
                .Distinct()
                .ToListAsync();


            var availableLocations = await _context.Locations
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = $"{i.Room}"
                })
                .ToListAsync();

            var availableDates = await _context.Events
                .Where(i => i.IsActive)
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = $"{i.Date:d}"
                })
                .ToListAsync();

            // Add message to dropdown list if no InterviewerIds or LocationIds are available
            if (availableInterviewers.Count == 0)
            {
                availableInterviewers.Add(new SelectListItem { Text = "No Interviewers available", Value = "" });
            }
            else
            {
                availableInterviewers = availableInterviewers.OrderBy(item => item.Text).ToList();
            }

            if (availableLocations.Count == 0)
            {
                availableLocations.Add(new SelectListItem { Text = "No Locations available", Value = "" });
            }
            else
            {
                availableLocations = availableLocations.OrderBy(item => item.Text).ToList();
            }

            // Create the view model
            var viewModel = new LocationInterviewerCreateViewModel
            {
                LocationInterviewer = new InterviewerLocation(),
                InterviewerNames = availableInterviewers,
                Dates = availableDates,
                Locations = availableLocations
            };


            return View(viewModel);
        }

        // POST: LocationInterviewers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,InterviewerId,LocationId,EventId")] InterviewerLocation locationInterviewer, bool InPerson)
        {
            if (locationInterviewer == null)
            {
                return NotFound();
            }

            if (InPerson)
            {
                locationInterviewer.Preference = InterviewLocationConstants.InPerson;
            }
            else
            {
                locationInterviewer.Preference = InterviewLocationConstants.IsVirtual;
            }

            if (ModelState.IsValid)
            {
                _context.Add(locationInterviewer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Get a list of all Interviewers
            var interviewers = await _userManager.GetUsersInRoleAsync(RolesConstants.InterviewerRole);

            var availableInterviewers = interviewers
                .Select(i => new SelectListItem
                {
                    Value = i.Id,
                    Text = $"{i.FirstName} {i.LastName}"
                })
                .ToList();

            var availableLocations = _context.Locations
                    .Select(i => new SelectListItem
                    {
                        Value = i.Id.ToString(),
                        Text = $"{i.Room}"
                    })
                    .ToList();

            var availableDates = _context.Events
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = $"{i.Date:d}"
                })
                    .ToList();

            // Add message to dropdown list if no InterviewerIds or LocationIds are available
            if (availableInterviewers.Count == 0)
            {
                availableInterviewers.Add(new SelectListItem { Text = "No Interviewers available", Value = "" });
            }
            else
            {
                availableInterviewers = availableInterviewers.OrderBy(item => item.Text).ToList();
            }

            if (availableLocations.Count == 0)
            {
                availableLocations.Add(new SelectListItem { Text = "No Locations available", Value = "" });
            }

            // Create the view model
            var viewModel = new LocationInterviewerCreateViewModel
            {
                LocationInterviewer = new InterviewerLocation(),
                InterviewerNames = availableInterviewers,
                Dates = availableDates,
                Locations = availableLocations
            };


            return View(viewModel);
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

            // Get a list of LocationIds already assigned to LocationInterviewers for today
            var assignedLocationIds = await _context.InterviewerLocations
                .Where(li => li.EventId == locationInterviewer.EventId)
                .Select(li => li.LocationId)
                .Distinct()
                .ToListAsync();

            // Get a list of all Locations except those already assigned to LocationInterviewers


            var availableLocations = await _context.Locations
                //.Where(l => (!assignedLocationIds.Contains(l.Id) || l.Id == locationInterviewer.LocationId) && (l.InPerson == inPerson && l.IsVirtual == isVirtual))
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),
                    Text = $"{i.Room}"
                })
                .ToListAsync();

            availableLocations = availableLocations.OrderBy(item => item.Text).ToList();

            if (availableLocations.Count == 0)
            {
                availableLocations.Add(new SelectListItem { Text = "No Locations available", Value = "" });
            }
            else
            {
                availableLocations.Insert(0, new SelectListItem { Text = "Unassigned", Value = "" });
            }

            var interviewer = await _userManager.FindByIdAsync(locationInterviewer.InterviewerId);
            if (interviewer is null)
            {
                return NotFound();
            }

            // Create the view model
            var viewModel = new LocationInterviewerCreateViewModel
            {
                LocationInterviewer = locationInterviewer,
                InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
                Locations = availableLocations
            };

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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(locationInterviewer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LocationInterviewerExists(locationInterviewer.Id))
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


            // Create the view model
            var viewModel = new LocationInterviewerCreateViewModel
            {
                LocationInterviewer = locationInterviewer
            };

            return View(viewModel);
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
            return RedirectToAction(nameof(Index));
        }

        private bool LocationInterviewerExists(int id)
        {
            return (_context.InterviewerLocations?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
