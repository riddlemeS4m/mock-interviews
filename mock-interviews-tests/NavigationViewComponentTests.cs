using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;
using MockInterviews.Models.ViewModels.Shared;
using MockInterviews.ViewComponents;

namespace MockInterviews.UnitTests;

public sealed class NavigationViewComponentTests
{
    [Theory]
    [InlineData("", "Home", "Admin", "home")]
    [InlineData("System", "System", "Index", "system")]
    public void System_admin_navigation_highlights_only_the_current_destination(
        string area,
        string controller,
        string action,
        string activeGroup)
    {
        var component = CreateComponent(area, controller, action);

        var result = Assert.IsType<ViewViewComponentResult>(component.Invoke());
        var groups = Assert.IsAssignableFrom<IReadOnlyList<NavigationGroupViewModel>>(result.ViewData?.Model);

        Assert.True(Assert.Single(groups, group => group.Id == activeGroup).IsActive);
        Assert.All(groups.Where(group => group.Id != activeGroup), group => Assert.False(group.IsActive));
    }

    private static NavigationViewComponent CreateComponent(string area, string controller, string action)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RolesConstants.SystemAdminRole)],
            "Test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var routeData = new RouteData();
        routeData.Values["area"] = area;
        routeData.Values["controller"] = controller;
        routeData.Values["action"] = action;
        var viewContext = new ViewContext { HttpContext = httpContext, RouteData = routeData };

        return new NavigationViewComponent
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = viewContext
            }
        };
    }
}
