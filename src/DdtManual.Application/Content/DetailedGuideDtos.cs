namespace DdtManual.Application.Content;

/// <summary>Published detailed guide (overview) from Strapi.</summary>
public sealed class DetailedGuideOverviewDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? MetaDescription { get; init; }
    public string? BodyMarkdown { get; init; }
    public string? OverrideOverviewTitle { get; init; }
    public bool HideContentsOnPrimaryPage { get; init; }
    public bool ShowGuidePagesOnRight { get; init; }
    public IReadOnlyList<DetailedGuidePageSummaryDto> Pages { get; init; } = [];
    public string? CollectionSlug { get; init; }
    public string? CollectionTitle { get; init; }
    public IReadOnlyList<CollectionRefDto> Collections { get; init; } = [];
    public IReadOnlyList<CollectionRelatedContentDto> RelatedContent { get; init; } = [];
    public IReadOnlyList<CollectionRelatedFileDto> RelatedFiles { get; init; } = [];
    public bool ShowLastReviewedDateOnPage { get; init; }
    public string? LastReviewedDateDisplay { get; init; }
    public bool ShowOwnerOnPage { get; init; } = true;
    public string? Owner { get; init; }
    public string? OwnerUrl { get; init; }
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
    public bool ShowDraftContentBanner { get; init; }

    /// <summary>Audience labels for <c>[[professions]]</c> (tags-profession plural/title).</summary>
    public IReadOnlyList<string> ApplicableProfessions { get; init; } = [];

    /// <summary>Phase tags for <c>[[phases]]</c>.</summary>
    public IReadOnlyList<ApplicablePhaseTagDto> ApplicablePhases { get; init; } = [];
}

public sealed class DetailedGuidePageSummaryDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}

/// <summary>tags-phase entry for <c>[[phases]]</c> shortcode (matches Service Manual).</summary>
public sealed record ApplicablePhaseTagDto(string Slug, string Title);

public sealed class CollectionRefDto
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

/// <summary>Child guide page from Strapi (after find + detail fetch).</summary>
public sealed record DetailedGuideChildPageDto
{
    public string PageTitle { get; init; } = string.Empty;
    public string PageSlug { get; init; } = string.Empty;
    public string? MetaDescription { get; init; }
    public string? BodyMarkdown { get; init; }
    public string? BeforeContentsMarkdown { get; init; }
    public bool HideTitleAndDescription { get; init; }
    public bool HideContentsNav { get; init; }
    public bool HideGuidePagesNav { get; init; }

    public string GuideTitle { get; init; } = string.Empty;
    public string GuideSlug { get; init; } = string.Empty;
    public string? GuideMetaDescription { get; init; }
    public string? OverrideOverviewTitle { get; init; }
    public bool HideContentsOnPrimaryPage { get; init; }
    public bool ShowGuidePagesOnRight { get; init; }

    public string? CollectionSlug { get; init; }
    public string? CollectionTitle { get; init; }
    public IReadOnlyList<CollectionRefDto> Collections { get; init; } = [];

    /// <summary>Ordered sibling pages (same order as overview) for contents + pagination.</summary>
    public IReadOnlyList<DetailedGuidePageSummaryDto> SiblingPages { get; init; } = [];

    public IReadOnlyList<CollectionRelatedContentDto> RelatedContent { get; init; } = [];
    public IReadOnlyList<CollectionRelatedFileDto> RelatedFiles { get; init; } = [];

    public bool ShowLastReviewedDateOnPage { get; init; }
    public string? LastReviewedDateDisplay { get; init; }
    public bool ShowOwnerOnPage { get; init; } = true;
    public string? Owner { get; init; }
    public string? OwnerUrl { get; init; }

    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
    public bool ShowDraftContentBanner { get; init; }

    /// <summary>Audience labels for <c>[[professions]]</c> on this guide page.</summary>
    public IReadOnlyList<string> ApplicableProfessions { get; init; } = [];

    /// <summary>Phase tags for <c>[[phases]]</c> on this guide page.</summary>
    public IReadOnlyList<ApplicablePhaseTagDto> ApplicablePhases { get; init; } = [];
}
