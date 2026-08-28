using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class FeedbackIntegritySpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Only_the_owner_can_update_feedback_on_a_completed_interview()
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
                Status = StatusConstants.Completed,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var owner = Factory.CreateAuthenticatedClient("owner", RolesConstants.StudentRole);
        using var other = Factory.CreateAuthenticatedClient("other", RolesConstants.StudentRole);

        var unauthorized = await other.GetAsync($"/InterviewEvents/ProvideFeedback/{interview.Id}");
        var unauthorizedPost = await other.PostFormWithAntiforgeryAsync("/InterviewEvents/Create", $"/InterviewEvents/ProvideFeedback/{interview.Id}", new[]
        {
            new KeyValuePair<string, string>("id", interview.Id.ToString()),
            new KeyValuePair<string, string>("Id", interview.Id.ToString()),
            new KeyValuePair<string, string>("InterviewerRating", "1")
        });
        var update = await owner.PostFormWithAntiforgeryAsync($"/InterviewEvents/ProvideFeedback/{interview.Id}", new[]
        {
            new KeyValuePair<string, string>("id", interview.Id.ToString()),
            new KeyValuePair<string, string>("Id", interview.Id.ToString()),
            new KeyValuePair<string, string>("InterviewerRating", "5"),
            new KeyValuePair<string, string>("InterviewerFeedback", "Specific and useful"),
            new KeyValuePair<string, string>("ProcessFeedback", "Everything was clear"),
            new KeyValuePair<string, string>("StudentId", "other"),
            new KeyValuePair<string, string>("Status", StatusConstants.NoShow),
            new KeyValuePair<string, string>("TimeslotId", "999999")
        });

        Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedPost.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);

        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Equal("owner", stored.StudentId);
        Assert.Equal(interview.TimeslotId, stored.TimeslotId);
        Assert.Equal(StatusConstants.Completed, stored.Status);
        Assert.Equal("5", stored.InterviewerRating);
        Assert.Equal("Specific and useful", stored.InterviewerFeedback);
    }

    [Fact]
    public async Task Feedback_cannot_be_submitted_before_completion()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Ongoing,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.GetAsync($"/InterviewEvents/ProvideFeedback/{interview.Id}");
        var post = await client.PostFormWithAntiforgeryAsync("/InterviewEvents/Create", $"/InterviewEvents/ProvideFeedback/{interview.Id}", new[]
        {
            new KeyValuePair<string, string>("id", interview.Id.ToString()),
            new KeyValuePair<string, string>("Id", interview.Id.ToString()),
            new KeyValuePair<string, string>("InterviewerRating", "5")
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
