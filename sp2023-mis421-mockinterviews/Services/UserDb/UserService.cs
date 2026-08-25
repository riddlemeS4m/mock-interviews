using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.UserDb;

namespace sp2023_mis421_mockinterviews.Services.UserDb
{
    public class UserService : GenericUserDbService<ApplicationUser>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public UserService(MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager) : base(context)
        {
            _userManager = userManager;
        }

        public async Task<ApplicationUser> GetUserById(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<ApplicationUser> GetUserByEmail(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<string> GetUserFullNameById(string userId)
        {
            return await _dbSet.Where(x => x.Id == userId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync() ?? throw new Exception($"User with id {userId} not found.");
        }

        public async Task<Dictionary<string, string>> GetUsersFullNamesByIds(IEnumerable<string> userIds)
        {
            var names = await _dbSet.Where(x => userIds.Contains(x.Id))
                .Select(x => new {x.Id, FullName = $"{x.FirstName} {x.LastName}"})
                .ToDictionaryAsync(x => x.Id, x => x.FullName);

            if(userIds.Count() != names.Count)
            {
                throw new Exception("Not all user IDs have a corresponding user.");
            }

            return names;
        }

        public async Task<Dictionary<string, ApplicationUser>> GetUsersByIds(IEnumerable<string> userIds)
        {
            return await _dbSet.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x);
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsersByRole(string roleName)
        {
            return await _userManager.GetUsersInRoleAsync(roleName);
        }
    }
}
