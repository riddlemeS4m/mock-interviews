using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class AuthorizationAndEventSetupSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Admin_routes_challenge_anonymous_and_forbid_students()
    {
        using var anonymous = Factory.CreateAnonymousClient();
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var anonymousResponse = await anonymous.GetAsync("/EventDates/Create");
        var studentResponse = await student.GetAsync("/EventDates/Create");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentResponse.StatusCode);
    }

    [Fact]
    public async Task System_admin_can_open_the_design_system()
    {
        using var anonymous = Factory.CreateAnonymousClient();
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var anonymousResponse = await anonymous.GetAsync("/System/Design");
        var systemAdminResponse = await systemAdmin.GetAsync("/System/Design");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, systemAdminResponse.StatusCode);
        Assert.Contains("UI Design System", await systemAdminResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_create_an_event_and_full_schedule()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/EventDates/Create", new[]
        {
            new KeyValuePair<string, string>("EventDate.Date", "2030-10-01"),
            new KeyValuePair<string, string>("EventDate.Name", "Fall mock interviews"),
            new KeyValuePair<string, string>("EventDate.IsActive", "true"),
            new KeyValuePair<string, string>("MaxSignUps", "3"),
            new KeyValuePair<string, string>("For221True", "y"),
            new KeyValuePair<string, string>("For221False", "n")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        Assert.Equal("/EventDates", response.Headers.Location?.OriginalString);

        var created = await Factory.InDatabaseScopeAsync(async context => await context.Events
            .SingleAsync(@event => @event.Name == "Fall mock interviews"));
        var slots = await Factory.InDatabaseScopeAsync(async context => await context.Timeslots
            .Where(slot => slot.EventId == created.Id)
            .OrderBy(slot => slot.Time)
            .ToListAsync());

        Assert.Equal(new DateTime(2030, 10, 1), created.Date);
        Assert.True(created.IsActive);
        Assert.Equal(For221.b, created.For221);
        Assert.Equal(18, slots.Count);
        Assert.All(slots, slot => Assert.Equal(3, slot.MaxSignUps));
        Assert.All(slots, slot => Assert.True(slot.IsActive));
        Assert.Equal(new[] { 2, 4, 6, 10, 12, 14 }, slots
            .Select((slot, index) => (slot, index))
            .Where(item => item.slot.IsStudent && item.slot.IsInterviewer)
            .Select(item => item.index)
            .ToArray());
    }
}
