using Microsoft.AspNetCore.Mvc;
using MockInterviews.Data.Constants;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.ViewComponents;

public sealed class NavigationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var groups = new List<NavigationGroupViewModel>
        {
            Group("home", "Home", "house", Item("Home", "Home", "Index", matchAllActions: true))
        };

        if (User.IsInRole(RolesConstants.StudentRole) || User.IsInRole(RolesConstants.InterviewerRole))
        {
            var signupItems = new List<NavigationItemViewModel>();
            if (User.IsInRole(RolesConstants.StudentRole))
            {
                signupItems.Add(Item("Student interviews", "InterviewEvents", "Create"));
                signupItems.Add(Item("Volunteer events", "VolunteerEvents", "Create"));
            }

            if (User.IsInRole(RolesConstants.InterviewerRole))
            {
                signupItems.Add(Item("Interviewer timeslots", "SignupInterviewerTimeslots", "Create"));
            }

            groups.Add(Group("signup", "Sign up", "user-plus", signupItems));
        }

        if (User.IsInRole(RolesConstants.AdminRole))
        {
            groups.Add(Group(
                "assignments",
                "Assignments",
                "clipboard-check",
                Item("Assign interviews", "InterviewEvents", "Index"),
                Item("Interviewer check-in", "SignupInterviewers", "Index"),
                Item("Assign rooms", "LocationInterviewers", "Index"),
                Item("Pre-assign interviews", "InterviewEvents", "PreAssignInterviews")));

            groups.Add(Group(
                "events",
                "Events and rooms",
                "calendar-days",
                Item("Volunteers", "VolunteerEvents", "Index"),
                Item("Rooms", "Locations", "Index"),
                Item("Timeslots", "Timeslots", "Index"),
                Item("Dates", "EventDates", "Index"),
                Item("Configuration", "GlobalConfigVars", "Index")));

            groups.Add(Group(
                "roster",
                "People and roles",
                "users",
                Item("Program roster", "MSTeamsStudentUploads", "Index"),
                Item("User roles and deletion", "UserRoles", "Index"),
                Item("Add interviewer roles", "UserRoles", "MassAssign"),
                Item("Upload program roster", "MSTeamsStudentUploads", "Create"),
                Item("Upload MIS 221 roster", "MSTeamsStudentUploads", "Upload221Students")));

            groups.Add(Group(
                "reports",
                "Reports",
                "chart-no-axes-combined",
                Item("Completed interviews", "InterviewEvents", "GetCompletedInterviews"),
                Item("Event statistics", "Reports", "EventStatistics"),
                Item("Allocation report", "Reports", "AllocationReport"),
                Item("Signup report", "Reports", "SignupReport"),
                Item("Lunch report", "SignupInterviewerTimeslots", "LunchReport"),
                Item("Attendance report", "InterviewEvents", "AttendanceReport")));
        }

        var resourceItems = new List<NavigationItemViewModel>
        {
            Item("Resources", "FAQs", "Resources")
        };
        if (User.IsInRole(RolesConstants.AdminRole))
        {
            resourceItems.Add(Item("Manage FAQs", "FAQs", "Index"));
        }

        groups.Add(Group("resources", "Resources", "book-open", resourceItems));

        if (User.IsInRole(RolesConstants.SystemAdminRole))
        {
            groups.Add(Group(
                "system",
                "System administration",
                "settings",
                Item("System administration", "System", "Index", "System", matchAllActions: true)));
        }

        return View(groups);
    }

    private NavigationGroupViewModel Group(
        string id,
        string label,
        string icon,
        params NavigationItemViewModel[] items)
        => Group(id, label, icon, (IReadOnlyList<NavigationItemViewModel>)items);

    private NavigationGroupViewModel Group(
        string id,
        string label,
        string icon,
        IReadOnlyList<NavigationItemViewModel> items)
        => new(id, label, icon, items.Select(SetActiveState).ToList());

    private NavigationItemViewModel SetActiveState(NavigationItemViewModel item)
    {
        var routeArea = ViewContext.RouteData.Values["area"]?.ToString() ?? string.Empty;
        var routeController = ViewContext.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var routeAction = ViewContext.RouteData.Values["action"]?.ToString() ?? string.Empty;

        var areaMatches = string.Equals(item.Area, routeArea, StringComparison.OrdinalIgnoreCase);
        var controllerMatches = string.Equals(item.Controller, routeController, StringComparison.OrdinalIgnoreCase);
        var actionMatches = item.MatchAllActions || string.Equals(item.Action, routeAction, StringComparison.OrdinalIgnoreCase);

        return item with { IsActive = areaMatches && controllerMatches && actionMatches };
    }

    private static NavigationItemViewModel Item(
        string label,
        string controller,
        string action,
        string area = "",
        bool matchAllActions = false)
        => new(label, controller, action, area, matchAllActions);
}
