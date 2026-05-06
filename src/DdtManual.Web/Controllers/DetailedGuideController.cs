using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using DdtManual.Web.Helpers;
using DdtManual.Web.Models.Templates;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

[Route("guidance/guides")]
public sealed class DetailedGuideController(ICmsContentClient cms) : Controller
{
    private const string GuideBase = "/guidance/guides";

    [HttpGet("{guideSlug}")]
    public async Task<IActionResult> Guide(string guideSlug, CancellationToken cancellationToken)
    {
        var dto = await cms.GetDetailedGuideBySlugAsync(guideSlug, cancellationToken);
        if (dto == null)
            return NotFound();

        var model = MapOverview(dto);
        return View("~/Views/Templates/DetailedGuide.cshtml", model);
    }

    [HttpGet("{guideSlug}/{pageSlug}")]
    public async Task<IActionResult> Page(string guideSlug, string pageSlug, CancellationToken cancellationToken)
    {
        var dto = await cms.GetDetailedGuidePageBySlugAsync(guideSlug, pageSlug, cancellationToken);
        if (dto == null)
            return NotFound();

        var model = MapChildPage(dto, guideSlug, pageSlug);
        return View("~/Views/Templates/DetailedGuide.cshtml", model);
    }

    private static ManualDetailedGuidePageModel MapOverview(DetailedGuideOverviewDto d)
    {
        var guideUrl = GuideBase + "/" + Uri.EscapeDataString(d.Slug);
        var collections = d.Collections.Select(c => new ManualCollectionRefModel { Slug = c.Slug, Title = c.Title }).ToList();

        var overviewTitle = string.IsNullOrWhiteSpace(d.OverrideOverviewTitle)
            ? "Overview"
            : d.OverrideOverviewTitle.Trim();

        var contents = new List<ManualGuideContentsItemModel>();
        var n = 1;

        contents.Add(new ManualGuideContentsItemModel
        {
            Number = n,
            Title = overviewTitle,
            Url = null,
            IsCurrent = true,
        });
        n++;

        foreach (var p in d.Pages)
        {
            var pageUrl = guideUrl + "/" + Uri.EscapeDataString(p.Slug);
            contents.Add(new ManualGuideContentsItemModel { Number = n, Title = p.Title, Url = pageUrl, IsCurrent = false });
            n++;
        }

        var showContents = !d.HideContentsOnPrimaryPage && contents.Count > 1;

        string? paginationNextUrl = null;
        string? paginationNextLabel = null;
        if (d.Pages.Count > 0)
        {
            paginationNextUrl = guideUrl + "/" + Uri.EscapeDataString(d.Pages[0].Slug);
            paginationNextLabel = d.Pages[0].Title;
        }

        return new ManualDetailedGuidePageModel
        {
            IsOverviewPage = true,
            GuideSlug = d.Slug,
            HeroTitle = d.Title,
            HeroIntro = d.MetaDescription,
            CollectionSlug = d.CollectionSlug,
            CollectionTitle = d.CollectionTitle,
            Collections = collections,
            ShowLastReviewedDateOnPage = d.ShowLastReviewedDateOnPage,
            LastReviewedDateDisplay = d.LastReviewedDateDisplay,
            ShowOwnerOnPage = d.ShowOwnerOnPage,
            Owner = d.Owner,
            OwnerUrl = d.OwnerUrl,
            ShowContents = showContents,
            ContentsUseNumbers = true,
            ContentsItems = contents,
            BodyHtml = RenderGuideBodyHtml(d.BodyMarkdown, d.Slug, d.Pages, d.ApplicableProfessions, d.ApplicablePhases),
            ShowPageHeader = true,
            PageTitle = overviewTitle,
            PageBeforeContentsHtml = null,
            PaginationNextUrl = paginationNextUrl,
            PaginationNextLabel = paginationNextLabel,
            RelatedContent = MapRelated(d.RelatedContent, d.Slug, d.Pages),
            RelatedFiles = MapFiles(d.RelatedFiles),
            ShowGuidePagesOnRight = false,
            GuidePagesRightNav = [],
            ApplyNoContentsSectionStyle = d.HideContentsOnPrimaryPage,
            CustomCss = d.CustomCss,
            CustomJs = d.CustomJs,
            ShowDraftContentBanner = d.ShowDraftContentBanner,
        };
    }

