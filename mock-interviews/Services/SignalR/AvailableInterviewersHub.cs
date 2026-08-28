using Microsoft.AspNetCore.SignalR;
using MockInterviews.Models.ViewModels.Shared;

namespace MockInterviews.Services.SignalR
{
    public class AvailableInterviewersHub : Hub
    {
        public async Task SendUpdate(List<AvailableInterviewer> interviewers)
        {
            await Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
        }
    }
}
