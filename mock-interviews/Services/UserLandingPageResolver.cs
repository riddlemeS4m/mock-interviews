using System.Security.Claims;
using MockInterviews.Data.Constants;

namespace MockInterviews.Services;

public sealed record UserLandingPage(string Action, string Controller, string? Area = null);

public sealed class UserLandingPageResolver
{
    public UserLandingPage Resolve(ClaimsPrincipal user)
    {
        if (user.IsInRole(RolesConstants.SystemAdminRole))
        {
            return new UserLandingPage("Admin", "Home");
        }

        if (user.IsInRole(RolesConstants.AdminRole))
        {
            return new UserLandingPage("Admin", "Home");
        }

        var isStudent = user.IsInRole(RolesConstants.StudentRole);
        var isInterviewer = user.IsInRole(RolesConstants.InterviewerRole);

        if (isStudent && isInterviewer)
        {
            return new UserLandingPage("Participant", "Home");
        }

        if (isStudent)
        {
            return new UserLandingPage("Student", "Home");
        }

        if (isInterviewer)
        {
            return new UserLandingPage("Interviewer", "Home");
        }

        return new UserLandingPage("AccessPending", "Home");
    }
}
