using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Data.Seeds;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.EventDatesController;
using MockInterviews.Services;

namespace MockInterviews.Controllers;

[Authorize(Roles = RolesConstants.AdministrationRoles)]
public class EventDatesController(
    MockInterviewsDbContext context,
    TimeslotService timeslotService,
    ILogger<EventDatesController> logger) : Controller
{
    // GET: EventDates
    public async Task<IActionResult> Index()
        => View(await BuildIndexViewModelAsync());

    // GET: EventDates/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        var @event = id is null ? null : await context.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return @event is null ? NotFound() : View(@event);
    }

    // GET: EventDates/Create
    public IActionResult Create() => View(new EventDateCreationViewModel());

    // POST: EventDates/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventDateCreationViewModel input)
    {
        if (string.IsNullOrWhiteSpace(input.EventDate.Name))
        {
            ModelState.AddModelError("EventDate.Name", "An event name is required.");
        }
        if (!TryGetFor221(input.For221True is not null, input.For221False is not null, out var for221))
        {
            ModelState.AddModelError("EventDate.For221", "Choose at least one eligible student group.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildIndexViewModelAsync(input, "event-create-dialog"));
        }

        var @event = new Event
        {
            Date = DateTime.SpecifyKind(input.EventDate.Date.Date, DateTimeKind.Utc),
            Name = input.EventDate.Name.Trim(),
            IsActive = input.EventDate.IsActive,
            For221 = for221
        };

        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Events.AddAsync(@event);
        await context.SaveChangesAsync();
        await TimeslotSeed.SeedTimeslots(timeslotService, @event, input.MaxSignUps);
        await transaction.CommitAsync();

        TempData["StatusMessage"] = $"{@event.Name} was created with its 18 half-hour timeslots.";
        return RedirectToAction(nameof(Index));
    }

    // GET: EventDates/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        var @event = id is null ? null : await context.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return @event is null ? NotFound() : View(EventDateEditViewModel.FromEvent(@event));
    }

    // POST: EventDates/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventDateEditViewModel input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), "An event name is required.");
        }
        if (!TryGetFor221(input.For221True, input.For221False, out var for221))
        {
            ModelState.AddModelError(nameof(input.For221True), "Choose at least one eligible student group.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var @event = await context.Events.SingleOrDefaultAsync(item => item.Id == id);
        if (@event is null)
        {
            return NotFound();
        }

        @event.Date = DateTime.SpecifyKind(input.Date.Date, DateTimeKind.Utc);
        @event.Name = input.Name.Trim();
        @event.IsActive = input.IsActive;
        @event.For221 = for221;
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = $"{@event.Name} was updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: EventDates/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        var @event = id is null ? null : await context.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return @event is null ? NotFound() : View(@event);
    }

    // POST: EventDates/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var @event = await context.Events.SingleOrDefaultAsync(item => item.Id == id);
        if (@event is null)
        {
            TempData["ErrorMessage"] = "That event no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            context.Events.Remove(@event);
            await context.SaveChangesAsync();
            TempData["StatusMessage"] = $"{@event.Name} was deleted.";
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Unable to delete event {EventId}", id);
            TempData["ErrorMessage"] = $"{@event.Name} could not be deleted because it has related scheduling records.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<EventDateIndexViewModel> BuildIndexViewModelAsync(
        EventDateCreationViewModel? editor = null,
        string? activeDialog = null)
    {
        var events = await context.Events.AsNoTracking().OrderByDescending(item => item.Date).ThenBy(item => item.Name).ToListAsync();
        return new EventDateIndexViewModel(events, editor ?? new EventDateCreationViewModel(), activeDialog);
    }

    private static bool TryGetFor221(bool includes221, bool includesUpper, out For221 for221)
    {
        for221 = includes221 && includesUpper ? For221.b : includes221 ? For221.y : For221.n;
        return includes221 || includesUpper;
    }
}
