using System.ComponentModel.DataAnnotations;

namespace MockInterviews.Options;

public sealed class SmtpEmailOptions : IValidatableObject
{
    public const string SectionName = "Email:Smtp";

    [Required]
    public string Host { get; init; } = default!;

    [Range(1, 65535)]
    public int Port { get; init; }

    public bool UseTls { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasPassword = !string.IsNullOrWhiteSpace(Password);
        if (hasUsername != hasPassword)
        {
            yield return new ValidationResult(
                "SMTP username and password must be configured together.",
                [nameof(Username), nameof(Password)]);
        }
    }
}
