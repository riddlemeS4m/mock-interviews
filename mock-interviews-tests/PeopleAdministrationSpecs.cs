using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class PeopleAdministrationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task People_workspace_is_limited_to_administrators()
    {
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var forbidden = await student.GetAsync("/UserRoles");
        var allowed = await systemAdmin.GetAsync("/UserRoles");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Admin_role_update_preserves_privileged_roles()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "managed-user");
            var adminRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.AdminRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = adminRole.Id });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/UserRoles/Manage?userId=managed-user", new[]
        {
            new KeyValuePair<string, string>("userId", "managed-user"),
            new KeyValuePair<string, string>("[0].RoleName", RolesConstants.StudentRole),
            new KeyValuePair<string, string>("[0].Selected", "true")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var roleNames = await Factory.InDatabaseScopeAsync(async context => await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == "managed-user"
            select role.Name).ToListAsync());
        Assert.Contains(RolesConstants.AdminRole, roleNames);
        Assert.Contains(RolesConstants.StudentRole, roleNames);
    }

    [Fact]
    public async Task Admin_password_reset_sends_an_email_link()
    {
        await Factory.InDatabaseScopeAsync(context => TestData.AddUserAsync(context, "reset-user"));
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/Users/ResetUserPassword?userId=reset-user", new[]
        {
            new KeyValuePair<string, string>("UserId", "reset-user")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var email = Assert.Single(Factory.SentEmails);
        Assert.Equal("Reset your Mock Interviews password", email.Subject);
    }

    [Fact]
    public async Task System_admin_cannot_remove_their_own_final_system_admin_role()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "system-admin-1");
            var systemAdminRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.SystemAdminRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = systemAdminRole.Id });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/UserRoles/Manage?userId=system-admin-1", new[]
        {
            new KeyValuePair<string, string>("userId", "system-admin-1"),
            new KeyValuePair<string, string>("[0].RoleName", RolesConstants.StudentRole),
            new KeyValuePair<string, string>("[0].Selected", "true")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roleNames = await Factory.InDatabaseScopeAsync(async context => await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == "system-admin-1"
            select role.Name).ToListAsync());
        Assert.Contains(RolesConstants.SystemAdminRole, roleNames);
    }

    [Fact]
    public async Task Accounts_with_related_records_cannot_be_deleted()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "assigned-user");
            var schedule = await TestData.AddEventWithTimeslotsAsync(context);
            context.InterviewerLocations.Add(new InterviewerLocation
            {
                InterviewerId = user.Id,
                EventId = schedule.Event.Id,
                Preference = InterviewLocationConstants.InPerson
            });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/UserRoles", "/Users/DeleteUserConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", "assigned-user")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var exists = await Factory.InDatabaseScopeAsync(context => context.Users.AnyAsync(user => user.Id == "assigned-user"));
        Assert.True(exists);
    }

    [Fact]
    public async Task Admin_cannot_delete_another_privileged_account()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            var user = await TestData.AddUserAsync(context, "other-admin");
            var adminRole = await context.Roles.SingleAsync(role => role.Name == RolesConstants.AdminRole);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = adminRole.Id });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostFormWithAntiforgeryAsync("/UserRoles", "/Users/DeleteUserConfirmed", new[]
        {
            new KeyValuePair<string, string>("id", "other-admin")
        });

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var exists = await Factory.InDatabaseScopeAsync(context => context.Users.AnyAsync(user => user.Id == "other-admin"));
        Assert.True(exists);
    }
}
