using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Seeds;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.EventsController;
using MockInterviews.Services;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class EventDatesController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly TimeslotService _timeslotService;
        private readonly EventService _eventService;
        private readonly ILogger<EventDatesController> _logger;

        public EventDatesController(MockInterviewsDbContext context,
            TimeslotService timeslotService,
            EventService eventService,
            ILogger<EventDatesController> logger)
        {
            _context = context;
            _timeslotService = timeslotService;
            _eventService = eventService;
            _logger = logger;
        }

        // GET: EventDates
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Called {method} method...", nameof(Index));
            return View(await _eventService.GetAllAsync());
        }

        // GET: EventDates/Details/5
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Called {method} method...", nameof(Details));

            var @event = await _eventService.GetByIdAsync(id);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // GET: EventDates/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Getting initial {method} view", nameof(Create));
            return View();
        }

        // POST: EventDates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Date,Name,For221,IsActive")] Event EventDate, int MaxSignUps)
        {
            _logger.LogInformation("{method} method was called with a body...", nameof(Create));

            var vm = new EventDateCreationViewModel();

            var value = GetFor221Value(Request.Form);

            if(value < 0)
            {
                ModelState.AddModelError("EventDate.For221", "Please indicate whether the event is for 221.");
                vm.EventDate = EventDate;
                return View(vm);
            }

            if (!ModelState.IsValid)
            {
                vm.EventDate = EventDate;
                return View(vm);
            }

            EventDate.For221 = (For221)For221Constants.GetFor221Int(value);

            var attempt = await _eventService.AddAsync(EventDate);

            if(attempt == null)
            {
                return NotFound();
            }

            TimeslotSeed.MaxSignups = MaxSignUps;
            await TimeslotSeed.SeedTimeslots(_timeslotService, EventDate);
        
            return RedirectToAction("Index","EventDates");
        }

        // GET: EventDates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Getting initial {method} view...", nameof(Edit));
            var @event = await _eventService.GetByIdAsync(id);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: EventDates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Date,Name,IsActive")] Event @event)
        {
            _logger.LogInformation("{method} method was called with a body...", nameof(Edit));

            if (id != @event.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(@event);
            }

            var value = GetFor221Value(Request.Form);

            if(value < 0)
            {
                ModelState.AddModelError("", "Please check indicate whether the event is for 221.");
                return View(@event);
            }

            @event.For221 = (For221)For221Constants.GetFor221Int(value);

            try
            {
                var attempt = await _eventService.UpdateAsync(@event);

                if(attempt == null)
                {
                    return NotFound();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await EventDateExists(@event.Id))
                {
                    return NotFound();
                }
            }

            return RedirectToAction("Index","EventDates");
        }

        // GET: EventDates/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation("Called {method} method...", nameof(Delete));

            var @event = await _eventService.GetByIdAsync(id);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: EventDates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Finalizing event deletion...");

            var @event = await _eventService.GetByIdAsync(id);

            if (@event != null)
            {
                var deleted = await _eventService.DeleteAsync(@event);

                if(!deleted)
                {
                    return NotFound();
                }
            }
            
            return RedirectToAction("Index", "EventDates");
        }

        private async Task<bool> EventDateExists(int id)
        {
            var exists = await _eventService.GetByIdAsync(id);
            return exists != null;  
            //return (_context.Events?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private static int GetFor221Value(IFormCollection form)
        {
            bool isFor221True = form["For221True"].Count > 0;
            bool isFor221False = form["For221False"].Count > 0;

            if (isFor221True && isFor221False)
            {
                return (int)For221.b;
            }
            else if (isFor221True)
            {
                return (int)For221.y;
            }
            else if (isFor221False)
            {
                return (int)For221.n;
            }
            else
            {
                return -1;
            }
        }
    }
}
