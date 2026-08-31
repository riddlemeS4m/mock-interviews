namespace MockInterviews.Email;

public sealed class EmailDeliveryException(string message) : Exception(message);
