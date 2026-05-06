namespace DdtManual.Application.Content;

/// <summary>Payload from Strapi <c>GET /api/guidance-areas/index</c> (custom guidance-area controller).</summary>
public sealed class GuidanceIndexDto
{
    public IReadOnlyList<GuidanceAreaGroupDto> Areas { get; init; } = [];
}

public sealed class GuidanceAreaGroupDto
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string ColourHex { get; init; } = "#1d70b8";
    public IReadOnlyList<GuidanceTagRefDto> FeaturedProfessions { get; init; } = [];
    public IReadOnlyList<GuidanceCollectionCardDto> Collections { get; init; } = [];
}

public sealed class GuidanceTagRefDto
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public sealed class GuidanceCollectionCardDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = "Collection";
    public string Description { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public bool Featured { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<GuidanceTagRefDto> ApplicableProfessions { get; init; } = [];
    public IReadOnlyList<GuidanceAreaRefDto> AlsoInAreas { get; init; } = [];
}

public sealed class GuidanceAreaRefDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}
