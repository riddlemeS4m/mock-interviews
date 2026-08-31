namespace MockInterviews.Email;

public sealed record EmailAttachment
{
    public EmailAttachment(string fileName, string contentType, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("An attachment filename is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("An attachment content type is required.", nameof(contentType));
        ArgumentNullException.ThrowIfNull(content);

        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Content { get; }
}
