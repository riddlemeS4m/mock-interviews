using MockInterviews.Services;
using Moq;

namespace MockInterviews.UnitTests;

public sealed class UserProfileCompletionServiceTests
{
    [Theory]
    [InlineData(null, "User", Classes.SecondSem, "Example", "", true)]
    [InlineData("Test", "User", Classes.SecondSem, null, "interviewer", true)]
    [InlineData("Test", "User", Classes.NotEnrolled, "Example", "student", true)]
    [InlineData("Test", "User", Classes.SecondSem, "Example", "student,interviewer", false)]
    [InlineData("Test", "User", Classes.NotEnrolled, null, "", false)]
    public async Task IsRequiredAsync_enforces_role_specific_profile_fields(
        string? firstName,
        string lastName,
        Classes @class,
        string? company,
        string roles,
        bool expected)
    {
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Class = @class,
            Company = company
        };
        var assignedRoles = roles.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var userManager = CreateUserManager();
        userManager
            .Setup(manager => manager.IsInRoleAsync(user, It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser _, string role) => assignedRoles.Contains(role));
        var service = new UserProfileCompletionService(userManager.Object);

        Assert.Equal(expected, await service.IsRequiredAsync(user));
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager()
        => new(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
}
