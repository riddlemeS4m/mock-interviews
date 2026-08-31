using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Options;

public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    [Required]
    public string ApiKey { get; init; } = default!;
}
