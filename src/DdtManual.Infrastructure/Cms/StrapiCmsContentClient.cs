using System.Text.Json;
using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient(
    IHttpClientFactory httpClientFactory,
    IOptions<CmsOptions> options,
    ILogger<StrapiCmsContentClient> logger) : ICmsContentClient
{
    internal const string HttpClientName = "Cms";

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var ok = !string.IsNullOrWhiteSpace(options.Value.BaseUrl);
        return Task.FromResult(ok);
    }

    public async Task<HomepageDto?> GetHomepageAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
            return null;

        const string relative =
            "api/homepage?fields[0]=title&fields[1]=headline&fields[2]=html&fields[3]=customJS&fields[4]=customCSS";

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync(relative, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for homepage", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return MapHomepage(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Homepage request failed");
            return null;
        }
    }

    private static HomepageDto? MapHomepage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;

        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var source = data;
        if (data.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
            source = attributes;

        return new HomepageDto
        {
            Title = GetString(source, "title") ?? string.Empty,
            Headline = GetString(source, "headline"),
            Html = GetString(source, "html"),
            CustomJs = GetString(source, "customJS") ?? GetString(source, "customJs"),
            CustomCss = GetString(source, "customCSS") ?? GetString(source, "customCss"),
        };
    }

    private static string? GetString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Null => null,
            _ => null,
        };
    }
}
