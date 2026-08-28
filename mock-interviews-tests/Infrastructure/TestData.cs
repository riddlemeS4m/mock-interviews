using MockInterviews.Data.Constants;

namespace MockInterviews.IntegrationTests.Infrastructure;

public static class TestData
{
    public static async Task<ApplicationUser> AddUserAsync(
        MockInterviewsDbContext context,
        string id,
        Classes @class = Classes.SecondSem,
        string? email = null)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = email ?? $"{id}@example.test",
            NormalizedUserName = (email ?? $"{id}@example.test").ToUpperInvariant(),
            Email = email ?? $"{id}@example.test",
            NormalizedEmail = (email ?? $"{id}@example.test").ToUpperInvariant(),
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = id,
            Class = @class,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<(Event Event, List<Timeslot> Timeslots)> AddEventWithTimeslotsAsync(
        MockInterviewsDbContext context,
        For221 for221 = For221.b,
        bool active = true,
        int maxSignups = 2,
        bool student = true,
        bool interviewer = true,
        bool volunteer = true,
        string? name = null)
    {
        var @event = new Event
        {
            Name = name ?? "Integration Event",
            Date = new DateTime(2030, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = active,
            For221 = for221
        };
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        var slots = Enumerable.Range(0, 4)
            .Select(index => new Timeslot
            {
                EventId = @event.Id,
                Time = @event.Date.AddHours(9).AddMinutes(index * 30),
                IsActive = true,
                IsStudent = student && index < 2,
                IsInterviewer = interviewer,
                IsVolunteer = volunteer,
                MaxSignUps = maxSignups
            })
            .ToList();
        context.Timeslots.AddRange(slots);
        await context.SaveChangesAsync();
        return (@event, slots);
    }
}
