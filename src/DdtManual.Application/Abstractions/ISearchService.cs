using DdtManual.Application.Content;

namespace DdtManual.Application.Abstractions;

/// <summary>
/// Site keyword search over published CMS index and DDT Standards CMS (title/summary scoring matches Service Manual).
/// </summary>
public interface ISearchService
{
    /// <param name="keywords">Optional; empty returns no results.</param>
    /// <param name="types">Optional content-type filter (e.g. <c>Standard</c>, <c>Collection</c>).</param>
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(
        string? keywords,
        IReadOnlyList<string>? types = null,
        CancellationToken cancellationToken = default);
}
