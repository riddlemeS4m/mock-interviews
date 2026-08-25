using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Models.MockInterviewDb;

namespace sp2023_mis421_mockinterviews.Data.Contexts
{
    public class MockInterviewsDbContext : DbContext
    {
        public MockInterviewsDbContext(DbContextOptions<MockInterviewsDbContext> options)
            : base(options)
        {
        }

        public DbSet<Question> Questions { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<InterviewerLocation> InterviewerLocations { get; set; }
        public DbSet<InterviewerSignup> InterviewerSignups { get; set; }
        public DbSet<InterviewerTimeslot> InterviewerTimeslots { get; set; }
        public DbSet<Timeslot> Timeslots { get; set; }
        public DbSet<VolunteerTimeslot> VolunteerTimeslots { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<RosteredStudent> RosteredStudents { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<EmailTemplate> EmailTemplates { get; set; }
    }
}
