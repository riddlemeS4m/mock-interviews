using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class DashboardSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Student_dashboard_uses_the_application_shell_and_combines_contiguous_volunteer_shifts()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var student = await TestData.AddUserAsync(context, "dashboard-student");
            var interviewer = await TestData.AddUserAsync(context, "dashboard-interviewer");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var location = new Location { Room = "Hewson 201", InPerson = true, IsVirtual = false };
            var signup = new InterviewerSignup
            {
                InterviewerId = interviewer.Id,
                FirstName = interviewer.FirstName!,
                LastName = interviewer.LastName!,
                InPerson = true,
                Type = InterviewTypeConstants.Behavioral
            };
            context.AddRange(location, signup);
            await context.SaveChangesAsync();

            var interviewerTimeslot = new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[0].Id
            };
            context.InterviewerTimeslots.Add(interviewerTimeslot);
            await context.SaveChangesAsync();

            context.Interviews.Add(new Interview
            {
                StudentId = student.Id,
                TimeslotId = slots[0].Id,
                LocationId = location.Id,
                InterviewerTimeslotId = interviewerTimeslot.Id,
                Type = InterviewTypeConstants.Behavioral,
                Status = StatusConstants.Default
            });
            context.VolunteerTimeslots.AddRange(
                new VolunteerTimeslot { StudentId = student.Id, TimeslotId = slots[1].Id },
                new VolunteerTimeslot { StudentId = student.Id, TimeslotId = slots[2].Id });
            await context.SaveChangesAsync();
        });

        using var client = Factory.CreateAuthenticatedClient("dashboard-student", RolesConstants.StudentRole);
        var response = await client.GetAsync("/Home/Student");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-shell-navigation", html);
        Assert.Contains("Interviews", html);
        Assert.Contains("Test dashboard-interviewer", html);
        Assert.Contains("Hewson 201", html);
        Assert.Contains("Volunteer shifts", html);
        Assert.Contains("9:30 AM–10:30 AM", html);
        Assert.DoesNotContain("bootstrap.min.css", html);
    }

    [Fact]
    public async Task Interviewer_dashboard_separates_active_and_completed_interviews_and_supports_three_schedule_groups()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            var interviewer = await TestData.AddUserAsync(context, "multi-schedule-interviewer");
            var activeStudent = await TestData.AddUserAsync(context, "active-student");
            var completedStudent = await TestData.AddUserAsync(context, "completed-student");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);

            var signups = Enumerable.Range(0, 3).Select(index => new InterviewerSignup
            {
                InterviewerId = interviewer.Id,
                FirstName = interviewer.FirstName!,
                LastName = interviewer.LastName!,
                InPerson = index != 1,
                IsVirtual = index == 1,
                Type = index == 2 ? InterviewTypeConstants.Technical : InterviewTypeConstants.Behavioral
            }).ToList();
            context.InterviewerSignups.AddRange(signups);
            await context.SaveChangesAsync();

            var interviewerTimeslots = signups.Select((signup, index) => new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[index].Id
            }).ToList();
            context.InterviewerTimeslots.AddRange(interviewerTimeslots);
            await context.SaveChangesAsync();

            var activeInterview = new Interview
            {
                StudentId = activeStudent.Id,
                TimeslotId = slots[0].Id,
                InterviewerTimeslotId = interviewerTimeslots[0].Id,
                Type = InterviewTypeConstants.Behavioral,
                Status = StatusConstants.Ongoing,
                StartedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            var completedInterview = new Interview
            {
                StudentId = completedStudent.Id,
                TimeslotId = slots[1].Id,
                InterviewerTimeslotId = interviewerTimeslots[1].Id,
                Type = InterviewTypeConstants.Behavioral,
                Status = StatusConstants.Completed
            };
            context.Interviews.AddRange(activeInterview, completedInterview);
            await context.SaveChangesAsync();
            return (
                ActiveInterviewId: activeInterview.Id,
                CompletedInterviewId: completedInterview.Id,
                SignupIds: signups.Select(signup => signup.Id).ToArray());
        });

        using var client = Factory.CreateAuthenticatedClient(
            "multi-schedule-interviewer",
            RolesConstants.InterviewerRole);
        var response = await client.GetAsync("/Home/Interviewer");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"data-interview-row=\"{ids.ActiveInterviewId}\"", html);
        Assert.DoesNotContain($"data-interview-row=\"{ids.CompletedInterviewId}\"", html);
        Assert.Contains("Test active-student", html);
        Assert.Contains("Test completed-student", html);
        Assert.Contains("data-dashboard-timer", html);
        Assert.Contains("Mark done", html);
        Assert.All(ids.SignupIds, signupId =>
            Assert.Contains($"/SignupInterviewerTimeslots/Edit/{signupId}", html));

        var complete = await client.PostFormWithAntiforgeryAsync(
            "/Home/Interviewer",
            $"/InterviewEvents/CompleteAssignedInterview/{ids.ActiveInterviewId}",
            []);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        Assert.Equal(StatusConstants.Completed, await Factory.InDatabaseScopeAsync(async context =>
            await context.Interviews
                .Where(interview => interview.Id == ids.ActiveInterviewId)
                .Select(interview => interview.Status)
                .SingleAsync()));
    }
}
