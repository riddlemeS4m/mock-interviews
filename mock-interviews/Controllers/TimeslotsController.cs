using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.TimeslotsController;

namespace MockInterviews.Controllers;

[Authorize(Roles = RolesConstants.AdministrationRoles)]
public class TimeslotsController(
    MockInterviewsDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<TimeslotsController> logger) : Controller
{
    // GET: Timeslots
    public async Task<IActionResult> Index()
    {
        var timeslots = await context.Timeslots.AsNoTracking().Include(slot => slot.Event)
            .Where(slot => slot.Event.IsActive)
            .OrderBy(slot => slot.Event.Date).ThenBy(slot => slot.Event.Name).ThenBy(slot => slot.Time)
            .ToListAsync();
        var groups = timeslots.GroupBy(slot => slot.EventId)
            .Select(group => new EventTimeslotGroupViewModel(group.First().Event, group.ToList()))
            .ToList();
        return View(new TimeslotIndexViewModel(groups));
    }

    // GET: Timeslots/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        var timeslot = id is null ? null : await context.Timeslots.AsNoTracking().Include(slot => slot.Event)
            .SingleOrDefaultAsync(slot => slot.Id == id && slot.Event.IsActive);
        if (timeslot is null)
        {
            return NotFound();
        }

        var interviewerIds = await context.InterviewerTimeslots.Where(item => item.TimeslotId == timeslot.Id)
            .Select(item => item.InterviewerSignup.InterviewerId).ToListAsync();
        var studentIds = await context.Interviews.Where(item => item.TimeslotId == timeslot.Id).Select(item => item.StudentId).ToListAsync();
        var volunteerIds = await context.VolunteerTimeslots.Where(item => item.TimeslotId == timeslot.Id).Select(item => item.StudentId).ToListAsync();
        var users = await userManager.Users.Where(user => interviewerIds.Concat(studentIds).Concat(volunteerIds).Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => $"{user.FirstName} {user.LastName}");
        string Name(string userId) => users.GetValueOrDefault(userId, "Deleted user");

        return View(new TimeslotDetailsViewModel
        {
            Timeslot = timeslot,
            InterviewerNames = interviewerIds.Select(Name).ToList(),
            StudentNames = studentIds.Select(Name).ToList(),
            VolunteerNames = volunteerIds.Select(Name).ToList()
        });
    }

    // GET: Timeslots/Create
    public async Task<IActionResult> Create() => View(await BuildCreateViewModelAsync());

    // POST: Timeslots/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Time,EventId,IsActive,IsVolunteer,IsInterviewer,IsStudent,MaxSignUps")] Timeslot timeslot)
    {
        if (!await context.Events.AnyAsync(@event => @event.Id == timeslot.EventId && @event.IsActive))
        {
            ModelState.AddModelError(nameof(timeslot.EventId), "Choose a current active event.");
        }
        if (timeslot.MaxSignUps < 0)
        {
            ModelState.AddModelError(nameof(timeslot.MaxSignUps), "Maximum signups must be zero or greater.");
        }
        if (!ModelState.IsValid)
        {
            return View(await BuildCreateViewModelAsync(timeslot));
        }

        timeslot.Time = DateTime.SpecifyKind(timeslot.Time, DateTimeKind.Utc);
        context.Timeslots.Add(timeslot);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Timeslot was created.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Timeslots/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        var timeslot = id is null ? null : await context.Timeslots.AsNoTracking().Include(slot => slot.Event)
            .SingleOrDefaultAsync(slot => slot.Id == id && slot.Event.IsActive);
        return timeslot is null ? NotFound() : View(TimeslotEditViewModel.FromTimeslot(timeslot));
    }

    // POST: Timeslots/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TimeslotEditViewModel input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }
        var timeslot = await context.Timeslots.Include(slot => slot.Event)
            .SingleOrDefaultAsync(slot => slot.Id == id && slot.Event.IsActive);
        if (timeslot is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            input.EventName = timeslot.Event.Name;
            input.EventDate = timeslot.Event.Date;
            input.Time = timeslot.Time;
            return View(input);
        }

        timeslot.MaxSignUps = input.MaxSignUps;
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Timeslot capacity was updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Timeslots/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        var timeslot = id is null ? null : await context.Timeslots.AsNoTracking().Include(slot => slot.Event)
            .SingleOrDefaultAsync(slot => slot.Id == id);
        return timeslot is null ? NotFound() : View(timeslot);
    }

    // POST: Timeslots/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var timeslot = await context.Timeslots.SingleOrDefaultAsync(slot => slot.Id == id);
        if (timeslot is null)
        {
            TempData["ErrorMessage"] = "That timeslot no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            context.Timeslots.Remove(timeslot);
            await context.SaveChangesAsync();
            TempData["StatusMessage"] = "Timeslot was deleted.";
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Unable to delete timeslot {TimeslotId}", id);
            TempData["ErrorMessage"] = "That timeslot could not be deleted because it has related scheduling records.";
        }
        return RedirectToAction(nameof(Index));
    }

    // GET: Timeslots/UpdateMaxTimeslots
    public IActionResult UpdateMaxTimeslots() => View(new UpdateMaximumSignupsViewModel());

    // POST: Timeslots/UpdateMaxSignups
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMaxSignups(UpdateMaximumSignupsViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(UpdateMaxTimeslots), input);
        }

        var timeslots = await context.Timeslots.ToListAsync();
        foreach (var timeslot in timeslots)
        {
            timeslot.MaxSignUps = input.MaxSignUps;
        }
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = $"Capacity was updated for all {timeslots.Count} timeslots.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Timeslot> BuildCreateViewModelAsync(Timeslot? timeslot = null)
    {
        ViewBag.EventOptions = new SelectList(await context.Events.AsNoTracking().Where(@event => @event.IsActive)
            .OrderBy(@event => @event.Date).ToListAsync(), nameof(Event.Id), nameof(Event.Name));
        return timeslot ?? new Timeslot { IsActive = true, IsVolunteer = true };
    }
}
