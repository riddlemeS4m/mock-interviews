using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Services;

public sealed class ParticipantSchedulingService(MockInterviewsDbContext context)
{
    public Task<Timeslot?> FindAdjacentStudentInterviewTimeslotAsync(Timeslot selectedTimeslot)
        => context.Timeslots
            .Include(timeslot => timeslot.Event)
            .SingleOrDefaultAsync(timeslot =>
                timeslot.EventId == selectedTimeslot.EventId &&
                timeslot.IsActive &&
                timeslot.Event.IsActive &&
                timeslot.Time == selectedTimeslot.Time.AddMinutes(30));

    public async Task<IReadOnlyList<Timeslot>> ComposeEligibleInterviewerTimeslotsAsync(
        IEnumerable<Timeslot> eligibleStartTimeslots)
    {
        var starts = eligibleStartTimeslots.ToList();
        if (starts.Count == 0)
        {
            return [];
        }

        var eventIds = starts.Select(timeslot => timeslot.EventId).Distinct().ToList();
        var eventTimeslots = await context.Timeslots
            .Include(timeslot => timeslot.Event)
            .Where(timeslot => eventIds.Contains(timeslot.EventId) && timeslot.IsActive && timeslot.Event.IsActive)
            .ToListAsync();
        var availableTimeslotsById = eventTimeslots.ToDictionary(timeslot => timeslot.Id);
        var eligibleTimeslots = new List<Timeslot>();

        foreach (var start in starts)
        {
            if (!availableTimeslotsById.TryGetValue(start.Id, out var persistedStart))
            {
                continue;
            }

            var adjacent = eventTimeslots.SingleOrDefault(timeslot =>
                timeslot.EventId == persistedStart.EventId &&
                timeslot.Time == persistedStart.Time.AddMinutes(30));
            if (adjacent is null)
            {
                continue;
            }

            eligibleTimeslots.Add(persistedStart);
            eligibleTimeslots.Add(adjacent);
        }

        return eligibleTimeslots
            .DistinctBy(timeslot => timeslot.Id)
            .OrderBy(timeslot => timeslot.Event.Date)
            .ThenBy(timeslot => timeslot.Time)
            .ToList();
    }

    public IReadOnlyList<EventDaySelectionViewModel> ComposeEventDays(
        IEnumerable<Timeslot> timeslots,
        IEnumerable<int>? selectedTimeslotIds = null)
    {
        var selected = selectedTimeslotIds?.ToHashSet() ?? [];
        return timeslots
            .GroupBy(timeslot => timeslot.Event)
            .OrderBy(group => group.Key.Date)
            .ThenBy(group => group.Key.Name)
            .Select(group => new EventDaySelectionViewModel(
                group.Key.Id,
                group.Key.Name,
                group.Key.Date,
                group.OrderBy(timeslot => timeslot.Time)
                    .Select(timeslot => new TimeslotSelectionViewModel(
                        timeslot.Id,
                        timeslot.Time,
                        timeslot.Time.AddMinutes(30),
                        selected.Contains(timeslot.Id)))
                    .ToList()))
            .ToList();
    }
}
