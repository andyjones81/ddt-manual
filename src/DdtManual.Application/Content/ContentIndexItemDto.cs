namespace DdtManual.Application.Content;

/// <summary>Published CMS item for the /content index (aligned with Service Manual <c>ContentIndexItem</c>).</summary>
public sealed class ContentIndexItemDto
{
    public string Title { get; init; } = string.Empty;
    public string? MetaDescription { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? ParentContentType { get; init; }
    public string? ParentSlug { get; init; }
    public string? CollectionTitle { get; init; }
    public string? CollectionSlug { get; init; }
    public IReadOnlyList<TagRefDto> ApplicablePhaseTags { get; init; } = [];
    public IReadOnlyList<TagRefDto> ApplicableProfessionTags { get; init; } = [];
}
