using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using DdtManual.Web.Models.Templates;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

[Route("guidance")]
public sealed class GuidanceController(ICmsContentClient cms) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? guidanceArea,
        [FromQuery] List<string>? professions,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var page = await cms.GetGuidanceIndexAsync(cancellationToken);
        if (page is null)
            return NotFound();

        var selectedProfessionSlugs = (professions ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedProfessionSet = selectedProfessionSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedAreaSlug = string.IsNullOrWhiteSpace(guidanceArea) ? null : guidanceArea.Trim();
        var selectedSearchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        static IReadOnlyList<ManualTagRefModel> ProfessionTagsForCollection(
            ManualGuidanceAreaModel area,
            ManualGuidanceCollectionCardModel collection)
        {
            return collection.ApplicableProfessions
                .Concat(area.FeaturedProfessions)
                .Where(p => !string.IsNullOrWhiteSpace(p.Slug) || !string.IsNullOrWhiteSpace(p.Title))
                .GroupBy(
                    p => !string.IsNullOrWhiteSpace(p.Slug) ? p.Slug : p.Title,
                    StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        var visibleAreas = MapAreas(page)
            .Where(area => string.IsNullOrWhiteSpace(selectedAreaSlug)
                || area.Slug.Equals(selectedAreaSlug, StringComparison.OrdinalIgnoreCase))
            .Select(area => new ManualGuidanceAreaModel
            {
                Name = area.Name,
                Slug = area.Slug,
                Summary = area.Summary,
                Description = area.Description,
                ColourHex = area.ColourHex,
                FeaturedProfessions = area.FeaturedProfessions,
                Collections = area.Collections
                    .Where(collection =>
                    {
                        return selectedProfessionSet.Count == 0
                            || collection.ApplicableProfessions.Any(p =>
                                p.Title.Equals("All DDaT Professions", StringComparison.OrdinalIgnoreCase))
                            || ProfessionTagsForCollection(area, collection)
                                .Any(p => selectedProfessionSet.Contains(p.Slug));
                    })
                    .ToList(),
            })
            .Where(area => area.Collections.Count > 0)
            .ToList();

        var uniqueCards = MapAreas(page)
            .SelectMany(a => a.Collections)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var collectionProfessionMappings = MapAreas(page)
            .SelectMany(area => area.Collections.Select(collection => new
            {
                CardKey = collection.Key,
                Professions = ProfessionTagsForCollection(area, collection),
            }))
            .Where(m => !string.IsNullOrWhiteSpace(m.CardKey))
            .SelectMany(m => m.Professions
                .Where(p => !string.IsNullOrWhiteSpace(p.Slug) && !string.IsNullOrWhiteSpace(p.Title))
                .Select(p => new { m.CardKey, Profession = p }))
            .ToList();

        var professionFilters = collectionProfessionMappings
            .GroupBy(x => x.Profession.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ManualGuidanceFilterOptionModel
            {
                Slug = g.First().Profession.Slug,
                Label = g.First().Profession.Title,
                Count = g.Select(x => x.CardKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            })
            .OrderBy(o => o.Label)
            .ToList();

        var model = new ManualGuidanceIndexViewModel
        {
            Areas = visibleAreas,
            AreaFilters = MapAreas(page)
                .Select(a => new ManualGuidanceFilterOptionModel
                {
                    Label = a.Name,
                    Slug = a.Slug,
                    Count = a.CollectionCount,
                    ColourHex = a.ColourHex,
                })
                .ToList(),
            ProfessionFilters = professionFilters,
            SelectedGuidanceAreaSlug = selectedAreaSlug,
            SelectedProfessionSlugs = selectedProfessionSlugs,
            SelectedSearchTerm = selectedSearchTerm,
            TotalCollectionCount = uniqueCards.Count,
        };

        ViewData["Title"] = "Guidance";
        ViewBag.HeroType = "collection";
        ViewBag.HeroTitle = "Guidance for DfE digital, data and technology teams";
        ViewBag.HeroIntro =
            "Browse guidance by area — grouped around the disciplines and topics that matter to your team. Each area contains collections of guides, standards, patterns and tools.";
        ViewBag.UseInverseNav = true;
        ViewBag.HideBadge = true;

        return View("~/Views/Templates/Guidance.cshtml", model);
    }

    private static List<ManualGuidanceAreaModel> MapAreas(GuidanceIndexDto page)
    {
        return page.Areas
            .Where(a => !string.IsNullOrWhiteSpace(a.Slug) && !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new ManualGuidanceAreaModel
            {
                Name = a.Name,
                Slug = a.Slug,
                Summary = a.Summary,
                Description = a.Description,
                ColourHex = a.ColourHex,
                FeaturedProfessions = a.FeaturedProfessions
                    .Select(t => new ManualTagRefModel { Slug = t.Slug, Title = t.Title })
                    .ToList(),
                Collections = a.Collections
                    .Select(c => new ManualGuidanceCollectionCardModel
                    {
                        Title = c.Title,
                        Slug = c.Slug,
                        Url = c.Url,
                        ContentType = c.ContentType,
                        Description = c.Description,
                        ItemCount = c.ItemCount,
                        Featured = c.Featured,
                        Tags = c.Tags.ToList(),
                        ApplicableProfessions = c.ApplicableProfessions
                            .Select(t => new ManualTagRefModel { Slug = t.Slug, Title = t.Title })
                            .ToList(),
                        AlsoInAreas = c.AlsoInAreas
                            .Select(x => new ManualCollectionRefModel { Title = x.Title, Slug = x.Slug })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();
    }
}
