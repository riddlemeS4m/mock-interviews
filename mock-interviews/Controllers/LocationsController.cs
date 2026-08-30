using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.LocationsController;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class LocationsController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly ILogger<LocationsController> _logger;

        public LocationsController(MockInterviewsDbContext context, ILogger<LocationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Locations
        public async Task<IActionResult> Index()
        {
            return View(await BuildIndexViewModelAsync());
        }

        // GET: Locations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        // GET: Locations/Create
        public IActionResult Create()
        {
            return View(new Location());
        }

        // POST: Locations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Room,IsVirtual,InPerson")] Location location)
        {
            if (ModelState.IsValid)
            {
                _context.Add(location);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"{location.Room} was added.";
                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), await BuildIndexViewModelAsync(location, "location-create-dialog"));
        }

        // GET: Locations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound();
            }
            return View(location);
        }

        // POST: Locations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Room,IsVirtual,InPerson")] Location location)
        {
            if (id != location.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(location);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await LocationExistsAsync(location.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["StatusMessage"] = $"{location.Room} was updated.";
                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), await BuildIndexViewModelAsync(location, "location-edit-dialog"));
        }

        // GET: Locations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        // POST: Locations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                TempData["ErrorMessage"] = "That room no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"{location.Room} was deleted.";
            }
            catch (DbUpdateException exception)
            {
                _logger.LogWarning(exception, "Unable to delete location {LocationId} because it is in use", id);
                TempData["ErrorMessage"] = $"{location.Room} could not be deleted because it is used by an interview or assignment.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> LocationExistsAsync(int id)
            => await _context.Locations.AnyAsync(location => location.Id == id);

        private async Task<LocationsIndexViewModel> BuildIndexViewModelAsync(
            Location? editor = null,
            string? activeDialog = null)
        {
            var locations = await _context.Locations
                .AsNoTracking()
                .OrderBy(location => location.Room)
                .ToListAsync();

            return new LocationsIndexViewModel(locations, editor ?? new Location(), activeDialog);
        }
    }
}
