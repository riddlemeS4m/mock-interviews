using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Models.ViewModels.MSTeamsStudentUploadsController
{
    public class MSTeamsStudentUploadViewModel
    {
        [Display(Name = "CSV roster file")]
        public IFormFile? RosterData { get; set; }
    }
}
