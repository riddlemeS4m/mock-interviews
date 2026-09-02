using Microsoft.AspNetCore.Identity;
using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class ManualSchedulingAdministrationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task System_admin_can_add_a_student_schedule_using_the_adjacent_time_not_adjacent_id()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            var student = await TestData.AddUserAsync(context, "student-1", Classes.SecondSem);
            var studentRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.StudentRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = student.Id, RoleId = studentRole.Id });

            var schedule = await TestData.AddEventWithTimeslotsAsync(context, For221.b);
            var originalAdjacent = schedule.Timeslots[1];
            context.Timeslots.Remove(originalAdjacent);
            await context.SaveChangesAsync();

            var nonConsecutiveAdjacent = new Timeslot
            {
                EventId = schedule.Event.Id,
                Time = schedule.Timeslots[0].Time.AddMinutes(30),
                IsActive = true,
                MaxSignUps = 2
            };
            context.Timeslots.Add(nonConsecutiveAdjacent);
            await context.SaveChangesAsync();
            return (student, schedule, nonConsecutiveAdjacent);
        });
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/InterviewEvents/CreateForStudent", new[]
        {
            new KeyValuePair<string, string>("StudentId", data.student.Id),
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.schedule.Timeslots[0].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/InterviewEvents", response.Headers.Location?.OriginalString);
        var selectedIds = await Factory.InDatabaseScopeAsync(context => context.Interviews
            .Where(interview => interview.StudentId == data.student.Id)
            .OrderBy(interview => interview.Timeslot.Time)
            .Select(interview => interview.TimeslotId)
            .ToArrayAsync());
        Assert.Equal(new[] { data.schedule.Timeslots[0].Id, data.nonConsecutiveAdjacent.Id }, selectedIds);
    }

    [Fact]
    public async Task System_admin_can_add_exact_interviewer_availability_blocks()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            var interviewer = await TestData.AddUserAsync(context, "interviewer-1");
            var interviewerRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.InterviewerRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = interviewer.Id, RoleId = interviewerRole.Id });
            await context.SaveChangesAsync();
            var firstDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            var secondDay = await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return (interviewer, firstDay, secondDay);
        });
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/CreateForInterviewer", new[]
        {
            new KeyValuePair<string, string>("InterviewerId", data.interviewer.Id),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "false"),
            new KeyValuePair<string, string>("SignupInterviewer.IsTechnical", "true"),
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.firstDay.Timeslots[0].Id.ToString()),
            new KeyValuePair<string, string>("SelectedTimeslotIds", data.secondDay.Timeslots[2].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/SignupInterviewers", response.Headers.Location?.OriginalString);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var signup = await context.InterviewerSignups.SingleAsync(item => item.InterviewerId == data.interviewer.Id);
            var availability = await context.InterviewerTimeslots
                .Where(item => item.InterviewerSignupId == signup.Id)
                .OrderBy(item => item.TimeslotId)
                .Select(item => item.TimeslotId)
                .ToArrayAsync();
            var locations = await context.InterviewerLocations
                .Where(item => item.InterviewerId == data.interviewer.Id)
                .OrderBy(item => item.EventId)
                .ToListAsync();

            Assert.False(signup.InPerson);
            Assert.True(signup.IsVirtual);
            Assert.Equal(new[] { data.firstDay.Timeslots[0].Id, data.secondDay.Timeslots[2].Id }.Order(), availability);
            Assert.Equal(2, locations.Count);
            Assert.All(locations, location => Assert.Equal(InterviewLocationConstants.IsVirtual, location.Preference));
        });
    }

    [Fact]
    public async Task Manual_scheduling_posts_require_antiforgery_and_reject_forged_timeslots()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            var interviewer = await TestData.AddUserAsync(context, "interviewer-1");
            var interviewerRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.InterviewerRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = interviewer.Id, RoleId = interviewerRole.Id });
            await context.SaveChangesAsync();
            await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return interviewer;
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var unprotectedStudentPost = await client.PostAsync("/InterviewEvents/CreateForStudent", new FormUrlEncodedContent([]));
        var unprotectedInterviewerPost = await client.PostAsync("/SignupInterviewerTimeslots/CreateForInterviewer", new FormUrlEncodedContent([]));
        var forged = await client.PostFormWithAntiforgeryAsync("/SignupInterviewerTimeslots/CreateForInterviewer", new[]
        {
            new KeyValuePair<string, string>("InterviewerId", data.Id),
            new KeyValuePair<string, string>("SignupInterviewer.InPerson", "true"),
            new KeyValuePair<string, string>("SignupInterviewer.IsTechnical", "true"),
            new KeyValuePair<string, string>("SelectedTimeslotIds", "999999")
        });

        Assert.Equal(HttpStatusCode.BadRequest, unprotectedStudentPost.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unprotectedInterviewerPost.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        await Factory.InDatabaseScopeAsync(async context =>
        {
            Assert.Empty(await context.InterviewerSignups.ToListAsync());
            Assert.Empty(await context.InterviewerTimeslots.ToListAsync());
        });
    }
}
