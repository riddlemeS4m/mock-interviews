using Microsoft.AspNetCore.SignalR;
using sp2023_mis421_mockinterviews.Models.Entities;
using sp2023_mis421_mockinterviews.Models.ViewModels;

namespace sp2023_mis421_mockinterviews.Services.SignalR
{
    public class AvailableInterviewersHub : Hub
    {
        public async Task SendUpdate(List<AvailableInterviewer> interviewers)
        {
            await Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
        }
    }
}
