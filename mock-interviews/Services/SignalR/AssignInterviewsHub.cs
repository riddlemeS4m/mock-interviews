using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MockInterviews.Data.Constants;

namespace MockInterviews.Services.SignalR
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public sealed class AssignInterviewsHub : Hub
    {
    }
}
