using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class StaffReportsSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Staff_reports_render_empty_states_without_dividing_by_zero()
    {
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var attendance = await client.GetAsync("/InterviewEvents/AttendanceReport");
        var completed = await client.GetAsync("/InterviewEvents/GetCompletedInterviews");
        var feedback = await client.GetAsync("/InterviewEvents/AssessFeedback");
        var lunch = await client.GetAsync("/SignupInterviewerTimeslots/LunchReport");

        Assert.Equal(HttpStatusCode.OK, attendance.StatusCode);
        Assert.Contains("No student signups", await attendance.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Contains("No completed interviews", await completed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, feedback.StatusCode);
        Assert.Contains("No feedback yet", await feedback.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, lunch.StatusCode);
        Assert.Contains("No interviewer availability", await lunch.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lunch_report_supports_more_than_two_events()
    {
        var eventNames = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "interviewer-1");
            var schedules = new[]
            {
                await TestData.AddEventWithTimeslotsAsync(context, For221.n, name: "First report event"),
                await TestData.AddEventWithTimeslotsAsync(context, For221.n, name: "Second report event"),
                await TestData.AddEventWithTimeslotsAsync(context, For221.n, name: "Third report event")
            };
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Test",
                LastName = "Interviewer",
                Lunch = true,
                InPerson = true
            };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.AddRange(schedules.Select(schedule => new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = schedule.Timeslots[1].Id
            }));
            await context.SaveChangesAsync();
            return schedules.Select(schedule => schedule.Event.Name).ToArray();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.GetAsync("/SignupInterviewerTimeslots/LunchReport");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(eventNames, eventName => Assert.Contains(eventName, html));
    }
}
