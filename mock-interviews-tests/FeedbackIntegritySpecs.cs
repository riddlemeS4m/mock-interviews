using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class FeedbackIntegritySpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Feedback_index_renders_an_action_for_each_completed_interview()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Completed,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.GetAsync("/InterviewEvents/FeedbackIndex");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/InterviewEvents/ProvideFeedback/{interview.Id}", content);
        Assert.Contains("Give feedback", content);
        Assert.Contains("data-shell-navigation", content);
        Assert.DoesNotContain("bootstrap", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Feedback_validation_preserves_attempted_written_feedback()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Completed,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.PostFormWithAntiforgeryAsync($"/InterviewEvents/ProvideFeedback/{interview.Id}", new[]
        {
            new KeyValuePair<string, string>("Id", interview.Id.ToString()),
            new KeyValuePair<string, string>("InterviewerRating", "6"),
            new KeyValuePair<string, string>("InterviewerFeedback", "Very specific feedback"),
            new KeyValuePair<string, string>("ProcessFeedback", "The process was clear")
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose a rating from 1 to 5.", content);
        Assert.Contains("Very specific feedback", content);
        Assert.Contains("The process was clear", content);

        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Null(stored.InterviewerRating);
        Assert.Null(stored.InterviewerFeedback);
        Assert.Null(stored.ProcessFeedback);
    }

    [Fact]
    public async Task Feedback_edit_state_is_rendered_for_completed_interviews()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Completed,
                Type = "Behavioral",
                InterviewerRating = "4",
                InterviewerFeedback = "Helpful coaching",
                ProcessFeedback = "Smooth scheduling"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var indexResponse = await client.GetAsync("/InterviewEvents/FeedbackIndex");
        var indexContent = await indexResponse.Content.ReadAsStringAsync();
        var formResponse = await client.GetAsync($"/InterviewEvents/ProvideFeedback/{interview.Id}");
        var formContent = await formResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Edit feedback", indexContent);
        Assert.Equal(HttpStatusCode.OK, formResponse.StatusCode);
        Assert.Contains("value=\"4\"", formContent);
        Assert.Contains("checked=\"checked\"", formContent);
        Assert.Contains("Helpful coaching", formContent);
        Assert.Contains("Smooth scheduling", formContent);
    }

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

        var feedbackIndex = await owner.GetAsync("/InterviewEvents/FeedbackIndex");
        var feedbackIndexContent = await feedbackIndex.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, feedbackIndex.StatusCode);
        Assert.Contains("Your feedback was saved.", feedbackIndexContent);

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
        var post = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/Create", $"/InterviewEvents/ProvideFeedback/{interview.Id}", new[]
        {
            new KeyValuePair<string, string>("id", interview.Id.ToString()),
            new KeyValuePair<string, string>("Id", interview.Id.ToString()),
            new KeyValuePair<string, string>("InterviewerRating", "5")
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
