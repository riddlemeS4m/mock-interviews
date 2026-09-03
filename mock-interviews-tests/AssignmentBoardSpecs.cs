using MockInterviews.IntegrationTests.Infrastructure;
using MockInterviews.Services;

namespace MockInterviews.IntegrationTests;

public sealed class AssignmentBoardSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Board_composes_only_exact_checked_in_type_compatible_and_not_busy_normal_candidates()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "student-2");
            await TestData.AddUserAsync(context, "eligible");
            await TestData.AddUserAsync(context, "busy");
            await TestData.AddUserAsync(context, "wrong-type");
            await TestData.AddUserAsync(context, "wrong-slot");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);

            var target = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.CheckedIn,
                CheckedInAt = DateTime.UtcNow,
                Type = InterviewTypeConstants.Behavioral
            };
            var eligible = Signup("eligible", behavioral: true);
            var busy = Signup("busy", behavioral: true);
            var wrongType = Signup("wrong-type", technical: true);
            var wrongSlot = Signup("wrong-slot", behavioral: true);
            context.AddRange(target, eligible, busy, wrongType, wrongSlot);
            await context.SaveChangesAsync();

            var eligibleAvailability = new InterviewerTimeslot { InterviewerSignupId = eligible.Id, TimeslotId = slots[0].Id };
            var busyAvailability = new InterviewerTimeslot { InterviewerSignupId = busy.Id, TimeslotId = slots[0].Id };
            var wrongTypeAvailability = new InterviewerTimeslot { InterviewerSignupId = wrongType.Id, TimeslotId = slots[0].Id };
            var wrongSlotAvailability = new InterviewerTimeslot { InterviewerSignupId = wrongSlot.Id, TimeslotId = slots[1].Id };
            context.InterviewerTimeslots.AddRange(eligibleAvailability, busyAvailability, wrongTypeAvailability, wrongSlotAvailability);
            await context.SaveChangesAsync();
            context.Interviews.Add(new Interview
            {
                StudentId = "student-2",
                TimeslotId = slots[1].Id,
                InterviewerTimeslotId = busyAvailability.Id,
                Status = StatusConstants.Ongoing,
                StartedAt = DateTime.UtcNow,
                Type = InterviewTypeConstants.Behavioral
            });
            await context.SaveChangesAsync();
            return target.Id;
        });

        using var scope = Factory.Services.CreateScope();
        var board = await scope.ServiceProvider.GetRequiredService<AssignmentBoardQueryService>().BuildAsync();
        var target = Assert.Single(board.CheckedIn, row => row.InterviewId == data);

        var candidate = Assert.Single(target.NormalCandidates);
        Assert.Equal("eligible", candidate.InterviewerId);
        Assert.Equal("Test eligible", candidate.Name);
        Assert.Equal(4, target.OverrideCandidates.Count);
        Assert.Contains(target.OverrideCandidates, candidate => candidate.InterviewerId == "busy" && candidate.Bypasses.Contains("current assignment"));
        Assert.Contains(target.OverrideCandidates, candidate => candidate.InterviewerId == "wrong-type" && candidate.Bypasses.Contains("interview type"));
        Assert.Contains(target.OverrideCandidates, candidate => candidate.InterviewerId == "wrong-slot" && candidate.Bypasses.Contains("exact-timeslot availability"));
    }

    [Fact]
    public async Task Board_fragment_is_administrator_only_and_renders_current_server_composed_state()
    {
        using var anonymous = Factory.CreateAnonymousClient();
        using var interviewer = Factory.CreateAuthenticatedClient("interviewer-1", RolesConstants.InterviewerRole);
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin", RolesConstants.SystemAdminRole);

        var anonymousResponse = await anonymous.GetAsync("/InterviewEvents/Board");
        var interviewerResponse = await interviewer.GetAsync("/InterviewEvents/Board");
        var systemAdminResponse = await systemAdmin.GetAsync("/InterviewEvents/Board");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, interviewerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, systemAdminResponse.StatusCode);
        Assert.Contains("assignment-board-region", await systemAdminResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Board_uses_the_tailwind_shell_and_renders_a_normal_assignment_dialog()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var interview = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.CheckedIn,
                CheckedInAt = DateTime.UtcNow,
                Type = InterviewTypeConstants.Behavioral
            };
            var signup = Signup("interviewer-1", behavioral: true);
            context.AddRange(interview, signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[0].Id
            });
            await context.SaveChangesAsync();
        });
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await admin.GetAsync("/InterviewEvents");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tailwind.css", html);
        Assert.Contains("assignment-dialog-", html);
        Assert.Contains("data-dialog", html);
        Assert.Contains("assignment-board.js", html);
        Assert.DoesNotContain("signal-r-interviews.js", html);
        Assert.DoesNotContain("signal-r-available-interviewers.js", html);
        Assert.DoesNotContain("modal fade", html);
    }

    [Fact]
    public async Task Assignment_hub_is_limited_to_administration_roles()
    {
        using var anonymous = Factory.CreateAnonymousClient();
        using var interviewer = Factory.CreateAuthenticatedClient("interviewer-1", RolesConstants.InterviewerRole);
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var anonymousResponse = await anonymous.PostAsync("/interviewhub/negotiate?negotiateVersion=1", new StringContent(string.Empty));
        var interviewerResponse = await interviewer.PostAsync("/interviewhub/negotiate?negotiateVersion=1", new StringContent(string.Empty));
        var adminResponse = await admin.PostAsync("/interviewhub/negotiate?negotiateVersion=1", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, interviewerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Retired_inline_assignment_candidate_endpoint_is_not_exposed()
    {
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await admin.GetAsync("/InterviewEvents/GetAvailableInterviewers/1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
