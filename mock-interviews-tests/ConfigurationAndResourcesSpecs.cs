using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class ConfigurationAndResourcesSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Configuration_is_available_to_system_admins_and_denied_to_students()
    {
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var systemAdminResponse = await systemAdmin.GetAsync("/GlobalConfigVars");
        var studentResponse = await student.GetAsync("/GlobalConfigVars");

        Assert.Equal(HttpStatusCode.OK, systemAdminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentResponse.StatusCode);
        var html = await systemAdminResponse.Content.ReadAsStringAsync();
        Assert.Contains("tailwind.css", html);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configuration_edit_validates_urls_and_legacy_get_mutations_do_not_write()
    {
        var setting = await Factory.InDatabaseScopeAsync(async context => await context.Settings
            .SingleAsync(item => item.Name == SettingsConstants.ZoomLink.Name));
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var legacyGet = await client.GetAsync($"/GlobalConfigVars/SetZoomLink?link=https%3A%2F%2Fevil.example");
        var invalidEdit = await client.PostFormWithAntiforgeryAsync($"/GlobalConfigVars/Edit/{setting.Id}", new[]
        {
            new KeyValuePair<string, string>("Id", setting.Id.ToString()),
            new KeyValuePair<string, string>("Name", setting.Name),
            new KeyValuePair<string, string>("Value", "javascript:alert(1)")
        });
        var unchanged = await Factory.InDatabaseScopeAsync(async context => await context.Settings
            .SingleAsync(item => item.Id == setting.Id));

        Assert.Equal(HttpStatusCode.NotFound, legacyGet.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invalidEdit.StatusCode);
        Assert.Equal(SettingsConstants.ZoomLink.DefaultValue, unchanged.Value);
    }

    [Fact]
    public async Task Participant_question_submission_is_saved_not_answered_and_returns_to_resources()
    {
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var response = await client.PostFormWithAntiforgeryAsync("/FAQs/Create", new[]
        {
            new KeyValuePair<string, string>("Q", "Where should I park?"),
            new KeyValuePair<string, string>("A", "A participant cannot publish an answer.")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        Assert.Equal("/FAQs/Resources", response.Headers.Location?.OriginalString);
        var question = await Factory.InDatabaseScopeAsync(async context => await context.Questions.SingleAsync());
        Assert.Equal("Where should I park?", question.Q);
        Assert.Null(question.A);
        Assert.Single(Factory.SentEmails);
    }

    [Fact]
    public async Task Resources_is_public_and_only_displays_answered_questions()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            context.Questions.AddRange(
                new Question { Q = "Answered question", A = "Published answer" },
                new Question { Q = "Unanswered question" });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAnonymousClient();

        var response = await client.GetAsync("/FAQs/Resources");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Published answer", html);
        Assert.DoesNotContain("Unanswered question", html);
        Assert.Contains("tailwind.css", html);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }
}
