using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class InterviewerCheckInSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Check_in_is_post_only_and_antiforgery_protected()
    {
        var signupId = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "interviewer-1");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var signup = new InterviewerSignup { InterviewerId = "interviewer-1", FirstName = "Test", LastName = "Interviewer", InPerson = true };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = schedule.Timeslots[0].Id });
            await context.SaveChangesAsync();
            return signup.Id;
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var get = await client.GetAsync($"/SignupInterviewers/CheckInInterviewer?id={signupId}");
        var unprotectedPost = await client.PostAsync("/SignupInterviewers/CheckInInterviewer", new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("id", signupId.ToString()) }));
        var protectedPost = await client.PostFormWithAntiforgeryAsync("/SignupInterviewers", "/SignupInterviewers/CheckInInterviewer", new[] { new KeyValuePair<string, string>("id", signupId.ToString()) });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unprotectedPost.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, protectedPost.StatusCode);
        var checkedIn = await Factory.InDatabaseScopeAsync(context => context.InterviewerSignups.Where(signup => signup.Id == signupId).Select(signup => signup.CheckedIn).SingleAsync());
        Assert.True(checkedIn);
    }

    [Fact]
    public async Task Retired_interviewer_check_in_endpoint_is_not_exposed()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var get = await client.GetAsync("/InterviewEvents/InterviewerSelfCheckIn");
        var post = await client.PostAsync("/InterviewEvents/InterviewerSelfCheckIn", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
