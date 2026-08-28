using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;

namespace MockInterviews.Services
{
    public class InterviewerSignupService : EntityService<InterviewerSignup>
    {
        private readonly ILogger<InterviewerSignupService> _logger;
        public InterviewerSignupService(MockInterviewsDbContext context, ILogger<InterviewerSignupService> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<Dictionary<string, string?>> GetCheckedInAndAvailableInterviewerIdsWithTypes(IEnumerable<string> busyInterviewers)
        {
            return await _dbSet.Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new {Id = x.InterviewerId, x.Type})
                .ToDictionaryAsync(x => x.Id, x => x.Type);
        }

        public async Task<Dictionary<int, string>> GetInterviewerIdsFromInterviews(Dictionary<int, int> interviews)
        {
            return await _dbSet.Where(x => interviews.Values.Contains(x.Id))
                .Select(x => new {x.Id, x.InterviewerId})
                .ToDictionaryAsync(x => x.Id, x => x.InterviewerId);
        }
    }
}
