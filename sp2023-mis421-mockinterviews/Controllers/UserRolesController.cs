using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Constants;
using sp2023_mis421_mockinterviews.Models.Identity;
using sp2023_mis421_mockinterviews.Models.ViewModels;
using System.Data;

namespace sp2023_mis421_mockinterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class UserRolesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserRolesController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index()
        {
            var userRolesViewModel = await _userManager.Users
                .Select(user => new UserRolesViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                    //Roles = await GetUserRoles(user)
                })
                .ToListAsync();

            return View(userRolesViewModel);
        }
        private async Task<List<string>> GetUserRoles(ApplicationUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
        }

        public async Task<IActionResult> Manage(string userId)
        {
            ViewBag.userId = userId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"User with Id = {userId} cannot be found";
                return View("NotFound");
            }
            ViewBag.UserName = user.UserName;
            var model = new List<ManageUserRolesViewModel>();
            var roles = await _roleManager.Roles.ToListAsync();
            foreach (var role in roles)
            {
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
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Manage(List<ManageUserRolesViewModel> model, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View();
            }
            var roles = await _userManager.GetRolesAsync(user);
            var result = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Cannot remove user existing roles");
                return View(model);
            }
            result = await _userManager.AddToRolesAsync(user, model.Where(x => x.Selected).Select(y => y.RoleName));
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Cannot add selected roles to user");
                return View(model);
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> MassAssign()
        {

            var interviewerRole = await _roleManager.FindByNameAsync(RolesConstants.InterviewerRole);

            if (interviewerRole != null)
            {
                var usersInInterviewerRole = await _userManager.GetUsersInRoleAsync(interviewerRole.Name);

                var filteredVMS = await _userManager.Users
                    .Where(user => !usersInInterviewerRole.Contains(user)) // Filter out users in the Interviewer role
                    .OrderBy(user => user.FirstName)
                    .Select(user => new MassAssignRolesViewModel
                    {
                        Name = user.FirstName + " " + user.LastName,
                        Email = user.Email,
                        IsAlreadyInRole = false, // Assuming you want to set this to false for users not in the role
                        OriginalIsAlreadyInRole = false,
                        UpdatedIsAlreadyInRole = false
                    })
                    .ToListAsync();

                return View("MassAssign", filteredVMS);
            }
            else
            {
                // Handle the case where the "Interviewer" role doesn't exist
                return BadRequest("The 'Interviewer' role does not exist.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> MassAssignPost(string[] SelectedEventIds1)
        {
            foreach (string x in SelectedEventIds1)
            {
                await _userManager.AddToRoleAsync(await _userManager.FindByEmailAsync(x), RolesConstants.InterviewerRole);
            }

            return RedirectToAction("Index", "UserRoles");
        }

        public async Task<IActionResult> MassAssignAdmin()
        {
            var interviewerRole = await _roleManager.FindByNameAsync(RolesConstants.AdminRole);

            if (interviewerRole != null)
            {
                var usersInInterviewerRole = await _userManager.GetUsersInRoleAsync(interviewerRole.Name);

                var filteredVMS = await _userManager.Users
                    .Where(user => !usersInInterviewerRole.Contains(user)) // Filter out users in the Interviewer role
                    .OrderBy(user => user.FirstName)
                    .Select(user => new MassAssignRolesViewModel
                    {
                        Name = user.FirstName + " " + user.LastName,
                        Email = user.Email,
                        IsAlreadyInRole = false, // Assuming you want to set this to false for users not in the role
                        OriginalIsAlreadyInRole = false,
                        UpdatedIsAlreadyInRole = false
                    })
                    .ToListAsync();

                return View("MassAssignAdmin", filteredVMS);
            }
            else
            {
                // Handle the case where the "Interviewer" role doesn't exist
                return BadRequest("The 'Interviewer' role does not exist.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> MassAssignAdmin(string[] SelectedEventIds1)
        {
            foreach (string x in SelectedEventIds1)
            {
                await _userManager.AddToRoleAsync(await _userManager.FindByEmailAsync(x), RolesConstants.AdminRole);
            }

            return RedirectToAction("Index", "UserRoles");
        }
    }
}
