using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class IntegrationDatabaseSafetyTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Dedicated_database_on_loopback_is_allowed(string host)
    {
        var connectionString = $"Host={host};Database=mock_interviews_test_db;Username=postgres;Password=postgres";

        MockInterviewsWebApplicationFactory.ValidateIntegrationDatabase(connectionString);
    }

    [Theory]
    [InlineData("database.example.test")]
    [InlineData("localhost,database.example.test")]
    public void Remote_database_host_is_rejected(string host)
    {
        var connectionString = $"Host={host};Database=mock_interviews_test_db;Username=postgres;Password=postgres";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MockInterviewsWebApplicationFactory.ValidateIntegrationDatabase(connectionString));

        Assert.Contains("loopback PostgreSQL host", exception.Message);
    }
}
