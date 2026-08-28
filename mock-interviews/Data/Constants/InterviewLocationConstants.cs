using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Data.Constants
{
    //does not need to be update-able
    //database can provide meaning through numbers, app can interpret numbers through enum
    public class InterviewLocationConstants
    {
        public const string InPerson = "In Person";
        public const string IsVirtual = "Virtual";

        public static List<SelectListItem> GetLocationOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = InPerson, Value = InPerson },
                new() { Text = IsVirtual, Value = IsVirtual },
            };
        }

        public static string GetLocationText(InterviewLocations? location)
        {
            return location switch
            {
                InterviewLocations.InPerson => InPerson,
                InterviewLocations.IsVirtual => IsVirtual,
                _ => string.Empty,
            };
        }
    }

    public enum InterviewLocations
    {
        InPerson,
        IsVirtual,
    }
}
