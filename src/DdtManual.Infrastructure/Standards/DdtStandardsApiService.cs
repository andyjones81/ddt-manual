using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DdtManual.Infrastructure.Standards;

/// <summary>Standards Strapi CMS API client (same integration surface as Service Manual <c>DdtStandardsApiService</c>).</summary>
public sealed class DdtStandardsApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DdtStandardsApiService> _logger;
    private readonly IConfiguration _configuration;
    private readonly StandardsCmsOptions _cmsOptions;

    public DdtStandardsApiService(
        HttpClient httpClient,
        ILogger<DdtStandardsApiService> logger,
        IConfiguration configuration,
        IOptions<StandardsCmsOptions> cmsOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _cmsOptions = cmsOptions.Value;
    }

    /// <summary>Get published standards (search, categories, pagination).</summary>
    public async Task<DdtStandardsResponse?> GetPublishedStandardsAsync(
        string? search = null,
        IEnumerable<string>? categories = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                "sort=title",
                $"pagination[page]={page}",
                $"pagination[pageSize]={pageSize}",
                "pagination[withCount]=true",
                "populate[categories]=true",
                "populate[sub_categories]=true"
            };

            if (!string.IsNullOrWhiteSpace(search))
                queryParams.Add($"filters[title][$containsi]={Uri.EscapeDataString(search.Trim())}");
            if (categories != null)
            {
                var catList = categories.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                for (var i = 0; i < catList.Count; i++)
                    queryParams.Add($"filters[categories][title][$in][{i}]={Uri.EscapeDataString(catList[i]!.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                var dir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
                queryParams.Add($"sort[0]={Uri.EscapeDataString(sortBy!.Trim())}:{dir}");
            }

            var queryString = string.Join("&", queryParams);
            var url = $"api/standards?{queryString}";

            _logger.LogInformation("Fetching published standards from Standards CMS: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var root = JsonNode.Parse(json);
            var dataArray = root?["data"] as JsonArray;
            var meta = root?["meta"];
            var pagination = meta?["pagination"];

            var list = new List<DdtStandardDto>();
            if (dataArray != null)
            {
                foreach (var item in dataArray)
                {
                    var dto = MapStrapiStandardToDto(item);
                    if (dto != null)
                        list.Add(dto);
                }
            }

            var total = pagination?["total"]?.GetValue<int>() ?? list.Count;
            var pageCount = pagination?["pageCount"]?.GetValue<int>() ?? 1;

            _logger.LogInformation("Successfully fetched {Count} standards from Standards CMS", list.Count);

            return new DdtStandardsResponse
            {
                Data = list,
                Pagination = new DdtStandardsPagination
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = pageCount,
                    TotalRecords = total
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching published standards from Standards CMS");
            throw;
        }
    }

    /// <summary>Get a single published standard by slug.</summary>
    public async Task<DdtStandardDetailDto?> GetStandardBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var trimmed = slug.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            var canonical = ResolveCanonicalSlug(trimmed);

            var detail = await FetchStandardDetailBySlugFilterAsync(canonical, cancellationToken).ConfigureAwait(false);
            if (detail != null)
                return detail;

            if (!string.Equals(trimmed, canonical, StringComparison.OrdinalIgnoreCase))
            {
                detail = await FetchStandardDetailBySlugFilterAsync(trimmed, cancellationToken).ConfigureAwait(false);
                if (detail != null)
                    return detail;
            }

            return await TryResolveStandardViaPublishedListAsync(trimmed, canonical, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON deserialization error for standard slug {Slug}", slug);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching standard with slug {Slug}", slug);
            throw;
        }
    }

    /// <summary>
    /// Built-in redirects for renamed standards (config <see cref="StandardsCmsOptions.SlugAliases"/> overrides these keys).
    /// </summary>
    private static readonly Dictionary<string, string> BuiltInPublicSlugAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Previous site used /standards/ddt-standards/accessibility-conformance; some links use "accessibility-statements".
        ["accessibility-statements"] = "accessibility-conformance",
    };

    private string ResolveCanonicalSlug(string requestedSlug)
    {
        var aliases = _cmsOptions.SlugAliases;
        if (aliases != null)
        {
            foreach (var kv in aliases)
            {
                if (string.Equals(kv.Key, requestedSlug, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }
        }

        if (BuiltInPublicSlugAliases.TryGetValue(requestedSlug, out var builtIn) && !string.IsNullOrWhiteSpace(builtIn))
            return builtIn.Trim();

        return requestedSlug;
    }

    private async Task<DdtStandardDetailDto?> FetchStandardDetailBySlugFilterAsync(string slug, CancellationToken cancellationToken)
    {
        var queryParams = "filters[slug][$eq]=" + Uri.EscapeDataString(slug) +
            "&populate[categories][populate][sub_categories]=true" +
            "&populate[sub_categories]=true" +
            "&populate[phases]=true";
        var url = $"api/standards?{queryParams}";

        _logger.LogInformation("Fetching standard with slug {Slug} from Standards CMS", slug);

        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Standards CMS returned status {StatusCode} for slug {Slug}. Response: {Content}",
                response.StatusCode,
                slug,
                errorContent.Length > 500 ? errorContent.Substring(0, 500) : errorContent);
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType != null && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Standards CMS returned non-JSON content type {ContentType} for slug {Slug}", contentType, slug);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || (!json.TrimStart().StartsWith('{') && !json.TrimStart().StartsWith('[')))
        {
            _logger.LogWarning("Standards CMS returned non-JSON response for slug {Slug}", slug);
            return null;
        }

        return ParseSingleStandardFromResponse(json);
    }

    /// <summary>
    /// When slug filter returns no row, find the standard in the published list and load full detail by id (same approach as Service Manual fallback).
    /// </summary>
    private async Task<DdtStandardDetailDto?> TryResolveStandardViaPublishedListAsync(
        string requestedSlug,
        string canonicalSlug,
        CancellationToken cancellationToken)
    {
        var listResponse = await GetPublishedStandardsAsync(page: 1, pageSize: 500, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rows = listResponse?.Data;
        if (rows == null || rows.Count == 0)
            return null;

        string[] candidates = [requestedSlug, canonicalSlug];
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var row = rows.FirstOrDefault(s => string.Equals(s.Slug, candidate, StringComparison.OrdinalIgnoreCase));
            if (row != null)
                return await FetchStandardDetailForListRowAsync(row, requestedSlug, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<DdtStandardDetailDto?> FetchStandardDetailForListRowAsync(
        DdtStandardDto row,
        string requestedSlug,
        CancellationToken cancellationToken)
    {
        if (row.Id > 0)
        {
            var byId = await FetchStandardDetailByIdAsync(row.Id, cancellationToken).ConfigureAwait(false);
            if (byId != null)
            {
                _logger.LogInformation(
                    "Resolved standard slug {Slug} via published list; loaded detail by numeric id {Id}",
                    requestedSlug,
                    row.Id);
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.DocumentId))
        {
            var byDoc = await FetchStandardDetailByDocumentIdAsync(row.DocumentId.Trim(), cancellationToken).ConfigureAwait(false);
            if (byDoc != null)
            {
                _logger.LogInformation(
                    "Resolved standard slug {Slug} via published list; loaded detail by documentId",
                    requestedSlug);
                return byDoc;
            }
        }

        return null;
    }

    private async Task<DdtStandardDetailDto?> FetchStandardDetailByIdAsync(int id, CancellationToken cancellationToken)
    {
        var queryParams =
            $"filters[id][$eq]={id}&populate[categories][populate][sub_categories]=true&populate[sub_categories]=true&populate[phases]=true";
        var url = $"api/standards?{queryParams}";
        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseSingleStandardFromResponse(json);
    }

    /// <summary>Strapi v5 entries are often keyed by <c>documentId</c>; numeric <c>id</c> filters may return nothing.</summary>
    private async Task<DdtStandardDetailDto?> FetchStandardDetailByDocumentIdAsync(string documentId, CancellationToken cancellationToken)
    {
        var queryParams = "filters[documentId][$eq]=" + Uri.EscapeDataString(documentId) +
            "&populate[categories][populate][sub_categories]=true" +
            "&populate[sub_categories]=true" +
            "&populate[phases]=true";
        var url = $"api/standards?{queryParams}";
        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseSingleStandardFromResponse(json);
    }

    public string? GetManageStandardUrl(string? documentId)
    {
        var baseUrl = _configuration["ManageStandards:Url"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(documentId))
            return null;
        return $"{baseUrl}/{Uri.EscapeDataString(documentId)}";
    }

    private static DdtStandardDto? MapStrapiStandardToDto(JsonNode? node)
    {
        if (node == null)
            return null;
        var attrs = node["attributes"] ?? node;
        var numericId = TryGetNumericId(node);
        var documentId = GetString(node, "documentId") ?? GetString(attrs, "documentId");
        var title = GetString(attrs, "title");
        var slug = GetString(attrs, "slug");
        if (string.IsNullOrEmpty(slug) && string.IsNullOrEmpty(title))
            return null;

        var catTitles = GetRelationTitles(node["categories"] ?? attrs["categories"] ?? attrs["category"], "title");
        var subCatTitles = GetRelationTitles(
            node["sub_categories"] ?? node["subCategories"] ?? attrs["sub_categories"] ?? attrs["subCategories"],
            "title");

        return new DdtStandardDto
        {
            Id = numericId,
            DocumentId = documentId,
            Title = title,
            Slug = slug ?? "",
            Summary = GetString(attrs, "summary"),
            Categories = catTitles,
            SubCategories = subCatTitles,
            IsPublished = true
        };
    }

    /// <summary>Strapi v4 uses numeric id; v5 may expose string ids — only treat as int when parseable.</summary>
    private static int TryGetNumericId(JsonNode node)
    {
        var idNode = node["id"];
        if (idNode is JsonValue jv && jv.TryGetValue(out int id))
            return id;
        return 0;
    }

    private static DdtStandardDetailDto? ParseSingleStandardFromResponse(string json)
    {
        var root = JsonNode.Parse(json);
        var dataArray = root?["data"] as JsonArray;
        var item = dataArray?.FirstOrDefault();
        if (item == null)
            return null;
        return MapStrapiStandardToDetailDto(item);
    }

    private static DdtStandardDetailDto? MapStrapiStandardToDetailDto(JsonNode? node)
    {
        if (node == null)
            return null;
        var attrs = node["attributes"] ?? node;
        var id = TryGetNumericId(node);
        var title = GetString(attrs, "title");
        var slug = GetString(attrs, "slug");
        if (string.IsNullOrEmpty(slug) && string.IsNullOrEmpty(title))
            return null;

        var standardSubCategories = GetRelationArray(node["sub_categories"] ?? node["subCategories"] ?? attrs["sub_categories"] ?? attrs["subCategories"]);
        var assignedSubCatIds = new HashSet<int>();
        var assignedSubCatByTitle = new Dictionary<string, (int Id, string? Desc)>(StringComparer.OrdinalIgnoreCase);
        foreach (var sc in standardSubCategories)
        {
            if (sc == null)
                continue;
            var scAttrs = sc["attributes"] ?? sc;
            var scId = sc["id"]?.GetValue<int>() ?? 0;
            var scName = GetString(scAttrs, "title");
            if (string.IsNullOrEmpty(scName))
                continue;
            assignedSubCatIds.Add(scId);
            assignedSubCatByTitle[scName] = (scId, GetString(scAttrs, "description"));
        }

        var categories = new List<DdtStandardCategoryDto>();
        var catNodes = GetRelationArray(node["categories"] ?? attrs["categories"] ?? attrs["category"]);
        foreach (var c in catNodes)
        {
            if (c == null)
                continue;
            var catAttrs = c["attributes"] ?? c;
            var catId = c["id"]?.GetValue<int>() ?? 0;
            var catName = GetString(catAttrs, "title");
            if (string.IsNullOrEmpty(catName))
                continue;
            var subCats = new List<DdtStandardSubCategoryDto>();
            var categorySubNodes = GetRelationArray(catAttrs?["sub_categories"] ?? catAttrs?["subCategories"]);
            foreach (var sc in categorySubNodes)
            {
                if (sc == null)
                    continue;
                var scId = sc["id"]?.GetValue<int>() ?? 0;
                if (!assignedSubCatIds.Contains(scId))
                    continue;
                var scAttrs = sc["attributes"] ?? sc;
                var scName = GetString(scAttrs, "title");
                if (string.IsNullOrEmpty(scName))
                    continue;
                assignedSubCatByTitle.TryGetValue(scName, out var extra);
                subCats.Add(
                    new DdtStandardSubCategoryDto
                    {
                        Id = scId,
                        Name = scName,
                        Description = GetString(scAttrs, "description") ?? extra.Desc
                    });
            }

            categories.Add(
                new DdtStandardCategoryDto
                {
                    Id = catId,
                    Name = catName,
                    Description = GetString(catAttrs, "description"),
                    SubCategories = subCats
                });
        }

        var placedSubCatTitles = new HashSet<string>(
            categories.SelectMany(c => c.SubCategories.Select(s => s.Name!).Where(n => n != null)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var kv in assignedSubCatByTitle)
        {
            if (placedSubCatTitles.Contains(kv.Key))
                continue;
            var firstCat = categories.FirstOrDefault();
            if (firstCat != null)
                firstCat.SubCategories.Add(
                    new DdtStandardSubCategoryDto { Id = kv.Value.Id, Name = kv.Key, Description = kv.Value.Desc });
            else
                categories.Add(
                    new DdtStandardCategoryDto
                    {
                        Name = "Other",
                        SubCategories =
                        [
                            new DdtStandardSubCategoryDto { Id = kv.Value.Id, Name = kv.Key, Description = kv.Value.Desc }
                        ]
                    });
        }

        var phases = new List<DdtStandardPhase>();
        var phaseNodes = GetRelationArray(node["phases"] ?? attrs["phases"]);
        foreach (var p in phaseNodes)
        {
            if (p == null)
                continue;
            var pAttrs = p["attributes"] ?? p;
            var name = GetString(pAttrs, "Title") ?? GetString(pAttrs, "title");
            if (!string.IsNullOrEmpty(name))
                phases.Add(new DdtStandardPhase { Id = p["id"]?.GetValue<int>() ?? 0, Name = name });
        }

        var documentId = GetString(node, "documentId") ?? GetString(attrs, "documentId");

        return new DdtStandardDetailDto
        {
            Id = id,
            DocumentId = documentId,
            Title = title,
            Slug = slug ?? "",
            Summary = GetString(attrs, "summary"),
            Purpose = GetString(attrs, "purpose"),
            HowToMeet = GetString(attrs, "howToMeet"),
            Governance = GetString(attrs, "governance"),
            RelatedGuidance = GetString(attrs, "relatedGuidance"),
            LegalBasis = GetString(attrs, "legalBasis"),
            LegalStandard = GetBool(attrs, "legalStandard"),
            Categories = categories,
            Phases = phases,
            Version = GetString(attrs, "version"),
            LastUpdated = TryGetDateTime(attrs, "lastUpdated") ?? TryGetDateTime(attrs, "updatedAt"),
            FirstPublished = TryGetDateTime(attrs, "firstPublished"),
            IsPublished = true
        };
    }

    private static DateTime? TryGetDateTime(JsonNode? node, string key)
    {
        var s = GetString(node, key);
        if (string.IsNullOrEmpty(s))
            return null;
        return DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    /// <summary>Strapi fields are often strings but components may expose numbers (e.g. version, ids copied into text fields).</summary>
    private static string? GetString(JsonNode? node, string key)
    {
        if (node == null)
            return null;
        var v = node[key];
        if (v == null)
            return null;

        if (v is JsonValue jv)
        {
            return JsonValueToDisplayString(jv);
        }

        // Nested object/array — avoid throwing; not usable as a plain string field.
        return v.ToJsonString();
    }

    private static string? JsonValueToDisplayString(JsonValue jv)
    {
        switch (jv.GetValueKind())
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return jv.TryGetValue(out string? s) ? s : null;
            case JsonValueKind.Number:
                if (jv.TryGetValue(out long l))
                    return l.ToString(CultureInfo.InvariantCulture);
                if (jv.TryGetValue(out double d))
                    return d.ToString(CultureInfo.InvariantCulture);
                return jv.ToString();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            default:
                return jv.ToString();
        }
    }

    private static bool? GetBool(JsonNode? node, string key)
    {
        if (node == null)
            return null;
        var v = node[key];
        if (v == null)
            return null;
        if (v is JsonValue jv)
        {
            if (jv.TryGetValue(out bool b))
                return b;
            if (jv.GetValueKind() == JsonValueKind.Number && jv.TryGetValue(out long l))
                return l != 0;
            if (jv.GetValueKind() == JsonValueKind.String && jv.TryGetValue(out string? s))
                return bool.TryParse(s, out var bs) ? bs : null;
        }

        return null;
    }

    private static JsonArray GetRelationArray(JsonNode? node)
    {
        if (node == null)
            return new JsonArray();
        if (node is JsonArray arr)
            return arr;
        var data = node["data"];
        if (data is JsonArray dataArr)
            return dataArr;
        if (data != null)
            return new JsonArray { data };
        return new JsonArray();
    }

    private static List<string> GetRelationTitles(JsonNode? node, string titleKey)
    {
        var list = new List<string>();
        var arr = GetRelationArray(node);
        foreach (var item in arr)
        {
            var attrs = item?["attributes"] ?? item;
            var t = GetString(attrs, titleKey);
            if (!string.IsNullOrEmpty(t))
                list.Add(t);
        }

        return list;
    }
}
