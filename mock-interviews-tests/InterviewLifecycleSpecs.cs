using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class InterviewLifecycleSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task System_admin_can_check_in_with_antiforgery_while_get_and_forged_posts_do_not_mutate()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = InterviewTypeConstants.Behavioral
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin", RolesConstants.SystemAdminRole);

        var get = await systemAdmin.GetAsync($"/InterviewEvents/StudentCheckIn/{interview.Id}");
        var forgedPost = await systemAdmin.PostAsync($"/InterviewEvents/StudentCheckIn/{interview.Id}", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, forgedPost.StatusCode);
        Assert.Equal(StatusConstants.Default, await Factory.InDatabaseScopeAsync(async context =>
            (await context.Interviews.SingleAsync()).Status));

        var checkIn = await systemAdmin.PostFormWithAntiforgeryAsync(
            "/InterviewEvents/Index",
            $"/InterviewEvents/StudentCheckIn/{interview.Id}",
            []);

        Assert.Equal(HttpStatusCode.NoContent, checkIn.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Equal(StatusConstants.CheckedIn, stored.Status);
        Assert.NotNull(stored.CheckedInAt);
    }

    [Fact]
    public async Task Assignment_uses_exact_availability_and_starts_a_checked_in_interview()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (@event, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var interview = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            };
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Test",
                LastName = "Interviewer",
                InPerson = true,
                Type = "Behavioral",
                CheckedIn = true
            };
            context.AddRange(interview, signup);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[1].Id
            });
            await context.SaveChangesAsync();
            return (interview, signup, @event, slots);
        });
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var checkIn = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", $"/InterviewEvents/StudentCheckIn/{data.interview.Id}", []);
        var unavailableAssignment = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", "/InterviewEvents/EditInline", new[]
        {
            new KeyValuePair<string, string>("id", data.interview.Id.ToString()),
            new KeyValuePair<string, string>("interviewerId", "interviewer-1")
        });

        Assert.Equal(HttpStatusCode.NoContent, checkIn.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unavailableAssignment.StatusCode);

        await Factory.InDatabaseScopeAsync(async context =>
        {
            var location = new Location { Room = "101", InPerson = true };
            context.Locations.Add(location);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = data.signup.Id,
                TimeslotId = data.slots[0].Id
            });
            context.InterviewerLocations.Add(new InterviewerLocation
            {
                InterviewerId = "interviewer-1",
                EventId = data.@event.Id,
                LocationId = location.Id,
                Preference = "In Person"
            });
            await context.SaveChangesAsync();
        });

        var assignment = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", "/InterviewEvents/EditInline", new[]
        {
            new KeyValuePair<string, string>("id", data.interview.Id.ToString()),
            new KeyValuePair<string, string>("interviewerId", "interviewer-1")
        });

        Assert.Equal(HttpStatusCode.NoContent, assignment.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Equal(StatusConstants.Ongoing, stored.Status);
        Assert.NotNull(stored.CheckedInAt);
        Assert.NotNull(stored.StartedAt);
        Assert.NotNull(stored.InterviewerTimeslotId);
        Assert.NotNull(stored.LocationId);
    }

    [Fact]
    public async Task Override_uses_a_real_same_event_availability_without_changing_the_interviews_schedule()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Override",
                LastName = "Interviewer",
                IsTechnical = true,
                CheckedIn = false
            };
            var interview = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.CheckedIn,
                CheckedInAt = DateTime.UtcNow,
                Type = InterviewTypeConstants.Behavioral
            };
            context.AddRange(signup, interview);
            await context.SaveChangesAsync();
            var availability = new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[1].Id
            };
            context.InterviewerTimeslots.Add(availability);
            await context.SaveChangesAsync();
            return (interview, availability, slots);
        });
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var overrideResult = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", "/InterviewEvents/Override", new[]
        {
            new KeyValuePair<string, string>("id", data.interview.Id.ToString()),
            new KeyValuePair<string, string>("interviewerId", "interviewer-1")
        });

        Assert.Equal(HttpStatusCode.Found, overrideResult.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Equal(StatusConstants.Ongoing, stored.Status);
        Assert.Equal(data.slots[0].Id, stored.TimeslotId);
        Assert.Equal(data.availability.Id, stored.InterviewerTimeslotId);
        Assert.NotNull(stored.StartedAt);
        Assert.Equal(1, await Factory.InDatabaseScopeAsync(async context => await context.InterviewerTimeslots.CountAsync()));
    }

    [Fact]
    public async Task Only_an_assigned_interviewer_or_admin_can_complete_an_interview()
    {
        var interviews = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "assigned");
            await TestData.AddUserAsync(context, "unassigned");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = new InterviewerSignup { InterviewerId = "assigned", FirstName = "Assigned", LastName = "Person" };
            context.InterviewerSignups.Add(signup);
            await context.SaveChangesAsync();
            var availability = new[]
            {
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[0].Id },
                new InterviewerTimeslot { InterviewerSignupId = signup.Id, TimeslotId = slots[1].Id }
            };
            context.InterviewerTimeslots.AddRange(availability);
            await context.SaveChangesAsync();
            var items = new[]
            {
                new Interview
                {
                    StudentId = "student-1",
                    TimeslotId = slots[0].Id,
                    InterviewerTimeslotId = availability[0].Id,
                    Status = StatusConstants.Ongoing,
                    Type = "Behavioral"
                },
                new Interview
                {
                    StudentId = "student-1",
                    TimeslotId = slots[1].Id,
                    InterviewerTimeslotId = availability[1].Id,
                    Status = StatusConstants.Ongoing,
                    Type = "Behavioral"
                }
            };
            context.Interviews.AddRange(items);
            await context.SaveChangesAsync();
            return items;
        });
        using var unassigned = Factory.CreateAuthenticatedClient("unassigned", RolesConstants.InterviewerRole);
        using var assigned = Factory.CreateAuthenticatedClient("assigned", RolesConstants.InterviewerRole);
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var getDoesNotMutate = await unassigned.GetAsync($"/InterviewEvents/StudentComplete/{interviews[0].Id}");
        var adminComplete = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", $"/InterviewEvents/StudentComplete/{interviews[0].Id}", []);
        var complete = await assigned.PostFormWithAntiforgeryAsync("/Home/Interviewer", $"/InterviewEvents/StudentComplete/{interviews[1].Id}", []);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, getDoesNotMutate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, adminComplete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.OrderBy(item => item.Id).ToListAsync());
        Assert.All(stored, item =>
        {
            Assert.Equal(StatusConstants.Completed, item.Status);
            Assert.NotNull(item.EndedAt);
        });
    }

    [Fact]
    public async Task Preassignment_uses_only_exact_interviewer_availability()
    {
        var data = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var signup = new InterviewerSignup
            {
                InterviewerId = "interviewer-1",
                FirstName = "Interview",
                LastName = "Person"
            };
            var interview = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            };
            context.AddRange(signup, interview);
            await context.SaveChangesAsync();
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = signup.Id,
                TimeslotId = slots[1].Id
            });
            await context.SaveChangesAsync();
            return (interview, signup, slots);
        });
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);
        var request = new[]
        {
            new
            {
                InterviewEventId = data.interview.Id.ToString(),
                SelectedValue = "interviewer-1"
            }
        };

        var unavailable = await admin.PostAsJsonAsync("/InterviewEvents/PreAssignInterviews", request);
        Assert.Equal(HttpStatusCode.OK, unavailable.StatusCode);
        Assert.Null(await Factory.InDatabaseScopeAsync(async context =>
            (await context.Interviews.SingleAsync()).InterviewerTimeslotId));

        await Factory.InDatabaseScopeAsync(async context =>
        {
            context.InterviewerTimeslots.Add(new InterviewerTimeslot
            {
                InterviewerSignupId = data.signup.Id,
                TimeslotId = data.slots[0].Id
            });
            await context.SaveChangesAsync();
        });

        var assigned = await admin.PostAsJsonAsync("/InterviewEvents/PreAssignInterviews", request);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.NotNull(await Factory.InDatabaseScopeAsync(async context =>
            (await context.Interviews.SingleAsync()).InterviewerTimeslotId));
    }

    [Fact]
    public async Task Only_an_admin_can_mark_an_interview_as_no_show()
    {
        var interview = await Factory.InDatabaseScopeAsync(async context =>
        {
            await TestData.AddUserAsync(context, "student-1");
            await TestData.AddUserAsync(context, "interviewer-1");
            var (_, slots) = await TestData.AddEventWithTimeslotsAsync(context);
            var item = new Interview
            {
                StudentId = "student-1",
                TimeslotId = slots[0].Id,
                Status = StatusConstants.Default,
                Type = "Behavioral"
            };
            context.Interviews.Add(item);
            await context.SaveChangesAsync();
            return item;
        });
        using var interviewer = Factory.CreateAuthenticatedClient("interviewer-1", RolesConstants.InterviewerRole);
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var getDoesNotMutate = await interviewer.GetAsync($"/InterviewEvents/StudentNoShow/{interview.Id}");
        var noShow = await admin.PostFormWithAntiforgeryAsync("/InterviewEvents/Index", $"/InterviewEvents/StudentNoShow/{interview.Id}", []);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, getDoesNotMutate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, noShow.StatusCode);
        var stored = await Factory.InDatabaseScopeAsync(async context => await context.Interviews.SingleAsync());
        Assert.Equal(StatusConstants.NoShow, stored.Status);
        Assert.NotNull(stored.EndedAt);
    }
}
