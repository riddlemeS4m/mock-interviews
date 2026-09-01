using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.UsersController
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

    }
}
