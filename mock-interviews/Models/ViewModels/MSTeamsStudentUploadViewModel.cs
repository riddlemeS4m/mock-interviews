using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels
{
    public class MSTeamsStudentUploadViewModel
    {
        [Display(Name = "RosteredStudent Data")]
        public IFormFile? RosterData { get; set; }
    }
}