    private ManualDetailedGuidePageModel MapChildPage(DetailedGuideChildPageDto d, string guideSlug, string pageSlug)
    {
        var guideUrl = GuideBase + "/" + Uri.EscapeDataString(guideSlug);
        var collections = d.Collections.Select(c => new ManualCollectionRefModel { Slug = c.Slug, Title = c.Title }).ToList();

        var overviewTitle = string.IsNullOrWhiteSpace(d.OverrideOverviewTitle)
            ? "Overview"
            : d.OverrideOverviewTitle.Trim();

        var siblings = d.SiblingPages.ToList();
        var contents = new List<ManualGuideContentsItemModel>();
        var rightNav = new List<ManualGuideRightNavItemModel>();
        var num = 1;

        contents.Add(new ManualGuideContentsItemModel
        {
            Number = num,
            Title = overviewTitle,
            Url = guideUrl,
            IsCurrent = false,
        });
        num++;

        foreach (var p in siblings)
        {
            var isCurrent = string.Equals(p.Slug, pageSlug, StringComparison.OrdinalIgnoreCase);
            var url = isCurrent ? null : guideUrl + "/" + Uri.EscapeDataString(p.Slug);
            contents.Add(new ManualGuideContentsItemModel
            {
                Number = num,
                Title = p.Title,
                Url = url,
                IsCurrent = isCurrent,
            });
            num++;
        }

        var rn = 1;
        foreach (var p in siblings)
        {
            var isCurrent = string.Equals(p.Slug, pageSlug, StringComparison.OrdinalIgnoreCase);
            rightNav.Add(new ManualGuideRightNavItemModel
            {
                Number = rn++,
                Title = p.Title,
                Url = isCurrent ? null : guideUrl + "/" + Uri.EscapeDataString(p.Slug),
                IsCurrent = isCurrent,
            });
        }

        var idx = siblings.FindIndex(p => string.Equals(p.Slug, pageSlug, StringComparison.OrdinalIgnoreCase));

        string? prevUrl = null;
        string? prevLabel = null;
        string? nextUrl = null;
        string? nextLabel = null;

        if (idx == 0)
        {
            prevUrl = guideUrl;
            prevLabel = overviewTitle;
        }
        else if (idx > 0)
        {
            prevUrl = guideUrl + "/" + Uri.EscapeDataString(siblings[idx - 1].Slug);
            prevLabel = siblings[idx - 1].Title;
        }

        if (idx >= 0 && idx < siblings.Count - 1)
        {
            nextUrl = guideUrl + "/" + Uri.EscapeDataString(siblings[idx + 1].Slug);
            nextLabel = siblings[idx + 1].Title;
        }

        var showContents = !d.HideTitleAndDescription
            && !d.HideContentsNav
            && !string.IsNullOrWhiteSpace(d.GuideTitle)
            && siblings.Count >= 1;

        var beforeHtml = string.IsNullOrWhiteSpace(d.BeforeContentsMarkdown)
            ? null
            : RenderGuideBodyHtml(d.BeforeContentsMarkdown, guideSlug, siblings, d.ApplicableProfessions, d.ApplicablePhases);

        var showPageHeader = !d.HideTitleAndDescription || !string.IsNullOrWhiteSpace(d.BeforeContentsMarkdown);

        return new ManualDetailedGuidePageModel
        {
            IsOverviewPage = false,
            GuideSlug = guideSlug,
            HeroTitle = d.GuideTitle,
            HeroIntro = d.GuideMetaDescription,
            CollectionSlug = d.CollectionSlug,
            CollectionTitle = d.CollectionTitle,
            Collections = collections,
            ShowLastReviewedDateOnPage = d.ShowLastReviewedDateOnPage,
            LastReviewedDateDisplay = d.LastReviewedDateDisplay,
            ShowOwnerOnPage = d.ShowOwnerOnPage,
            Owner = d.Owner,
            OwnerUrl = d.OwnerUrl,
            ShowContents = showContents,
            ContentsUseNumbers = true,
            ContentsItems = contents,
            BodyHtml = RenderGuideBodyHtml(d.BodyMarkdown, guideSlug, siblings, d.ApplicableProfessions, d.ApplicablePhases),
            ShowPageHeader = showPageHeader,
            PageTitle = d.HideTitleAndDescription ? null : d.PageTitle,
            PageBeforeContentsHtml = beforeHtml,
            PaginationPrevUrl = prevUrl,
            PaginationPrevLabel = prevLabel,
            PaginationNextUrl = nextUrl,
            PaginationNextLabel = nextLabel,
            RelatedContent = MapRelated(d.RelatedContent, guideSlug, siblings),
            RelatedFiles = MapFiles(d.RelatedFiles),
            ShowGuidePagesOnRight = d.ShowGuidePagesOnRight && !d.HideGuidePagesNav && rightNav.Count > 0,
            GuidePagesRightNav = rightNav,
            ApplyNoContentsSectionStyle = d.HideContentsNav || d.HideTitleAndDescription,
            CustomCss = d.CustomCss,
            CustomJs = d.CustomJs,
            ShowDraftContentBanner = d.ShowDraftContentBanner,
        };
    }

