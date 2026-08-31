using System.Net.Mail;

namespace MockInterviews.Email;

public sealed record EmailAddress
{
    public EmailAddress(string address, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(address) || !MailAddress.TryCreate(address, out _))
        {
            throw new ArgumentException("A valid email address is required.", nameof(address));
        }

        Address = address;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }

    public string Address { get; }
    public string? DisplayName { get; }
}
