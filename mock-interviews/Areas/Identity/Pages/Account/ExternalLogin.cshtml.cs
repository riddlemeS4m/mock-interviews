// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Identity;
using MockInterviews.Services;

namespace MockInterviews.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly MockInterviewsDbContext _context;
        private readonly AccountRoleProvisioner _roleProvisioner;
        private readonly UserProfileCompletionService _profileCompletionService;
        public bool IsStudent { get; set; }

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILogger<ExternalLoginModel> logger,
            MockInterviewsDbContext context,
            AccountRoleProvisioner roleProvisioner,
            UserProfileCompletionService profileCompletionService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _context = context;
            _roleProvisioner = roleProvisioner;
            _profileCompletionService = profileCompletionService;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ProviderDisplayName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }
            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }
            [Display(Name = "Company")]
            public string Company { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        //initial "sign in with microsoft" button click goes here
        public IActionResult OnPost(string provider, string returnUrl = null)
        {

            //returnUrl = "https://mockinterviews.uamishub.com/signin-microsoft";
            //System.Console.WriteLine("Test");
            //System.Console.WriteLine(returnUrl);
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            //System.Console.WriteLine(redirectUrl);
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        //after microsoft login, this is called
        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            //Console.WriteLine("Test");
            //Console.WriteLine(returnUrl);
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser is not null)
                {
                    await _roleProvisioner.ProvisionStudentRoleAsync(existingUser);
                    await _signInManager.RefreshSignInAsync(existingUser);
                    if (await _profileCompletionService.IsRequiredAsync(existingUser))
                    {
                        return RedirectToPage("/Account/Manage/ProfileEdit", new { ReturnUrl = returnUrl });
                    }
                }

                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            if (result.IsNotAllowed)
            {
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser is not null && !await _userManager.IsEmailConfirmedAsync(existingUser))
                {
                    existingUser.EmailConfirmed = true;
                    var updateResult = await _userManager.UpdateAsync(existingUser);
                    if (updateResult.Succeeded)
                    {
                        await _roleProvisioner.ProvisionStudentRoleAsync(existingUser);
                        await _signInManager.SignInAsync(existingUser, isPersistent: false, info.LoginProvider);
                        if (await _profileCompletionService.IsRequiredAsync(existingUser))
                        {
                            return RedirectToPage("/Account/Manage/ProfileEdit", new { ReturnUrl = returnUrl });
                        }

                        return LocalRedirect(returnUrl);
                    }
                }

                ErrorMessage = "This external sign-in could not be confirmed.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // If the user does not have an account, then ask the user to create an account.
            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;
            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                IsStudent = await _context.RosteredStudents.AnyAsync(record => record.Email == email);
                if (IsStudent)
                {
                    Input = new InputModel
                    {
                        Email = email,
                        FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName),
                        LastName = info.Principal.FindFirstValue(ClaimTypes.Surname),
                        Company = "none"
                    };
                }
                else
                {
                    Input = new InputModel
                    {
                        Email = email,
                        FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName),
                        LastName = info.Principal.FindFirstValue(ClaimTypes.Surname),
                        Company = ""
                    };
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var providerEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(providerEmail))
            {
                ModelState.AddModelError(string.Empty, "The external provider did not supply an email address.");
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                return Page();
            }

            Input.Email = providerEmail;

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.EmailConfirmed = true;

                var textInfo = new CultureInfo("en-US", false).TextInfo;
                user.FirstName = textInfo.ToTitleCase(Input.FirstName);
                user.LastName = textInfo.ToTitleCase(Input.LastName);
                if (Input.Company != "none" && Input.Company != "" && Input.Company != null)
                {
                    user.Company = textInfo.ToTitleCase(Input.Company);
                }
                else
                {
                    user.Company = null;
                }

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var normalizedEmail = Input.Email.Trim().ToUpper();
                var exists = await _context.RosteredStudents
                    .FirstOrDefaultAsync(record => record.Email.ToUpper() == normalizedEmail);

                if (exists != null)
                {
                    if (exists.In221)
                    {
                        user.Class = Classes.FirstSem;
                    }
                }

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        await _roleProvisioner.ProvisionStudentRoleAsync(user);

                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        if (await _profileCompletionService.IsRequiredAsync(user))
                        {
                            return RedirectToPage("/Account/Manage/ProfileEdit", new { ReturnUrl = returnUrl });
                        }

                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
