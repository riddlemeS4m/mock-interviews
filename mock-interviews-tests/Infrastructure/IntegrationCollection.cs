namespace MockInterviews.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<MockInterviewsWebApplicationFactory>
{
    public const string Name = "PostgreSQL integration tests";
}
