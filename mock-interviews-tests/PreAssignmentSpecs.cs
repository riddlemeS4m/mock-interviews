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

    [Fact]
    public async Task Preassignment_uses_volunteered_availability_before_check_in_and_while_the_interviewer_is_busy()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "student-2");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = Signup("interviewer-1", behavioral: true, checkedIn: false);
            var target = new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var current = new Interview { StudentId = "student-2", TimeslotId = slots[1].Id, Status = StatusConstants.Ongoing, Type = InterviewTypeConstants.Behavioral };
            context.AddRange(signup, target, current);
            await context.SaveChangesAsync();
            var targetAvailability = new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id };
            var currentAvailability = new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[1].Id };
            context.InterviewerTimeslots.AddRange(targetAvailability, currentAvailability);
            await context.SaveChangesAsync();
            current.InterviewerTimeslotId = currentAvailability.Id;
            await context.SaveChangesAsync();
            return (timeslotId: slots[0].Id, targetInterviewId: target.Id, availabilityId: targetAvailability.Id);
        });
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PreAssignmentService>();

        var page = await service.BuildAsync();
        Assert.Contains(page.Timeslots.Single(slot => slot.TimeslotId == ids.timeslotId).Interviews.Single().Candidates,
            candidate => candidate.InterviewerId == "interviewer-1");

        var result = await service.ApplyAsync(new PreAssignmentTimeslotRequest
        {
            TimeslotId = ids.timeslotId,
            Assignments = [new PreAssignmentInterviewRequest { InterviewId = ids.targetInterviewId, InterviewerId = "interviewer-1" }]
        });

        Assert.Equal(PreAssignmentCommandStatus.Success, result.Status);
        Assert.Equal(ids.availabilityId, await Factory.InDatabaseScopeAsync(context => context.Interviews
            .Where(interview => interview.Id == ids.targetInterviewId)
            .Select(interview => interview.InterviewerTimeslotId)
            .SingleAsync()));
    }

    [Fact]
    public async Task Simultaneous_preassignments_cannot_plan_one_interviewer_twice_in_the_same_timeslot()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "student-2");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = Signup("interviewer-1", behavioral: true, checkedIn: false);
            var first = new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var second = new Interview { StudentId = "student-2", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            context.AddRange(signup, first, second);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id });
            await context.SaveChangesAsync();
            return (timeslotId: slots[0].Id, firstInterviewId: first.Id, secondInterviewId: second.Id);
        });
        using var firstScope = Factory.Services.CreateScope();
        using var secondScope = Factory.Services.CreateScope();
        var firstService = firstScope.ServiceProvider.GetRequiredService<PreAssignmentService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<PreAssignmentService>();

        var results = await Task.WhenAll(
            firstService.ApplyAsync(Request(ids.timeslotId, ids.firstInterviewId)),
            secondService.ApplyAsync(Request(ids.timeslotId, ids.secondInterviewId)));

        Assert.Single(results, result => result.Status == PreAssignmentCommandStatus.Success);
        Assert.Single(results, result => result.Status == PreAssignmentCommandStatus.Conflict && result.Message == "A selected interviewer already has a planned interview for this timeslot.");
        Assert.Equal(1, await Factory.InDatabaseScopeAsync(context => context.Interviews.CountAsync(interview => interview.InterviewerTimeslotId != null)));
    }

    [Fact]
    public async Task Simultaneous_preassignments_cannot_overwrite_one_interview_with_different_interviewers()
    {
        var ids = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            await TestData.AddUserAsync(context, "interviewer-2");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var interview = new Interview { StudentId = "student-1", TimeslotId = slots[0].Id, Status = StatusConstants.Default, Type = InterviewTypeConstants.Behavioral };
            var first = Signup("interviewer-1", behavioral: true, checkedIn: false);
            var second = Signup("interviewer-2", behavioral: true, checkedIn: false);
            context.AddRange(interview, first, second);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.AddRange(
                new InterviewerTimeslot { InterviewerSignupId = first.Id, TimeslotId = slots[0].Id },
                new InterviewerTimeslot { InterviewerSignupId = second.Id, TimeslotId = slots[0].Id });
            await context.SaveChangesAsync();
            return (timeslotId: slots[0].Id, interviewId: interview.Id);
        });
        using var firstScope = Factory.Services.CreateScope();
        using var secondScope = Factory.Services.CreateScope();
        var firstService = firstScope.ServiceProvider.GetRequiredService<PreAssignmentService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<PreAssignmentService>();

        var results = await Task.WhenAll(
            firstService.ApplyAsync(Request(ids.timeslotId, ids.interviewId, "interviewer-1")),
            secondService.ApplyAsync(Request(ids.timeslotId, ids.interviewId, "interviewer-2")));

        Assert.Single(results, result => result.Status == PreAssignmentCommandStatus.Success);
        Assert.Single(results, result => result.Status == PreAssignmentCommandStatus.Conflict && result.Message == "One or more pre-assignments changed in another session. Refresh the page and try again.");
        Assert.NotNull(await Factory.InDatabaseScopeAsync(async context =>
            (await context.Interviews.SingleAsync()).InterviewerTimeslotId));
    }

    private static PreAssignmentTimeslotRequest Request(int timeslotId, int interviewId, string interviewerId = "interviewer-1") => new()
    {
        TimeslotId = timeslotId,
        Assignments = [new PreAssignmentInterviewRequest { InterviewId = interviewId, InterviewerId = interviewerId }]
    };

    private static InterviewerSignup Signup(string interviewerId, bool behavioral = false, bool technical = false, bool checkedIn = true) => new()
    {
        InterviewerId = interviewerId,
        FirstName = interviewerId,
        LastName = "Signup",
        CheckedIn = checkedIn,
        IsBehavioral = behavioral,
        IsTechnical = technical
    };
}
