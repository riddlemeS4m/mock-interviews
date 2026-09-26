using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class TimeslotAdministrationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Invalid_timeslot_creation_preserves_the_selected_event_and_entered_values()
    {
        var @event = await Factory.InDatabaseScopeAsync(async context =>
        {
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, name: "Create form event");
            return schedule.Event;
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/Timeslots/Create", new[]
        {
            new KeyValuePair<string, string>("EventId", @event.Id.ToString()),
            new KeyValuePair<string, string>("Time", "2030-10-01T09:00"),
            new KeyValuePair<string, string>("MaxSignUps", "-1"),
            new KeyValuePair<string, string>("IsStudent", "true"),
            new KeyValuePair<string, string>("IsVolunteer", "true"),
            new KeyValuePair<string, string>("IsActive", "true")
        });

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create form event", html);
        Assert.Contains($"value=\"{@event.Id}\" selected", html);
        Assert.Contains("value=\"-1\"", html);
    }
}
