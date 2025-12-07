using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace sp2023_mis421_mockinterviews.Data.Contexts
{
    public class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
    {
        public UsersDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
            
            // This connection string is only used at design time for migrations
            // The actual runtime connection string comes from configuration
            optionsBuilder.UseNpgsql("Host=localhost;Database=design_time_users;Username=postgres;Password=postgres");
            
            return new UsersDbContext(optionsBuilder.Options);
        }
    }
}

