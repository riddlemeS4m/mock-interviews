using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class StudentInterviewSignupSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Eligible_student_signup_creates_a_pair_and_records_confirmation()
    {
        (Event @event, List<Timeslot> slots) = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1", Classes.SecondSem);
            var interviewEvent = await TestData.AddEventWithTimeslotsAsync(context);
            await TestData.AddEventWithTimeslotsAsync(context, For221.n);
            return interviewEvent;
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var page = await client.GetAsync("/InterviewEvents/Create");
        var response = await client.PostFormWithAntiforgeryAsync("/InterviewEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds", slots[0].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        Assert.Equal("/", response.Headers.Location?.OriginalString);

        var interviews = await Factory.InDatabaseScopeAsync(async context => await context.Interviews
            .Where(interview => interview.StudentId == "student-1")
            .OrderBy(interview => interview.TimeslotId)
            .ToListAsync());
        Assert.Collection(interviews,
            interview =>
            {
                Assert.Equal(slots[0].Id, interview.TimeslotId);
                Assert.Equal(StatusConstants.Default, interview.Status);
                Assert.Equal("Behavioral", interview.Type);
            },
            interview =>
            {
                Assert.Equal(slots[1].Id, interview.TimeslotId);
                Assert.Equal(StatusConstants.Default, interview.Status);
                Assert.Equal("Technical", interview.Type);
            });
        Assert.Single(Factory.SentEmails);

        var repeat = await client.PostFormWithAntiforgeryAsync("/VolunteerEvents/Create", "/InterviewEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds", slots[0].Id.ToString())
        });
        var countAfterRepeat = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.CountAsync());
        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);
        Assert.Equal(2, countAfterRepeat);
        Assert.Single(Factory.SentEmails);
    }

    [Fact]
    public async Task Student_signup_rejects_inactive_forged_full_and_repeat_submissions_without_writes()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1", Classes.SecondSem);
            await TestData.AddUserAsync(context, "other-student");
            var active = await TestData.AddEventWithTimeslotsAsync(context, maxSignups: 1);
            var inactive = await TestData.AddEventWithTimeslotsAsync(context, active: false);
            context.Interviews.Add(new Interview
            {
                StudentId = "other-student",
                TimeslotId = active.Timeslots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            });
            await context.SaveChangesAsync();
            return (active, inactive);
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        foreach (var requestedSlotId in new[] { data.active.Timeslots[0].Id, data.inactive.Timeslots[0].Id, 999_999 })
        {
            var response = await client.PostFormWithAntiforgeryAsync("/InterviewEvents/Create", new[]
            {
                new KeyValuePair<string, string>("SelectedEventIds", requestedSlotId.ToString())
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var studentInterviews = await Factory.InDatabaseScopeAsync(async context => await context.Interviews
            .CountAsync(interview => interview.StudentId == "student-1"));
        Assert.Equal(0, studentInterviews);
        Assert.Empty(Factory.SentEmails);
    }

    [Fact]
    public async Task Student_signup_exposes_only_class_eligible_events_and_rejects_a_forged_choice()
    {
        var events = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1", Classes.FirstSem);
            var eligible = await TestData.AddEventWithTimeslotsAsync(
                context,
                For221.y,
                name: "MIS 221 Event");
            var ineligible = await TestData.AddEventWithTimeslotsAsync(
                context,
                For221.n,
                name: "Upper Level Event");
            return (eligible, ineligible);
        });
        using var client = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);

        var page = await client.GetAsync("/InterviewEvents/Create");
        var html = await page.Content.ReadAsStringAsync();
        var forged = await client.PostFormWithAntiforgeryAsync("/InterviewEvents/Create", new[]
        {
            new KeyValuePair<string, string>("SelectedEventIds", events.ineligible.Timeslots[0].Id.ToString())
        });

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("MIS 221 Event", html);
        Assert.DoesNotContain("Upper Level Event", html);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.False(await Factory.InDatabaseScopeAsync(context => context.Interviews.AnyAsync()));
    }
}
