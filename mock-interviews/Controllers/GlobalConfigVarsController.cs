using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;

namespace MockInterviews.Controllers
{
    [Authorize(Roles = RolesConstants.AdminRole)]
    public class GlobalConfigVarsController : Controller
    {
        private readonly MockInterviewsDbContext _context;

        public GlobalConfigVarsController(MockInterviewsDbContext context)
        {
            _context = context;
        }

        // GET: GlobalConfigVars
        public async Task<IActionResult> Index()
        {
              return View(await _context.Settings.ToListAsync());
        }

        // GET: GlobalConfigVars/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Settings == null)
            {
                return NotFound();
            }

            var globalConfigVar = await _context.Settings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (globalConfigVar == null)
            {
                return NotFound();
            }

            return View(globalConfigVar);
        }

        // GET: GlobalConfigVars/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: GlobalConfigVars/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Value")] Setting globalConfigVar)
        {
            if (ModelState.IsValid)
            {
                _context.Add(globalConfigVar);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(globalConfigVar);
        }

        // GET: GlobalConfigVars/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Settings == null)
            {
                return NotFound();
            }

            var globalConfigVar = await _context.Settings.FindAsync(id);
            if (globalConfigVar == null)
            {
                return NotFound();
            }
            return View(globalConfigVar);
        }

        // POST: GlobalConfigVars/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Value")] Setting globalConfigVar)
        {
            if (id != globalConfigVar.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(globalConfigVar);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GlobalConfigVarExists(globalConfigVar.Id))
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
            return View(globalConfigVar);
        }

        // GET: GlobalConfigVars/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Settings == null)
            {
                return NotFound();
            }

            var globalConfigVar = await _context.Settings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (globalConfigVar == null)
            {
                return NotFound();
            }

            return View(globalConfigVar);
        }

        // POST: GlobalConfigVars/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var globalConfigVar = await _context.Settings.FindAsync(id);
            if (globalConfigVar != null)
            {
                _context.Settings.Remove(globalConfigVar);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<bool> GetBanner()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "disruption_banner");
            var value = banner?.Value ?? throw new InvalidOperationException("Setting 'disruption_banner' does not exist, or it is not an integer.");
            return int.Parse(value) != 0;
        }

        public async Task<bool> GetZoomLinkVisible()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link_visible");
            var value = banner?.Value ?? throw new InvalidOperationException("Setting 'zoom_link_visible' does not exist, or it is not an integer.");
            return int.Parse(value) != 0;
        }

        public async Task<string> GetZoomLink()
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link");
            return banner?.Value ?? throw new InvalidOperationException("Setting 'zoom_link' does not exist.");
        }

        public async Task<IActionResult> SetZoomLink(string link)
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link");
            if (banner is null)
            {
                throw new InvalidOperationException("Setting 'zoom_link' does not exist.");
            }

            banner.Value = link;
            _context.Update(banner);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index","Home");
        }

        public async Task<IActionResult> SetZoomLinkVisible(int display)
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "zoom_link_visible");
            if (banner is null)
            {
                throw new InvalidOperationException("Setting 'zoom_link_visible' does not exist.");
            }

            banner.Value = display.ToString();
            _context.Update(banner);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> SetDisruptionBanner(int display)
        {
            var banner = await _context.Settings.FirstOrDefaultAsync(m => m.Name == "disruption_banner");
            if (banner is null)
            {
                throw new InvalidOperationException("Setting 'disruption_banner' does not exist.");
            }

            banner.Value = display.ToString();
            _context.Update(banner);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        private bool GlobalConfigVarExists(int id)
        {
          return _context.Settings.Any(e => e.Id == id);
        }

    }
}
