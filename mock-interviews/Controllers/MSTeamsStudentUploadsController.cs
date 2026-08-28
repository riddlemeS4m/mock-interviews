using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.MSTeamsStudentUploadsController;

namespace MockInterviews.Controllers
{
    public class MSTeamsStudentUploadsController : Controller
    {
        private readonly MockInterviewsDbContext _context;

        public MSTeamsStudentUploadsController(MockInterviewsDbContext context)
        {
            _context = context;
        }

        // GET: MSTeamsStudentUploads
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> Index()
        {
            return View(await _context.RosteredStudents.ToListAsync());
        }

        // GET: MSTeamsStudentUploads/Details/5
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.RosteredStudents == null)
            {
                return NotFound();
            }

            var mSTeamsStudentUpload = await _context.RosteredStudents
                .FirstOrDefaultAsync(m => m.Id == id);
            if (mSTeamsStudentUpload == null)
            {
                return NotFound();
            }

            return View(mSTeamsStudentUpload);
        }

        // GET: MSTeamsStudentUploads/Create
        [Authorize(Roles = RolesConstants.AdminRole)]

        public IActionResult Create()
        {
            var viewModel = new MSTeamsStudentUploadViewModel();
            return View(viewModel);
        }

        // POST: MSTeamsStudentUploads/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Create(MSTeamsStudentUploadViewModel viewModel)
        {
            var RosterData = viewModel.RosterData;

            if (ModelState.IsValid)
            {
                if (RosterData == null || RosterData.Length == 0)
                {
                    return RedirectToAction("Index", "Home");
                }

                try
                {
                    _context.RosteredStudents.RemoveRange(_context.RosteredStudents);
                    await _context.SaveChangesAsync();

                    var records = new List<RosteredStudent>();

                    using (var stream = RosterData.OpenReadStream())
                    using (var parser = new TextFieldParser(stream))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");

                        while (!parser.EndOfData)
                        {
                            // Read current line as an array of fields
                            var fields = parser.ReadFields();

                            if (fields is { Length: >= 3 })
                            {
                                var record = new RosteredStudent
                                {
                                    MicrosoftId = fields[0],
                                    Email = fields[1],
                                    Name = fields[2]
                                };
                                records.Add(record);
                            }
                        }
                    }

                    var filteredRecords = records
                        .Where(record => record.Email[(record.Email.IndexOf('@') + 1)..] == "crimson.ua.edu")
                        .ToList();

                    // Now, you have a list of Roster objects (records)
                    // You can save them to the database using Entity Framework Core

                    // Example: Save records to the database
                    await _context.RosteredStudents.AddRangeAsync(filteredRecords);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error processing CSV file: {ex.Message}");
                }
            }

            return BadRequest("Something went wrong.");
        }

        public IActionResult UploadMastersStudents()
        {
            var viewModel = new MSTeamsStudentUploadViewModel();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> UploadMastersStudents(MSTeamsStudentUploadViewModel viewModel)
        {
            var RosterData = viewModel.RosterData;

            if (ModelState.IsValid)
            {
                if (RosterData == null || RosterData.Length == 0)
                {
                    return RedirectToAction("Index", "Home");
                }

                try
                {
                    var records = new List<RosteredStudent>();

                    using (var stream = RosterData.OpenReadStream())
                    using (var parser = new TextFieldParser(stream))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");

                        while (!parser.EndOfData)
                        {
                            //expected format is LastName in column 1, FirstName in column 2, and Email in column 3
                            var fields = parser.ReadFields();

                            if (fields is { Length: >= 3 })
                            {
                                var record = new RosteredStudent
                                {
                                    Email = fields[2],
                                    Name = fields[1] + " " + fields[0],
                                    InMasters = true
                                };
                                records.Add(record);
                            }
                        }
                    }

                    foreach(var record in records)
                    {
                        var studentExists = await _context.RosteredStudents.FirstOrDefaultAsync(x => x.Email == record.Email);
                        if (studentExists == null)
                        {
                            if(record.Email != "Email" && record.Email[(record.Email.IndexOf('@') + 1)..] != "crimson.ua.edu")
                            {
                                await _context.RosteredStudents.AddAsync(record);
                            }
                        }
                        else if(studentExists != null)
                        {
                            studentExists.InMasters = true;
                            _context.RosteredStudents.Update(studentExists);
                        }
                    }
                    
                    await _context.SaveChangesAsync();

                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error processing CSV file: {ex.Message}");
                }
            }

            return BadRequest("Something went wrong.");
        }

        public IActionResult Upload221Students()
        {
            var viewModel = new MSTeamsStudentUploadViewModel();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Upload221Students(MSTeamsStudentUploadViewModel viewModel)
        {
            var RosterData = viewModel.RosterData;

            if (ModelState.IsValid)
            {
                if (RosterData == null || RosterData.Length == 0)
                {
                    return BadRequest("Uploaded file was empty.");
                }

                try
                {
                    var records = new List<RosteredStudent>();

                    using (var stream = RosterData.OpenReadStream())
                    using (var parser = new TextFieldParser(stream))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");

                        while (!parser.EndOfData)
                        {
                            // Expected format is LastName in column 1, Firstname in column 2, Username in column 3, and Email in column 7
                            var fields = parser.ReadFields();

                            // This import reads the seventh field as the email address.
                            if (fields is { Length: >= 7 })
                            {
                                var record = new RosteredStudent
                                {
                                    Email = fields[6],
                                    Name = fields[1] + " " + fields[0],
                                    In221 = true
                                };
                                records.Add(record);
                            }
                        }
                    }

                    foreach (var record in records)
                    {
                        var studentExists = await _context.RosteredStudents.FirstOrDefaultAsync(x => x.Email == record.Email);
                        if (studentExists == null)
                        {
                            if (record.Email != "Email" && record.Email[(record.Email.IndexOf('@') + 1)..] != "crimson.ua.edu")
                            {
                                await _context.RosteredStudents.AddAsync(record);
                            }
                        }
                        else if (studentExists != null)
                        {
                            studentExists.In221 = true;
                            _context.RosteredStudents.Update(studentExists);
                        }
                    }

                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error processing CSV file: {ex.Message}");
                }
            }

            return BadRequest("Something went wrong.");
        }

        // GET: MSTeamsStudentUploads/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.RosteredStudents == null)
            {
                return NotFound();
            }

            var mSTeamsStudentUpload = await _context.RosteredStudents.FindAsync(id);
            if (mSTeamsStudentUpload == null)
            {
                return NotFound();
            }
            return View(mSTeamsStudentUpload);
        }

        // POST: MSTeamsStudentUploads/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> Edit(int id, [Bind("Id,MicrosoftId,Email,Name")] RosteredStudent mSTeamsStudentUpload)
        {
            if (id != mSTeamsStudentUpload.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(mSTeamsStudentUpload);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MSTeamsStudentUploadExists(mSTeamsStudentUpload.Id))
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
            return View(mSTeamsStudentUpload);
        }

        // GET: MSTeamsStudentUploads/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.RosteredStudents == null)
            {
                return NotFound();
            }

            var mSTeamsStudentUpload = await _context.RosteredStudents
                .FirstOrDefaultAsync(m => m.Id == id);
            if (mSTeamsStudentUpload == null)
            {
                return NotFound();
            }

            return View(mSTeamsStudentUpload);
        }

        // POST: MSTeamsStudentUploads/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RolesConstants.AdminRole)]

        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mSTeamsStudentUpload = await _context.RosteredStudents.FindAsync(id);
            if (mSTeamsStudentUpload != null)
            {
                _context.RosteredStudents.Remove(mSTeamsStudentUpload);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MSTeamsStudentUploadExists(int id)
        {
          return (_context.RosteredStudents?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        public async Task<IActionResult> AttendanceReportAll()
        {

            return View("AttendanceReportAll");
        }
    }
}
