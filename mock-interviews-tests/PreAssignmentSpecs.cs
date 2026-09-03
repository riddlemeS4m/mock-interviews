using MockInterviews.IntegrationTests.Infrastructure;
using MockInterviews.Models.ViewModels.InterviewEventsController;
using MockInterviews.Services;

namespace MockInterviews.IntegrationTests;

public sealed class PreAssignmentSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Preassignment_page_uses_tailwind_and_groups_waiting_interviews_by_exact_timeslot()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "student-2");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = Signup("interviewer-1", behavioral: true);
            context.AddRange(
                new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral },
                new Interview { StudentId = "student-2", TimeslotId = slots[1].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral },
                signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id });
            await context.SaveChangesAsync();
        });
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await admin.GetAsync("/InterviewEvents/PreAssignInterviews");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tailwind.css", html);
        Assert.Contains("preassignment-timeslot-", html);
        Assert.Contains("Save this timeslot", html);
        Assert.DoesNotContain("TimeslotSeed", html);
        Assert.DoesNotContain("$.ajax", html);
        Assert.DoesNotContain("bootstrap.Toast", html);
    }

    [Fact]
    public async Task Preassignment_validates_the_entire_timeslot_before_changing_any_interview()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "student-2");
            await TestData.AddUserAsync(context, "eligible");
            await TestData.AddUserAsync(context, "wrong-type");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var first = new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var second = new Interview { StudentId = "student-2", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var eligible = Signup("eligible", behavioral: true);
            var wrongType = Signup("wrong-type", technical: true);
            context.AddRange(first, second, eligible, wrongType);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.AddRange(
                new InterviewerTimeslot { InterviewerSignupId = eligible.Id, TimeslotId = slots[0].Id },
                new InterviewerTimeslot { InterviewerSignupId = wrongType.Id, TimeslotId = slots[0].Id });
            await context.SaveChangesAsync();
            return (timeslotId: slots[0].Id, firstInterviewId: first.Id, secondInterviewId: second.Id);
        });
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PreAssignmentService>();

        var result = await service.ApplyAsync(new PreAssignmentTimeslotRequest
        {
            TimeslotId = ids.timeslotId,
            Assignments =
            [
                new PreAssignmentInterviewRequest { InterviewId = ids.firstInterviewId, InterviewerId = "eligible" },
                new PreAssignmentInterviewRequest { InterviewId = ids.secondInterviewId, InterviewerId = "wrong-type" }
            ]
        });

        Assert.Equal(PreAssignmentCommandStatus.Validation, result.Status);
        var stored = await Factory.InDatabaseScopeAsync(context => context.Interviews.OrderBy(interview => interview.Id).ToListAsync());
        Assert.All(stored, interview => Assert.Null(interview.InterviewerTimeslotId));
    }

    [Fact]
    public async Task System_admin_can_preassign_an_exact_timeslot_with_antiforgery()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var interview = new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var signup = Signup("interviewer-1", behavioral: true);
            context.AddRange(interview, signup);
            await context.SaveChangesAsync();
            var availability = new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id };
            context.InterviewerTimeslots.Add(availability);
            await context.SaveChangesAsync();
            return (timeslotId: slots[0].Id, interviewId: interview.Id, availabilityId: availability.Id);
        });
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin", RolesConstants.SystemAdminRole);

        var forged = await systemAdmin.PostAsync("/InterviewEvents/PreAssignInterviews", new FormUrlEncodedContent([]));
        var response = await systemAdmin.PostFormWithAntiforgeryAsync(
            "/InterviewEvents/PreAssignInterviews",
            "/InterviewEvents/PreAssignInterviews",
            [
                new KeyValuePair<string, string>("TimeslotId", ids.timeslotId.ToString()),
                new KeyValuePair<string, string>("Assignments[0].InterviewId", ids.interviewId.ToString()),
                new KeyValuePair<string, string>("Assignments[0].InterviewerId", "interviewer-1")
            ]);

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(context => context.Interviews.SingleAsync());
        Assert.Equal(ids.availabilityId, stored.InterviewerTimeslotId);
        Assert.Equal(StatusConstants.Default, stored.Status);
    }

    private static InterviewerSignup Signup(string interviewerId, bool behavioral = false, bool technical = false) => new()
    {
        InterviewerId = interviewerId,
        FirstName = interviewerId,
        LastName = "Signup",
        CheckedIn = true,
        IsBehavioral = behavioral,
        IsTechnical = technical
    };
}
