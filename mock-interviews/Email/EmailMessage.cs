namespace MockInterviews.Email;

public sealed class EmailMessage
{
    public EmailMessage(
        EmailAddress from,
        EmailAddress to,
        string subject,
        string plainTextBody,
        string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("An email subject is required.", nameof(subject));
        if (plainTextBody is null) throw new ArgumentNullException(nameof(plainTextBody));
        if (htmlBody is null) throw new ArgumentNullException(nameof(htmlBody));

        Subject = subject;
        PlainTextBody = plainTextBody;
        HtmlBody = htmlBody;
        Attachments = (attachments ?? []).ToArray();
    }

    public EmailAddress From { get; }
    public EmailAddress To { get; }
    public string Subject { get; }
    public string PlainTextBody { get; }
    public string HtmlBody { get; }
    public IReadOnlyList<EmailAttachment> Attachments { get; }
}
