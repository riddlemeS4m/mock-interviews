using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class VolunteerSignupSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Eligible_volunteer_signup_persists_selected_slots_and_records_notifications()
    {
        var slots = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, eventSlots) = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return eventSlots;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", slots[0].Id.ToString()),
            new KeyValuePair<string, string>("SelectedEventIds1", slots[1].Id.ToString())
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var savedSlotIds = await Factory.InDatabaseScopeAsync(async context => await context.VolunteerTimeslots
            .Where(item => item.StudentId == "student-1")
            .Select(item => item.TimeslotId)
            .OrderBy(id => id)
            .ToListAsync());
        Assert.Equal(new[] { slots[0].Id, slots[1].Id }, savedSlotIds);
        Assert.Equal(2, Factory.SentEmails.Count);
    }

    [Fact]
    public async Task Volunteer_signup_rejects_conflicting_and_forged_slots_without_writes()
    {
        var slots = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, eventSlots) = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            context.Interviews.Add(new Interview
            {
                StudentId = "student-1",
                TimeslotId = eventSlots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            });
            await context.SaveChangesAsync();
            return eventSlots;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var conflicting = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", slots[0].Id.ToString())
        });
        var forged = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", "999999")
        });

        Assert.Equal(HttpStatusCode.OK, conflicting.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        var count = await Factory.InDatabaseScopeAsync(async context => await context.VolunteerTimeslots.CountAsync());
        Assert.Equal(0, count);
        Assert.Empty(Factory.SentEmails);
    }

    [Fact]
    public async Task Volunteer_signup_rejects_inactive_and_duplicate_selections()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var active = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var inactive = await TestData.AddEventWithTimeslotsAsync(context, For221.n, active: false);
            return (active, inactive);
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var initial = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", data.active.Timeslots[0].Id.ToString())
        });
        var duplicate = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", data.active.Timeslots[0].Id.ToString())
        });
        var inactive = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds1", data.inactive.Timeslots[0].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        Assert.Equal(1, await Factory.InDatabaseScopeAsync(context => context.VolunteerTimeslots.CountAsync()));
        Assert.Equal(2, Factory.SentEmails.Count);
    }
}
