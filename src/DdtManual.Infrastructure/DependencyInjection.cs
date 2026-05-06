using System.Net.Http.Headers;
using DdtManual.Application.Abstractions;
using DdtManual.Infrastructure.Cms;
using DdtManual.Infrastructure.Search;
using DdtManual.Infrastructure.Standards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace DdtManual.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CmsOptions>(configuration.GetSection(CmsOptions.SectionName));
        services.Configure<StandardsCmsOptions>(configuration.GetSection(StandardsCmsOptions.SectionName));

        services.AddHttpClient(StrapiCmsContentClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CmsOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                var trimmed = opts.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri(trimmed + "/");
            }

            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        services.AddHttpClient(SiteSearchService.StandardsHttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<StandardsCmsOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                var trimmed = opts.BaseUrl.TrimEnd('/');
                client.BaseAddress = new Uri(trimmed + "/");
            }

            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        services.AddHttpClient<DdtStandardsApiService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<StandardsCmsOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        services.AddScoped<ICmsContentClient, StrapiCmsContentClient>();
        services.AddScoped<ISearchService, SiteSearchService>();
        return services;
    }
}
