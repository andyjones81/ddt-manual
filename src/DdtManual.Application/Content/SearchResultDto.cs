namespace DdtManual.Application.Content;

/// <summary>A single search hit from CMS content or DDT Standards (same shape as Service Manual <c>SearchResultItem</c>).</summary>
public sealed class SearchResultDto
{
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string? PartOfCollectionTitle { get; init; }
    public string? PartOfCollectionUrl { get; init; }
}
