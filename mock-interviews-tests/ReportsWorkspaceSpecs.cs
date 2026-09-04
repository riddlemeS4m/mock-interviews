using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class ReportsWorkspaceSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Reports_are_available_to_system_admins_and_forbidden_to_students()
    {
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var forbidden = await student.GetAsync("/Reports/EventStatistics");
        var eventStatistics = await systemAdmin.GetAsync("/Reports/EventStatistics");
        var signupReport = await systemAdmin.GetAsync("/Reports/SignupReport");
        var allocationReport = await systemAdmin.GetAsync("/Reports/AllocationReport");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, eventStatistics.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signupReport.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allocationReport.StatusCode);
        Assert.Contains("No events yet", await eventStatistics.Content.ReadAsStringAsync());
        Assert.Contains("No active timeslots", await signupReport.Content.ReadAsStringAsync());
        Assert.Contains("No allocation data", await allocationReport.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reports_render_participant_counts_for_active_timeslots()
    {
        var schedule = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.n, name: "Reports event");
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Test",
                LastName = "Interviewer",
                InPerson = true
            };
            context.InterviewerSignups.Add(signup);
            context.Interviews.Add(new Interview
            {
                StudentId = "student-1",
                TimeslotId = schedule.Timeslots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            });
            context.VolunteerTimeslots.Add(new VolunteerTimeslot
            {
                StudentId = "student-1",
                TimeslotId = schedule.Timeslots[0].Id
            });
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = schedule.Timeslots[0].Id
            });
            await context.SaveChangesAsync();
            return schedule;
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var eventStatistics = await client.GetAsync("/Reports/EventStatistics");
        var signupReport = await client.GetAsync("/Reports/SignupReport");
        var allocationReport = await client.GetAsync("/Reports/AllocationReport");

        Assert.Equal(HttpStatusCode.OK, eventStatistics.StatusCode);
        Assert.Contains(schedule.Event.Name, await eventStatistics.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, signupReport.StatusCode);
        Assert.Contains("Signup report", await signupReport.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, allocationReport.StatusCode);
        Assert.Contains("Allocation report", await allocationReport.Content.ReadAsStringAsync());
    }
}
