using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Contexts;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.MSTeamsStudentUploadsController;

namespace MockInterviews.Controllers;

[Authorize(Roles = RolesConstants.AdministrationRoles)]
public class MSTeamsStudentUploadsController(MockInterviewsDbContext context, ILogger<MSTeamsStudentUploadsController> logger) : Controller
{
    // GET: MSTeamsStudentUploads
    public async Task<IActionResult> Index()
        => View(new RosterIndexViewModel(await context.RosteredStudents.AsNoTracking().OrderBy(student => student.Name).ToListAsync()));

    // GET: MSTeamsStudentUploads/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        var student = id is null ? null : await context.RosteredStudents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return student is null ? NotFound() : View(student);
    }

    // GET: MSTeamsStudentUploads/Create
    public IActionResult Create() => View(new MSTeamsStudentUploadViewModel());

    // POST: MSTeamsStudentUploads/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MSTeamsStudentUploadViewModel input)
    {
        var records = await ParsePrimaryRosterAsync(input.RosterData);
        if (records is null)
        {
            return View(input);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        context.RosteredStudents.RemoveRange(context.RosteredStudents);
        await context.RosteredStudents.AddRangeAsync(records);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["StatusMessage"] = $"Program roster replaced with {records.Count} students.";
        return RedirectToAction(nameof(Index));
    }

    [NonAction]
    public IActionResult UploadMastersStudents() => NotFound();

    // GET: MSTeamsStudentUploads/Upload221Students
    public IActionResult Upload221Students() => View(new MSTeamsStudentUploadViewModel());

    // POST: MSTeamsStudentUploads/Upload221Students
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload221Students(MSTeamsStudentUploadViewModel input)
    {
        var records = await Parse221RosterAsync(input.RosterData);
        if (records is null)
        {
            return View(input);
        }

        var existingStudents = await context.RosteredStudents.ToListAsync();
        var byEmail = existingStudents.ToDictionary(student => NormalizeEmail(student.Email), StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (byEmail.TryGetValue(NormalizeEmail(record.Email), out var existing))
            {
                existing.In221 = true;
            }
            else
            {
                context.RosteredStudents.Add(record);
                byEmail.Add(NormalizeEmail(record.Email), record);
            }
        }
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"MIS 221 membership was added for {records.Count} students. Existing MIS 221 flags were retained.";
        return RedirectToAction(nameof(Index));
    }

    // GET: MSTeamsStudentUploads/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        var student = id is null ? null : await context.RosteredStudents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return student is null ? NotFound() : View(RosterStudentEditViewModel.FromStudent(student));
    }

    // POST: MSTeamsStudentUploads/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RosterStudentEditViewModel input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), "A student name is required.");
        }

        var student = await context.RosteredStudents.SingleOrDefaultAsync(item => item.Id == id);
        if (student is null)
        {
            return NotFound();
        }

        var normalizedEmail = NormalizeEmail(input.Email);
        if (await context.RosteredStudents.AnyAsync(item => item.Id != id && item.Email.ToUpper() == normalizedEmail))
        {
            ModelState.AddModelError(nameof(input.Email), "Another roster record already uses this email address.");
        }
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        student.Email = input.Email.Trim().ToLowerInvariant();
        student.Name = input.Name.Trim();
        student.In221 = input.In221;
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Roster record was updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET: MSTeamsStudentUploads/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        var student = id is null ? null : await context.RosteredStudents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        return student is null ? NotFound() : View(student);
    }

    // POST: MSTeamsStudentUploads/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await context.RosteredStudents.SingleOrDefaultAsync(item => item.Id == id);
        if (student is null)
        {
            TempData["ErrorMessage"] = "That roster record no longer exists.";
            return RedirectToAction(nameof(Index));
        }
        context.RosteredStudents.Remove(student);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Roster record was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<RosteredStudent>?> ParsePrimaryRosterAsync(IFormFile? file)
    {
        var rows = await ReadRowsAsync(file, 3, "Program roster files need Microsoft ID, email, and name columns.");
        if (rows is null) return null;
        var records = new List<RosteredStudent>();
        foreach (var row in rows)
        {
            if (IsHeader(row[1])) continue;
            if (!TryGetEmail(row[1], out var email) || string.IsNullOrWhiteSpace(row[2]))
            {
                ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "Every program roster row needs a valid email address and name.");
                return null;
            }
            records.Add(new RosteredStudent { MicrosoftId = row[0].Trim(), Email = email, Name = row[2].Trim() });
        }
        return ValidateDuplicates(records);
    }

    private async Task<List<RosteredStudent>?> Parse221RosterAsync(IFormFile? file)
    {
        var rows = await ReadRowsAsync(file, 7, "MIS 221 roster files need at least seven columns, with email in column seven.");
        if (rows is null) return null;
        var records = new List<RosteredStudent>();
        foreach (var row in rows)
        {
            if (IsHeader(row[6])) continue;
            if (!TryGetEmail(row[6], out var email) || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
            {
                ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "Every MIS 221 roster row needs first name, last name, and a valid email address.");
                return null;
            }
            records.Add(new RosteredStudent { Email = email, Name = $"{row[1].Trim()} {row[0].Trim()}", In221 = true });
        }
        return ValidateDuplicates(records);
    }

    private async Task<List<string[]>?> ReadRowsAsync(IFormFile? file, int columnCount, string columnError)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "Choose a non-empty CSV file.");
            return null;
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "Choose a CSV file.");
            return null;
        }

        try
        {
            var rows = new List<string[]>();
            using var stream = file.OpenReadStream();
            using var parser = new TextFieldParser(stream) { TextFieldType = FieldType.Delimited, TrimWhiteSpace = false };
            parser.SetDelimiters(",");
            while (!parser.EndOfData)
            {
                var row = parser.ReadFields();
                if (row is null || row.All(string.IsNullOrWhiteSpace)) continue;
                if (row.Length < columnCount)
                {
                    ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), columnError);
                    return null;
                }
                rows.Add(row);
            }
            if (rows.Count == 0)
            {
                ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "The CSV file did not contain any roster rows.");
                return null;
            }
            return rows;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to read uploaded roster file {FileName}", file.FileName);
            ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), "The CSV file could not be read.");
            return null;
        }
    }

    private List<RosteredStudent>? ValidateDuplicates(List<RosteredStudent> records)
    {
        var duplicate = records.GroupBy(record => NormalizeEmail(record.Email), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            ModelState.AddModelError(nameof(MSTeamsStudentUploadViewModel.RosterData), $"The CSV contains duplicate email address '{duplicate.First().Email}'.");
            return null;
        }
        return records;
    }

    private static bool IsHeader(string value) => string.Equals(value.Trim(), "Email", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetEmail(string value, out string email)
    {
        var trimmed = value.Trim();
        var valid = System.Net.Mail.MailAddress.TryCreate(trimmed, out var parsed) && parsed.Address == trimmed;
        email = valid ? trimmed.ToLowerInvariant() : string.Empty;
        return valid;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
