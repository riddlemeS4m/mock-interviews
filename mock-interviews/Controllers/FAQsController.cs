using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Email;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.Identity;
using MockInterviews.Options;

namespace MockInterviews.Controllers
{
    public class FAQsController : Controller
    {
        private readonly MockInterviewsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailTransport _emailTransport;
        private readonly ILogger<FAQsController> _logger;
        private readonly string _superUserEmail;
        public FAQsController(MockInterviewsDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailTransport emailTransport,
            ILogger<FAQsController> logger,
            IOptions<SuperUserOptions> superUserOptions)
        {
            _context = context;
            _userManager = userManager;
            _emailTransport = emailTransport;
            _logger = logger;
            _superUserEmail = superUserOptions.Value.Email;
        }

        // GET: FAQs
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Questions.ToListAsync());
        }
        public async Task<IActionResult> Resources()
        {
            var manualUrl = await _context.Settings
                .Where(x => x.Name == SettingsConstants.MockInterviewManual.Name)
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            var parkingPassUrl = await _context.Settings
                .Where(x => x.Name == SettingsConstants.GuestParkingPass.Name)
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            ViewData["ManualUrl"] = GetPublicAssetUrl(manualUrl);
            ViewData["ParkingPassUrl"] = GetPublicAssetUrl(parkingPassUrl);

            return View(await _context.Questions.Where(x => x.A != null).ToListAsync());
        }

        // GET: FAQs/Details/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Questions == null)
            {
                return NotFound();
            }

            var fAQs = await _context.Questions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fAQs == null)
            {
                return NotFound();
            }

            return View(fAQs);
        }

        // GET: FAQs/Create
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.StudentRole + "," + RolesConstants.InterviewerRole)]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.StudentRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Create([Bind("Id, Question, A")] Question faq)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId is null ? null : await _userManager.FindByIdAsync(userId);

            if (ModelState.IsValid)
            {

                _context.Add(faq);
                await _context.SaveChangesAsync();
                if (User.IsInRole(RolesConstants.AdminRole))
                {
                    return RedirectToAction("Index", "Question");
                }
                else
                {
                    ASendAnEmail emailer = new NewFAQSubmitted();
                    await emailer.SendEmailAsync(_emailTransport, _superUserEmail, "A Required: Student Submitted New Question", _superUserEmail, user is null ? "Deleted user" : user.FirstName + " " + user.LastName, faq.Q, null);

                    return RedirectToAction("Resources", "Question");
                }

            }
            return View(faq);
        }

        // GET: FAQs/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Questions == null)
            {
                return NotFound();
            }

            var fAQs = await _context.Questions.FindAsync(id);
            if (fAQs == null)
            {
                return NotFound();
            }
            return View(fAQs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Edit(int id, [Bind("Id, Q, A")] Question faq)
        {
            if (id != faq.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(faq);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FAQsExists(faq.Id))
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
            return View(faq);
        }

        // GET: FAQs/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Questions == null)
            {
                return NotFound();
            }

            var fAQs = await _context.Questions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fAQs == null)
            {
                return NotFound();
            }

            return View(fAQs);
        }

        // POST: FAQs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fAQs = await _context.Questions.FindAsync(id);
            if (fAQs != null)
            {
                _context.Questions.Remove(fAQs);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private static string? GetPublicAssetUrl(string? assetUrl)
        {
            if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

        private bool FAQsExists(int id)
        {
            return (_context.Questions?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
