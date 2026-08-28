using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;

namespace MockInterviews.Data.Contexts
{
    public class MockInterviewsDbContext : IdentityDbContext<ApplicationUser>
    {
        public MockInterviewsDbContext(DbContextOptions<MockInterviewsDbContext> options)
            : base(options)
        {
        }

        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<Location> Locations { get; set; } = null!;
        public DbSet<Interview> Interviews { get; set; } = null!;
        public DbSet<InterviewerLocation> InterviewerLocations { get; set; } = null!;
        public DbSet<InterviewerSignup> InterviewerSignups { get; set; } = null!;
        public DbSet<InterviewerTimeslot> InterviewerTimeslots { get; set; } = null!;
        public DbSet<Timeslot> Timeslots { get; set; } = null!;
        public DbSet<VolunteerTimeslot> VolunteerTimeslots { get; set; } = null!;
        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<RosteredStudent> RosteredStudents { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;

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
