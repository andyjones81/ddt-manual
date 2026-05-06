namespace DdtManual.Infrastructure.Standards;

public sealed class DdtStandardsResponse
{
    public List<DdtStandardDto> Data { get; set; } = [];
    public DdtStandardsPagination? Pagination { get; set; }
    public string? Stage { get; set; }
}

public sealed class DdtStandardsPagination
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
}

public sealed class DdtStandardDto
{
    public int Id { get; set; }

    /// <summary>Strapi v5 document id (UUID) when the REST entry id is not numeric.</summary>
    public string? DocumentId { get; set; }

    public string? StandardUuid { get; set; }
    public string? LegacyId { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? Version { get; set; }
    public string? Stage { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? FirstPublished { get; set; }
    public DateTime? LastUpdated { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<string> SubCategories { get; set; } = [];
    public List<DdtStandardPhase> Phases { get; set; } = [];
}

public sealed class DdtStandardPhase
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class DdtStandardDetailDto
{
    public int Id { get; set; }
    /// <summary>Strapi documentId (e.g. for linking to Create and manage standards admin).</summary>
    public string? DocumentId { get; set; }
    public string? StandardUuid { get; set; }
    public string? LegacyId { get; set; }
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? Purpose { get; set; }
    public string? HowToMeet { get; set; }
    public string? Governance { get; set; }
    public string? Version { get; set; }
    public string? Stage { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? FirstPublished { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? RelatedGuidance { get; set; }
    public string? LegalBasis { get; set; }
    public bool? LegalStandard { get; set; }
    public List<DdtStandardCategoryDto> Categories { get; set; } = [];
    public List<DdtStandardPhase> Phases { get; set; } = [];
}

public sealed class DdtStandardCategoryDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<DdtStandardSubCategoryDto> SubCategories { get; set; } = [];
}

public sealed class DdtStandardSubCategoryDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
