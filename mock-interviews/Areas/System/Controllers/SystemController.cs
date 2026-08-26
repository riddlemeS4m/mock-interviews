using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MockInterviews.Data.Constants;

namespace MockInterviews.Areas.SystemArea.Controllers;

[Area("System")]
[Authorize(Roles = RolesConstants.SystemAdminRole)]
public sealed class SystemController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
