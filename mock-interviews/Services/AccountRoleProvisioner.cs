using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Identity;

namespace MockInterviews.Services;

public sealed class AccountRoleProvisioner(
    MockInterviewsDbContext context,
    UserManager<ApplicationUser> userManager)
{
    public async Task<bool> ProvisionStudentRoleAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var normalizedEmail = user.Email.Trim().ToUpper();
        var isRostered = await context.RosteredStudents
            .AnyAsync(record => record.Email.ToUpper() == normalizedEmail);

        if (!isRostered || await userManager.IsInRoleAsync(user, RolesConstants.StudentRole))
        {
            return isRostered;
        }

        var result = await userManager.AddToRoleAsync(user, RolesConstants.StudentRole);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Unable to provision the student role: {errors}");
        }

        return true;
    }
}
