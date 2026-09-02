using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class VolunteerAdministrationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Volunteer_administration_renders_an_empty_state()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.GetAsync("/VolunteerEvents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No volunteer availability", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task System_admin_can_review_and_cancel_a_volunteer_availability_range()
    {
        var availabilityIds = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "volunteer-1");
            var (_, timeslots) = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var assignments = new[]
            {
                new VolunteerTimeslot { StudentId = "volunteer-1", TimeslotId = timeslots[0].Id },
                new VolunteerTimeslot { StudentId = "volunteer-1", TimeslotId = timeslots[1].Id }
            };
            context.VolunteerTimeslots.AddRange(assignments);
            await context.SaveChangesAsync();
            return assignments.Select(assignment => assignment.Id).ToArray();
        });
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var index = await client.GetAsync("/VolunteerEvents");
        var confirmation = await client.GetAsync($"/VolunteerEvents/DeleteRange?timeslots={availabilityIds[0]}&timeslots={availabilityIds[1]}");
        var unprotectedPost = await client.PostAsync("/VolunteerEvents/DeleteRangeConfirmed", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("timeslots", availabilityIds[0].ToString()),
            new KeyValuePair<string, string>("timeslots", availabilityIds[1].ToString())
        }));

        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("Volunteer availability", await index.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unprotectedPost.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.VolunteerTimeslots.AnyAsync()));

        var protectedPost = await client.PostFormWithAntiforgeryAsync(
            $"/VolunteerEvents/DeleteRange?timeslots={availabilityIds[0]}&timeslots={availabilityIds[1]}",
            "/VolunteerEvents/DeleteRangeConfirmed",
            new[]
            {
                new KeyValuePair<string, string>("timeslots", availabilityIds[0].ToString()),
                new KeyValuePair<string, string>("timeslots", availabilityIds[1].ToString())
            });

        Assert.Equal(HttpStatusCode.Redirect, protectedPost.StatusCode);
        Assert.False(await Factory.InDatabaseScopeAsync(context => context.VolunteerTimeslots.AnyAsync()));
    }

    [Fact]
    public async Task Volunteer_administration_is_not_available_to_students()
    {
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.GetAsync("/VolunteerEvents");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
