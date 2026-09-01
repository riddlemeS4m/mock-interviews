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

        var page = await client.GetAsync("/SignupInterviewerTimeslots/Create");
        var pageHtml = await page.Content.ReadAsStringAsync();
        var response = await SubmitSignupAsync(client, data.Timeslots[1].Id);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains($"value=\"{data.Timeslots[1].Id}\"", pageHtml);
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
        Assert.False(data.Timeslots[1].IsInterviewer);
        Assert.Equal(new[] { data.Timeslots[1].Id }, graph.availability.Select(item => item.TimeslotId).ToArray());
        Assert.Equal(Data.Constants.InterviewLocationConstants.InPerson, graph.location.Preference);
        Assert.Equal(3, Factory.SentEmails.Count);
        Assert.Contains(
            Factory.SentEmails,
            message => message.HtmlBody.Contains("You have been invited to Mock Interviews."));
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
    public async Task Anonymous_signup_rejects_an_email_that_belongs_to_an_existing_account()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "existing-user", email: "interviewer@example.test");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return (user, schedule);
        });
        using var client = Factory.CreateAnonymousClient();

        var response = await SubmitSignupAsync(client, data.schedule.Timeslots[0].Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in to add interviewer availability", await response.Content.ReadAsStringAsync());
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Equal(1, await context.Users.CountAsync());
            var role = await context.Roles.SingleAsync(item => item.Name == RolesConstants.InterviewerRole);
            Assert.False(await context.UserRoles.AnyAsync(item => item.UserId == data.user.Id && item.RoleId == role.Id));
            Assert.False(await context.InterviewerSignups.AnyAsync(item => item.InterviewerId == data.user.Id));
        });
    }

    [Fact]
    public async Task Repeat_anonymous_signup_requires_sign_in_and_does_not_add_availability()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
            await TestData.AddEventWithTimeslotsAsync(context, For221.n));
        using var client = Factory.CreateAnonymousClient();

        var initial = await SubmitSignupAsync(client, data.Timeslots[0].Id);
        var duplicate = await SubmitSignupAsync(client, data.Timeslots[0].Id);
        var additional = await SubmitSignupAsync(client, data.Timeslots[2].Id);

        Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, additional.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Single(await context.InterviewerSignups.ToListAsync());
            Assert.Single(await context.InterviewerLocations.ToListAsync());
            Assert.Single(await context.InterviewerTimeslots.ToListAsync());
        });
        Assert.Equal(3, Factory.SentEmails.Count);
    }

    [Fact]
    public async Task Authenticated_edit_reconciles_locations_and_rejects_forged_slots()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "interviewer-1");
            var firstDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var secondDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Test",
                LastName = "Interviewer",
                InPerson = true,
                IsBehavioral = true,
                Type = "Behavioral"
            };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = firstDay.Timeslots[0].Id });
            context.InterviewerLocations.Add(new InterviewerLocation { InterviewerId = "interviewer-1", EventId = firstDay.Event.Id, Preference = Data.Constants.InterviewLocationConstants.InPerson });
            await context.SaveChangesAsync();
            return (signup, firstDay, secondDay);
        });
        using var client = Factory.CreateAuthenticatedClient("interviewer-1", RolesConstants.InterviewerRole);

        var update = await client.PostFormWithAntiforgeryAsync($"/SignupInterviewerTimeslots/Edit/{data.signup.Id}", new[]
        {
            new KeyValuePair<string, string>("SignupInterviewer.Id", data.signup.Id.ToString()),
            new KeyValuePair<string, string>("SignupInterviewer.InterviewerId", "interviewer-1"),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "false"),
            new KeyValuePair<string, string>("SignupInterviewer.IsBehavioral", "true"),
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.secondDay.Timeslots[1].Id.ToString())
        });
        var forged = await client.PostFormWithAntiforgeryAsync($"/SignupInterviewerTimeslots/Edit/{data.signup.Id}", new[]
        {
            new KeyValuePair<string, string>("SignupInterviewer.Id", data.signup.Id.ToString()),
            new KeyValuePair<string, string>("SignupInterviewer.InterviewerId", "interviewer-1"),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "true"),
            new KeyValuePair<string, string>("SignupInterviewer.IsBehavioral", "true"),
            new KeyValuePair<string, string>("SelectedTimeslotIds", "999999")
        });

        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var locations = await context.InterviewerLocations
                .Where(location => location.InterviewerId == "interviewer-1")
                .ToListAsync();
            Assert.Single(locations);
            Assert.Equal(data.secondDay.Event.Id, locations[0].EventId);
            Assert.Equal(Data.Constants.InterviewLocationConstants.IsVirtual, locations[0].Preference);
            Assert.Equal(new[] { data.secondDay.Timeslots[1].Id }, await context.InterviewerTimeslots
                .Where(timeslot => timeslot.InterviewerSignupId == data.signup.Id)
                .OrderBy(timeslot => timeslot.TimeslotId)
                .Select(timeslot => timeslot.TimeslotId)
                .ToArrayAsync());
        });
    }

    [Fact]
    public async Task Authenticated_edit_cannot_modify_another_interviewers_signup()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "attacker");
            await TestData.AddUserAsync(context, "owner");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var ownerSignup = new InterviewerSignup
            {
                InterviewerId = "owner",
                FirstName = "Owner",
                LastName = "Interviewer",
                InPerson = true,
                IsBehavioral = true,
                Type = "Behavioral"
            };
            context.InterviewerSignups.Add(ownerSignup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = ownerSignup.Id,
                TimeslotId = schedule.Timeslots[0].Id
            });
            await context.SaveChangesAsync();
            return (ownerSignup, schedule);
        });
        using var client = Factory.CreateAuthenticatedClient("attacker", RolesConstants.InterviewerRole);

        var response = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/Create", $"/SignupInterviewerTimeslots/Edit/{data.ownerSignup.Id}", new[]
        {
            new KeyValuePair<string, string>("SignupInterviewer.Id", data.ownerSignup.Id.ToString()),
            new KeyValuePair<string, string>("SignupInterviewer.InterviewerId", "attacker"),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "false"),
            new KeyValuePair<string, string>("SignupInterviewer.IsTechnical", "true"),
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.schedule.Timeslots[1].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var signup = await context.InterviewerSignups.SingleAsync(item => item.Id == data.ownerSignup.Id);
            var availability = await context.InterviewerTimeslots
                .Where(item => item.InterviewerSignupId == signup.Id)
                .Select(item => item.TimeslotId)
                .ToListAsync();

            Assert.Equal("owner", signup.InterviewerId);
            Assert.True(signup.InPerson);
            Assert.True(signup.IsBehavioral);
            Assert.Equal(new[] { data.schedule.Timeslots[0].Id }, availability);
        });
    }

    [Fact]
    public async Task Authenticated_student_interviewer_can_submit_the_same_slots_shown_on_create()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-interviewer", Classes.SecondSem, "student-interviewer@example.test");
            var eligible = await TestData.AddEventWithTimeslotsAsync(context, For221.y, name: "MIS 221");
            var ineligible = await TestData.AddEventWithTimeslotsAsync(context, For221.n, name: "Upper level");
            return (eligible, ineligible);
        });
        using var client = Factory.CreateAuthenticatedClient("student-interviewer", RolesConstants.StudentRole, RolesConstants.InterviewerRole);

        var page = await client.GetAsync("/SignupInterviewerTimeslots/Create");
        var response = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.eligible.Timeslots[0].Id.ToString()),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "false"),
            new KeyValuePair<string, string>("SignupInterviewer.IsBehavioral", "true"),
            new KeyValuePair<string, string>("Email", "forged@example.test"),
            new KeyValuePair<string, string>("Company", "Forged Co"),
            new KeyValuePair<string, string>("FirstName", "Forged"),
            new KeyValuePair<string, string>("LastName", "Name")
        });
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("MIS 221", html);
        Assert.DoesNotContain("Upper level", html);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.InterviewerTimeslots.AnyAsync(slot => slot.TimeslotId == data.eligible.Timeslots[0].Id)));
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
            new("SelectedTimeslotIds", selectedTimeslotId.ToString()),
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
