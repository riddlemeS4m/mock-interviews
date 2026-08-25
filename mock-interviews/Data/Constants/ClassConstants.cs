using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Data.Constants
{
    //does not need to be update-able
    //database can provide meaning through numbers, app can interpret numbers and display strings
    public class ClassConstants
    {
        public const string NotEnrolled = "No Class Specified";
        public const string PreMIS = "Pre-MIS (MIS 200)";
        public const string FirstSemester = "1st Semester (MIS 221)";
        public const string SecondSemester = "2nd Semester (MIS 321 / MIS 330)";
        public const string ThirdSemester = "3rd Semester (MIS 405 / MIS 430)";
        public const string Capstone = "4th Semester (Capstone)";
        public const string PostCapstone = "Post-Capstone"; //let's make AMP a special designation somewhere
        public const string Cybersecurity = "Cybersecurity Major";
        //public const string MBA = "Stem to MBA/Traditional MBA";

        public static IEnumerable<SelectListItem> GetClassOptions()
        {
            var classes = Enum.GetValues(typeof(Classes))
                              .Cast<Classes>()
                              .Select(e => new SelectListItem
                              {
                                  Value = e.ToString(),
                                  Text = GetClassText(e)
                              }).ToList();
            classes.Remove(classes.First());

            return classes;
        }

        public static string GetClassText(Classes? cls)
        {
            return cls switch
            {
                Classes.NotEnrolled => NotEnrolled,
                Classes.NotYetMIS => PreMIS,
                Classes.FirstSem => FirstSemester,
                Classes.SecondSem => SecondSemester,
                Classes.ThirdSem => ThirdSemester,
                Classes.Capstone => Capstone,
                Classes.PostCapstone => PostCapstone,
                Classes.Cybersecurity => Cybersecurity,
                _ => string.Empty,
            };
        }
    }

    public enum Classes
    {
        NotEnrolled,
        NotYetMIS,
        FirstSem,
        SecondSem,
        ThirdSem,
        Capstone,
        PostCapstone,
        Cybersecurity,
        //MBA
    }
}
