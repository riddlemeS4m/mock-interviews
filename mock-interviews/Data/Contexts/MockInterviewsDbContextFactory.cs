using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MockInterviews.Extensions;

namespace MockInterviews.Data.Contexts
{
    public class MockInterviewsDbContextFactory : IDesignTimeDbContextFactory<MockInterviewsDbContext>
    {
        // `dotnet ef migrations bundle` creates the context without connecting to the database.
        // Docker builds intentionally omit .env, so this keeps bundle generation independent of
        // deployment credentials. Commands that connect to a database must provide a connection string.
        private const string BuildOnlyConnectionString =
            "Host=localhost;Database=design_time_mockinterviews;Username=postgres;Password=postgres";

        public MockInterviewsDbContext CreateDbContext(string[] args)
        {
            // EF commands do not build Program's host, so load the local .env explicitly.
            // NoClobber preserves a connection string supplied by CI or the shell.
            ApplicationConfigurationExtensions.LoadEnvironmentFile();

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            var connectionString = configuration["ConnectionString:DefaultConnection"]
                ?? BuildOnlyConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<MockInterviewsDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            
            return new MockInterviewsDbContext(optionsBuilder.Options);
        }
    }
}
