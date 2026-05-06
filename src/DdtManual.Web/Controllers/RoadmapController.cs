using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

[Route("roadmap")]
public sealed class RoadmapController(
    ICmsContentClient cmsContentClient,
    IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var roadmap = await cmsContentClient.GetRoadmapAsync(cancellationToken);

        if (roadmap is null)
        {
            if (environment.IsDevelopment())
                roadmap = RoadmapDto.DevelopmentPlaceholder();
            else
                return NotFound();
        }

        return View(roadmap);
    }
}
