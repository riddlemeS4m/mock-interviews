using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class RosterAdministrationSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Program_roster_import_replaces_records_after_successful_validation()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            context.RosteredStudents.Add(new RosteredStudent { Email = "old@crimson.ua.edu", Name = "Old Record" });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostCsvWithAntiforgeryAsync("/MSTeamsStudentUploads/Create", "Microsoft ID,Email,Name\nlegacy-1,Student@Crimson.Ua.Edu,Student One");
        var students = await Factory.InDatabaseScopeAsync(async context => await context.RosteredStudents.ToListAsync());

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var student = Assert.Single(students);
        Assert.Equal("student@crimson.ua.edu", student.Email);
        Assert.Equal("Student One", student.Name);
        Assert.Equal("legacy-1", student.MicrosoftId);
    }

    [Fact]
    public async Task Malformed_program_roster_does_not_replace_existing_records()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            context.RosteredStudents.Add(new RosteredStudent { Email = "keep@crimson.ua.edu", Name = "Keep Record" });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostCsvWithAntiforgeryAsync("/MSTeamsStudentUploads/Create", "Microsoft ID,Email,Name\nlegacy-1,not-an-email,Invalid");
        var students = await Factory.InDatabaseScopeAsync(async context => await context.RosteredStudents.ToListAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var student = Assert.Single(students);
        Assert.Equal("keep@crimson.ua.edu", student.Email);
    }

    [Fact]
    public async Task Mis221_import_adds_membership_without_clearing_existing_flags()
    {
        await Factory.InDatabaseScopeAsync(async context =>
        {
            context.RosteredStudents.Add(new RosteredStudent { Email = "existing@crimson.ua.edu", Name = "Existing", In221 = true });
            await context.SaveChangesAsync();
        });
        using var client = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await client.PostCsvWithAntiforgeryAsync("/MSTeamsStudentUploads/Upload221Students", "Last,First,Unused,Unused,Unused,Unused,Email\nNew,Student,,,,,new@crimson.ua.edu");
        var students = await Factory.InDatabaseScopeAsync(async context => await context.RosteredStudents.OrderBy(student => student.Email).ToListAsync());

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        Assert.Equal(2, students.Count);
        Assert.All(students, student => Assert.True(student.In221));
    }

    [Fact]
    public async Task Roster_administration_requires_an_administrator_and_masters_route_is_absent()
    {
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);
        using var systemAdmin = Factory.CreateAuthenticatedClient("system-admin-1", RolesConstants.SystemAdminRole);

        var forbidden = await student.GetAsync("/MSTeamsStudentUploads/Upload221Students");
        var allowed = await systemAdmin.GetAsync("/MSTeamsStudentUploads");
        var masters = await systemAdmin.GetAsync("/MSTeamsStudentUploads/UploadMastersStudents");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, masters.StatusCode);
    }
}
