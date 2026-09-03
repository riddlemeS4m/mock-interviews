using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.SignupInterviewersController;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Controllers
{
    public class SignupInterviewersController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly IHubContext<AssignInterviewsHub> _hubContext;

        public SignupInterviewersController(MockInterviewsDbContext context,
            IHubContext<AssignInterviewsHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: SignupInterviewers
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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

            var vm = new SignupInterviewerViewModel
            {
                SignupInterviewer = signupInterviewer
            };

            return View(vm);
        }

        // GET: SignupInterviewers/Edit/5
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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
        [Authorize(Roles = RolesConstants.AdministrationRoles)]
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

        [Authorize(Roles = RolesConstants.AdministrationRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInInterviewer(int id)
        {
            if (_context.InterviewerSignups == null || id == 0)
            {
                return NotFound();
            }

            var si = await _context.InterviewerSignups.FindAsync(id);

            if (si == null)
            {
                return NotFound();
            }

            try
            {
                si.CheckedIn = !si.CheckedIn;
                _context.Update(si);
                await _context.SaveChangesAsync();

                await UpdateHub();

                TempData["StatusMessage"] = si.CheckedIn ? "Interviewer checked in." : "Interviewer checked out.";
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

        private Task UpdateHub()
            => _hubContext.Clients.All.SendAsync("BoardChanged");
    }
}
