using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.UserRolesController;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public class UserRolesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly MockInterviewsDbContext _context;

        public UserRolesController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            MockInterviewsDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderBy(user => user.Email).ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();
            foreach (var user in users)
            {
                userRolesViewModel.Add(new UserRolesViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Roles = await GetUserRoles(user)
                });
            }

            return View(userRolesViewModel);
        }
        private async Task<List<string>> GetUserRoles(ApplicationUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
        }

        public async Task<IActionResult> Manage(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            var model = new List<ManageUserRolesViewModel>();
            var allowedRoles = User.IsInRole(RolesConstants.SystemAdminRole)
                ? new[] { RolesConstants.AdminRole, RolesConstants.SystemAdminRole, RolesConstants.StudentRole, RolesConstants.InterviewerRole }
                : new[] { RolesConstants.StudentRole, RolesConstants.InterviewerRole };
            var roles = await _roleManager.Roles.Where(role => role.Name != null && allowedRoles.Contains(role.Name)).ToListAsync();
            foreach (var role in roles)
            {
                if (role.Name is null)
                {
                    continue;
                }

                var userRolesViewModel = new ManageUserRolesViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.Name
                };
                if (await _userManager.IsInRoleAsync(user, role.Name))
                {
                    userRolesViewModel.Selected = true;
                }
                else
                {
                    userRolesViewModel.Selected = false;
                }
                model.Add(userRolesViewModel);
            }
            ViewData["UserName"] = user.Email ?? user.UserName;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(List<ManageUserRolesViewModel> model, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View();
            }
            var isSystemAdmin = User.IsInRole(RolesConstants.SystemAdminRole);
            var allowedRoles = isSystemAdmin
                ? new[] { RolesConstants.AdminRole, RolesConstants.SystemAdminRole, RolesConstants.StudentRole, RolesConstants.InterviewerRole }
                : new[] { RolesConstants.StudentRole, RolesConstants.InterviewerRole };
            var selectedRoles = model.Where(item => item.Selected && allowedRoles.Contains(item.RoleName, StringComparer.OrdinalIgnoreCase))
                .Select(item => item.RoleName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var roles = await _userManager.GetRolesAsync(user);
            var manageableExistingRoles = roles.Where(role => allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
            if (string.Equals(user.Id, _userManager.GetUserId(User), StringComparison.Ordinal)
                && manageableExistingRoles.Any(role => role is RolesConstants.AdminRole or RolesConstants.SystemAdminRole)
                && !selectedRoles.Any(role => role is RolesConstants.AdminRole or RolesConstants.SystemAdminRole))
            {
                ModelState.AddModelError(string.Empty, "You cannot remove your own final privileged access.");
                return View(model);
            }
            if (isSystemAdmin && manageableExistingRoles.Contains(RolesConstants.SystemAdminRole)
                && !selectedRoles.Contains(RolesConstants.SystemAdminRole)
                && await _userManager.GetUsersInRoleAsync(RolesConstants.SystemAdminRole) is { Count: 1 })
            {
                ModelState.AddModelError(string.Empty, "At least one System Admin is required.");
                return View(model);
            }
            var rolesToRemove = manageableExistingRoles
                .Except(selectedRoles, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rolesToAdd = selectedRoles
                .Except(roles, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, string.Join(" ", result.Errors.Select(error => error.Description)));
                return View(model);
            }
            result = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, string.Join(" ", result.Errors.Select(error => error.Description)));
                return View(model);
            }
            await transaction.CommitAsync();
            return RedirectToAction("Index");
        }

    }
}
