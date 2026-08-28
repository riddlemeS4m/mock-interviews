using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;

namespace MockInterviews.Services
{
    public class InterviewerLocationService : EntityService<InterviewerLocation>
    {
        private readonly ILogger<InterviewerLocationService> _logger;
        public InterviewerLocationService(MockInterviewsDbContext context, ILogger<InterviewerLocationService> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<Dictionary<string, string>> GetInterviewersRoomsByIds(IEnumerable<string> userIds)
        {
            var dict = await _dbSet.Where(x => userIds.Contains(x.InterviewerId)
                    && x.Event != null && x.Location != null && x.Event.Date.Date == DateTime.UtcNow.Date)
                // The preceding predicate establishes that Location is present for this projection.
                .Select(x => new { Id = x.InterviewerId, Room = x.Location!.Room })
                .ToDictionaryAsync(x => x.Id, x => x.Room);

            return dict;
        }
    }
}
