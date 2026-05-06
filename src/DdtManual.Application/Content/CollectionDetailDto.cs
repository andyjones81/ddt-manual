namespace DdtManual.Application.Content;

/// <summary>CMS collection document mapped from Strapi for rendering the collection template.</summary>
public sealed class CollectionDetailDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string MetaDescription { get; init; } = string.Empty;
    /// <summary>Markdown body from Strapi (converted to HTML in the web layer).</summary>
    public string? BodyMarkdown { get; init; }
    public IReadOnlyList<CollectionSectionDto> Sections { get; init; } = [];
    public IReadOnlyList<CollectionRelatedContentDto> RelatedContent { get; init; } = [];
    public IReadOnlyList<CollectionRelatedFileDto> RelatedFiles { get; init; } = [];
    public bool ShowDraftContentBanner { get; init; }
    public bool ShowLastReviewedDateOnPage { get; init; }
    public string? LastReviewedDateDisplay { get; init; }
    public string? Owner { get; init; }
    public string? OwnerUrl { get; init; }
    public IReadOnlyList<TagRefDto> AudienceTags { get; init; } = [];
}

public sealed class CollectionSectionDto
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<CollectionLinkDto> Items { get; init; } = [];
}

public sealed class CollectionLinkDto
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public string? LinkType { get; init; }
    public string? Grade { get; init; }
    public bool OpenInNewTab { get; init; }
    public bool PriorityInGroup { get; init; }
    /// <summary>Used for job-spec links (<c>?collection=</c>) when rendering the collection page.</summary>
    public string? CollectionSlugForQuery { get; init; }
}

public sealed class CollectionRelatedContentDto
{
    public string HeaderMarkdown { get; init; } = string.Empty;
    public string? ContentMarkdown { get; init; }
}

public sealed class CollectionRelatedFileDto
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string SizeDisplay { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public string? Caption { get; init; }
}
