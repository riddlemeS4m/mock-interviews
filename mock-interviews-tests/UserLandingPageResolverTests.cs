using MockInterviews.Services;

namespace MockInterviews.UnitTests;

public sealed class UserLandingPageResolverTests
{
    private readonly UserLandingPageResolver _resolver = new();

    [Theory]
    [InlineData("", "AccessPending", "Home", null)]
    [InlineData(RolesConstants.StudentRole, "Student", "Home", null)]
    [InlineData(RolesConstants.InterviewerRole, "Interviewer", "Home", null)]
    [InlineData("student,interviewer", "Participant", "Home", null)]
    [InlineData(RolesConstants.AdminRole, "Admin", "Home", null)]
    [InlineData(RolesConstants.SystemAdminRole, "Admin", "Home", null)]
    public void Resolve_selects_the_expected_landing_page(
        string roles,
        string action,
        string controller,
        string? area)
    {
        var result = _resolver.Resolve(CreatePrincipal(roles));

        Assert.Equal(new UserLandingPage(action, controller, area), result);
    }

    [Fact]
    public void Resolve_gives_privileged_roles_precedence_over_participant_roles()
    {
        var result = _resolver.Resolve(CreatePrincipal("student,interviewer,admin,systemadmin"));

        Assert.Equal(new UserLandingPage("Admin", "Home"), result);
    }

    private static ClaimsPrincipal CreatePrincipal(string roles)
    {
        var claims = roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(role => new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
