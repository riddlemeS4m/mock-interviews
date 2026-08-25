using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.MockInterviewDb;
using sp2023_mis421_mockinterviews.Services.GoogleDrive;

namespace sp2023_mis421_mockinterviews.Data.Seeds
{
    public class GoogleDriveServiceSeed
    {
        public const string Manual = "Manual";
        public const string ParkingPass = "Parking";
        public const string ManualConfigVar = "mock_interview_manual";
        public const string ParkingPassConfigVar = "guest_parking_pass";
        private const string ParkingPassFilePath = "wwwroot/lib/GuestParking_Spring2024.pdf";
        private const string ManualFilePath = "wwwroot/lib/MockInterviewManual_Spring2024.docx";
        private readonly GoogleDriveSiteContentService _driveService;
        private readonly MockInterviewsDbContext _context;
        public GoogleDriveServiceSeed(GoogleDriveSiteContentService siteContentDriveService, 
            MockInterviewsDbContext context)
        {
            _driveService = siteContentDriveService;
            _context = context;
        }
        public async Task Test()
        {
            try
            {
                var iManualExists = await _driveService.GetAllFileIdsFromQuery(Manual);
                var manualExists = iManualExists.ToList();

                if (manualExists.Count == 0)
                {
                    await _driveService.UploadFileFromFilePath(ManualFilePath);
                }

                var iParkingPassExists = await _driveService.GetAllFileIdsFromQuery(ParkingPass);
                var parkingPassExists = iParkingPassExists.ToList();

                if (parkingPassExists.Count == 0)
                {
                    await _driveService.UploadFileFromFilePath(ParkingPassFilePath);
                }
            }
            catch (Exception)
            {
                throw;
            }

            await SeedSiteContentConfigVars();
        }

        public async Task SeedSiteContentConfigVars()
        {
            try
            {
                var manualExists = await _context.Settings
                    .Where(x => x.Name == ManualConfigVar)
                    .FirstOrDefaultAsync();

                if (manualExists == null)
                {
                    var iManualId = await _driveService.GetAllFileIdsFromQuery(Manual);
                    var manualId = iManualId.FirstOrDefault() ?? throw new Exception("Uploading manual to Google Drive failed.");

                    await _context.Settings.AddAsync(new Setting
                    {
                        Name = ManualConfigVar,
                        Value = manualId
                    });
                }

                var parkingPassExists = await _context.Settings
                    .Where(x => x.Name == ParkingPassConfigVar)
                    .FirstOrDefaultAsync();

                if (parkingPassExists == null)
                {
                    var iParkingPassId = await _driveService.GetAllFileIdsFromQuery(ParkingPass);
                    var parkingPassId = iParkingPassId.FirstOrDefault() ?? throw new Exception("Uploading parking pass to Google Drive failed.");

                    await _context.Settings.AddAsync(new Setting
                    {
                        Name = ParkingPassConfigVar,
                        Value = parkingPassId
                    });
                }


                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
