using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels.ReportsController;
using MockInterviews.Models.ViewModels.TimeslotsController;
using MockInterviews.Services;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class TimeslotsController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TimeslotService _timeslotService;
        private readonly ILogger<TimeslotsController> _logger;

        public TimeslotsController(MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager,
            TimeslotService timeslotService,
            ILogger<TimeslotsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _timeslotService = timeslotService;
            _logger = logger;
        }

        // GET: Timeslots
        public async Task<IActionResult> Index()
        {
            var timeslots = await _context.Timeslots
                .Include(t => t.Event)
                .Where(t => t.Event.IsActive)
                .ToListAsync();
            var eventdates = await _context.Events
                .Where(x => x.IsActive)
                .ToListAsync();

            var countlist = new List<ParticipantCountViewModel>();
            foreach (Timeslot timeslot in timeslots)
            {
                countlist.Add(new ParticipantCountViewModel
                {
                    Timeslot = timeslot,
                    StudentCount = 0,
                    InterviewerCount = 0,
                    VolunteerCount = 0
                });
            }

            var viewModel = new TimeslotViewModel()
            {
                Timeslots = countlist,
                EventDates = eventdates
            };

            return View(viewModel);
        }

        // GET: Timeslots/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Timeslots == null)
            {
                return NotFound();
            }

            var timeslot = await _context.Timeslots.Include(t => t.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (timeslot == null)
            {
                return NotFound();
            }

            var interviewerList = await _context.InterviewerTimeslots
                .Include(x => x.InterviewerSignup)
                .Where(x => x.TimeslotId == timeslot.Id)
                .ToListAsync();
            var interviewerNamesList = new List<string>();
            if (interviewerList != null && interviewerList.Count != 0)
            {
                var interviewerIds = interviewerList
                .Select(x => x.InterviewerSignup.InterviewerId)
                .ToList();
                foreach (var interviewerid in interviewerIds)
                {
                    var interviewer = await _userManager.FindByIdAsync(interviewerid);
                    interviewerNamesList.Add(interviewer is null ? "Deleted user" : interviewer.FirstName + " " + interviewer.LastName);
                }
            }

            var studentList = await _context.Interviews
               .Where(x => x.TimeslotId == timeslot.Id)
               .ToListAsync();
            var studentNamesList = new List<string>();
            if (studentList != null && studentList.Count != 0)
            {
                var studentIds = studentList
                .Select(x => x.StudentId)
                .ToList();
                foreach (var interviewerid in studentIds)
                {
                    var student = await _userManager.FindByIdAsync(interviewerid);
                    studentNamesList.Add(student is null ? "Deleted user" : student.FirstName + " " + student.LastName);
                }
            }

            var volunteerList = await _context.VolunteerTimeslots
               .Where(x => x.TimeslotId == timeslot.Id)
               .ToListAsync();
            var volunteerNamesList = new List<string>();
            if (volunteerList != null && volunteerList.Count != 0)
            {
                var volunteerIds = volunteerList
                .Select(x => x.StudentId)
                .ToList();
                foreach (var interviewerid in volunteerIds)
                {
                    var volunteer = await _userManager.FindByIdAsync(interviewerid);
                    volunteerNamesList.Add(volunteer is null ? "Deleted user" : volunteer.FirstName + " " + volunteer.LastName);
                }
            }

            var viewModel = new TimeslotDetailsViewModel
            {
                Timeslot = timeslot,
                InterviewerNames = interviewerNamesList,
                StudentNames = studentNamesList,
                VolunteerNames = volunteerNamesList
            };

            return View(viewModel);
        }

        // GET: Timeslots/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Timeslots/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Time,IsActive,IsVolunteer,IsInterviewer,IsStudent")] Timeslot timeslot)
        {
            if (ModelState.IsValid)
            {
                _context.Add(timeslot);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(timeslot);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        // GET: Timeslots/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Timeslots == null)
            {
                return NotFound();
            }

            var timeslot = await _context.Timeslots.FindAsync(id);
            if (timeslot == null)
            {
                return NotFound();
            }
            return View(timeslot);
        }

        // POST: Timeslots/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Time,IsActive,IsVolunteer,IsInterviewer,IsStudent,MaxSignUps,EventId")] Timeslot timeslot)
        {
            if (id != timeslot.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(timeslot);
                    await _context.SaveChangesAsync();

                    var pairedtimeslot = await _context.Timeslots.FindAsync(id + 1);
                    if (pairedtimeslot != null)
                    {
                        pairedtimeslot.MaxSignUps = timeslot.MaxSignUps;
                        _context.Update(pairedtimeslot);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TimeslotExists(timeslot.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(TimeslotsController.Index), "Timeslots");
            }
            return View(timeslot);
        }


        [Authorize(Roles = RolesConstants.AdminRole)]
        // GET: Timeslots/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Timeslots == null)
            {
                return NotFound();
            }

            var timeslot = await _context.Timeslots
                .FirstOrDefaultAsync(m => m.Id == id);
            if (timeslot == null)
            {
                return NotFound();
            }

            return View(timeslot);
        }

        // POST: Timeslots/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var timeslot = await _context.Timeslots.FindAsync(id);
            if (timeslot != null)
            {
                _context.Timeslots.Remove(timeslot);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult UpdateMaxTimeslots()
        {
            return View();
        }

        public async Task<IActionResult> UpdateMaxSignupsConfirmed(int maxsignups)
        {
            var timeslots = await _context.Timeslots.ToListAsync();
            foreach (var timeslot in timeslots)
            {
                timeslot.MaxSignUps = maxsignups;
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        private bool TimeslotExists(int id)
        {
            return (_context.Timeslots?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
