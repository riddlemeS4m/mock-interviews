using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Constants;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Services
{
    public class InterviewService : EntityService<Interview>
    {
        private readonly ILogger<InterviewService> _logger;
        public InterviewService(MockInterviewsDbContext context,
            ILogger<InterviewService> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<Interview> GetByIdAsync(int id)
        {
            return await _dbSet.Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Include(x => x.Location)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<string>> GetActivelyInterviewingInterviewerIds()
        {
            return await _dbSet.Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing)
                .Select(x => x.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<IEnumerable<Interview>> GetTopNonCompletedInterviews(int numberOfInterviews)
        {
            return await _dbSet.Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => i.Status != StatusConstants.Completed &&
                    i.Status != StatusConstants.NoShow &&
                    i.Status != StatusConstants.Excused &&
                    i.Timeslot.Event.IsActive)
                .OrderBy(i => i.TimeslotId)
                .Take(numberOfInterviews)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves all active interview events, including related location, interviewer timeslot, 
        /// interviewer signups, and associated event details, ordered by timeslot ID.
        /// </summary>
        /// <returns>A collection of active <see cref="Interview"/> entities.</returns>
        public async Task<IEnumerable<Interview>> GetAllActiveInterviews()
        {
            var interviewEvents = await _dbSet
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => i.Timeslot.Event.IsActive
                    && i.Status == StatusConstants.Default)
                .OrderBy(i => i.Timeslot.Time)
                .ToListAsync();
            
            return interviewEvents;
        }

        public async Task<IEnumerable<Interview>> GetAllActiveInterviewsByIds(List<int> ids)
        {
            var interviewEvents = await _dbSet
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => i.Timeslot.Event.IsActive
                    && ids.Contains(i.Id))
                .OrderBy(i => i.Timeslot.Time)
                .ToListAsync();
            
            return interviewEvents;
        }

        public async Task<IEnumerable<Interview>> GetActiveInterviewsForOneStudent(string userId)
        {
            return await _dbSet.Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.StudentId == userId
                    && x.Timeslot.Event.IsActive)
                .ToListAsync();
        }

        public Dictionary<int, string> GetStudentIdsFromInterviews(IEnumerable<Interview> interviews)
        {
            return interviews
                .Select(x => new {x.Id, x.StudentId})
                .ToDictionary(x => x.Id, x => x.StudentId);
        }

        public Dictionary<int, int> GetInterviewSignupIdsFromInterviews(IEnumerable<Interview> interviews)
        {
            return interviews
                .Select(x => new {x.Id, x.InterviewerTimeslot.InterviewerSignupId})
                .ToDictionary(x => x.Id, x => x.InterviewerSignupId);
        }
    }
}