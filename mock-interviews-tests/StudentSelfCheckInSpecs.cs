using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class StudentSelfCheckInSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Check_in_requires_an_explicit_post_and_is_idempotent()
    {
        var interviews = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "owner");
            await TestData.AddUserAsync(context, "other");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var first = new Interview
            {
                StudentId = "owner",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            };
            var second = new Interview
            {
                StudentId = "owner",
                TimeslotId = slots[1].Id,
                Status = StatusConstants.Default,
                Type = "Technical"
            };
            context.Interviews.AddRange(first, second);
            await context.SaveChangesAsync();
            return (first, second);
        });
        using var owner = Factory.CreateAuthenticatedClient("owner", RolesConstants.StudentRole);
        using var other = Factory.CreateAuthenticatedClient("other", RolesConstants.StudentRole);

        var get = await owner.GetAsync("/InterviewEvents/StudentSelfCheckIn");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(StatusConstants.Default, await Factory.InDatabaseScopeAsync(async context =>
            (await context.Interviews.SingleAsync(item => item.Id == interviews.first.Id)).Status));

        var checkIn = await owner.PostFormWithAntiforgeryAsync(
            "/SignupInterviewerTimeslots/Create",
            "/InterviewEvents/StudentSelfCheckInConfirmed",
            []);
        var repeatedCheckIn = await owner.PostFormWithAntiforgeryAsync(
            "/SignupInterviewerTimeslots/Create",
            "/InterviewEvents/StudentSelfCheckInConfirmed",
            []);
        var otherCheckIn = await other.PostFormWithAntiforgeryAsync(
            "/SignupInterviewerTimeslots/Create",
            "/InterviewEvents/StudentSelfCheckInConfirmed",
            []);

        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedCheckIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherCheckIn.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews
            .OrderBy(item => item.Id)
            .ToListAsync());
        Assert.Equal(StatusConstants.CheckedIn, stored.Single(item => item.Id == interviews.first.Id).Status);
        Assert.NotNull(stored.Single(item => item.Id == interviews.first.Id).CheckedInAt);
        Assert.Equal(StatusConstants.Default, stored.Single(item => item.Id == interviews.second.Id).Status);
    }
}
