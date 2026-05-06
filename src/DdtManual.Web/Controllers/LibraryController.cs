using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

public sealed class LibraryController : Controller
{
    [Route("your-library")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Your library";
        return View("~/Views/Library/Index.cshtml");
    }

    [Route("my-library")]
    public IActionResult LegacyMyLibraryRedirect() =>
        RedirectPermanent("/your-library");
}
