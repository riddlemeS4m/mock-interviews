using System.Globalization;
using System.Text;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.InterviewsController;
using MockInterviews.Models.Identity;
using MockInterviews.Models.ViewModels;
using MockInterviews.Data.Constants;
using MockInterviews.Data.Access.Emails;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Data.Contexts;
using MockInterviews.Services;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Controllers
{
    public class IVM
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public bool Technical { get; set; }
        public bool Behavioral { get; set; }
    }

    public class AVM
    {
        public List<SelectListItem> BehavioralInterviewers { get; set; }
        public List<SelectListItem> TechnicalInterviewers { get; set; }
    }

    public class PAVM
    {
        public List<SelectListItem> AvailableInterviewers { get; set; }
        public Interview Student { get; set; }
        public string StudentName { get; set; }
        public string StudentClass { get; set; }
    }

    public class PARVM

    {
        public string InterviewEventId { get; set; }
        public string SelectedValue { get; set; }
    }
    
    public class IVMComparer : IEqualityComparer<IVM>
    {
        public bool Equals(IVM x, IVM y)
        {
            if (x == null || y == null)
                return false;

            return x.Name == y.Name &&
                   x.Id == y.Id &&
                   x.Technical == y.Technical &&
                   x.Behavioral == y.Behavioral;
        }

        public int GetHashCode(IVM obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + obj.Name.GetHashCode();
                hash = hash * 23 + obj.Id.GetHashCode();
                hash = hash * 23 + obj.Technical.GetHashCode();
                hash = hash * 23 + obj.Behavioral.GetHashCode();
                return hash;
            }
        }
    }

    public class EditInlineResponse
    {
        public string StudentName { get; set; }
        public string InterviewType { get; set; }
        public string InterviewerName { get; set; }
        public string Location { get; set; }
    }

    public class InterviewEventsController : Controller
    {
        private readonly IManageInterviews _manager;
        private readonly MockInterviewsDbContext _context;
        private readonly TimeslotService _timeslotService;
        private readonly InterviewService _interviewService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserService _userService;
        private readonly ISendGridClient _sendGridClient;
        private readonly IHubContext<AssignInterviewsHub> _hubContext;
        private readonly IHubContext<AvailableInterviewersHub> _hubContextInterviewer;
        private readonly ILogger<InterviewEventsController> _logger;

        public InterviewEventsController(IManageInterviews manager,
            MockInterviewsDbContext context,
            TimeslotService timeslotService,
            InterviewService interviewService,
            UserManager<ApplicationUser> userManager, 
            UserService userService,
            ISendGridClient sendGridClient,
            IHubContext<AssignInterviewsHub> hubContext,
            IHubContext<AvailableInterviewersHub> hubContextInterviewer,
            ILogger<InterviewEventsController> logger)
        {
            _manager = manager;
            _context = context;
            _timeslotService = timeslotService;
            _interviewService = interviewService;
            _userManager = userManager;
            _userService = userService;
            _sendGridClient = sendGridClient;
            _hubContext = hubContext;
            _hubContextInterviewer = hubContextInterviewer;
            _logger = logger;
        }
	    // adding a dummy comment bc I feel like it
        //--Dalton Wright, Fall 2023

        // GET: InterviewEvents
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Index()
        {         
            //var interviewers = new List<AvailableInterviewer>();
            var busyInterviewers = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing)
                .Select(x => x.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new AvailableInterviewer
                {
                    InterviewerId = x.InterviewerId,
                    InterviewType = x.Type,
                })
                .ToListAsync();

            foreach (var iv in interviewers)
            {
                iv.Name = await _userManager.Users
                    .Where(x => x.Id == iv.InterviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync();

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 8);

                iv.Room = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == iv.InterviewerId &&
                        x.Event.Date.Date == date)
                    .Select(x => x.Location.Room)
                    .FirstOrDefaultAsync() ?? "Not Assigned";               
            }

            interviewers.Sort((x, y) => string.Compare(x.Name, y.Name));
            
            var hoursInAdvanceStr = _context.Settings
                .Where(x => x.Name == "interview_index_hours")
                .Select(x => x.Value)
                .FirstOrDefault();

            var hoursInAdvance = 3;
            try
            {
                hoursInAdvance = int.Parse(hoursInAdvanceStr);
            } catch
            {
                throw new Exception("Setting 'interview_index_hours' is not an integer.");
            }

            var timeslot = await _context.Timeslots
                .OrderByDescending(x => x.MaxSignUps)
                .FirstOrDefaultAsync();
            var maxsignups = timeslot.MaxSignUps * hoursInAdvance * 2; //* 2 because there are two interviews per hour
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => i.Status != StatusConstants.Completed && 
                    i.Status != StatusConstants.NoShow && 
                    i.Status != StatusConstants.Excused &&
                    i.Timeslot.Event.IsActive)
                .OrderBy(i => i.TimeslotId)
                .ThenBy(i => i.Id)
                .Take(maxsignups)
                .ToListAsync();

            // 1. Collect unique StudentIds
            var studentIds = interviewEvents
                .Select(ie => ie.StudentId)
                .Distinct()
                .ToList();

            // 2. Fetch student names for all unique StudentIds
            var students = await _userManager.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}", u.Class })
                .ToListAsync();

            var studentNames = students
                .Select(u => new { u.Id, u.FullName })
                .ToDictionary(u => u.Id, u => u.FullName);

            var studentClasses = students
                .Select(u => new { u.Id, Class = ClassConstants.GetClassText(u.Class) })
                .ToDictionary(u => u.Id, u => u.Class);

            //3. Collect unique InterviewerIds
            var interviewerIds = await _context.InterviewerSignups
                .Select(ie => ie.InterviewerId)
                .Distinct()
                .ToListAsync();

            // 4. Fetch interviewer names for all unique InterviewerIds
            var interviewerNames = await _userManager.Users
                .Where(u => interviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}" })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            var model = new InterviewEventIndexViewModel();
            var eventslist = new List<InterviewEventViewModel>();

            foreach(Interview interviewEvent in interviewEvents)
            {
                var interviewEventViewModel = new InterviewEventViewModel();

                if (studentNames.TryGetValue(interviewEvent.StudentId, out var studentName))
                {
                    interviewEventViewModel.StudentName = studentName;
                }

                if(studentClasses.TryGetValue(interviewEvent.StudentId, out var studentClass))
                {
                    interviewEventViewModel.Class = studentClass;
                }

                if (interviewEvent.InterviewerTimeslot != null)
                {
                    if (interviewerNames.TryGetValue(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId, out var interviewerName))
                    {
                        interviewEventViewModel.InterviewerName = interviewerName;
                    }

                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
                else
                {
                    interviewEventViewModel.InterviewerName = "Not Assigned";
                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
            }

            model.Interviews = eventslist;
            model.AvailableInterviewers = interviewers;

            model.TechnicalInterviewers = new();
            model.BehavioralInterviewers = new();

            return View(model);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> AttendanceReport()
        {
            //can't find my other attendance report method for some reason
            var uniqueStudentIds = await _context.Interviews
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();
            
            if(uniqueStudentIds.Count == 0 || uniqueStudentIds == null)
            {
                return BadRequest("There are no students signed up yet.");
            }

            var students = await _userManager.Users
                .Where(u => uniqueStudentIds.Contains(u.Id))
                .Select(u => new
                {
                    u.FirstName,
                    u.LastName,
                    u.Class // Replace with the actual property name
                })
                .ToListAsync();
            
            var classReports = students
                .GroupBy(x => x.Class)
                .Select(g => new ClassReport
                {
                    ClassName = ClassConstants.GetClassText((Classes)g.Key),
                    StudentCount = g.Count()
                })
                .Where(r => r.StudentCount > 0)
                .ToList();

            var total = classReports
                .Select(x => x.StudentCount)
                .Sum();

            var signedup221 = classReports
                .Where(x => x.ClassName == ClassConstants.GetClassText(Classes.FirstSem))
                .Select(x => x.StudentCount)
                .FirstOrDefault();

            var summaries = new List<ClassReport>
            {
                new ClassReport
                {
                    ClassName = "Total",
                    StudentCount = total
                }
            };

            var entireProgram = await _context.RosteredStudents.CountAsync();
            var entire221 = await _context.RosteredStudents.Where(x => x.In221 == true).CountAsync();

            double percentEntireProgram = (double)total / entireProgram;
            double percentEntire221 = (double)signedup221 / entire221;

            // Round to two decimal places
            percentEntireProgram = Math.Round(percentEntireProgram, 2) * 100;
            percentEntire221 = Math.Round(percentEntire221, 2) * 100;

            summaries.Add(new ClassReport
            {
                ClassName = "Total % Signed Up",
                StudentCount = (int)percentEntireProgram
            });
            summaries.Add(new ClassReport
            {
                ClassName = "221 % Signed Up",
                StudentCount = (int)percentEntire221
            });


            var viewModel = new AttendanceReportViewModel()
            {
                ClassReports = classReports,
                SummaryStats = summaries
            };

            return View("AttendanceReport", viewModel);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> AssessFeedback()
        {
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .ToListAsync();

            var model = new List<InterviewEventViewModel>();
            var interviewEventViewModel = new InterviewEventViewModel();
            foreach (Interview interviewEvent in interviewEvents)
            {
                var student = await _userManager.FindByIdAsync(interviewEvent.StudentId);

                if (interviewEvent.InterviewerTimeslot != null)
                {
                    var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);

                    interviewEventViewModel = new InterviewEventViewModel
                    {
                        InterviewEvent = interviewEvent,
                        StudentName = student.FirstName + " " + student.LastName,
                        InterviewerName = interviewer.FirstName + " " + interviewer.LastName
                    };

                    model.Add(interviewEventViewModel);
                }
                else
                {
                    interviewEventViewModel = new InterviewEventViewModel
                    {
                        InterviewEvent = interviewEvent,
                        StudentName = student.FirstName + " " + student.LastName,
                        Class = ClassConstants.GetClassText(student.Class),
                        InterviewerName = "Not Assigned"
                    };

                    model.Add(interviewEventViewModel);
                }
            }

            return View("Feedback",model);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> FeedbackIndex()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userFull = await _userManager.FindByIdAsync(userId);

            var model = new IndexViewModel();
            model.StudentScheduledInterviews = new List<InterviewEventViewModel>();
            if (User.IsInRole(RolesConstants.AdminRole) || User.IsInRole(RolesConstants.StudentRole))
            {
                var interviewEvents = await _context.Interviews
                    .Include(v => v.InterviewerTimeslot)
                    .ThenInclude(v => v.InterviewerSignup)
                    .Include(v => v.Location)
                    .Include(v => v.Timeslot)
                    .ThenInclude(v => v.Event)
                    .Where(v => v.StudentId == userId && v.Status == StatusConstants.Completed)
                    .ToListAsync();

                if (interviewEvents != null)
                {
                    foreach (Interview interviewEvent in interviewEvents)
                    {
                        if (interviewEvent.InterviewerTimeslot != null)
                        {
                            var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);

                            model.StudentScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = $"{userFull.FirstName} {userFull.LastName}",
                                Class = ClassConstants.GetClassText(userFull.Class),
                                InterviewerName = $"{interviewer.FirstName} {interviewer.LastName}"
                            });
                        }
                        else
                        {
                            model.StudentScheduledInterviews.Add(new InterviewEventViewModel()
                            {
                                InterviewEvent = interviewEvent,
                                StudentName = $"{userFull.FirstName} {userFull.LastName}",
                                Class = ClassConstants.GetClassText(userFull.Class),
                                InterviewerName = "Not Assigned"
                            });
                        }
                    }
                }
            }

            return View("FeedbackIndex", model);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> ProvideFeedback(int id)
        {
            var interviewEvent = await _context.Interviews
                .Include(x=>x.InterviewerTimeslot)
                .ThenInclude(x=>x.InterviewerSignup)
                .Include(x => x.Location)
                .Include(x=>x.Timeslot)
                .ThenInclude(x=>x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);
            var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);
            var model = new InterviewEventViewModel()
            {
                InterviewEvent = interviewEvent,
                InterviewerName = $"{interviewer.FirstName} {interviewer.LastName}"
            };

            return View("ProvideFeedback", model);
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        public async Task<IActionResult> ProvideFeedback(int id, [Bind("Id,StudentId,TimeslotId,LocationId,Status,Type,InterviewerTimeslotId,InterviewerRating,InterviewerFeedback,ProcessFeedback")] Interview interviewEvent)
        {
            if (id != interviewEvent.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(interviewEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterviewEventExists(interviewEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction("FeedbackIndex","InterviewEvents");
            }


            var interviewEventActual = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Include(x => x.Location)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);
            var interviewer = await _userManager.FindByIdAsync(interviewEventActual.InterviewerTimeslot.InterviewerSignup.InterviewerId);
            var model = new InterviewEventViewModel()
            {
                InterviewEvent = interviewEventActual,
                InterviewerName = $"{interviewer.FirstName} {interviewer.LastName}"
            };

            return View("ProvideFeedback", model);
        }


        // GET: InterviewEvents/Details/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            var student = await _userManager.Users
                .Where(x => x.Id == interviewEvent.StudentId)
                .Select(x => new {x.FirstName, x.LastName, x.Class})
                .FirstOrDefaultAsync();

            if(interviewEvent.InterviewerTimeslot == null)
            {
                var viewModel = new InterviewEventViewModel
                {
                    InterviewEvent = interviewEvent,
                    InterviewerName = "Not Assigned",
                    Class = ClassConstants.GetClassText(student.Class),
                    StudentName = student.FirstName + " " + student.LastName
                };

                return View(viewModel);
            }


            var interviewer = await _userManager.Users
                .Where(x => x.Id == interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Select(x => new { x.FirstName, x.LastName })
                .FirstOrDefaultAsync();

            var secondViewModel = new InterviewEventViewModel
            {
                InterviewEvent = interviewEvent,
                InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
                StudentName = student.FirstName + " " + student.LastName,
                Class = ClassConstants.GetClassText(student.Class)
            };

            return View(secondViewModel);
        }

        // GET: InterviewEvents/Create
        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> Create()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var user = await _userManager.Users
                .Where(x => x.Id == userId)
                .FirstOrDefaultAsync();

            var timeslots = new List<Timeslot>();
            if (user.Class == Classes.NotYetMIS || user.Class == Classes.FirstSem)
            {
                timeslots = await _context.Timeslots
                    .Where(x => x.IsStudent)
                    .Include(y => y.Event)
                    .Where(x => _context.Interviews.Count(y => y.TimeslotId == x.Id) < x.MaxSignUps)
                    .Where(x => x.Event.For221 != For221.n 
                        && x.Event.IsActive)
                    .ToListAsync();
            }
            else
            {
                timeslots = await _context.Timeslots
                    .Where(x => x.IsStudent)
                    .Include(y => y.Event)
                    .Where(x => _context.Interviews.Count(y => y.TimeslotId == x.Id) < x.MaxSignUps)
                    .Where(x => x.Event.For221 != For221.y 
                        && x.Event.IsActive)
                    .ToListAsync();
            }

            var interviewEvents = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.StudentId == userId
                    && x.Timeslot.Event.IsActive)
                .ToListAsync();

            bool signedUp = false;
            if (interviewEvents.Count > 1)
            {
                signedUp = true;
            }

            InterviewEventSignupViewModel model = new InterviewEventSignupViewModel
            {
                Timeslots = timeslots,
                ApplicationUser = user,
                SignedUp = signedUp
            };

            return View(model);
        }

        // POST: InterviewEvents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.StudentRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int SelectedEventIds)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            var interviewTypeTwo = InterviewTypeConstants.Technical;
            if (user.Class == Classes.NotYetMIS|| user.Class == Classes.FirstSem)
            {
                interviewTypeTwo = InterviewTypeConstants.Behavioral;
            }

            var interviewEvents = new List<Interview>
            {
                new Interview 
                {
                    TimeslotId = SelectedEventIds,
                    StudentId = userId,
                    Status = StatusConstants.Default,
                    Type = InterviewTypeConstants.Behavioral
                },
                new Interview 
                {
                    TimeslotId = SelectedEventIds + 1,
                    StudentId = userId,
                    Status = StatusConstants.Default,
                    Type= interviewTypeTwo
                }
            };

			if (ModelState.IsValid)
            {
                _context.AddRange(interviewEvents);
                await _context.SaveChangesAsync();

                var emailTimes = new List<Interview>();
                List<string> calendarEvents = new();

                var newEvent = await _context.Interviews
                    .Include(v => v.Timeslot)
                    .ThenInclude(y => y.Event)
                    .Where(v => v.TimeslotId == SelectedEventIds)
                    .FirstOrDefaultAsync();
                emailTimes.Add(newEvent);
                newEvent = await _context.Interviews
                    .Include(v => v.Timeslot)
                    .ThenInclude(y => y.Event)
                    .Where(v => v.TimeslotId == SelectedEventIds + 1)
                    .FirstOrDefaultAsync();
                emailTimes.Add(newEvent);

                string interviewDetails = "";
                foreach (var interview in emailTimes)
                {
                    var plainBytes = Encoding.UTF8.GetBytes(CreateCalendarEvent(interview.Timeslot.Time, interview.Timeslot.Time.AddMinutes(30)));
                    string tempEvent = Convert.ToBase64String(plainBytes);
                    calendarEvents.Add(tempEvent);
                    interviewDetails += interview.ToString();
                }

                ASendAnEmail emailer = new StudentSignupEmail();
                await emailer.SendEmailAsync(_sendGridClient, "UA MIS Mock Interview Sign-Up Confirmation", user.Email, user.FirstName, interviewDetails, calendarEvents); ;

				return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // GET: InterviewEvents/Edit/5
        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound();
            }

            var all = await OutsourceQuery2024(interviewEvent);
            var behavioralInterviewers = OutsourceQueryBehavioral2024(all);
            var technicalInterviewers = OutsourceQueryTechnical2024(all);

            var requestedInterviewers = new List<SelectListItem>();
            if (interviewEvent.Type == InterviewTypeConstants.Technical)
            {
                requestedInterviewers = technicalInterviewers;
            }
            else
            {
                requestedInterviewers = behavioralInterviewers;
            }

            var studentname = await _userManager.Users
                .Where(x => x.Id == interviewEvent.StudentId)
                .Select(x => x.FirstName + " " + x.LastName)
                .FirstOrDefaultAsync();

            var interviewEventManageViewModel = new InterviewEventManageViewModel
            {
                InterviewEvent = interviewEvent,
                BehavioralInterviewers = behavioralInterviewers,
                TechnicalInterviewers = technicalInterviewers,
                RequestedInterviewers = requestedInterviewers,
                StudentName = studentname
            };

            return View(interviewEventManageViewModel);
        }

        // POST: InterviewEvents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = RolesConstants.AdminRole +","+RolesConstants.InterviewerRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,LocationId,TimeslotId,Type,Status,InterviewerTimeslotId")] Interview interviewEvent, string InterviewerId)
        {
            if (id != interviewEvent.Id)
            {
                return NotFound();
            }

            //if(InterviewerId == "0")
            //{
            //    interviewEvent.SignupInterviewerTimeslot = null;
            //}

            if (ModelState.IsValid)
            {
                if(InterviewerId == "0")
                {
                    interviewEvent.InterviewerTimeslot = null;
                    interviewEvent.InterviewerTimeslotId = null;
                    interviewEvent.Location = null;
                    interviewEvent.LocationId = null;
                }
                else
                {
                    var signupInterviewTimeslot = await _context.InterviewerTimeslots
                        .Include(x => x.InterviewerSignup)
                        .Include(x => x.Timeslot)
                        .ThenInclude(x => x.Event)
                        .Where(x => x.InterviewerSignup.InterviewerId == InterviewerId)
                        .FirstOrDefaultAsync();

                    if(signupInterviewTimeslot != null && interviewEvent.Status == StatusConstants.CheckedIn)
                    {
                        interviewEvent.Status = StatusConstants.Ongoing;
                    }

                    var interviewerPreference = "";
                    if(signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson)
                    {
                        interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                    }
                    else if(signupInterviewTimeslot.InterviewerSignup.IsVirtual)
                    {
                        interviewerPreference = InterviewLocationConstants.IsVirtual;
                    }
                    else if(signupInterviewTimeslot.InterviewerSignup.InPerson)
                    {
                        interviewerPreference = InterviewLocationConstants.InPerson;
                    }

                    var location = await _context.InterviewerLocations
                        .Include(x => x.Location)
                        .Where(x => x.InterviewerId == InterviewerId && 
                            x.Preference == interviewerPreference && 
                            x.EventId == signupInterviewTimeslot.Timeslot.EventId && 
                            x.LocationId != null) 
                        .FirstOrDefaultAsync();

                    if (location == null)
                    {
                        interviewEvent.Location = null;
                        interviewEvent.LocationId = null;
                    }
                    else
                    {
                        interviewEvent.Location = location.Location;
                        interviewEvent.LocationId = location.Location.Id;
                    }

                    interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                    interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;
                }
                
                try
                {
                    _context.Update(interviewEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterviewEventExists(interviewEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                if (User.IsInRole(RolesConstants.AdminRole))
                {
                    await UpdateHub(id);
                    await UpdateHub();

                    return RedirectToAction(nameof(Index));
                }
                return RedirectToAction("Index", "Home");
            }

            return View(interviewEvent);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Override(int? id)
        {
            if (id == null || _context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            //var selectedInterviewers = await _userManager.GetUsersInRoleAsync(RolesConstants.InterviewerRole);
            //var interviewers = selectedInterviewers.ToList();

            var selectedInterviewers = await _context.InterviewerTimeslots
                .Include(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Timeslot.EventId == interviewEvent.Timeslot.EventId && 
                    x.Timeslot.Event.IsActive)
                .Select(x => x.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var selectedInterviewersNames = new List<SelectListItem>();
            if (selectedInterviewers.Count == 0)
            {
                if (interviewEvent.InterviewerTimeslot != null)
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId,
                        Text = interviewEvent.InterviewerTimeslot.InterviewerSignup.FirstName + " " + interviewEvent.InterviewerTimeslot.InterviewerSignup.LastName
                    });
                }
                else
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = "0",
                        Text = "--Unassigned--"
                    });
                }
            }
            else
            {
                if (interviewEvent.InterviewerTimeslot != null)
                {
                    selectedInterviewersNames.Add(new SelectListItem
                    {
                        Value = interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId,
                        Text = interviewEvent.InterviewerTimeslot.InterviewerSignup.FirstName + " " + interviewEvent.InterviewerTimeslot.InterviewerSignup.LastName
                    });
                }
                foreach (string sit in selectedInterviewers)
                {
                    var user = await _userManager.Users
                        .Where(u => u.Id == sit)
                        .Select(u => new { u.Id, u.FirstName, u.LastName })
                        .FirstOrDefaultAsync();

                    if (user != null)
                    {
                        selectedInterviewersNames.Add(new SelectListItem
                        {
                            Value = user.Id,
                            Text = user.FirstName + " " + user.LastName
                        });
                    }
                }
                selectedInterviewersNames = selectedInterviewersNames.OrderBy(item => item.Text).ToList();
                selectedInterviewersNames.Insert(0, new SelectListItem
                {
                    Value = "0",
                    Text = "--Unassigned--"
                });

            }


            var interviewEventManageViewModel = new InterviewEventManageViewModel
            {
                InterviewEvent = interviewEvent,
                RequestedInterviewers = selectedInterviewersNames,
                BehavioralInterviewers = selectedInterviewersNames,
                TechnicalInterviewers = selectedInterviewersNames
            };

            return View(interviewEventManageViewModel);
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Override(int id, [Bind("Id,StudentId,LocationId,TimeslotId,Type,Status,InterviewerTimeslotId")] Interview interviewEvent, string InterviewerId)
        {
            if (id != interviewEvent.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (InterviewerId == "0")
                {
                    interviewEvent.InterviewerTimeslot = null;
                }
                else
                {
                    var signupInterviewTimeslot = await _context.InterviewerTimeslots
                        .Include(x => x.InterviewerSignup)
                        .Include(x => x.Timeslot)
                        .ThenInclude(x => x.Event)
                        .Where(x => x.TimeslotId == interviewEvent.TimeslotId && x.InterviewerSignup.InterviewerId == InterviewerId)
                        .FirstOrDefaultAsync();

                    if(signupInterviewTimeslot != null)
                    {
                        var interviewerPreference = "";
                        if (signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.IsVirtual)
                        {
                            interviewerPreference = InterviewLocationConstants.IsVirtual;
                        }
                        else if (signupInterviewTimeslot.InterviewerSignup.InPerson)
                        {
                            interviewerPreference = InterviewLocationConstants.InPerson;
                        }

                        var location = await _context.InterviewerLocations
                            .Include(x => x.Location)
                            .Where(x => x.InterviewerId == InterviewerId && x.Preference == interviewerPreference && x.EventId == signupInterviewTimeslot.Timeslot.EventId && x.LocationId != null)
                            .FirstOrDefaultAsync();

                        interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                        interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;

                        if(location != null)
                        {
                            interviewEvent.LocationId = location.Location.Id;
                            interviewEvent.Location = location.Location;
                        } 

                        if(interviewEvent.Status == StatusConstants.Ongoing)
                        {
                            interviewEvent.StartedAt = DateTime.UtcNow;
                        } else if (interviewEvent.Status == StatusConstants.CheckedIn)
                        {
                            interviewEvent.CheckedInAt = DateTime.UtcNow;
                        } else if (interviewEvent.Status == StatusConstants.Completed)
                        {
                            interviewEvent.EndedAt = DateTime.UtcNow;
                        }
                    }
                }

                try
                {
                    _context.Update(interviewEvent);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterviewEventExists(interviewEvent.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                var newInterviewEvent = await _context.Interviews
                    .Include(x => x.Location)
                    .Include(x => x.InterviewerTimeslot)
                    .ThenInclude(x => x.InterviewerSignup)
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .FirstOrDefaultAsync(x => x.Id == id);

                var student = await _userManager.Users
                    .Where(x => x.Id == newInterviewEvent.StudentId)
                    .FirstOrDefaultAsync();

                var interviewername = "Not Assigned";
                var interviewerId = "0";

                if (newInterviewEvent.InterviewerTimeslot != null)
                {
                    interviewerId = newInterviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId;
                    interviewername = await _userManager.Users
                        .Where(x => x.Id == interviewerId)
                        .Select(x => x.FirstName + " " + x.LastName)
                        .FirstOrDefaultAsync();
                }

                if (newInterviewEvent.Location == null)
                {
                    newInterviewEvent.Location = new Location()
                    {
                        Room = "Not Assigned"
                    };
                }

                var time = $"{newInterviewEvent.Timeslot.Time:hh:mm tt}";
                var date = $"{newInterviewEvent.Timeslot.Event.Date:M/d/yyyy}";

                await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", newInterviewEvent, student.GetFullName(), student.GetClass(), interviewerId, interviewername, time, date);
                await UpdateHub();

                return RedirectToAction(nameof(Index));
            }

            return View(interviewEvent);
        }

        // GET: InterviewEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (interviewEvent == null)
            {
                return NotFound();
            }

            var student = await _userManager.FindByIdAsync(interviewEvent.StudentId);

            if (interviewEvent.InterviewerTimeslot == null)
            {
                var viewModel = new InterviewEventViewModel
                {
                    InterviewEvent = interviewEvent,
                    InterviewerName = "Not Assigned",
                    StudentName = student.FirstName + " " + student.LastName,
                    Class = ClassConstants.GetClassText(student.Class)
                };

                return View(viewModel);
            }


            var interviewer = await _userManager.FindByIdAsync(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId);

            var secondViewModel = new InterviewEventViewModel
            {
                InterviewEvent = interviewEvent,
                InterviewerName = interviewer.FirstName + " " + interviewer.LastName,
                StudentName = student.FirstName + " " + student.LastName,
                Class = ClassConstants.GetClassText(student.Class)
            };

            await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", new Interview() { Id = (int)id }, "delete", "0", "", "", "");

            return View(secondViewModel);
        }

        // POST: InterviewEvents/Delete/5
        [Authorize(Roles = RolesConstants.AdminRole)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent != null)
            {
                _context.Interviews.Remove(interviewEvent);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction("Index","Home");
        }

        private bool InterviewEventExists(int id)
        {
          return (_context.Interviews?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private async Task<List<string>> OutsourceQuery(Interview interviewEvent)
        {
            // Get the timeslot of the current user's interview
            var timeslot = _context.Interviews
                .Include(i => i.Timeslot.Event)
                .Where(i => i.StudentId == interviewEvent.StudentId && i.TimeslotId == interviewEvent.TimeslotId)
                .Select(i => i.Timeslot)
                .FirstOrDefault();

            // Get the SignupInterviewerTimeslots for the same timeslot and event date as the user's interview
            var signupInterviewerTimeslots = _context.InterviewerTimeslots
                .Include(s => s.InterviewerSignup)
                .Where(s => s.TimeslotId == timeslot.Id && 
                    s.Timeslot.Event.Date == timeslot.Event.Date && 
                    s.Timeslot.Event.IsActive == true)
                .ToList();

            var interviewers = _context.InterviewerSignups
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have signed up to deliver the same type of interview as the student needs
            var selectedTypes = _context.InterviewerSignups
                .Where(u => ((interviewEvent.Type == InterviewTypeConstants.Technical && u.IsTechnical) ||
                             (interviewEvent.Type == InterviewTypeConstants.Behavioral && u.IsBehavioral)))
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have a timeslot that matches the student's timeslot
            var selectedTimeslot = signupInterviewerTimeslots
                .Select(u => u.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            //get list of all distinct interviewers that are in an interview event with a status of checked in or ongoing
            var selectedStatusNot = _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Include(x => x.InterviewerTimeslot.InterviewerSignup)
                .Where(x => x.InterviewerTimeslot.Timeslot.Event.IsActive == true)
                .Select(x => x.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            var selectedStatus = interviewers
                .Except(selectedStatusNot)
                .ToList();

            //Get list of all distinct interviewers that are assigned to a location
            var selectedLocation = _context.InterviewerLocations
                .Where(x => x.LocationId != null &&
                    x.Event == timeslot.Event &&
                    x.Event.IsActive == true)
                .Select(x => x.InterviewerId)
                .Distinct()
                .ToList();

            //Get list of all distinct interviewers that have interviewed this student
            var haveInterviewed = _context.Interviews
                .Where(x => x.StudentId == interviewEvent.StudentId)
                .Select(x => x.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToList();

            var haveNotInterviewed = interviewers
                .Except(haveInterviewed)
                .ToList();

            //Get list of all distinct interviewers that are not the student
            var notStudent = interviewers
                .Where(x => x != interviewEvent.StudentId)
                .Distinct()
                .ToList();

            //combine the previous four query results
            var selectedInterviewers = selectedTypes
                .Intersect(selectedTimeslot)
                .Intersect(selectedStatus)
                .Intersect(selectedLocation)
                .Intersect(haveNotInterviewed)
                .Intersect(notStudent)
                .ToList();

            return selectedInterviewers;
        }

        private async Task<List<IVM>> OutsourceQuery2024(Interview ie)
        {
            var allInterviewers = await _context.InterviewerTimeslots
                .Include(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x =>
                    x.Timeslot.Event.IsActive &&
                    x.Timeslot.Event.Date == ie.Timeslot.Event.Date &&
                    x.InterviewerSignup.CheckedIn &&
                    !_context.Interviews
                        .Where(i => i.Status == "Ongoing")
                        .Select(i => i.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                        .Contains(x.InterviewerSignup.InterviewerId))
                .Select(x => new IVM
                {
                    Name = x.InterviewerSignup.FirstName + " " + x.InterviewerSignup.LastName,
                    Id = x.InterviewerSignup.InterviewerId.ToString(),
                    Technical = x.InterviewerSignup.IsTechnical,
                    Behavioral = x.InterviewerSignup.IsBehavioral
                })
                .ToListAsync();

            return allInterviewers;
        }

        private static List<SelectListItem> OutsourceQueryTechnical2024(List<IVM> all)
        {
            var allInterviewers = all
                .Distinct(new IVMComparer())
                .OrderBy(x => x.Name)
                .ToList();

            var technicalInterviewers = allInterviewers
                .Where(x => x.Technical)
                .Select(x => new SelectListItem
                {
                    Value = x.Id,
                    Text = x.Name
                })
                .ToList();

            technicalInterviewers.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "--Unassigned--"
            });

            return technicalInterviewers;
        }

        private static List<SelectListItem> OutsourceQueryBehavioral2024(List<IVM> all)
        {
            var allInterviewers = all
                .Distinct(new IVMComparer())
                .OrderBy(x => x.Name)
                .ToList();

            var behavioralInterviewers = allInterviewers
                .Where(x => x.Behavioral)
                .Select(x => new SelectListItem
                {
                    Value = x.Id,
                    Text = x.Name
                })
                .ToList();

            behavioralInterviewers.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "--Unassigned--"
            });

            return behavioralInterviewers;
        }

        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> UserDelete(int? id)
        {
            if (id == null || _context.Interviews == null)
            {
                return NotFound();
            }

            var interviewEvent = await _context.Interviews.Include(i => i.Location).Include(i => i.InterviewerTimeslot)
                .Include(i => i.Timeslot)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (interviewEvent == null)
            {
                return NotFound();
            }

            if (interviewEvent.StudentId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                return NotFound();
            }

            return View(interviewEvent);
        }

        // POST: InterviewEvents/Delete/5
        [Authorize(Roles = RolesConstants.StudentRole)]
        public async Task<IActionResult> UserDeleteConfirmed(int id)
        {
            var interviewEvent = await _context.Interviews.FindAsync(id);
            if (interviewEvent != null && interviewEvent.StudentId == User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                _context.Interviews.Remove(interviewEvent);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> CreateForStudent()
        {
            var students = await _userService.GetUsersByRole(RolesConstants.StudentRole);
            var studentList = new List<SelectListItem>();
            foreach (var student in students)
            {
                studentList.Add(new SelectListItem
                {
                    Value = student.Id,
                    Text = student.FirstName + " " + student.LastName
                });
            }

            studentList.Sort((x, y) => string.Compare(x.Text, y.Text));

            var timeslots = await _timeslotService.GetActiveTimeslotsByRole(true, false);
            var timeslotsList = timeslots.ToList();

            InterviewSignupByAdminViewModel model = new()
            {
                Timeslots = timeslotsList,
                Events = timeslotsList.Select(x => x.Event).Distinct().ToList(),
                Students = studentList,
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> CreateForStudent(int SelectedEventIds, string StudentId)
        {
            var students = await _userService.GetUsersByRole(RolesConstants.StudentRole);
            var studentList = new List<SelectListItem>();
            foreach (var student in students)
            {
                studentList.Add(new SelectListItem
                {
                    Value = student.Id,
                    Text = student.FirstName + " " + student.LastName
                });
            }

            studentList.Sort((x, y) => string.Compare(x.Text, y.Text));

            var timeslots = await _timeslotService.GetActiveTimeslotsByRole(true, false);
            var timeslotsList = timeslots.ToList();

            int days = timeslotsList.Select(x => x.EventId)
                .Distinct()
                .Count();

            var vm = new InterviewSignupByAdminViewModel()
            {
                Timeslots = timeslotsList,
                Events = timeslotsList.Select(x => x.Event).Distinct().ToList(),
                Students = studentList
            };

            if(SelectedEventIds == 0)
            {
                ModelState.AddModelError("StudentId","Please select a timeslot");
                return View(vm);
            }

            if(string.IsNullOrEmpty(StudentId))
            {
                ModelState.AddModelError("StudentId", "Please select a student");
                return View(vm);
            }

            var user = await _userService.GetByIdAsync(StudentId);

            var interviewTypeTwo = InterviewTypeConstants.Technical;
            if (user.Class == Classes.NotYetMIS|| user.Class == Classes.FirstSem)
            {
                interviewTypeTwo = InterviewTypeConstants.Behavioral;
            }

            var interviewEvents = new List<Interview>
            {
                new() 
                {
                    TimeslotId = SelectedEventIds,
                    StudentId = StudentId,
                    Status = StatusConstants.Default,
                    Type = InterviewTypeConstants.Behavioral
                },
                new() 
                {
                    TimeslotId = SelectedEventIds + 1,
                    StudentId = StudentId,
                    Status = StatusConstants.Default,
                    Type= interviewTypeTwo
                }
            };

            await _interviewService.AddRange(interviewEvents);

            return RedirectToAction("Index", "InterviewEvents");
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> StudentCheckIn(int id)
        {
            _logger.LogInformation("Interviewee checked in. Interview Id: {id}", id);

            if (_context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            interviewEvent.Status = StatusConstants.CheckedIn;
            interviewEvent.CheckedInAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [Authorize(Roles = RolesConstants.AdminRole + "," + RolesConstants.InterviewerRole)]
        public async Task<IActionResult> StudentComplete(int id)
        {
            _logger.LogInformation($"Interview marked completed. Id: {id}");

            if (id == null || _context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            interviewEvent.Status = StatusConstants.Completed;
            interviewEvent.EndedAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [Authorize(Roles=RolesConstants.AdminRole)]
        public async Task<IActionResult> StudentNoShow(int id)
        {
            _logger.LogInformation($"Interview marked no-show. Id: {id}");

            if (id == null || _context.Interviews == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound("Interview not found.");
            }

            interviewEvent.Status = StatusConstants.NoShow;
            interviewEvent.EndedAt = DateTime.UtcNow;

            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict("Concurrency exception occurred.");
            }
            catch (DbUpdateException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error occurred.");
            }

            await UpdateHub(id);

            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> EditInline(int Id, string Status, string InterviewerId, string Type)
        {
            _logger.LogInformation($"Id: {Id}, Status: {Status}, InterviewerId: {InterviewerId}, Type: {Type}");

            if (Id == 0 || Status == null || Type == null)
            {
                return BadRequest("Interview not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .Where(x => x.Id == Id)
                .FirstOrDefaultAsync();

            if (interviewEvent == null)
            {
                return NotFound();
            }

            interviewEvent.Status = Status;
            interviewEvent.Type = Type;

            if (InterviewerId == "0" || InterviewerId == "" || InterviewerId == null)
            {
                interviewEvent.InterviewerTimeslot = null;
                interviewEvent.InterviewerTimeslotId = null;
                interviewEvent.Location = null;
                interviewEvent.LocationId = null;
            }
            else
            {
                var signupInterviewTimeslot = await _context.InterviewerTimeslots
                    .Include(x => x.InterviewerSignup)
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.InterviewerSignup.InterviewerId == InterviewerId)
                    .FirstOrDefaultAsync();

                if (signupInterviewTimeslot != null && interviewEvent.Status == StatusConstants.CheckedIn)
                {
                    interviewEvent.Status = StatusConstants.Ongoing;
                    interviewEvent.StartedAt = DateTime.UtcNow;
                }

                var interviewerPreference = "";
                if (signupInterviewTimeslot.InterviewerSignup.IsVirtual && signupInterviewTimeslot.InterviewerSignup.InPerson){
                    interviewerPreference = InterviewLocationConstants.InPerson + "/" + InterviewLocationConstants.IsVirtual;
                } else if (signupInterviewTimeslot.InterviewerSignup.IsVirtual){
                    interviewerPreference = InterviewLocationConstants.IsVirtual;
                } else if (signupInterviewTimeslot.InterviewerSignup.InPerson){
                    interviewerPreference = InterviewLocationConstants.InPerson;
                }

                var location = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Where(x => x.InterviewerId == InterviewerId &&
                        x.Preference == interviewerPreference &&
                        x.EventId == signupInterviewTimeslot.Timeslot.EventId &&
                        x.LocationId != null)
                    .FirstOrDefaultAsync();

                if(location == null)
                {
                    interviewEvent.Location = null;
                    interviewEvent.LocationId = null;
                }
                else
                {
                    interviewEvent.Location = location.Location;
                    interviewEvent.LocationId = location.Location.Id;
                }

                interviewEvent.InterviewerTimeslot = signupInterviewTimeslot;
                interviewEvent.InterviewerTimeslotId = signupInterviewTimeslot.Id;
            }
            
            try
            {
                _context.Update(interviewEvent);
                await _context.SaveChangesAsync();

                await UpdateHub(Id);
                await UpdateHub();

                var ie = await _context.Interviews
                    .Include(x => x.InterviewerTimeslot)
                    .ThenInclude(x => x.InterviewerSignup)
                    .Where(x => x.Id == Id)
                    .Select(x => new
                    {
                        x.StudentId,
                        InterviewerName = x.InterviewerTimeslot.InterviewerSignup.FirstName + " " + x.InterviewerTimeslot.InterviewerSignup.LastName,
                    })
                    .FirstOrDefaultAsync();

                var locationRoom = interviewEvent.Location.Room;
                if(locationRoom == null)
                {
                    locationRoom = "**Not Assigned**";
                }

                var student = await _userManager.Users
                    .Where(x => x.Id == ie.StudentId)
                    .Select(x => new
                    {
                        StudentName = x.FirstName + " " + x.LastName,
                    })
                    .FirstOrDefaultAsync();

                var response = new EditInlineResponse
                {
                    StudentName = student.StudentName, // Replace with actual values
                    InterviewType = interviewEvent.Type, // Replace with actual values
                    InterviewerName = ie.InterviewerName, // Replace with actual values
                    Location = interviewEvent.Location.Room, // Replace with actual values
                };

                await UpdateHub(Id);

                return Ok(response);

            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InterviewEventExists(interviewEvent.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<ActionResult<AVM>> GetAvailableInterviewers(int id)
        {
            if (id == 0 || !_context.Interviews.Any(x => x.Id == id))
            {
                return BadRequest("Invalid ID or Interview Event not found.");
            }

            var interviewEvent = await _context.Interviews
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (interviewEvent == null)
            {
                return NotFound("Interview Event not found.");
            }

            var vm = new AVM();

            var all = await OutsourceQuery2024(interviewEvent);
            vm.BehavioralInterviewers = OutsourceQueryBehavioral2024(all);
            vm.TechnicalInterviewers = OutsourceQueryTechnical2024(all);

            return vm;
        }

        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> GetCompletedInterviews()
        {
            var interviewEvents = await _context.Interviews
                .Include(i => i.Location)
                .Include(i => i.InterviewerTimeslot)
                .ThenInclude(i => i.InterviewerSignup)
                .Include(i => i.Timeslot)
                .ThenInclude(j => j.Event)
                .Where(i => (i.Status == StatusConstants.Completed ||
                    i.Status == StatusConstants.NoShow ||
                    i.Status == StatusConstants.Excused) &&
                    i.Timeslot.Event.IsActive)
                .ToListAsync();

            // 1. Collect unique StudentIds
            var studentIds = interviewEvents
                .Select(ie => ie.StudentId)
                .Distinct()
                .ToList();

            // 2. Fetch student names for all unique StudentIds
            var students = await _userManager.Users
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}", u.Class })
                .ToListAsync();

            var studentNames = students
                .Select(u => new { u.Id, u.FullName })
                .ToDictionary(u => u.Id, u => u.FullName);

            var studentClasses = students
                .Select(u => new { u.Id, Class = ClassConstants.GetClassText(u.Class) })
                .ToDictionary(u => u.Id, u => u.Class);

            //3. Collect unique InterviewerIds
            var interviewerIds = await _context.InterviewerSignups
                .Select(ie => ie.InterviewerId)
                .Distinct()
                .ToListAsync();

            // 4. Fetch interviewer names for all unique InterviewerIds
            var interviewerNames = await _userManager.Users
                .Where(u => interviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, FullName = $"{u.FirstName} {u.LastName}" })
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            var eventslist = new List<InterviewEventViewModel>();

            foreach (Interview interviewEvent in interviewEvents)
            {
                var interviewEventViewModel = new InterviewEventViewModel();

                if (studentNames.TryGetValue(interviewEvent.StudentId, out var studentName))
                {
                    interviewEventViewModel.StudentName = studentName;
                }

                if (studentClasses.TryGetValue(interviewEvent.StudentId, out var studentClass))
                {
                    interviewEventViewModel.Class = studentClass;
                }

                if (interviewEvent.InterviewerTimeslot != null)
                {
                    if (interviewerNames.TryGetValue(interviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId, out var interviewerName))
                    {
                        interviewEventViewModel.InterviewerName = interviewerName;
                    }

                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
                else
                {
                    interviewEventViewModel.InterviewerName = "Not Assigned";
                    interviewEventViewModel.InterviewEvent = interviewEvent;

                    eventslist.Add(interviewEventViewModel);
                }
            }

            return View(eventslist);
        }

        [Authorize]
        public async Task<IActionResult> StudentSelfCheckIn()
        {
            if(User.IsInRole (RolesConstants.StudentRole))
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var ie = await _context.Interviews
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.StudentId == userId &&
                        x.Timeslot.Event.IsActive &&
                        x.Status == StatusConstants.Default)
                    .FirstOrDefaultAsync();

                var vm = new SelfCheckInViewModel()
                {
                    IsCheckedIn = false,
                    CheckInMessage = "You couldn't be checked in automatically. Please alert event staff."
                };

                if (ie == null)
                {
                    return View("SelfCheckIn", vm);
                }

                vm.IsCheckedIn = true;
                vm.CheckInMessage = "You have been checked in automatically! Please take a seat until event staff calls you.";

                ie.Status = StatusConstants.CheckedIn;
                ie.CheckedInAt = DateTime.UtcNow;
                _context.Update(ie);
                await _context.SaveChangesAsync();

                await UpdateHub(ie.Id);

                return View("SelfCheckIn", vm);
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> InterviewerSelfCheckIn()
        {
            if(User.IsInRole(RolesConstants.AdminRole))
            {
                var sits = await _context.InterviewerTimeslots
                                .Include(x => x.Timeslot)
                                .ThenInclude(x => x.Event)
                                .Where(x => x.Timeslot.Event.IsActive)
                                .Select(x => x.InterviewerSignupId)
                                .Distinct()
                                .ToListAsync();

                var interviewers = await _context.InterviewerSignups
                    .Where(x => sits.Contains(x.Id))
                    .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                    .OrderBy(x => x.Text)
                    .ToListAsync();

                var vm = new InterviewerCheckInViewModel
                {
                    Interviewers = interviewers,
                    CheckedIn = false
                };

                return View("InterviewerCheckIn", vm);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> InterviewerSelfCheckIn(string InterviewerId)
        {
            string id = InterviewerId;

            if (id == null || id == "0" || id == "")
            {
                return BadRequest("No interviewer was selected.");
            }

            int newId = 0;
            try
            {
                newId = int.Parse(id);
            }
            catch
            {
                return BadRequest("InterviewerSignup ID was invalid.");
            }

            var interviewer = await _context.InterviewerSignups
                .Where(x => x.Id == newId)
                .FirstOrDefaultAsync();

            if(interviewer == null)
            {
                return BadRequest("Interviewer not signed up.");
            }

            interviewer.CheckedIn = !interviewer.CheckedIn;

            _context.Update(interviewer);
            await _context.SaveChangesAsync();

            var sits = await _context.InterviewerTimeslots
                    .Include(x => x.Timeslot)
                    .ThenInclude(x => x.Event)
                    .Where(x => x.Timeslot.Event.IsActive)
                    .Select(x => x.InterviewerSignupId)
                    .Distinct()
                    .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => sits.Contains(x.Id))
                .Select(x => new SelectListItem { Text = x.FirstName + " " + x.LastName, Value = x.Id.ToString() })
                .OrderBy(x => x.Text)
                .ToListAsync();

            var room = "";

            if (interviewer.CheckedIn)
            {
                string interviewerId = interviewer.InterviewerId;

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 3);

                var li = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == interviewerId &&
                        x.Event.Date.Date == date)
                    .FirstOrDefaultAsync();

                if(li != null)
                {
                    room = li.Location.Room;
                }
                else
                {
                    room = "Not Assigned";
                }
            }

            var vm = new InterviewerCheckInViewModel
            {
                Interviewers = interviewers,
                CheckedIn = interviewer.CheckedIn,
                Name = interviewer.FirstName + " " + interviewer.LastName,
                Room = room
            };

            await UpdateHub();

            return View("InterviewerCheckIn",vm);
        }

        [HttpGet]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> PreAssignInterviews()
        {
            var vm = await _manager.ListOfAssignedStudents();            

            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = RolesConstants.AdminRole)]
        public async Task<IActionResult> PreAssignInterviews([FromBody] List<PARVM> requests)
        {
            _logger.LogInformation("Preassignment requested...");

            if (requests == null || requests.Count == 0)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Invalid data. No interviews to assign." });
            }

            var dictionary = requests.ToDictionary(x => int.Parse(x.InterviewEventId), x => x.SelectedValue);

            try {
                await _manager.AssignStudentsToInterviewers(dictionary);
            } catch {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to assign students to interviewers" });
            }

            return StatusCode(StatusCodes.Status200OK, new { message = "Success!"});
        }

        private static DateTime CombineDateWithTimeString(DateTime date, string timeString)
        {
            DateTime dateTime = DateTime.ParseExact(timeString, "h:mm tt", CultureInfo.InvariantCulture);
            TimeSpan timeSpan = dateTime.TimeOfDay;
            return date.Date + timeSpan;
        }

        private static string CreateCalendarEvent(DateTime start, DateTime end)
        {

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("BEGIN:VCALENDAR");
            stringBuilder.AppendLine("VERSION:2.0");
            stringBuilder.AppendLine("PRODID:-//YourCompany//YourProduct//EN"); // Optional identifier
            stringBuilder.AppendLine("BEGIN:VTIMEZONE");
            stringBuilder.AppendLine("TZID:America/Chicago");
            stringBuilder.AppendLine("BEGIN:DAYLIGHT");
            stringBuilder.AppendLine("TZOFFSETFROM:-0600");
            stringBuilder.AppendLine("TZOFFSETTO:-0500");
            stringBuilder.AppendLine("TZNAME:CDT");
            stringBuilder.AppendLine("DTSTART:19700308T020000");
            stringBuilder.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU");
            stringBuilder.AppendLine("END:DAYLIGHT");
            stringBuilder.AppendLine("BEGIN:STANDARD");
            stringBuilder.AppendLine("TZOFFSETFROM:-0500");
            stringBuilder.AppendLine("TZOFFSETTO:-0600");
            stringBuilder.AppendLine("TZNAME:CST");
            stringBuilder.AppendLine("DTSTART:19701101T020000");
            stringBuilder.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU");
            stringBuilder.AppendLine("END:STANDARD");
            stringBuilder.AppendLine("END:VTIMEZONE");
            stringBuilder.AppendLine("BEGIN:VEVENT");
            stringBuilder.AppendLine("UID:" + Guid.NewGuid());
            stringBuilder.AppendFormat("DTSTAMP:{0:yyyyMMddTHHmmssZ}\r\n", DateTime.UtcNow); // Added DTSTAMP
            stringBuilder.AppendLine("SEQUENCE:0"); // Added SEQUENCE for indicating the version of the event
            stringBuilder.AppendFormat("DTSTART;TZID=America/Chicago:{0:yyyyMMddTHHmmss}\r\n", start);
            stringBuilder.AppendFormat("DTEND;TZID=America/Chicago:{0:yyyyMMddTHHmmss}\r\n", end);
            stringBuilder.AppendLine("SUMMARY:UA MIS Mock Interviews");
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-P14D"); // 14 days before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            // Add a reminder for 3 days before the event
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-P3D"); // 3 days before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            // Add a reminder for 30 minutes before the event
            stringBuilder.AppendLine("BEGIN:VALARM");
            stringBuilder.AppendLine("TRIGGER:-PT30M"); // 30 minutes before
            stringBuilder.AppendLine("ACTION:DISPLAY");
            stringBuilder.AppendLine("DESCRIPTION:Reminder");
            stringBuilder.AppendLine("END:VALARM");
            stringBuilder.AppendLine("END:VEVENT");
            stringBuilder.AppendLine("END:VCALENDAR");


            return stringBuilder.ToString();
        }

        private async Task UpdateHub(int id)
        {
            var newInterviewEvent = await _context.Interviews
                .Include(x => x.Location)
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Include(x => x.Timeslot)
                .ThenInclude(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);

            var student = await _userManager.Users
                .Where(x => x.Id == newInterviewEvent.StudentId)
                .FirstOrDefaultAsync();

            var studentName = "";
            if (newInterviewEvent.Status == StatusConstants.Completed || newInterviewEvent.Status == StatusConstants.NoShow || newInterviewEvent.Status == StatusConstants.Excused)
            {
                studentName = "delete";
            } else {
                studentName = student.GetFullName();
            }

            var interviewername = "Not Assigned";
            var interviewerId = "0";

            if (newInterviewEvent.InterviewerTimeslot != null)
            {
                interviewerId = newInterviewEvent.InterviewerTimeslot.InterviewerSignup.InterviewerId;
                interviewername = await _userManager.Users
                    .Where(x => x.Id == interviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync();
            }

            if (newInterviewEvent.Location == null)
            {
                newInterviewEvent.Location = new Location()
                {
                    Room = "Not Assigned"
                };
            }

            var time = $"{newInterviewEvent.Timeslot.Time:hh:mm tt}";
            var date = $"{newInterviewEvent.Timeslot.Event.Date:M/d/yyyy}";

            _logger.LogInformation("Requesting all connected clients to update.");
            await _hubContext.Clients.All.SendAsync("ReceiveInterviewEventUpdate", newInterviewEvent, studentName, student.GetClass(), interviewerId, interviewername, time, date);
            _logger.LogInformation("Requested.");
        }

        private async Task UpdateHub()
        {
            var busyInterviewers = await _context.Interviews
                .Include(x => x.InterviewerTimeslot)
                .ThenInclude(x => x.InterviewerSignup)
                .Where(x => x.Status == StatusConstants.Ongoing)
                .Select(x => x.InterviewerTimeslot.InterviewerSignup.InterviewerId)
                .Distinct()
                .ToListAsync();

            var interviewers = await _context.InterviewerSignups
                .Where(x => x.CheckedIn && !busyInterviewers.Contains(x.InterviewerId))
                .Select(x => new AvailableInterviewer
                {
                    InterviewerId = x.InterviewerId,
                    InterviewType = x.Type,
                })
            .ToListAsync();

            foreach (var iv in interviewers)
            {
                iv.Name = await _userManager.Users
                    .Where(x => x.Id == iv.InterviewerId)
                    .Select(x => x.FirstName + " " + x.LastName)
                    .FirstOrDefaultAsync();

                var date = DateTime.UtcNow.Date;
                //var date = new DateTime(2024, 2, 8);

                iv.Room = await _context.InterviewerLocations
                    .Include(x => x.Location)
                    .Include(x => x.Event)
                    .Where(x => x.InterviewerId == iv.InterviewerId &&
                        x.Event.Date == date)
                    .Select(x => x.Location.Room)
                    .FirstOrDefaultAsync() ?? "Not Assigned";
            }

            interviewers.Sort((x, y) => string.Compare(x.Name, y.Name));

            _logger.LogInformation("Requesting all clients to update their available interviewers lists...");
            await _hubContextInterviewer.Clients.All.SendAsync("ReceiveAvailableInterviewersUpdate", interviewers);
            _logger.LogInformation("Requested.");
        }
    }
}
