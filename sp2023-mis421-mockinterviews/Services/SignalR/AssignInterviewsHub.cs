using Microsoft.AspNetCore.SignalR;
using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Services.SignalR
{
    public class AssignInterviewsHub : Hub
    {
        public async Task SendUpdate(Interview interview, string studentName, string studentClass, string interviewerId, string interviewerName, string time, string date)
        {
            await Clients.All.SendAsync("ReceiveInterviewEventUpdate", interview, studentName, studentClass, interviewerId, interviewerName, time, date);
        }
    }
}