    /// <summary>Resolves <c>[[ServiceStandardList]]</c> in related blocks when guide context is provided.</summary>
    private static List<ManualRelatedContentModel> MapRelated(
        IReadOnlyList<CollectionRelatedContentDto> items,
        string? guideSlug = null,
        IReadOnlyList<DetailedGuidePageSummaryDto>? standardListPages = null)
    {
        return items.Select(r =>
        {
            var contentMd = r.ContentMarkdown;
            if (!string.IsNullOrEmpty(contentMd) && !string.IsNullOrWhiteSpace(guideSlug) && standardListPages != null)
                contentMd = GovUkMarkdown.ReplaceServiceStandardListShortcode(contentMd, guideSlug, standardListPages) ?? contentMd;

            return new ManualRelatedContentModel
            {
                HeaderHtml = GovUkMarkdown.ToGovUkInlineHtml(r.HeaderMarkdown),
                ContentHtml = string.IsNullOrWhiteSpace(contentMd)
                    ? null
                    : GovUkMarkdown.ToGovUkHtmlForRelatedContent(contentMd),
            };
        }).ToList();
    }

    /// <summary>
    /// Service Manual order: <c>[[professions]]</c> / <c>[[phases]]</c>, then <c>[[ServiceStandardList]]</c>, then GOV.UK markdown.
    /// </summary>
    private static string RenderGuideBodyHtml(
        string? markdown,
        string guideSlug,
        IReadOnlyList<DetailedGuidePageSummaryDto> pages,
        IReadOnlyList<string>? applicableProfessions = null,
        IReadOnlyList<ApplicablePhaseTagDto>? applicablePhases = null)
    {
        var withAudience = GovUkMarkdown.ReplaceGuideAudienceShortcodes(markdown, applicableProfessions, applicablePhases);
        var withShortcodes = GovUkMarkdown.ReplaceServiceStandardListShortcode(withAudience, guideSlug, pages);
        var html = GovUkMarkdown.ToBodyHtml(withShortcodes ?? string.Empty);
        // Belt-and-braces: shortcodes inside rendered paragraphs (or HTML-only fragments) still match here.
        return GovUkMarkdown.ReplaceServiceStandardListShortcode(html, guideSlug, pages) ?? html;
    }

    private static List<ManualRelatedFileModel> MapFiles(IReadOnlyList<CollectionRelatedFileDto> files) =>
        files.Select(f => new ManualRelatedFileModel
        {
            Name = f.Name,
            Url = f.Url,
            SizeDisplay = f.SizeDisplay,
            FileType = f.FileType,
            Caption = f.Caption,
        }).ToList();
}
