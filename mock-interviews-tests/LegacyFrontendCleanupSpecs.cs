using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class LegacyFrontendCleanupSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Root_view_start_defaults_to_the_tailwind_shell()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.GetAsync("/Home/AttemptLogout");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/css/tailwind.css", html);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Home/Privacy")]
    [InlineData("/Home/Error")]
    public async Task Public_fallback_pages_use_the_tailwind_shell(string path)
    {
        using var client = Factory.CreateAnonymousClient();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/css/tailwind.css", html);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Student_profile_uses_the_tailwind_shell_without_inline_styles()
    {
        var studentId = await Factory.InDatabaseScopeAsync(async context =>
        {
            var student = await TestData.AddUserAsync(context, "profile-student");
            return student.Id;
        });
        using var client = Factory.CreateAuthenticatedClient("interviewer-1", RolesConstants.InterviewerRole);

        var response = await client.GetAsync($"/Users/ExternalUserProfileView?userId={studentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Test profile-student", html);
        Assert.Contains("/css/tailwind.css", html);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/EmailTemplates")]
    [InlineData("/RoleManager")]
    [InlineData("/UserRoles/MassAssign")]
    [InlineData("/UserRoles/MassAssignAdmin")]
    [InlineData("/SignupInterviewers/Create")]
    public async Task Retired_legacy_surfaces_are_not_routable(string path)
    {
        using var client = Factory.CreateAuthenticatedClient(
            "system-admin-1",
            RolesConstants.AdminRole,
            RolesConstants.SystemAdminRole,
            RolesConstants.InterviewerRole);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/css/style.css")]
    [InlineData("/js/site.js")]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/plugins/slick/slick.css")]
    public async Task Legacy_frontend_assets_are_not_served(string path)
    {
        using var client = Factory.CreateAnonymousClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
