using System.ComponentModel.DataAnnotations;

namespace sp2023_mis421_mockinterviews.Options;

public sealed class SendGridOptions
{
    [Required]
    public string ApiKey { get; init; } = default!;
}