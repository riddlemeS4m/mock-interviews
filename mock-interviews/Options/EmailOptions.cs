using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const string SendGridProvider = "SendGrid";
    public const string SmtpProvider = "Smtp";

    [Required]
    public string Provider { get; init; } = default!;
}
