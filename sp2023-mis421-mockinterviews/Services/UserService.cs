using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.Identity;

namespace sp2023_mis421_mockinterviews.Services
{
    public class UserService
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<ApplicationUser> GetByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException($"User with id {userId} not found.");
        }

        public async Task<Dictionary<string, ApplicationUser>> GetUsersByIds(IEnumerable<string> userIds)
        {
            return await _context.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x);
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsersByRole(string roleName)
        {
            return await _userManager.GetUsersInRoleAsync(roleName);
        }
    }
}
