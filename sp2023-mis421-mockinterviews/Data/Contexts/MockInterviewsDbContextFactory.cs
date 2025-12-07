using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace sp2023_mis421_mockinterviews.Data.Contexts
{
    public class MockInterviewsDbContextFactory : IDesignTimeDbContextFactory<MockInterviewsDbContext>
    {
        public MockInterviewsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MockInterviewsDbContext>();
            
            // This connection string is only used at design time for migrations
            // The actual runtime connection string comes from configuration
            optionsBuilder.UseNpgsql("Host=localhost;Database=design_time_mockinterviews;Username=postgres;Password=postgres");
            
            return new MockInterviewsDbContext(optionsBuilder.Options);
        }
    }
}

