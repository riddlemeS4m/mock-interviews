using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Options;

public sealed class SendGridOptions
{
    [Required]
    public string ApiKey { get; init; } = default!;
}
