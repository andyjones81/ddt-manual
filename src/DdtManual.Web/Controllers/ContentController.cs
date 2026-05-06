using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

/// <summary>Published CMS content index (aligned with Service Manual <c>/content</c>).</summary>
[Route("content")]
public sealed class ContentController(ICmsContentClient cmsContentClient) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await cmsContentClient.GetPublishedContentIndexAsync(cancellationToken);
        return View(items);
    }

    /// <summary>GET /content/profession/{slug} — content tagged with this profession.</summary>
    [HttpGet("profession/{slug}")]
    public async Task<IActionResult> ByProfession(string slug, CancellationToken cancellationToken)
    {
        var items = await cmsContentClient.GetPublishedContentIndexAsync(cancellationToken);
        var tag = items
            .SelectMany(i => i.ApplicableProfessionTags)
            .FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (tag == null)
            return NotFound();

        var professionItems = items
            .Where(i => i.ApplicableProfessionTags.Any(pt =>
                string.Equals(pt.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var model = new ContentByProfessionViewModel
        {
            ProfessionTitle = tag.Title,
            ProfessionSlug = tag.Slug,
            Items = professionItems,
        };
        return View(model);
    }
}
