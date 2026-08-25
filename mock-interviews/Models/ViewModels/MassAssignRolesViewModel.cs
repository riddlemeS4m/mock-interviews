using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class MassAssignRolesViewModel
    {
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Is Already in Role")]
        public bool IsAlreadyInRole { get; set; }

        // New properties for tracking changes
        [Display(Name = "Original Is Already in Role")]
        public bool OriginalIsAlreadyInRole { get; set; }

        [Display(Name = "Updated Is Already in Role")]
        public bool UpdatedIsAlreadyInRole { get; set; }
    }
}
