using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;

namespace MockInterviews.Services
{
    public class TimeslotService : EntityService<Timeslot>
    {
        private readonly ILogger<TimeslotService> _logger;
        public TimeslotService(MockInterviewsDbContext context,
            ILogger<TimeslotService> logger) : base(context)
        {
            _logger = logger;
        }

        /// <summary>
        ///     Returns enumerable list of timeslots that are not labeled with the specified For221 state.
        /// </summary>
        public async Task<IEnumerable<Timeslot>> GetAvailableStudentsTimeslotsByClass(For221 for221)
        {
            return await _dbSet
                .Where(x => x.IsStudent)
                .Include(y => y.Event)
                .Where(x => _context.Interviews.Count(y => y.TimeslotId == x.Id) < x.MaxSignUps)
                .Where(x => x.Event.For221 != for221
                    && x.Event.IsActive)
                .ToListAsync();
        }

        /// <summary>
        ///     Returns enumerable list of timeslots for all active events.
        /// </summary>
        
        public async Task<IEnumerable<Timeslot>> GetActiveTimeslots()
        {
            return await _dbSet
                .Include(x => x.Event)
                .Where(x => x.Event.IsActive)
                .ToListAsync();
        }

        /// <summary>
        ///     Parameters: bool isStudent, bool isInterviewer <br />
        ///     Returns: enumerable list of timeslots for all active events based on role.
        /// </summary>
        
        public async Task<IEnumerable<Timeslot>> GetActiveTimeslotsByRole(bool isStudent, bool isInterviewer)
        {
            _logger.LogInformation("Getting all active timeslots by role...");

            if(isStudent)
            {
                _logger.LogInformation("Returning all active student timeslots...");
                return await _dbSet
                    .Include(x => x.Event)
                    .Where(x => x.Event.IsActive && x.IsStudent)
                    .ToListAsync();
            }
            else if(isInterviewer)
            {
                _logger.LogInformation("Returning all active interviewer timeslots...");
                return await _dbSet
                    .Include(x => x.Event)
                    .Where(x => x.Event.IsActive && x.IsInterviewer)
                    .ToListAsync();
            }
            else
            {
                _logger.LogInformation("Returning all active volunteer timeslots...");
                return await _dbSet
                    .Include(x => x.Event)
                    .Where(x => x.Event.IsActive && x.IsVolunteer)
                    .ToListAsync();
            }
        }
    }
}