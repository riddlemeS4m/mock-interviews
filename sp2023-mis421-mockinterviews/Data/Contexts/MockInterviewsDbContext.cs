using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Models.MockInterviewDb;
using sp2023_mis421_mockinterviews.Models.UserDb;

namespace sp2023_mis421_mockinterviews.Data.Contexts
{
    public class MockInterviewsDbContext : IdentityDbContext<ApplicationUser>
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Interview>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(interview => interview.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VolunteerTimeslot>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(timeslot => timeslot.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InterviewerSignup>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(signup => signup.InterviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InterviewerLocation>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(location => location.InterviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
