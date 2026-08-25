using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Data.Constants
{
    //does not need to be update-able
    //database can provide meaning through numbers, app can interpret numbers through enum
    public class InterviewTypeConstants
    {
        public const string Behavioral = "Behavioral";
        public const string Technical = "Technical";
        public const string Case = "Case";

        public static List<SelectListItem> GetInterviewTypesOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = Behavioral, Value = Behavioral },
                new() { Text = Technical, Value = Technical },
                new() { Text = Case, Value = Case }
            };
        }

        public static string GetInterviewTypeText(InterviewTypes? interviewType)
        {
            return interviewType switch
            {
                InterviewTypes.Behavioral => Behavioral,
                InterviewTypes.Technical => Technical,
                InterviewTypes.Case => Case,
                _ => string.Empty
            };
        }
    }

    public enum InterviewTypes
    {
        Behavioral,
        Technical,
        Case
    }
}
