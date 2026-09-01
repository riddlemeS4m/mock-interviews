using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.MSTeamsStudentUploadsController;

public sealed record RosterIndexViewModel(IReadOnlyList<RosteredStudent> Students);

public sealed class RosterStudentEditViewModel
{
    public int Id { get; set; }
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Name { get; set; } = string.Empty;
    [Display(Name = "MIS 221 student")]
    public bool In221 { get; set; }

    public static RosterStudentEditViewModel FromStudent(RosteredStudent student) => new()
    {
        Id = student.Id,
        Email = student.Email,
        Name = student.Name,
        In221 = student.In221
    };
}
