using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class RoomAssignmentSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task System_admin_can_create_a_room_assignment_for_active_availability()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "interviewer-1");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var signup = new InterviewerSignup { InterviewerId = "interviewer-1", FirstName = "Test", LastName = "Interviewer", InPerson = true };
            var location = new Location { Room = "Room 101", InPerson = true, IsVirtual = true };
            context.InterviewerSignups.Add(signup);
            context.Locations.Add(location);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = schedule.Timeslots[0].Id });
            await context.SaveChangesAsync();
            return (schedule.Event, location);
        });
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/LocationInterviewers/Create", new[]
        {
            new KeyValuePair<string, string>("LocationInterviewer.InterviewerId", "interviewer-1"),
            new KeyValuePair<string, string>("LocationInterviewer.EventId", data.Event.Id.ToString()),
            new KeyValuePair<string, string>("LocationInterviewer.LocationId", data.location.Id.ToString()),
            new KeyValuePair<string, string>("InPerson", "true")
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.InterviewerLocations.AnyAsync()));
    }

    [Fact]
    public async Task Room_assignment_rejects_forged_options()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/LocationInterviewers/Create", new[]
        {
            new KeyValuePair<string, string>("LocationInterviewer.InterviewerId", "missing"),
            new KeyValuePair<string, string>("LocationInterviewer.EventId", "999999"),
            new KeyValuePair<string, string>("LocationInterviewer.LocationId", "999999"),
            new KeyValuePair<string, string>("InPerson", "true")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await Factory.InDatabaseScopeAsync(context => context.InterviewerLocations.AnyAsync()));
    }
}
