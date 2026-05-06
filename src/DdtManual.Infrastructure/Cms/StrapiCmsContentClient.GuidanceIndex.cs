using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient
{
    public async Task<GuidanceIndexDto?> GetGuidanceIndexAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
            return null;

        const string relative = "api/guidance-areas/index";
        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync(relative, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for guidance index", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return StrapiGuidanceIndexMapper.Map(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Guidance index request failed");
            return null;
        }
    }
}
