namespace DdtManual.Web.Models.Templates;

/// <summary>Pre-rendered collection page — map from CMS in the application layer.</summary>
public sealed class ManualCollectionPageModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    /// <summary>Main intro / body HTML (already sanitised for your trust boundary).</summary>
    public string? BodyHtml { get; set; }
    public IReadOnlyList<ManualCollectionSectionModel> Sections { get; set; } = [];
    public IReadOnlyList<ManualRelatedContentModel> RelatedContent { get; set; } = [];
    public IReadOnlyList<ManualRelatedFileModel> RelatedFiles { get; set; } = [];
    public bool ShowDraftContentBanner { get; set; }
    public bool ShowLastReviewedDateOnPage { get; set; }
    public string? LastReviewedDateDisplay { get; set; }
    public string? Owner { get; set; }
    public string? OwnerUrl { get; set; }
    public IReadOnlyList<ManualTagRefModel> AudienceTags { get; set; } = [];
}

public sealed class ManualCollectionSectionModel
{
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<ManualCollectionLinkModel> Items { get; set; } = [];
}

public sealed class ManualCollectionLinkModel
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? LinkType { get; set; }
    public string? Grade { get; set; }
    public bool OpenInNewTab { get; set; }
    /// <summary>Optional collection slug for job-spec style query strings.</summary>
    public string? CollectionSlugForQuery { get; set; }
}

public sealed class ManualRelatedContentModel
{
    public string HeaderHtml { get; set; } = string.Empty;
    public string? ContentHtml { get; set; }
}

public sealed class ManualRelatedFileModel
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SizeDisplay { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

public sealed class ManualCollectionRefModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class ManualTagRefModel
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

/// <summary>Detailed guide overview or child page — align fields with the main Service Manual when mapping from CMS.</summary>
public sealed class ManualDetailedGuidePageModel
{
    public bool IsOverviewPage { get; set; } = true;
    public string GuideSlug { get; set; } = string.Empty;
    public string HeroTitle { get; set; } = string.Empty;
    public string? HeroIntro { get; set; }
    public string? CollectionSlug { get; set; }
    public string? CollectionTitle { get; set; }
    public IReadOnlyList<ManualCollectionRefModel> Collections { get; set; } = [];
    public bool ShowLastReviewedDateOnPage { get; set; }
    public string? LastReviewedDateDisplay { get; set; }
    public bool ShowOwnerOnPage { get; set; } = true;
    public string? Owner { get; set; }
    public string? OwnerUrl { get; set; }
    public bool ShowContents { get; set; } = true;
    public bool ContentsUseNumbers { get; set; } = true;
    public IReadOnlyList<ManualGuideContentsItemModel> ContentsItems { get; set; } = [];
    public string? BodyHtml { get; set; }
    public bool ShowPageHeader { get; set; }
    public string? PageTitle { get; set; }
    public string? PageBeforeContentsHtml { get; set; }
    public string? PaginationPrevUrl { get; set; }
    public string? PaginationPrevLabel { get; set; }
    public string? PaginationNextUrl { get; set; }
    public string? PaginationNextLabel { get; set; }
    public IReadOnlyList<ManualRelatedContentModel> RelatedContent { get; set; } = [];
    public IReadOnlyList<ManualRelatedFileModel> RelatedFiles { get; set; } = [];
    public bool ShowGuidePagesOnRight { get; set; }
    public IReadOnlyList<ManualGuideRightNavItemModel> GuidePagesRightNav { get; set; } = [];
    public bool ApplyNoContentsSectionStyle { get; set; }
    public string? CustomCss { get; set; }
    public string? CustomJs { get; set; }
    public bool ShowDraftContentBanner { get; set; }
}

public sealed class ManualGuideContentsItemModel
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class ManualGuideRightNavItemModel
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class ManualStandardsListPageModel
{
    public string PageTitle { get; set; } = "DDT Standards";
    public string Intro { get; set; } = string.Empty;
    public IReadOnlyList<ManualStandardSummaryModel> Standards { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SearchQuery { get; set; }
    public IReadOnlyList<string> Categories { get; set; } = [];
    public IReadOnlyList<string> SelectedCategories { get; set; } = [];
    /// <summary>Counts per category label for filter sidebar (full dataset).</summary>
    public IReadOnlyDictionary<string, int> CategoryCounts { get; set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public int TotalStandardsCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchQuery) || SelectedCategories.Count > 0;
}

public sealed class ManualStandardSummaryModel
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CategoryLabel { get; set; }
}

public sealed class ManualStandardDetailPageModel
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? PurposeHtml { get; set; }
    public string? HowToMeetHtml { get; set; }
    public string? GovernanceHtml { get; set; }
    public string? RelatedGuidanceHtml { get; set; }
    public IReadOnlyList<ManualStandardCategoryModel> Categories { get; set; } = [];
    public bool LegalStandard { get; set; }
    public string? CompassStandardUrl { get; set; }
    public string? Version { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime? FirstPublished { get; set; }
    public IReadOnlyList<ManualStandardSummaryModel> RelatedStandards { get; set; } = [];
    public string ListPageUrl { get; set; } = "/standards";
}

public sealed class ManualStandardCategoryModel
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> SubCategoryNames { get; set; } = [];
}

