using Microsoft.AspNetCore.Mvc.Rendering;

namespace MockInterviews.Data.Constants
{
    //does not need to be update-able
    //database can provide meaning through numbers, app can interpret numbers through enum
    //also needs to be part of a seed class
    public class RolesConstants
    {
        public const string AdminRole = "admin";
        public const string StudentRole = "student";
        public const string InterviewerRole = "interviewer";
        //public const string DesignateStudent = "crimson.ua.edu"; //this DOES need to be update-able

        public static List<SelectListItem> GetRoleOptions()
        {
            return new List<SelectListItem>
            {
                new() { Text = AdminRole, Value = AdminRole },
                new() { Text = StudentRole, Value = StudentRole },
                new() { Text = InterviewerRole, Value = InterviewerRole }
            };
        }

        public static string GetRoleText(Roles? role)
        {
            return role switch
            {
                Roles.admin => AdminRole,
                Roles.student => StudentRole,
                Roles.interviewer => InterviewerRole,
                _ => string.Empty
            };
        }
    }

    //probably not necessary
    //in principle, rolesconstants will do the same thing as roles for minimal impact
    public enum Roles
    {
        admin,
        student,
        interviewer
    }
}
