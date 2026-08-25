using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Constants;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.MockInterviewDb;

namespace sp2023_mis421_mockinterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class EmailTemplatesController : Controller
    {
        private readonly MockInterviewsDbContext _context;

        public EmailTemplatesController(MockInterviewsDbContext context)
        {
            _context = context;
        }

        // GET: EmailTemplates
        public async Task<IActionResult> Index()
        {
              return View(await _context.EmailTemplates.ToListAsync());
        }

        // GET: EmailTemplates/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.EmailTemplates == null)
            {
                return NotFound();
            }

            var emailTemplate = await _context.EmailTemplates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (emailTemplate == null)
            {
                return NotFound();
            }

            return View(emailTemplate);
        }

        // GET: EmailTemplates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: EmailTemplates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SubjectLine,Body")] EmailTemplate emailTemplate)
        {
            if (ModelState.IsValid)
            {
                _context.Add(emailTemplate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(emailTemplate);
        }

        // GET: EmailTemplates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.EmailTemplates == null)
            {
                return NotFound();
            }

            var emailTemplate = await _context.EmailTemplates.FindAsync(id);
            if (emailTemplate == null)
            {
                return NotFound();
            }
            return View(emailTemplate);
        }

        // POST: EmailTemplates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SubjectLine,Body")] EmailTemplate emailTemplate)
        {
            if (id != emailTemplate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(emailTemplate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmailTemplateExists(emailTemplate.Id))
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
            return View(emailTemplate);
        }

        // GET: EmailTemplates/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.EmailTemplates == null)
            {
                return NotFound();
            }

            var emailTemplate = await _context.EmailTemplates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (emailTemplate == null)
            {
                return NotFound();
            }

            return View(emailTemplate);
        }

        // POST: EmailTemplates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.EmailTemplates == null)
            {
                return Problem("Entity set 'MockInterviewDataDbContext.EmailTemplates'  is null.");
            }
            var emailTemplate = await _context.EmailTemplates.FindAsync(id);
            if (emailTemplate != null)
            {
                _context.EmailTemplates.Remove(emailTemplate);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EmailTemplateExists(int id)
        {
          return _context.EmailTemplates.Any(e => e.Id == id);
        }
    }
}
