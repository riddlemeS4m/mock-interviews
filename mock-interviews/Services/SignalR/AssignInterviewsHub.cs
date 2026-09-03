using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MockInterviews.Data.Constants;
using MockInterviews.Models.Entities;

namespace MockInterviews.Services.SignalR
{
    [Authorize(Roles = RolesConstants.AdministrationRoles)]
    public class AssignInterviewsHub : Hub
    {
        public async Task SendUpdate(Interview interview, string studentName, string studentClass, string interviewerId, string interviewerName, string time, string date)
        {
            await Clients.All.SendAsync("ReceiveInterviewEventUpdate", interview, studentName, studentClass, interviewerId, interviewerName, time, date);
        }
    }
}
