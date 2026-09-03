using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MockInterviews.Data.Constants;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Services.SignalR
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public class AvailableInterviewersHub : Hub
    {
        public async Task SendUpdate(List<AvailableInterviewer> interviewers)
        {
            await Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
        }
    }
}
