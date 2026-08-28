using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Data.Constants
{
    //does not need to be update-able
    //database can provide meaning through numbers, app can interpret numbers through enum
    public class StatusConstants
    {
        //moving a student's interview to completed or no show status removes it from the list of interviews that are displayed
        public const string Default = "Not Arrived";
        public const string CheckedIn = "Checked In";
        public const string Ongoing = "Ongoing";
        public const string Completed = "Completed";
        public const string NoShow = "No Show";
        public const string Excused = "Excused";

        public static List<SelectListItem> GetCompleteStatusOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = Default, Value = Default },
                new() { Text = CheckedIn, Value = CheckedIn },
                new() { Text = Ongoing, Value = Ongoing },
                new() { Text = Completed, Value = Completed },
                new() { Text = NoShow, Value = NoShow },
                new() { Text = Excused, Value = Excused },
            };
        }

        public static List<SelectListItem> GetUnassignedStatusOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = Default, Value = Default },
                new() { Text = CheckedIn, Value = CheckedIn },
                new() { Text = NoShow, Value = NoShow },
            };

        }
        public static List<SelectListItem> GetInterviewerCompleteStatusOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = Completed, Value = Completed },
                new() { Text = NoShow, Value = NoShow },
            };
        }

        public static string GetStatusText(Statuses? status)
        {
            return status switch
            {
                Statuses.Default => Default,
                Statuses.CheckedIn => CheckedIn,
                Statuses.Ongoing => Ongoing,
                Statuses.Completed => Completed,
                Statuses.NoShow => NoShow,
                Statuses.Excused => Excused,
                _ => string.Empty,
            };
        }

        public enum Statuses
        {
            Default,
            CheckedIn,
            Ongoing,
            Completed,
            NoShow,
            Excused
        }
    }
}
