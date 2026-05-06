using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient
{
    public async Task<IReadOnlyList<ContentIndexItemDto>> GetPublishedContentIndexAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
            return [];

        var client = httpClientFactory.CreateClient(HttpClientName);
        var items = new List<ContentIndexItemDto>();
        try
        {
            await PublishedContentIndexAggregator.AggregateAsync(client, logger, items, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Published content index aggregation failed");
        }

        return items
            .OrderBy(i => i.ContentType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
