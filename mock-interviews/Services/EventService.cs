using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;

namespace MockInterviews.Services
{
    public class EventService : EntityService<Event>
    {
        private readonly ILogger<EventService> _logger;
        public EventService(MockInterviewsDbContext context,
            ILogger<EventService> logger) : base(context)
        {
            _logger = logger;
        }

        public override async Task<IEnumerable<Event>> GetAllAsync()
        {
            _logger.LogInformation("Getting all events...");
            return await base.GetAllAsync();
        }

        public async Task<Event?> GetByIdAsync(int id)
        {
            _logger.LogInformation("Getting event with id {id}...", id);
            var @event = await base.GetByIdAsync(id);

            if (@event == null)
            {
                _logger.LogWarning("Event with id {id} not found", id);
                return null;
            }

            _logger.LogInformation("Event with id {id} found", id);
            _logger.LogInformation(@event.ToString());
            return @event;
        }

        public override async Task<Event> AddAsync(Event @event)
        {
            _logger.LogInformation("Adding event...");
            var eventAttempt = await base.AddAsync(@event);

            _logger.LogInformation("Event added successfully");
            _logger.LogInformation(eventAttempt.ToString());
            return eventAttempt;
        }

        public override async Task<Event> UpdateAsync(Event @event)
        {
            _logger.LogInformation("Updating event with id {id}...", @event.Id);
            var updatedEvent = await base.UpdateAsync(@event);

            _logger.LogInformation("Event with id {id} updated successfully", @event.Id);
            _logger.LogInformation(updatedEvent.ToString());
            return updatedEvent;
        }

        public async Task<bool> DeleteAsync(Event @event)
        {
            _logger.LogInformation("Deleting event with id {id}...", @event.Id);
            var deleted = await base.DeleteAsync(@event.Id);

            if (!deleted)
            {
                _logger.LogWarning("Event with id {id} not found", @event.Id);
                return false;
            }

            _logger.LogInformation("Event with id {id} deleted successfully", @event.Id);
            return true;
        }
    }
}
