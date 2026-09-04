using Microsoft.Extensions.Logging.Abstractions;
using MockInterviews.Email;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MockInterviews.IntegrationTests;

public sealed class EmailTransportSpecs
{
    [Fact]
    public async Task SendGrid_transport_wraps_client_failures_as_delivery_failures()
    {
        var client = new Mock<ISendGridClient>();
        client.Setup(item => item.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid is unavailable."));
        var transport = new SendGridEmailTransport(client.Object, NullLogger<SendGridEmailTransport>.Instance);
        var message = new EmailMessage(
            new MockInterviews.Email.EmailAddress("sender@example.test", "Sender"),
            new MockInterviews.Email.EmailAddress("recipient@example.test"),
            "Subject",
            "Plain text",
            "<p>HTML</p>");

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() => transport.SendAsync(message));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }
}
