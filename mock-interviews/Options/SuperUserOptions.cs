using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Options;

public sealed class SuperUserOptions
{
    public const string SectionName = "SuperUser";

    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;
}
