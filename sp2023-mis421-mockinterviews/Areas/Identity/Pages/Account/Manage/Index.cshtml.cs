// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using sp2023_mis421_mockinterviews.Models.UserDb;
using sp2023_mis421_mockinterviews.Data.Constants;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Services.GoogleDrive;
using sp2023_mis421_mockinterviews.Data.Contexts;

namespace sp2023_mis421_mockinterviews.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MockInterviewsDbContext _context;
        private readonly GoogleDriveResumeService _driveResumeService;
        private readonly GoogleDrivePfpService _drivePfpService;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MockInterviewsDbContext context,
            GoogleDriveResumeService driveResumeService,
            GoogleDrivePfpService drivePfpService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _driveResumeService = driveResumeService;
            _drivePfpService = drivePfpService;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }
        public string Id { get; set; }


        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

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
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            ///
            [Display(Name = "First Name")]
            public string FirstName { get; set; }
            [Display(Name = "Last Name")]
            public string LastName { get; set; }
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
            [Display(Name = "Class")]
            public Classes Class { get; set; }
            [Display(Name = "Company")]
            public string Company { get; set; }
            // [Display(Name = "Profile Picture")]
            // public byte[] ProfilePicture { get; set; }
            // [Display(Name = "Resume")]
            // public byte[] Resume { get; set; }
            [Display(Name = "Profile Picture")]
            public string? ProfilePicture { get; set; }
            [Display(Name = "Resume")]
            public string? Resume { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            var firstName = user.FirstName;
            var lastName = user.LastName;
            var userClass = user.Class;
            var profilePicture = user.ProfilePicture;
            var resume = user.Resume;
            var company = user.Company;
            Username = userName;
            Id = user.Id;
            
            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                FirstName = firstName,
                LastName = lastName,
                Class = userClass,
                ProfilePicture = profilePicture,
                Resume = resume,
                Company = company
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
            //return RedirectToAction("ProfileView","Users");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var company = user.Company;

            if (Input.Company != company)
            {
                user.Company = Input.Company;
                await _userManager.UpdateAsync(user);
            }

            var firstName = user.FirstName;
            var lastName = user.LastName;

            if (Input.FirstName != firstName)
            {
                user.FirstName = Input.FirstName;
                await _userManager.UpdateAsync(user);
            }

            if (Input.LastName != lastName)
            {
                user.LastName = Input.LastName;
                await _userManager.UpdateAsync(user);
            }

            //lock users in 221 or before 221 out of changing their class. 
            //Only way these students can change their class is by the admin uploading an updated 221 roster
            var userClass = user.Class;

            if(Input.Class != userClass && (userClass == Classes.FirstSem || userClass == null)) 
            {
                var shouldBeIn221 = await _context.RosteredStudents.FirstOrDefaultAsync(x => x.Email == user.Email);
                if(shouldBeIn221 == null)
                {
                    user.Class = Classes.NotYetMIS;
                    await _userManager.UpdateAsync(user);
                }
                else if(shouldBeIn221.In221)
                {
                    user.Class = Classes.FirstSem;
                    await _userManager.UpdateAsync(user);
                }
                else
                {
                    user.Class = Input.Class;
                    await _userManager.UpdateAsync(user);
                }
            }

            if (Request.Form.Files.Count > 0)
            {
                // profile picture
                var profilePictureFile = Request.Form.Files["profilePicture"];
                if (profilePictureFile != null)
                {
                    if(user.ProfilePicture != null)
                    {
                        try
                        {
                            await _drivePfpService.DeleteFile(user.ProfilePicture);
                        }
                        catch
                        {
                            user.ProfilePicture = null;
                        }
                    }

                    var fileId = await _drivePfpService.UploadFile(profilePictureFile);
                    user.ProfilePicture = fileId;

                    await _userManager.UpdateAsync(user);
                }

                // resume
                if (User.IsInRole(RolesConstants.StudentRole))
                {
                    var resumeFile = Request.Form.Files["resume"];
                    if (resumeFile != null)
                    {
                        if (user.Resume != null)
                        {
                            try
                            {
                                await _driveResumeService.DeleteFile(user.Resume);
                            }
                            catch
                            {
                                user.Resume = null;
                            }
                        }

                        var fileId = await _driveResumeService.UploadFile(resumeFile);
                        user.Resume = fileId;

                        await _userManager.UpdateAsync(user);
                    }
                }
            }
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}
