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
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Identity_validation_loads_jquery_before_the_validation_plugins()
    {
        using var client = Factory.CreateAnonymousClient();

        var response = await client.GetAsync("/Identity/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertValidationStackOrder(html);
    }

    [Fact]
    public async Task Mvc_validation_loads_jquery_before_the_validation_plugins()
    {
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.GetAsync("/Locations/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertValidationStackOrder(html);
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

    private static void AssertValidationStackOrder(string html)
    {
        var jqueryIndex = html.IndexOf("/lib/jquery/dist/jquery.min.js", StringComparison.Ordinal);
        var validationIndex = html.IndexOf("/lib/jquery-validation/dist/jquery.validate.min.js", StringComparison.Ordinal);
        var unobtrusiveIndex = html.IndexOf("/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js", StringComparison.Ordinal);

        Assert.True(jqueryIndex >= 0, "The validation page did not load jQuery.");
        Assert.True(validationIndex > jqueryIndex, "jQuery Validation must load after jQuery.");
        Assert.True(unobtrusiveIndex > validationIndex, "Unobtrusive Validation must load after jQuery Validation.");
    }
}