public sealed class ManualSearchPageModel
{
    public string Keywords { get; set; } = string.Empty;
    public bool HighlightKeywords { get; set; } = true;
    /// <summary>Active <c>type</c> query filters.</summary>
    public IReadOnlyList<string> Types { get; set; } = [];
    /// <summary>Content-type counts for the current result set.</summary>
    public IReadOnlyList<ManualSearchFacetModel> Facets { get; set; } = [];
    public IReadOnlyList<ManualSearchResultItemModel> Results { get; set; } = [];
}

public sealed class ManualSearchFacetModel
{
    public string ContentType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ManualSearchResultItemModel
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? PartOfCollectionTitle { get; set; }
    public string? PartOfCollectionUrl { get; set; }
}

/// <summary>/guidance index — areas and collection/guide cards from CMS guidance-areas index API.</summary>
public sealed class ManualGuidanceIndexViewModel
{
    public IReadOnlyList<ManualGuidanceAreaModel> Areas { get; set; } = [];
    public IReadOnlyList<ManualGuidanceFilterOptionModel> AreaFilters { get; set; } = [];
    public IReadOnlyList<ManualGuidanceFilterOptionModel> ProfessionFilters { get; set; } = [];
    public string? SelectedGuidanceAreaSlug { get; set; }
    public IReadOnlyList<string> SelectedProfessionSlugs { get; set; } = [];
    public string? SelectedSearchTerm { get; set; }
    public int TotalCollectionCount { get; set; }
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SelectedGuidanceAreaSlug)
        || SelectedProfessionSlugs.Count > 0
        || !string.IsNullOrWhiteSpace(SelectedSearchTerm);
}

public sealed class ManualGuidanceAreaModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string ColourHex { get; set; } = "#1d70b8";
    public IReadOnlyList<ManualTagRefModel> FeaturedProfessions { get; set; } = [];
    public IReadOnlyList<ManualGuidanceCollectionCardModel> Collections { get; set; } = [];
    public int CollectionCount => Collections.Count;
    public int TotalItemCount => Collections.Sum(c => c.ItemCount);
    public string IntroText => !string.IsNullOrWhiteSpace(Description)
        ? Description!
        : Summary ?? string.Empty;
}

public sealed class ManualGuidanceCollectionCardModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = "Collection";
    public string Description { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public bool Featured { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
    public IReadOnlyList<ManualTagRefModel> ApplicableProfessions { get; set; } = [];
    public IReadOnlyList<ManualCollectionRefModel> AlsoInAreas { get; set; } = [];
    public string Key => $"{ContentType}:{Slug}";
}

public sealed class ManualGuidanceFilterOptionModel
{
    public string Label { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? ColourHex { get; set; }
}
