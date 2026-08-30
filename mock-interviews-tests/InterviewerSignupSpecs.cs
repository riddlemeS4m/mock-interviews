using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class InterviewerSignupSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Valid_signup_creates_the_interviewer_availability_graph_and_notifications()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
            await TestData.AddEventWithTimeslotsAsync(context, For221.n));
        using var client = Factory.CreateAnonymousClient();

        var response = await SubmitSignupAsync(client, data.Timeslots[0].Id);

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var graph = await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await context.Users.SingleAsync(item => item.Email == "interviewer@example.test");
            var signup = await context.InterviewerSignups.SingleAsync(item => item.InterviewerId == user.Id);
            var availability = await context.InterviewerTimeslots
                .Where(item => item.InterviewerSignupId == signup.Id)
                .OrderBy(item => item.TimeslotId)
                .ToListAsync();
            var location = await context.InterviewerLocations
                .SingleAsync(item => item.InterviewerId == user.Id && item.EventId == data.Event.Id);
            var role = await context.Roles.SingleAsync(item => item.Name == RolesConstants.InterviewerRole);
            var hasRole = await context.UserRoles.AnyAsync(item => item.UserId == user.Id && item.RoleId == role.Id);
            return (user, signup, availability, location, hasRole);
        });

        Assert.True(graph.hasRole);
        Assert.True(graph.signup.InPerson);
        Assert.True(graph.signup.IsBehavioral);
        Assert.Equal(
            new[] { data.Timeslots[0].Id, data.Timeslots[1].Id },
            graph.availability.Select(item => item.TimeslotId).ToArray());
        Assert.Equal(Data.Constants.InterviewLocationConstants.InPerson, graph.location.Preference);
        Assert.Equal(3, Factory.SentEmails.Count);
        Assert.Contains(
            Factory.SentEmails,
            message => message.Contents?.Any(content =>
                content.Type == "text/html" && content.Value.Contains("You have been invited to Mock Interviews.")) == true);
    }

    [Fact]
    public async Task Invalid_signup_is_atomic()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
            await TestData.AddEventWithTimeslotsAsync(context, For221.n));
        using var client = Factory.CreateAnonymousClient();

        var response = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/Create", SignupFields(
            data.Timeslots[0].Id,
            includeInterviewType: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.False(await context.Users.AnyAsync(item => item.Email == "interviewer@example.test"));
            Assert.Empty(await context.InterviewerSignups.ToListAsync());
            Assert.Empty(await context.InterviewerTimeslots.ToListAsync());
            Assert.Empty(await context.InterviewerLocations.ToListAsync());
        });
        Assert.Empty(Factory.SentEmails);
    }

    [Fact]
    public async Task Existing_account_is_reused_and_receives_the_interviewer_role()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "existing-user", email: "interviewer@example.test");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return (user, schedule);
        });
        using var client = Factory.CreateAnonymousClient();

        var response = await SubmitSignupAsync(client, data.schedule.Timeslots[0].Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Equal(1, await context.Users.CountAsync());
            var role = await context.Roles.SingleAsync(item => item.Name == RolesConstants.InterviewerRole);
            Assert.True(await context.UserRoles.AnyAsync(item => item.UserId == data.user.Id && item.RoleId == role.Id));
            Assert.True(await context.InterviewerSignups.AnyAsync(item => item.InterviewerId == data.user.Id));
        });
    }

    [Fact]
    public async Task Repeat_signup_rejects_duplicates_but_can_add_new_availability()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
            await TestData.AddEventWithTimeslotsAsync(context, For221.n));
        using var client = Factory.CreateAnonymousClient();

        var initial = await SubmitSignupAsync(client, data.Timeslots[0].Id);
        var duplicate = await SubmitSignupAsync(client, data.Timeslots[0].Id);
        var additional = await SubmitSignupAsync(client, data.Timeslots[2].Id);

        Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, additional.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Single(await context.InterviewerSignups.ToListAsync());
            Assert.Single(await context.InterviewerLocations.ToListAsync());
            Assert.Equal(4, await context.InterviewerTimeslots.CountAsync());
        });
        Assert.Equal(5, Factory.SentEmails.Count);
    }

    private static async Task<HttpResponseMessage> SubmitSignupAsync(HttpClient client, int selectedTimeslotId)
        => await client.PostFormWithAntiforgeryAsync(
            "/SignupInterviewerTimeslots/Create",
            SignupFields(selectedTimeslotId, includeInterviewType: true));

    private static IEnumerable<KeyValuePair<string, string>> SignupFields(
        int selectedTimeslotId,
        bool includeInterviewType)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("SelectedEventIds1", selectedTimeslotId.ToString()),
            new("SignupInterviewer.InPerson", "true"),
            new("Lunch", "true"),
            new("Email", "interviewer@example.test"),
            new("Company", "Example Company"),
            new("FirstName", "Interview"),
            new("LastName", "Person")
        };
        if (includeInterviewType)
        {
            fields.Add(new("SignupInterviewer.IsBehavioral", "true"));
        }

        return fields;
    }
}
