using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using MockInterviews.Interfaces.IServices;
using MockInterviews.Models.Entities;
using MockInterviews.Models.ViewModels.InterviewEventsController;
using MockInterviews.Services.SignalR;

namespace MockInterviews.Services
{
    public class ManageInterviewsService : IManageInterviews
    {
        private readonly InterviewService _interviewService;
        private readonly InterviewerTimeslotService _interviewerTimeslotService;
        private readonly UserService _userService;
        private readonly IHubContext<AssignInterviewsHub> _interviewsHub;
        private readonly IHubContext<AvailableInterviewersHub> _interviewersHub;
        private readonly ILogger<ManageInterviewsService> _logger;

        public ManageInterviewsService(
            InterviewService interviewService,
            InterviewerTimeslotService interviewerTimeslotService,
            UserService userService,
            IHubContext<AssignInterviewsHub> interviewsHub,
            IHubContext<AvailableInterviewersHub> interviewersHub,
            ILogger<ManageInterviewsService> logger)
        {
            _interviewService = interviewService;
            _interviewerTimeslotService = interviewerTimeslotService;
            _userService = userService;
            _interviewsHub = interviewsHub;
            _interviewersHub = interviewersHub;
            _logger = logger;
        }

        public async Task AssignStudentsToInterviewers(Dictionary<int, string> keyValuePairs)
        {
            var filteredDict = keyValuePairs.Where(x => x.Value != "0").ToDictionary(x => x.Key, x => x.Value);

            var interviews = await _interviewService.GetAllActiveInterviewsByIds(keyValuePairs.Keys.ToList());
            var interviewers = await _interviewerTimeslotService.GetAllActiveInterviewersByIds(filteredDict.Values.ToList());

            var interviewsToUpdate = new List<Interview>();


            foreach (var item in keyValuePairs)
            {
                var interview = interviews.Where(x => x.Id == item.Key).FirstOrDefault();
                if (interview is null)
                {
                    continue;
                }

                if (item.Value != "0")
                {
                    var interviewerTimeslot = interviewers.Where(x => x.InterviewerSignup.InterviewerId == item.Value && interview.TimeslotId == x.TimeslotId).FirstOrDefault();

                    if (interviewerTimeslot != null)
                    {
                        interview.InterviewerTimeslot = interviewerTimeslot;
                        interview.InterviewerTimeslotId = interviewerTimeslot.Id;
                        interviewsToUpdate.Add(interview);
                    }
                }
            }

            var studentIds = interviewsToUpdate.Select(x => x.StudentId).ToList();
            var students = await _userService.GetUsersByIds(studentIds);

            if (interviewsToUpdate.Count > 0)
            {
                await _interviewService.UpdateRangeAsync(interviewsToUpdate);

                foreach (var interview in interviewsToUpdate)
                {
                    if (interview.InterviewerTimeslot is not { } interviewerTimeslot)
                    {
                        continue;
                    }

                    students.TryGetValue(interview.StudentId, out var student);
                    var studentName = student?.GetFullName() ?? "Deleted user";
                    var studentClass = student?.GetClass() ?? string.Empty;
                    await _interviewsHub.Clients.All.SendAsync("ReceiveInterviewEventUpdate", interview, studentName, studentClass, interviewerTimeslot.InterviewerSignup.InterviewerId, interviewerTimeslot.InterviewerSignup.GetInterviewerName(), interview.Timeslot.Time, interview.Timeslot.Event.Date);
                }
            }
        }

        public async Task<List<InterviewEventManageViewModel>> ListOfAssignedStudents()
        {
            var allInterviewers = (await _interviewerTimeslotService.GetAllActiveInterviewers()).ToList();
            var allInterviews = (await _interviewService.GetAllActiveInterviews()).ToList();

            var studentIds = allInterviews.Select(x => x.StudentId).ToList();
            var students = await _userService.GetUsersByIds(studentIds);

            var preassignments = new List<InterviewEventManageViewModel>();
            foreach (var interview in allInterviews)
            {
                var student = students.Where(x => x.Key == interview.StudentId).FirstOrDefault();
                var defaultItem = new SelectListItem
                {
                    Value = "0",
                    Text = "--Unassigned--"
                };

                var firstItem = new SelectListItem();

                if (interview.InterviewerTimeslotId != null && interview.InterviewerTimeslotId != 0)
                {
                    if (interview.InterviewerTimeslot is { } interviewerTimeslot)
                    {
                        firstItem.Text = interviewerTimeslot.InterviewerSignup.GetInterviewerName();
                        firstItem.Value = interviewerTimeslot.InterviewerSignup.InterviewerId;
                    }
                }

                var availableInterviewers = allInterviewers
                    .Where(x => x.Timeslot.Event.Date == interview.Timeslot.Event.Date
                        && x.TimeslotId == interview.TimeslotId)
                    .Select(x => new SelectListItem
                    {
                        Text = x.InterviewerSignup.GetInterviewerName(),
                        Value = x.InterviewerSignup.InterviewerId
                    })
                    .ToList();

                if (availableInterviewers.Count == 0)
                {
                    preassignments.Add(new()
                    {
                        InterviewEvent = interview,
                        RequestedInterviewers = new List<SelectListItem> {
                            new() {
                                Value = "0",
                                Text = "--None Available--"
                            }
                        },
                        StudentName = student.Value.GetFullName(),
                        StudentClass = student.Value.GetClass()
                    });
                }
                else
                {
                    availableInterviewers = availableInterviewers.OrderBy(x => x.Text).ToList();

                    availableInterviewers.Insert(0, defaultItem);

                    if (!string.IsNullOrEmpty(firstItem.Value))
                    {
                        var interviewer = availableInterviewers.Where(x => x.Value == firstItem.Value).FirstOrDefault();
                        if (interviewer != null)
                        {
                            availableInterviewers.Remove(interviewer);
                        }

                        availableInterviewers.Insert(0, firstItem);
                    }

                    preassignments.Add(new()
                    {
                        InterviewEvent = interview,
                        RequestedInterviewers = availableInterviewers,
                        StudentName = student.Value.GetFullName(),
                        StudentClass = student.Value.GetClass()
                    });
                }
            }

            return preassignments;
        }
    }
}
