using System.Text.Json;
using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient
{
    public async Task<RoadmapDto?> GetRoadmapAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
            return null;

        const string relative =
            "api/roadmap?fields[0]=title&fields[1]=metaDescription&fields[2]=body&fields[3]=updateHistory";

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync(relative, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for roadmap", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return MapRoadmap(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Roadmap request failed");
            return null;
        }
    }

    private static RoadmapDto? MapRoadmap(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;

        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var source = data;
        if (data.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
            source = attributes;

        return new RoadmapDto
        {
            Title = GetString(source, "title") ?? string.Empty,
            MetaDescription = GetString(source, "metaDescription"),
            BodyMarkdown = StrapiRichTextMarkdown.FromJsonElement(source, "body"),
            UpdateHistoryMarkdown = StrapiRichTextMarkdown.FromJsonElement(source, "updateHistory"),
        };
    }
}
