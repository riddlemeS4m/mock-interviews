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
            foreach(var roleItem in RolesConstants.GetRoleOptions())
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
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        public static async Task SeedSuperAdminAsync(
            UserManager<ApplicationUser> userManager,
            string adminEmail,
            string adminPwd)
        {
            var defaultUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = SuperUser.FirstName,
                LastName = SuperUser.LastName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            if (userManager.Users.All(u => u.Email != defaultUser.Email))
            {
                var user = await userManager.FindByEmailAsync(defaultUser.Email);
                if (user == null)
                {
                    await userManager.CreateAsync(defaultUser, adminPwd);

                    foreach(var roleItem in RolesConstants.GetRoleOptions())
                    {
                        var role = roleItem.Value;
                        await userManager.AddToRoleAsync(defaultUser, role);
                    }
                }
            }
        }
    }
}
