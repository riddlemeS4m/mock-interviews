using Microsoft.AspNetCore.Identity;
using sp2023_mis421_mockinterviews.Data.Constants;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        [Display(Name ="First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [DefaultValue(Classes.NotEnrolled)]
        public Classes Class { get; set; 
        }
        public string? Company { get; set; }
        [Display(Name = "Profile Picture")]

        public string? ProfilePicture { get; set; }
        
        public string? Resume { get; set; }

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }

        public string GetClass()
        {
            return ClassConstants.GetClassText(Class);
        }
    }
}
