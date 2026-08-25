using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Constants;
using sp2023_mis421_mockinterviews.Models.UserDb;
using sp2023_mis421_mockinterviews.Models.ViewModels;

namespace sp2023_mis421_mockinterviews.Controllers
{
    public class CreateUserModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View();
        }

        //[HttpGet]
        [Authorize(Roles = RolesConstants.InterviewerRole)]
        public async Task<IActionResult> ExternalUserProfileView(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            var viewModel = new ExternalUserProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Class = ClassConstants.GetClassText((Classes)user.Class)
            };

            return View(viewModel);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (userId == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return View("DeleteUser",user);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> DeleteUserConfirmed(string Id)
        {
            var user = await _userManager.FindByIdAsync(Id);
            if (user == null)
            {
                return Problem("User not found.");
            }
            else
            {
                await _userManager.DeleteAsync(user);
            }

            return RedirectToAction("Index", "UserRoles");
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public IActionResult CreateProvisionaryUser()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> CreateProvisionaryUser(CreateUserModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser 
                { 
                    FirstName = model.FirstName, 
                    LastName = model.LastName, 
                    Email = model.Email, 
                    UserName = model.Email 
                };
                var result = await _userManager.CreateAsync(user, $"{model.FirstName}Spring2024!");

                if (result.Succeeded)
                {
                    var newUser = await _userManager.FindByEmailAsync(model.Email) ?? throw new Exception($"User with email {model.Email} was not successfully created.");
                    var roleResult = await _userManager.AddToRoleAsync(newUser, RolesConstants.InterviewerRole);

                    if(roleResult.Succeeded)
                    {
                        return RedirectToAction("Index", "UserRoles");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // If model state is not valid or user creation fails, return to the creation page with errors
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> ResetUserPassword(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            var model = new ResetPasswordViewModel { UserId = userId };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ViewBag.ErrorMessage = "User not found.";
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "UserRoles");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }
    }
}
