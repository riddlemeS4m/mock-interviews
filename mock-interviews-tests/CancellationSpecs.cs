using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class CancellationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Student_can_cancel_only_their_own_interview()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "owner");
            await TestData.AddUserAsync(context, "other");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "owner",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var other = Factory.CreateAuthenticatedClient("other", RolesConstants.StudentRole);
        using var owner = Factory.CreateAuthenticatedClient("owner", RolesConstants.StudentRole);

        var get = await other.GetAsync($"/InterviewEvents/UserDeleteConfirmed/{interview.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.Interviews.AnyAsync()));

        var response = await owner.PostFormWithAntiforgeryAsync($"/InterviewEvents/UserDelete/{interview.Id}", "/InterviewEvents/UserDeleteConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", interview.Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await Factory.InDatabaseScopeAsync(context => context.Interviews.AnyAsync()));
    }

    [Fact]
    public async Task Student_with_one_remaining_interview_is_not_offered_another_signup()
    {
        var interviews = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "owner");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var appointments = new[]
            {
                new Interview { StudentId = "owner", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = "Behavioral" },
                new Interview { StudentId = "owner", TimeslotId = slots[1].Id, Status = StatusConstants.Default, Type = "Technical" }
            };
            context.Interviews.AddRange(appointments);
            await context.SaveChangesAsync();
            return appointments;
        });
        using var client = Factory.CreateAuthenticatedClient("owner", RolesConstants.StudentRole);

        var cancellation = await client.PostFormWithAntiforgeryAsync($"/InterviewEvents/UserDelete/{interviews[0].Id}", "/InterviewEvents/UserDeleteConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", interviews[0].Id.ToString())
        });
        var signupPage = await client.GetAsync("/InterviewEvents/Create");

        Assert.Equal(HttpStatusCode.Redirect, cancellation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signupPage.StatusCode);
        Assert.Contains("You are already scheduled", await signupPage.Content.ReadAsStringAsync());
        Assert.Equal(1, await Factory.InDatabaseScopeAsync(context => context.Interviews.CountAsync(interview => interview.StudentId == "owner")));
    }

    [Fact]
    public async Task Volunteer_can_cancel_only_their_own_assignment()
    {
        var assignment = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "owner");
            await TestData.AddUserAsync(context, "other");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var item = new VolunteerTimeslot { StudentId = "owner", TimeslotId = slots[0].Id };
            context.VolunteerTimeslots.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var other = Factory.CreateAuthenticatedClient("other", RolesConstants.StudentRole);
        using var owner = Factory.CreateAuthenticatedClient("owner", RolesConstants.StudentRole);

        var forbidden = await other.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", "/VolunteerEvents/UserDeleteRangeConfirmed", new[]
        {
            new KeyValuePair<string, string>("timeslots", assignment.Id.ToString())
        });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.VolunteerTimeslots.AnyAsync()));

        var response = await owner.PostFormWithAntiforgeryAsync($"/VolunteerEvents/UserDeleteRange?timeslots={assignment.Id}", "/VolunteerEvents/UserDeleteRangeConfirmed", new[]
        {
            new KeyValuePair<string, string>("timeslots", assignment.Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await Factory.InDatabaseScopeAsync(context => context.VolunteerTimeslots.AnyAsync()));
    }

    [Fact]
    public async Task Interviewer_cancellation_enforces_ownership_and_removes_orphans()
    {
        var signupId = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "owner");
            await TestData.AddUserAsync(context, "other");
            var (@event, slots) = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var signup = new InterviewerSignup
            {
                InterviewerId = "owner",
                FirstName = "Test",
                LastName = "Owner",
                InPerson = true
            };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.AddRange(
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id },
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[1].Id });
            context.InterviewerLocations.Add(new InterviewerLocation
            {
                InterviewerId = "owner",
                EventId = @event.Id,
                Preference = MockInterviews.Data.Constants.InterviewLocationConstants.InPerson
            });
            await context.SaveChangesAsync();
            return signup.Id;
        });
        using var other = Factory.CreateAuthenticatedClient("other", RolesConstants.InterviewerRole);
        using var owner = Factory.CreateAuthenticatedClient("owner", RolesConstants.InterviewerRole);

        var forbidden = await other.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/Create", "/SignupInterviewerTimeslots/UserDeleteRangeConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", signupId.ToString())
        });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.True(await Factory.InDatabaseScopeAsync(context => context.InterviewerTimeslots.AnyAsync()));

        var response = await owner.PostFormWithAntiforgeryAsync($"/SignupInterviewerTimeslots/UserDeleteRange/{signupId}", "/SignupInterviewerTimeslots/UserDeleteRangeConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", signupId.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Empty(await context.InterviewerTimeslots.ToListAsync());
            Assert.Empty(await context.InterviewerLocations.ToListAsync());
            Assert.Empty(await context.InterviewerSignups.ToListAsync());
            Assert.Equal(2, await context.Users.CountAsync());
        });
    }

    [Fact]
    public async Task Interviewer_single_and_admin_range_cancellations_are_post_only_and_validate_the_range()
    {
        var availability = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "interviewer");
            var firstDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var secondDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var signup = new InterviewerSignup { InterviewerId = "interviewer", FirstName = "Test", LastName = "Interviewer" };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            var blocks = new[]
            {
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = firstDay.Timeslots[0].Id },
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = firstDay.Timeslots[1].Id },
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = secondDay.Timeslots[0].Id }
            };
            context.InterviewerTimeslots.AddRange(blocks);
            await context.SaveChangesAsync();
            return blocks;
        });
        using var interviewer = Factory.CreateAuthenticatedClient("interviewer", RolesConstants.InterviewerRole);
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var singleGet = await interviewer.GetAsync($"/SignupInterviewerTimeslots/UserDeleteConfirmed?id={availability[0].Id}");
        var rangeGet = await admin.GetAsync($"/SignupInterviewerTimeslots/DeleteRangeConfirmed?timeslots={availability[0].Id}&timeslots={availability[2].Id}");
        var confirmation = await admin.GetAsync($"/SignupInterviewerTimeslots/DeleteRange?timeslots={availability[0].Id}&timeslots={availability[1].Id}");
        var invalidRangePost = await admin.PostFormWithAntiforgeryAsync(
            "/SignupInterviewerTimeslots/CreateForInterviewer",
            "/SignupInterviewerTimeslots/DeleteRangeConfirmed",
            new[]
            {
                new KeyValuePair<string, string>("timeslots", availability[0].Id.ToString()),
                new KeyValuePair<string, string>("timeslots", availability[2].Id.ToString())
            });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, singleGet.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, rangeGet.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Contains("DeleteRangeConfirmed", await confirmation.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, invalidRangePost.StatusCode);
        Assert.Equal(3, await Factory.InDatabaseScopeAsync(context => context.InterviewerTimeslots.CountAsync()));

        var validRangePost = await admin.PostFormWithAntiforgeryAsync(
            $"/SignupInterviewerTimeslots/DeleteRange?timeslots={availability[0].Id}&timeslots={availability[1].Id}",
            "/SignupInterviewerTimeslots/DeleteRangeConfirmed",
            new[]
            {
                new KeyValuePair<string, string>("timeslots", availability[0].Id.ToString()),
                new KeyValuePair<string, string>("timeslots", availability[1].Id.ToString())
            });

        Assert.Equal(HttpStatusCode.Redirect, validRangePost.StatusCode);
        Assert.Equal(1, await Factory.InDatabaseScopeAsync(context => context.InterviewerTimeslots.CountAsync()));
    }
}
