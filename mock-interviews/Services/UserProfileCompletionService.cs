using Microsoft.AspNetCore.Identity;
using MockInterviews.Data.Constants;
using MockInterviews.Models.Identity;

namespace MockInterviews.Services;

public sealed class UserProfileCompletionService(UserManager<ApplicationUser> userManager)
{
    public async Task<bool> IsRequiredAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
        {
            return true;
        }

        if (await userManager.IsInRoleAsync(user, RolesConstants.InterviewerRole) &&
            string.IsNullOrWhiteSpace(user.Company))
        {
            return true;
        }

        return await userManager.IsInRoleAsync(user, RolesConstants.StudentRole) &&
            user.Class == Classes.NotEnrolled;
    }
}
