using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using MockInterviews.Models.ViewModels;
using MockInterviews.Models.Identity;
using MockInterviews.Models.Entities;
using MockInterviews.Services.SignalR;
using MockInterviews.Data.Contexts;
using MockInterviews.Data.Constants;

namespace MockInterviews.Controllers
{
    public class SignupInterviewersController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly ISendGridClient _sendGridClient;
        private readonly IHubContext<AvailableInterviewersHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SignupInterviewersController> _logger;

        public SignupInterviewersController(MockInterviewsDbContext context,
            ISendGridClient sendGridClient,
            IHubContext<AvailableInterviewersHub> hubContext,
            UserManager<ApplicationUser> userManager,
            ILogger<SignupInterviewersController> logger)
        {
            _context = context;
            _sendGridClient = sendGridClient;
            _hubContext = hubContext;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: SignupInterviewers
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Index()
        {
            var sits = await _context.InterviewerTimeslots
                .Include(s => s.InterviewerSignup)
                .Include(s => s.Timeslot)
                .ThenInclude(s => s.Event)
                .Where(s => s.Timeslot.Event.IsActive)
                .Select(s => s.InterviewerSignupId)
                .Distinct()
                .ToListAsync();

            var sis = await _context.InterviewerSignups
                .Where(s => sits.Contains(s.Id))
                .ToListAsync();

            return View(sis);
        }

        // GET: SignupInterviewers/Details/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.InterviewerSignups == null)
            {
                return NotFound();
            }

            var signupInterviewer = await _context.InterviewerSignups
                .FirstOrDefaultAsync(m => m.Id == id);
            if (signupInterviewer == null)
            {
                return NotFound();
            }

            var vm = new SignupInterviewerViewModel {
                SignupInterviewer = signupInterviewer
            };

            return View(vm);
        }

        // GET: SignupInterviewers/Create
        [Authorize(Roles = RolesConstants.InterviewerRole)]
        public IActionResult Create()
        {
            return View();
        }

        // POST: SignupInterviewers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.InterviewerRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,IsVirtual,InPerson,IsTechnical,IsBehavioral,InterviewerId")] InterviewerSignup signupInterviewer)
        {
            if (ModelState.IsValid)
            {
                _context.Add(signupInterviewer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(signupInterviewer);
        }

        // GET: SignupInterviewers/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.InterviewerSignups == null)
            {
                return NotFound();
            }

            var signupInterviewer = await _context.InterviewerSignups.FindAsync(id);
            if (signupInterviewer == null)
            {
                return NotFound();
            }

            return View(signupInterviewer);
        }

        // POST: SignupInterviewers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,IsVirtual,InPerson,IsTechnical,IsBehavioral,InterviewerId")] InterviewerSignup signupInterviewer)
        {
            if (id != signupInterviewer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(signupInterviewer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SignupInterviewerExists(signupInterviewer.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            return View(signupInterviewer);
        }

        // GET: SignupInterviewers/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.InterviewerSignups == null)
            {
                return NotFound();
            }

            var signupInterviewer = await _context.InterviewerSignups
                .FirstOrDefaultAsync(m => m.Id == id);
            if (signupInterviewer == null)
            {
                return NotFound();
            }

            return View(signupInterviewer);
        }

        // POST: SignupInterviewers/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var signupInterviewer = await _context.InterviewerSignups.FindAsync(id);
            if (signupInterviewer != null)
            {
                _context.InterviewerSignups.Remove(signupInterviewer);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> CheckInInterviewer(int id)
        {
            if (_context.InterviewerSignups == null || id == 0)
            {
                return NotFound();
            }

            var si = await _context.InterviewerSignups.FindAsync(id);

            if(si == null)
            {                 
                return NotFound();  
            }

            try
            {
                si.CheckedIn = !si.CheckedIn;
                _context.Update(si);
                await _context.SaveChangesAsync();

                await UpdateHub();

                //return NoContent();
                return RedirectToAction(nameof(Index));
            } 
            catch
            {
                return BadRequest(new InvalidOperationException("Interviewer was unable to be checked in."));
            }
        }
        private bool SignupInterviewerExists(int id)
        {
          return (_context.InterviewerSignups?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private async Task UpdateHub()
        {
            var busyInterviewers = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                // EF Core parses Include expressions without dereferencing an optional navigation.
                .ThenInclude(x => x!.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing && x.InterviewerTimeslot != null)
                .Select(x => x.InterviewerTimeslot!.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new AvailableInterviewer
                {
                    InterviewerId = x.InterviewerId,
                    InterviewType = x.Type ?? string.Empty,
                })
            .ToListAsync();

            foreach (var iv in interviewers)
            {
                iv.Name = await _userManager.Users
                    .Where(x => x.Id == iv.InterviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync() ?? "Deleted user";

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 8);

                iv.Room = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == iv.InterviewerId &&
                        x.Event != null && x.Event.Date == date && x.Location != null)
                    .Select(x => x.Location!.Room)
                    .FirstOrDefaultAsync() ?? "Not Assigned";
            }

            interviewers.Sort((x, y) => string.Compare(x.Name, y.Name));

            _logger.LogInformation("Requesting all clients to update their available interviewers lists...");
            await _hubContext.Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
            _logger.LogInformation("Requested.");
        }
    }
}
