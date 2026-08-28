using Microsoft.AspNetCore.Identity;
using MockInterviews.Data.Constants;
using MockInterviews.Models.Identity;

namespace MockInterviews.Data.Seeds
{
    public static class IdentitySeed
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            //Seed Roles
            foreach (var roleItem in RolesConstants.GetRoleOptions())
            {
                var role = roleItem.Value;
                await SeedRoleAsync(roleManager, role);
            }
        }

        private static async Task SeedRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            // Check if the role already exists
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                // If the role doesn't exist, create it
                EnsureSucceeded(
                    await roleManager.CreateAsync(new IdentityRole(roleName)),
                    $"creating the '{roleName}' role");
            }
        }

        public static async Task SeedSuperAdminAsync(
            UserManager<ApplicationUser> userManager,
            string adminEmail,
            string adminPwd)
        {
            var user = await userManager.FindByEmailAsync(adminEmail);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = SuperUser.FirstName,
                    LastName = SuperUser.LastName,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                };

                EnsureSucceeded(
                    await userManager.CreateAsync(user, adminPwd),
                    $"creating seeded administrator '{adminEmail}'");
            }

            foreach (var roleItem in RolesConstants.GetRoleOptions())
            {
                var role = roleItem.Value;
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    EnsureSucceeded(
                        await userManager.AddToRoleAsync(user, role),
                        $"adding the seeded administrator '{adminEmail}' to the '{role}' role");
                }
            }
        }

        private static void EnsureSucceeded(IdentityResult result, string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            var errors = string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException($"Identity seeding failed while {operation}: {errors}");
        }
    }
}
