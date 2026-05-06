using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

public sealed class HomeController(
    ICmsContentClient cmsContentClient,
    IWebHostEnvironment environment) : Controller
{
    [Route("/")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var homepage = await cmsContentClient.GetHomepageAsync(cancellationToken);

        if (homepage is null)
        {
            if (environment.IsDevelopment())
                homepage = HomepageDto.DevelopmentPlaceholder();
            else
                return NotFound();
        }

        return View(homepage);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
