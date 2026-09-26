namespace MockInterviews.Models.ViewModels.UserRolesController
{
    public class ManageUserRolesPageViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<ManageUserRolesViewModel> Roles { get; set; } = [];
    }

    public class ManageUserRolesViewModel
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}
