using MockInterviews.IntegrationTests.Infrastructure;

namespace MockInterviews.IntegrationTests;

public sealed class LocationsCrudSpecs(MockInterviewsWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Locations_are_admin_only_and_render_modal_first_actions()
    {
        using var anonymous = Factory.CreateAnonymousClient();
        using var student = Factory.CreateAuthenticatedClient("student-1", RolesConstants.StudentRole);
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var anonymousResponse = await anonymous.GetAsync("/Locations");
        var studentResponse = await student.GetAsync("/Locations");
        var adminResponse = await admin.GetAsync("/Locations");
        var html = await adminResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Contains("data-dialog-target=\"location-create-dialog\"", html);
        Assert.Contains("id=\"location-edit-dialog\"", html);
        Assert.Contains("id=\"location-delete-dialog\"", html);
    }

    [Fact]
    public async Task Admin_can_create_edit_and_delete_a_location()
    {
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var createResponse = await admin.PostFormWithAntiforgeryAsync("/Locations", "/Locations/Create", new[]
        {
            new KeyValuePair<string, string>("Room", "Hewson 3021"),
            new KeyValuePair<string, string>("IsVirtual", "true"),
            new KeyValuePair<string, string>("InPerson", "true")
        });

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/Locations", createResponse.Headers.Location?.OriginalString);

        var created = await Factory.InDatabaseScopeAsync(async context =>
            await context.Locations.SingleAsync(location => location.Room == "Hewson 3021"));

        var editResponse = await admin.PostFormWithAntiforgeryAsync("/Locations", $"/Locations/Edit/{created.Id}", new[]
        {
            new KeyValuePair<string, string>("Id", created.Id.ToString()),
            new KeyValuePair<string, string>("Room", "Hewson 3022"),
            new KeyValuePair<string, string>("IsVirtual", "false"),
            new KeyValuePair<string, string>("InPerson", "true")
        });

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal("/Locations", editResponse.Headers.Location?.OriginalString);

        var updated = await Factory.InDatabaseScopeAsync(async context =>
            await context.Locations.AsNoTracking().SingleAsync(location => location.Id == created.Id));
        Assert.Equal("Hewson 3022", updated.Room);
        Assert.False(updated.IsVirtual);
        Assert.True(updated.InPerson);

        var deleteResponse = await admin.PostFormWithAntiforgeryAsync("/Locations", "/Locations/Delete", new[]
        {
            new KeyValuePair<string, string>("id", created.Id.ToString())
        });

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.Equal("/Locations", deleteResponse.Headers.Location?.OriginalString);
        Assert.False(await Factory.InDatabaseScopeAsync(async context =>
            await context.Locations.AnyAsync(location => location.Id == created.Id)));
    }

    [Fact]
    public async Task Invalid_location_submission_reopens_the_create_dialog_with_validation()
    {
        using var admin = Factory.CreateAuthenticatedClient("admin-1", RolesConstants.AdminRole);

        var response = await admin.PostFormWithAntiforgeryAsync("/Locations", "/Locations/Create", new[]
        {
            new KeyValuePair<string, string>("Room", string.Empty),
            new KeyValuePair<string, string>("IsVirtual", "false"),
            new KeyValuePair<string, string>("InPerson", "true")
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"location-create-dialog\"", html);
        Assert.Contains("data-dialog-auto-open=\"true\"", html);
        Assert.Contains("The Room field is required.", html);
    }
}
