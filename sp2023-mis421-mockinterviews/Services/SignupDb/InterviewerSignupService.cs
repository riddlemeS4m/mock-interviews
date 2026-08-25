using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.MockInterviewDb;

namespace sp2023_mis421_mockinterviews.Services.SignupDb
{
    public class InterviewerSignupService : GenericSignupDbService<InterviewerSignup>
    {
        private readonly ILogger<InterviewerSignupService> _logger;
        public InterviewerSignupService(MockInterviewsDbContext context, ILogger<InterviewerSignupService> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<Dictionary<string,string>> GetCheckedInAndAvailableInterviewerIdsWithTypes(IEnumerable<string> busyInterviewers)
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