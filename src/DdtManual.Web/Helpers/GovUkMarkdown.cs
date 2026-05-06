using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DdtManual.Application.Content;
using DdtManual.Web.Services;
using Markdig;

namespace DdtManual.Web.Helpers
{
    public record HeadingEntry(string Id, string Text, int Level);

    public static class GovUkMarkdown
    {
        private static readonly MarkdownPipeline SharedMarkdownPipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public static string ToGovUkHtml(string? markdown)
        {
            try
            {
                if (string.IsNullOrEmpty(markdown))
                    return string.Empty;

                // Pre-process [panel] shortcodes before Markdig runs
                markdown = ApplyPanelShortcodes(markdown);

                // Pre-process [callout] shortcode before Markdig runs
                markdown = ApplyCalloutShortcodes(markdown);

                // Pre-process [actionLink] shortcode before Markdig runs
                markdown = ApplyActionLinkShortcodes(markdown);

                // Pre-process [fileDownload] shortcode before Markdig runs
                markdown = ApplyFileDownloadShortcodes(markdown);

                // Pre-process [ddtStandard] shortcode before Markdig runs
                markdown = ApplyDdtStandardShortcodes(markdown);

                // Pre-process [noBullets] shortcode before Markdig runs
                markdown = ApplyNoBulletsShortcodes(markdown);

                // Pre-process [spaced] shortcode before Markdig runs
                markdown = ApplySpacedBulletsShortcodes(markdown);

                // Pre-process [metric] / [metric-grid] shortcodes before Markdig runs
                markdown = ApplyMetricShortcodes(markdown);

                // Pre-process [stat-card] / [stat-card-grid] shortcodes before Markdig runs
                markdown = ApplyStatCardShortcodes(markdown);

                // Pre-process [pill] shortcodes before Markdig runs
                markdown = ApplyPillShortcodes(markdown);

                // Pre-process [chevron-cards] shortcode before Markdig runs
                markdown = ApplyChevronCardsShortcodes(markdown);

                // Pre-process [chevroncard ordered] / [chevroncard unordered] action list before Markdig runs
                markdown = ApplyChevroncardActionListShortcodes(markdown);

                // Pre-process {new-tab} link modifiers before Markdig runs
                // (Markdig's generic attributes extension would otherwise consume the {…} syntax)
                markdown = ApplyNewTabLinks(markdown);

                // Pre-process {sortable} table markers — replace marker with a sentinel comment
                // so we can identify which tables to wrap after Markdig has rendered them
                markdown = ApplySortableTableMarkers(markdown);

                // Pre-process sized section break variants (---xl, ---l, ---m) before Markdig runs
                markdown = ApplySectionBreaks(markdown);

                // Pre-process >x> / >!> inline panel syntax
                markdown = ApplyCustomPanels(markdown);

                // Normalise reversed markdown links: [url](link text) → [link text](url)
                markdown = NormaliseReversedMarkdownLinks(markdown);

                // Configure Markdig pipeline to allow raw HTML passthrough
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

                // Convert markdown to HTML
                var html = Markdown.ToHtml(markdown, pipeline);

                // Apply GOV.UK classes and heading IDs
                html = ApplyGovUkClasses(html);

                // Wrap sortable tables (sentinel comment → data-module div)
                html = WrapSortableTables(html);

                // Unwrap any paragraphs wrapping our injected HTML blocks
                html = RestoreCustomPanels(html);

                // Apply any remaining {new-tab} modifiers that survived as post-HTML (belt-and-braces)
                html = ApplyLinkModifiers(html);

                return html;
            }
            catch (Exception ex)
            {
                // Return error information for debugging
                return $"<div class=\"govuk-error-summary\"><h2 class=\"govuk-error-summary__title\">Markdown Processing Error</h2><div class=\"govuk-error-summary__body\"><p class=\"govuk-body\">Error: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p></div></div>";
            }
        }

        /// <summary>
        /// Renders markdown as GOV.UK HTML for body content. No heading demotion: # → h1, ## → h2, ### → h3, #### → h4, etc.
        /// </summary>
        public static string ToGovUkHtmlForBody(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;
            return ToGovUkHtml(markdown);
        }

        private static readonly Regex ServiceStandardListShortcodePattern = new(
            @"\[\[\s*ServiceStandardList\s*\]\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Replaces [[ServiceStandardList]] with a standards-style list built from detailed guide pages.
        /// Matches optional spaces (CMS/markdown often inserts them) and fullwidth bracket variants.
        /// </summary>
        public static string? ReplaceServiceStandardListShortcode(string? markdown, string? guideSlug, IReadOnlyList<DetailedGuidePageSummaryDto>? pages)
        {
            if (string.IsNullOrEmpty(markdown))
                return markdown;

            // Normalise fullwidth square brackets (some editors paste U+FF3B / U+FF3D).
            var normalized = markdown.Replace("\uFF3B", "[").Replace("\uFF3D", "]");
            if (!ServiceStandardListShortcodePattern.IsMatch(normalized))
                return markdown;

            var listHtml = BuildServiceStandardListHtml(guideSlug, pages);
            return ServiceStandardListShortcodePattern.Replace(normalized, listHtml);
        }

        private static string BuildServiceStandardListHtml(string? guideSlug, IReadOnlyList<DetailedGuidePageSummaryDto>? pages)
        {
            if (pages == null || pages.Count == 0 || string.IsNullOrWhiteSpace(guideSlug))
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<ol class=\"ss-standard-list\" role=\"list\">");

            var rowNumber = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                var slug = page.Slug;
                if (string.IsNullOrWhiteSpace(slug))
                    continue;

                rowNumber++;
                var href = $"/guidance/guides/{guideSlug.Trim('/')}/{slug.Trim('/')}";
                sb.Append("<li>");
                sb.Append("<a class=\"govuk-link govuk-body\" href=\"");
                sb.Append(HtmlEncode(href));
                sb.Append("\">");
                sb.Append(HtmlEncode(page.Title));
                sb.Append("</a>");
                sb.Append("</li>");
            }

            sb.Append("</ol>");
            return sb.ToString();
        }

        public static string PhaseClassFromSlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return string.Empty;

            var normalized = slug.Trim().ToLowerInvariant().Replace('_', '-');

            return normalized switch
            {
                "discovery" => "ss-ph--disc",
                "alpha" => "ss-ph--alpha",
                "beta" => "ss-ph--beta",
                "private-beta" => "ss-ph--beta",
                "public-beta" => "ss-ph--beta",
                "live" => "ss-ph--live",
                _ when normalized.EndsWith("-beta", StringComparison.Ordinal) => "ss-ph--beta",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Replaces <c>[[professions]]</c> and <c>[[phases]]</c> with DfE / Service Manual badge markup (see main app
        /// <c>DetailedGuideController</c>). Run before <see cref="ReplaceServiceStandardListShortcode"/> and markdown HTML conversion.
        /// </summary>
        public static string ReplaceGuideAudienceShortcodes(
            string? markdown,
            IReadOnlyList<string>? professionLabels,
            IReadOnlyList<ApplicablePhaseTagDto>? phaseTags)
        {
            if (string.IsNullOrEmpty(markdown))
                return markdown ?? string.Empty;

            var professionsHtml = BuildProfessionsShortcodeHtml(professionLabels);
            var phasesHtml = BuildPhasesShortcodeHtml(phaseTags);

            var result = markdown.Replace("[[professions]]", professionsHtml, StringComparison.Ordinal);
            result = result.Replace("[[phases]]", phasesHtml, StringComparison.Ordinal);
            return result;
        }

        private static string BuildProfessionsShortcodeHtml(IReadOnlyList<string>? labels)
        {
            if (labels == null || labels.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<div class=\"govuk-!-margin-bottom-4\" role=\"group\" aria-label=\"Professions\">");
            foreach (var p in labels)
            {
                if (string.IsNullOrWhiteSpace(p))
                    continue;
                sb.Append("<span class=\"dfe-f-badge dfe-f-badge--small dfe-f-badge--blue govuk-!-margin-right-2 govuk-!-margin-bottom-2\">");
                sb.Append(HtmlEncode(p.Trim()));
                sb.Append("</span>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string BuildPhasesShortcodeHtml(IReadOnlyList<ApplicablePhaseTagDto>? phases)
        {
            if (phases == null || phases.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<div class=\"ss-row__meta\">");
            foreach (var phase in phases)
            {
                if (string.IsNullOrWhiteSpace(phase.Title))
                    continue;
                var cls = PhaseClassFromSlug(phase.Slug);
                sb.Append("<span class=\"ss-ph");
                if (!string.IsNullOrEmpty(cls))
                {
                    sb.Append(' ');
                    sb.Append(cls);
                }

                sb.Append("\">");
                sb.Append(HtmlEncode(phase.Title));
                sb.Append("</span>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        /// <summary>
        /// Renders markdown for dfe-f-related-content__section body. Same as ToGovUkHtml but paragraphs and other body text
        /// use govuk-body-s instead of govuk-body so related content is always small.
        /// </summary>
        public static string ToGovUkHtmlForRelatedContent(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;
            var html = ToGovUkHtml(markdown);
            // Replace govuk-body with govuk-body-s so body text is small (don't change govuk-body-l or govuk-body-s)
            return Regex.Replace(html, @"\bgovuk-body\b(?!-)", "govuk-body-s", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Enriches dfe-f-document-list blocks in the HTML with file size and last-modified from blob storage.
        /// For each document item whose link href points to the provider's blob storage, fetches metadata and
        /// updates the meta line to "TypeLabel, 120KB, updated 15 Jan 2025". When provider returns null, the meta is left as-is.
        /// </summary>
        public static async Task<string> EnrichDocumentListWithBlobMetadataAsync(string? html, IBlobMetadataProvider provider, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(html) || provider == null)
                return html ?? "";

            var metaTagRegex = new Regex(@"<p class=""dfe-f-document-item__meta"">(.*?)</p>", RegexOptions.Singleline);
            var hrefRegex = new Regex(@"<a[^>]+href=""([^""]+)""[^>]*>", RegexOptions.Singleline);
            var matches = metaTagRegex.Matches(html).Cast<Match>().ToList();
            if (matches.Count == 0)
                return html;

            var replacements = new List<(int Start, int Length, string NewTag)>();

            foreach (var m in matches)
            {
                var typeLabel = m.Groups[1].Value.Trim();
                var beforeMeta = html[..m.Index];
                var hrefMatches = hrefRegex.Matches(beforeMeta);
                if (hrefMatches.Count == 0)
                    continue;
                var lastHref = hrefMatches[^1];
                var href = lastHref.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(href))
                    continue;

                var metadata = await provider.GetMetadataAsync(href, cancellationToken).ConfigureAwait(false);
                if (metadata == null)
                    continue;

                // Strip ", file size and updated unknown" to get the type label only
                const string unknownSuffix = ", file size and updated unknown";
                var displayTypeLabel = typeLabel.EndsWith(unknownSuffix, StringComparison.OrdinalIgnoreCase)
                    ? typeLabel[..typeLabel.IndexOf(unknownSuffix, StringComparison.OrdinalIgnoreCase)].Trim()
                    : typeLabel;

                var sizeStr = FormatFileSize(metadata.SizeBytes);
                var dateStr = FormatLastModified(metadata.LastModified);
                var newInner = HtmlEncode($"{displayTypeLabel}, {sizeStr}, updated {dateStr}");
                var newTag = $"<p class=\"dfe-f-document-item__meta\">{newInner}</p>";
                replacements.Add((m.Index, m.Length, newTag));
            }

            // Apply replacements from end to start so indices remain valid
            var result = html;
            foreach (var (start, length, newTag) in replacements.OrderByDescending(r => r.Start))
                result = result[..start] + newTag + result[(start + length)..];

            return result;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{(bytes / (1024.0 * 1024)):F1}MB";
            if (bytes >= 1024)
                return $"{(bytes / 1024.0):F0}KB";
            return $"{bytes}B";
        }

        private static string FormatLastModified(DateTimeOffset d)
        {
            // "18 Jan 2025" (day without leading zero, full month name, year)
            return d.ToString("d MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        }

        /// <summary>
        /// Converts reversed markdown links [url](link text) into standard [link text](url)
        /// so they render with the correct link text and href.
        /// </summary>
        private static string NormaliseReversedMarkdownLinks(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            // Match [url](text) where url looks like http(s)://... and swap to [text](url)
            return Regex.Replace(markdown, @"\[(https?://[^\]]+)\]\(([^)]+)\)", "[$2]($1)", RegexOptions.IgnoreCase);
        }

        // Attribute helpers

        /// <summary>Reads a named attribute value from a shortcode tag string, e.g. colour="blue".</summary>
        private static string Attr(string tag, string name, string fallback = "")
        {
            var m = Regex.Match(tag, $@"{name}=""([^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : fallback;
        }

        /// <summary>Reads attribute value with optional quotes, e.g. type=warning or title="Legal requirement".</summary>
        private static string AttrOrUnquoted(string tag, string name, string fallback = "")
        {
            var quoted = Regex.Match(tag, $@"{name}=""([^""]*)""", RegexOptions.IgnoreCase);
            if (quoted.Success) return quoted.Groups[1].Value.Trim();
            var unquoted = Regex.Match(tag, $@"{name}=([^,\]]+)", RegexOptions.IgnoreCase);
            return unquoted.Success ? unquoted.Groups[1].Value.Trim() : fallback;
        }

        // [callout] shortcode

        /// <summary>
        /// Converts [callout type=warning] or [callout type=important, title=Legal requirement] ... [/callout]
        /// into a styled callout panel. type: warning (red), important (orange), information (blue).
        /// Optional title= renders as strong; inner content is markdown-rendered as paragraphs.
        /// </summary>
        private static string ApplyCalloutShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Regex.Replace(
                markdown,
                @"\[callout([^\]]*)\]([\s\S]*?)\[/callout\]",
                m =>
                {
                    var attrs = m.Groups[1].Value.Trim();
                    var inner = m.Groups[2].Value.Trim();
                    var type = AttrOrUnquoted(attrs, "type", "information").ToLowerInvariant();
                    if (type != "warning" && type != "important" && type != "information")
                        type = "information";
                    var title = AttrOrUnquoted(attrs, "title", "");
                    var titleHtml = !string.IsNullOrWhiteSpace(title)
                        ? $"<strong>{HtmlEncode(title.Trim())}</strong>\n"
                        : "";
                    var bodyHtml = "";
                    if (!string.IsNullOrEmpty(inner))
                    {
                        inner = string.Join("\n", inner.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimStart()));
                        inner = NormaliseReversedMarkdownLinks(inner);
                        bodyHtml = Markdown.ToHtml(inner, pipeline);
                        bodyHtml = ApplyGovUkClasses(bodyHtml);
                    }
                    return "\n<div class=\"dfe-f-callout " + type + "\">\n" + titleHtml + bodyHtml + "</div>\n";
                },
                RegexOptions.IgnoreCase);
        }

        // [panel] shortcode

        /// <summary>
        /// Converts [panel ...] ... [/panel] shortcodes in markdown into the
        /// dfe-f-panel-component HTML component.
        ///
        /// Shortcode syntax (all attributes optional except title):
        ///
        ///   [panel id="my-panel" colour="blue" title="My title"
        ///          tags="Mandatory, Architecture / Standards"
        ///          description="Trigger text shown below the title."
        ///          phases="Discovery,Alpha,Beta,Live"
        ///          columns="2"]
        ///     [steps label="What your team needs to do"]
        ///     1. First step
        ///     2. Second step
        ///     [/steps]
        ///     [checks label="What assessors and reviewers check"]
        ///     - Assessors check this
        ///     - And this
        ///     [/checks]
        ///     [links]
        ///     Find and use standards ↗ | https://example.com | primary
        ///     Standards by profession  | https://example.com
        ///     [/links]
        ///   [/panel]
        ///
        /// columns: "1" forces a single full-width column; "2" (default) renders side-by-side.
        /// [steps] and [checks] support an optional label="..." attribute to override the default heading.
        /// Colour options: blue (default) | green | orange | red | yellow | grey | black
        /// </summary>
        private static string ApplyPanelShortcodes(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"\[panel([^\]]*)\]([\s\S]*?)\[/panel\]",
                m => RenderPanelShortcode(m.Groups[1].Value, m.Groups[2].Value),
                RegexOptions.IgnoreCase);
        }

        // [noBullets] shortcode

        /// <summary>
        /// Converts [noBullets]...[/noBullets] into a div whose lists render without bullets/numbers.
        /// Inner content is processed as markdown (so links, lists, etc. work).
        /// </summary>
        private static string ApplyNoBulletsShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Regex.Replace(
                markdown,
                @"\[noBullets\]([\s\S]*?)\[/noBullets\]",
                m =>
                {
                    var inner = m.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(inner)) return "\n<div class=\"dfe-no-bullets\"></div>\n";
                    // Strip leading indentation from each line so Markdig doesn't treat the block as a code block (4+ spaces = code)
                    inner = string.Join("\n", inner.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimStart()));
                    inner = NormaliseReversedMarkdownLinks(inner);
                    var html = Markdown.ToHtml(inner, pipeline);
                    html = ApplyGovUkClasses(html);
                    // Remove bullet/number list styling so [noBullets] outputs plain <ul class="govuk-list"> (no govuk-list--bullet)
                    html = Regex.Replace(html, @"class=""govuk-list govuk-list--bullet""", "class=\"govuk-list govuk-list--spaced govuk-body-s\"", RegexOptions.IgnoreCase);
                    html = Regex.Replace(html, @"class=""govuk-list govuk-list--number""", "class=\"govuk-list govuk-body-s\"", RegexOptions.IgnoreCase);
                    return html.Trim();
                },
                RegexOptions.IgnoreCase);
        }

        // [spaced] shortcode

        /// <summary>
        /// Converts [spaced]...[/spaced] so bullet/number lists get GOV.UK spaced list modifiers:
        /// <c>govuk-list--spaced</c> (standard spacing) and <c>govuk-bullets--spaced</c> (hook / project styling).
        /// Inner content is processed as markdown.
        /// Example:
        /// <code>
        /// [spaced]
        /// - First item
        /// - Second item
        /// [/spaced]
        /// </code>
        /// </summary>
        private static string ApplySpacedBulletsShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Regex.Replace(
                markdown,
                @"\[spaced\]([\s\S]*?)\[/spaced\]",
                m =>
                {
                    var inner = m.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(inner))
                        return "\n\n";
                    inner = string.Join("\n", inner.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimStart()));
                    inner = NormaliseReversedMarkdownLinks(inner);
                    var html = Markdown.ToHtml(inner, pipeline);
                    html = ApplyGovUkClasses(html);
                    html = Regex.Replace(
                        html,
                        @"class=""govuk-list govuk-list--bullet""",
                        "class=\"govuk-list govuk-list--bullet govuk-list--spaced\"",
                        RegexOptions.IgnoreCase);
                    html = Regex.Replace(
                        html,
                        @"class=""govuk-list govuk-list--number""",
                        "class=\"govuk-list govuk-list--number govuk-list--spaced\"",
                        RegexOptions.IgnoreCase);
                    return "\n\n" + html.Trim() + "\n\n";
                },
                RegexOptions.IgnoreCase);
        }

        // [metric] / [metric-grid] shortcodes

        /// <summary>
        /// Converts [metric ...] shortcodes into dfe-f-metric card HTML.
        /// Wrap multiple cards in [metric-grid]...[/metric-grid] for a responsive row.
        ///
        /// Shortcode syntax:
        ///   [metric num="23" label="Total requests" change="6 new this month" colour="grey"]
        ///   [metric num="2"  label="Awaiting triage" change="Action required" change-variant="negative" colour="red"]
        ///
        /// Attributes:
        ///   num            — large number to display (required)
        ///   label          — descriptive label (required)
        ///   change         — optional change / status line
        ///   change-variant — positive | negative | warning (default: neutral)
        ///   colour         — blue | green | orange | red | yellow | grey | black | teal | purple (default: grey)
        ///
        /// Grid wrapper:
        ///   [metric-grid]
        ///   [metric num="12" label="Approved" colour="green"]
        ///   [metric num="2"  label="Overdue"  colour="red"]
        ///   [/metric-grid]
        /// </summary>
        private static string ApplyMetricShortcodes(string markdown)
        {
            // Wrap [metric-grid]...[/metric-grid] first
            markdown = Regex.Replace(
                markdown,
                @"\[metric-grid\]([\s\S]*?)\[/metric-grid\]",
                m => $"\n<div class=\"dfe-f-metric-grid\">{RenderMetricShortcodes(m.Groups[1].Value)}</div>\n",
                RegexOptions.IgnoreCase);

            // Standalone [metric ...] tags
            markdown = Regex.Replace(
                markdown,
                @"\[metric([^\]]*)\]",
                m => RenderSingleMetric(m.Groups[1].Value),
                RegexOptions.IgnoreCase);

            return markdown;
        }

        private static string RenderMetricShortcodes(string inner)
        {
            return Regex.Replace(
                inner,
                @"\[metric([^\]]*)\]",
                m => RenderSingleMetric(m.Groups[1].Value),
                RegexOptions.IgnoreCase);
        }

        private static string RenderSingleMetric(string attrs)
        {
            var num = Attr(attrs, "num", "");
            var label = Attr(attrs, "label", "");
            var change = Attr(attrs, "change", "");
            var changeVariant = Attr(attrs, "change-variant", "");
            var colour = Attr(attrs, "colour", "grey");

            var changeCls = string.IsNullOrWhiteSpace(changeVariant)
                ? "dfe-f-metric__change"
                : $"dfe-f-metric__change dfe-f-metric__change--{changeVariant}";

            var changeHtml = string.IsNullOrWhiteSpace(change)
                ? ""
                : $"<div class=\"{changeCls}\">{HtmlEncode(change)}</div>";

            return $"\n<div class=\"dfe-f-metric dfe-f-metric--{HtmlEncode(colour)}\">" +
                   $"<div class=\"dfe-f-metric__num\">{HtmlEncode(num)}</div>" +
                   $"<div class=\"dfe-f-metric__label\">{HtmlEncode(label)}</div>" +
                   $"{changeHtml}" +
                   $"</div>\n";
        }

        // [stat-card] / [stat-card-grid] shortcodes

        /// <summary>
        /// Converts [stat-card ...] shortcodes into dfe-f-stat-card HTML.
        /// Wrap multiple cards in [stat-card-grid]...[/stat-card-grid] for a responsive row.
        ///
        /// Shortcode syntax:
        ///   [stat-card title="Strategic alignment" stat="11.2" meta="avg / 15 · High alignment" progress="75" colour="blue"]
        ///   [stat-card title="Urgency" stat="5.8" meta="avg / 10" progress="58" link="/reports/urgency" link-text="View report" colour="orange"]
        ///
        /// Attributes:
        ///   title      — card heading (required)
        ///   stat       — large stat value, e.g. "11.2" (optional)
        ///   meta       — sub-label text (optional)
        ///   progress   — 0–100 percentage for progress bar (optional, omit to hide)
        ///   link       — URL for footer link (optional)
        ///   link-text  — footer link label (default: "View details")
        ///   colour     — blue | green | orange | red | yellow | grey | black | teal | purple (default: teal)
        ///
        /// Grid wrapper:
        ///   [stat-card-grid]
        ///   [stat-card title="Alignment" stat="11.2" progress="75" colour="blue"]
        ///   [stat-card title="Urgency"   stat="5.8"  progress="58" colour="orange"]
        ///   [/stat-card-grid]
        /// </summary>
        private static string ApplyStatCardShortcodes(string markdown)
        {
            // Wrap [stat-card-grid]...[/stat-card-grid] first
            markdown = Regex.Replace(
                markdown,
                @"\[stat-card-grid\]([\s\S]*?)\[/stat-card-grid\]",
                m => $"\n<div class=\"dfe-f-stat-card-grid\">{RenderStatCardShortcodes(m.Groups[1].Value)}</div>\n",
                RegexOptions.IgnoreCase);

            // Standalone [stat-card ...] tags
            markdown = Regex.Replace(
                markdown,
                @"\[stat-card([^\]]*)\]",
                m => RenderSingleStatCard(m.Groups[1].Value),
                RegexOptions.IgnoreCase);

            return markdown;
        }

        private static string RenderStatCardShortcodes(string inner)
        {
            return Regex.Replace(
                inner,
                @"\[stat-card([^\]]*)\]",
                m => RenderSingleStatCard(m.Groups[1].Value),
                RegexOptions.IgnoreCase);
        }

        private static string RenderSingleStatCard(string attrs)
        {
            var title = Attr(attrs, "title", "");
            var stat = Attr(attrs, "stat", "");
            var meta = Attr(attrs, "meta", "");
            var progress = Attr(attrs, "progress", "");
            var link = Attr(attrs, "link", "");
            var linkText = Attr(attrs, "link-text", "View details");
            var colour = Attr(attrs, "colour", "teal");

            var statHtml = string.IsNullOrWhiteSpace(stat)
                ? ""
                : $"<div class=\"dfe-f-stat-card__stat\">{HtmlEncode(stat)}</div>";

            var metaHtml = string.IsNullOrWhiteSpace(meta)
                ? ""
                : $"<div class=\"dfe-f-stat-card__meta\">{HtmlEncode(meta)}</div>";

            var progressHtml = "";
            if (int.TryParse(progress, out var pct))
            {
                pct = Math.Clamp(pct, 0, 100);
                progressHtml = $"<div class=\"dfe-f-stat-card__progress\"><div class=\"dfe-f-stat-card__progress-fill\" style=\"width:{pct}%\"></div></div>";
            }

            var linkHtml = string.IsNullOrWhiteSpace(link)
                ? ""
                : $"<a class=\"dfe-f-stat-card__link\" href=\"{HtmlEncode(link)}\">{HtmlEncode(linkText)}</a>";

            return $"\n<div class=\"dfe-f-stat-card dfe-f-stat-card--{HtmlEncode(colour)}\">" +
                   $"<h3 class=\"dfe-f-stat-card__title\">{HtmlEncode(title)}</h3>" +
                   $"{statHtml}{metaHtml}{progressHtml}{linkHtml}" +
                   $"</div>\n";
        }

        // [chevron-cards] / [card-list] shortcode

        /// <summary>
        /// Converts [chevron-cards heading="..."] or [card-list ...] [card title="..." description="..." href="..."] ... [/chevron-cards]
        /// into a simple clickable card list (ul.dfe-card-list). Whole card is one link. No chevron.
        /// </summary>
        private static string ApplyChevronCardsShortcodes(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"\[(?:chevron-cards|card-list)([^\]]*)\]([\s\S]*?)\[/(?:chevron-cards|card-list)\]",
                m => RenderCardList(m.Groups[1].Value, m.Groups[2].Value),
                RegexOptions.IgnoreCase);
        }

        private static string RenderCardList(string attrs, string body)
        {
            var heading = Attr(attrs, "heading", "");
            var columnsAttr = Attr(attrs, "columns", "1");
            var listClass = "dfe-card-list";
            if (columnsAttr == "2") listClass += " dfe-card-list--2-col";
            else if (columnsAttr == "3") listClass += " dfe-card-list--3-col";

            var cardPattern = @"\[card([^\]]*)\]";
            var cards = Regex.Matches(body, cardPattern, RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m =>
                {
                    var a = m.Groups[1].Value;
                    var ext = Attr(a, "external", "").Equals("true", StringComparison.OrdinalIgnoreCase);
                    var target = ext ? " target=\"_blank\" rel=\"noopener noreferrer\"" : "";
                    return (Title: Attr(a, "title", ""), Description: Attr(a, "description", ""), Href: Attr(a, "href", "#"), Target: target);
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Href))
                .ToList();

            var headingHtml = string.IsNullOrWhiteSpace(heading)
                ? ""
                : $"<h2 class=\"govuk-heading-l govuk-!-margin-bottom-6\">{HtmlEncode(heading)}</h2>\n";

            var itemsHtml = string.Join("\n",
                cards.Select(c =>
                    $"  <li class=\"dfe-card-list__item\">" +
                    $"<a href=\"{HtmlEncode(c.Href)}\" class=\"dfe-card-list__link govuk-link\"{c.Target}>" +
                    $"<span class=\"dfe-card-list__heading govuk-heading-s\">{HtmlEncode(c.Title)}</span>" +
                    $"<span class=\"dfe-card-list__description govuk-body\">{HtmlEncode(c.Description)}</span>" +
                    $"</a></li>"));

            var listHtml = $"<ul class=\"{listClass}\">\n{itemsHtml}\n</ul>";
            return $"\n<div class=\"dfe-card-list-block\">\n{headingHtml}{listHtml}\n</div>\n";
        }

        // [actionLink] shortcode

        /// <summary>
        /// Converts [actionLink] ... [Link text](url) ... [/actionLink] into a GOV.UK-style action link
        /// (prominent link with arrow icon). See https://components.publishing.service.gov.uk/component-guide/action_link
        /// </summary>
        private static string ApplyActionLinkShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            return Regex.Replace(
                markdown,
                @"\[actionLink\]([\s\S]*?)\[/actionLink\]",
                m => RenderActionLinkShortcode(m.Groups[1].Value),
                RegexOptions.IgnoreCase);
        }

        /// <summary>GOV.UK-style action link icon: circle with arrow (viewBox 0 0 27 27).</summary>
        private static readonly string ActionLinkIconSvg =
            "<svg class=\"dfe-f-action-link__svg\" xmlns=\"http://www.w3.org/2000/svg\" focusable=\"false\" aria-hidden=\"true\" viewBox=\"0 0 27 27\">" +
            "<circle class=\"dfe-f-action-link__icon-circle\" cx=\"13.5\" cy=\"13.5\" r=\"13.5\"/>" +
            "<g class=\"dfe-f-action-link__icon-arrow\">" +
            "<path d=\"m17.701 13.526-3.827-3.828L14.973 8.6l4.926 4.926-4.926 4.926-1.099-1.099 3.827-3.827Z\"/>" +
            "<path d=\"M8.363 12.749h9.762v1.554H8.363v-1.554Z\"/>" +
            "</g></svg>";

        private static string RenderActionLinkShortcode(string inner)
        {
            var trimmed = inner.Trim();
            var linkMatch = Regex.Match(trimmed, @"\[([^\]]+)\]\(([^)]+)\)");
            if (!linkMatch.Success) return trimmed;
            var text = linkMatch.Groups[1].Value.Trim();
            var url = linkMatch.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(url)) url = "#";
            // Blank lines so Markdig parses the block as HTML, not wrapped in <p>
            return "\n\n<div class=\"dfe-f-action-link\">" +
                   "<span class=\"dfe-f-action-link__icon\">" + ActionLinkIconSvg + "</span>" +
                   "<span class=\"dfe-f-action-link__link-wrapper\">" +
                   $"<a class=\"govuk-link dfe-f-action-link__link\" href=\"{HtmlEncode(url)}\">{HtmlEncode(text)}</a>" +
                   "</span></div>\n\n";
        }

        // [fileDownload] shortcode

        /// <summary>
        /// Converts [fileDownload] ... [link text](url) ... [/fileDownload] into a styled document list.
        /// Each markdown link becomes a document item with file type derived from the URL/link text extension.
        /// Use in markdown: [fileDownload] [filename.docx](https://example.com/file.docx) [/fileDownload]
        /// </summary>
        private static string ApplyFileDownloadShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            return Regex.Replace(
                markdown,
                @"\[fileDownload\]([\s\S]*?)\[/fileDownload\]",
                m => RenderFileDownloadShortcode(m.Groups[1].Value),
                RegexOptions.IgnoreCase);
        }

        private static string RenderFileDownloadShortcode(string inner)
        {
            var links = Regex.Matches(inner.Trim(), @"\[([^\]]+)\]\(([^)]+)\)")
                .Cast<Match>()
                .Select(m => (Text: m.Groups[1].Value.Trim(), Url: m.Groups[2].Value.Trim()))
                .Where(x => !string.IsNullOrEmpty(x.Url))
                .ToList();
            if (links.Count == 0) return string.Empty;

            var items = links.Select(link =>
            {
                var (typeCode, typeLabel) = GetFileTypeFromUrlOrName(link.Url, link.Text);
                var href = string.IsNullOrEmpty(link.Url) ? "#" : link.Url;
                var title = link.Text ?? "";
                var metaText = $"{typeLabel}, file size and updated unknown";
                return "<div class=\"dfe-f-document-item\">" +
                       $"<div class=\"dfe-f-document-item__icon\">{HtmlEncode(typeCode)}</div>" +
                       "<div class=\"dfe-f-document-item__info\">" +
                       $"<h4 class=\"govuk-heading-s dfe-f-document-item__title\"><a class=\"govuk-link\" href=\"{HtmlEncode(href)}\">{HtmlEncode(title)}</a></h4>" +
                       $"<p class=\"dfe-f-document-item__meta\">{HtmlEncode(metaText)}</p>" +
                       "</div></div>";
            });
            return "\n<div class=\"dfe-f-document-list\">\n" + string.Join("\n", items) + "\n</div>\n";
        }

        // [ddtStandard] shortcode

        /// <summary>
        /// Converts [ddtStandard] - [Title (DDTS-XXX)](url) ... [/ddtStandard] into a styled standards list.
        /// Each markdown link becomes an item; optional (DDTS-XXX) in the link text is shown as a code badge.
        /// </summary>
        private static string ApplyDdtStandardShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            return Regex.Replace(
                markdown,
                @"\[ddtStandard\]([\s\S]*?)\[/ddtStandard\]",
                m => RenderDdtStandardShortcode(m.Groups[1].Value),
                RegexOptions.IgnoreCase);
        }

        private static readonly Regex DdtStandardCodeInTitle = new(@"^(.+?)\s*\((DDTS-\d+)\)\s*$", RegexOptions.Compiled);

        private static string RenderDdtStandardShortcode(string inner)
        {
            var linkMatches = Regex.Matches(inner.Trim(), @"\[([^\]]+)\]\(([^)]+)\)")
                .Cast<Match>()
                .Select(m => (Text: m.Groups[1].Value.Trim(), Url: m.Groups[2].Value.Trim()))
                .Where(x => !string.IsNullOrEmpty(x.Url))
                .ToList();
            if (linkMatches.Count == 0) return string.Empty;

            var items = linkMatches.Select(link =>
            {
                var title = link.Text ?? "";
                var href = link.Url ?? "#";
                string? code = null;
                var codeMatch = DdtStandardCodeInTitle.Match(title);
                if (codeMatch.Success)
                {
                    title = codeMatch.Groups[1].Value.Trim();
                    code = codeMatch.Groups[2].Value;
                }
                var isExternal = href.Contains("standards.education.gov.uk", StringComparison.OrdinalIgnoreCase);
                var target = isExternal ? " target=\"_blank\" rel=\"noopener noreferrer\"" : "";
                var codeDisplay = !string.IsNullOrEmpty(code) ? code : "STD";
                return "<a href=\"" + HtmlEncode(href) + "\" class=\"dfe-f-std-card-a\"" + target + ">" +
                       "<span class=\"dfe-f-std-card-a__code\">" + HtmlEncode(codeDisplay) + "</span>" +
                       "<div class=\"dfe-f-std-card-a__body\">" +
                       "<span class=\"dfe-f-std-card-a__name\">" + HtmlEncode(title) +
                       (isExternal ? " <span class=\"govuk-visually-hidden\">(opens in new tab)</span>" : "") + "</span>" +
                       "</div>" +
                       "<span class=\"dfe-f-std-card-a__arrow\" aria-hidden=\"true\">›</span>" +
                       "</a>";
            });
            return "\n<div class=\"dfe-f-std-block-a\">" +
                   "<p class=\"dfe-f-std-block-a__label\">Digital, data and technology standards</p>" +
                   "\n<div class=\"dfe-f-std-cards-a\">\n" +
                   string.Join("\n", items) +
                   "\n</div>\n</div>\n";
        }

        /// <summary>Returns (short code for icon e.g. PDF, type label e.g. PDF or Spreadsheet).</summary>
        private static (string Code, string Label) GetFileTypeFromUrlOrName(string url, string linkText)
        {
            var ext = GetFileExtension(url ?? "") ?? GetFileExtension(linkText ?? "");
            var extLower = ext?.ToLowerInvariant() ?? "";
            return extLower switch
            {
                "pdf" => ("PDF", "PDF"),
                "doc" or "docx" => ("DOCX", "Word document"),
                "xls" or "xlsx" => ("XLSX", "Spreadsheet"),
                "csv" => ("CSV", "CSV"),
                "ppt" or "pptx" => ("PPTX", "Presentation"),
                "odt" => ("ODT", "OpenDocument text"),
                "ods" => ("ODS", "OpenDocument spreadsheet"),
                _ => ("FILE", "Document")
            };
        }

        /// <summary>Gets the file extension from a path or URL, ignoring query string and fragment (e.g. .xlsx from "file.xlsx?d=123").</summary>
        private static string? GetFileExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var pathOnly = path;
            var q = pathOnly.IndexOf('?');
            if (q >= 0) pathOnly = pathOnly[..q];
            var h = pathOnly.IndexOf('#');
            if (h >= 0) pathOnly = pathOnly[..h];
            var lastDot = pathOnly.LastIndexOf('.');
            if (lastDot < 0 || lastDot >= pathOnly.Length - 1) return null;
            var ext = pathOnly[(lastDot + 1)..].Trim();
            return string.IsNullOrEmpty(ext) ? null : ext;
        }

        // [chevroncard ordered] / [chevroncard unordered] action list

        private const string ActionListChevronSvg = "<svg class=\"chevron dfe-icon dfe-icon__chevron-right\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\"><path d=\"M15.5 12a1 1 0 0 1-.29.71l-5 5a1 1 0 0 1-1.42-1.42l4.3-4.29-4.3-4.29a1 1 0 0 1 1.42-1.42l5 5a1 1 0 0 1 .29.71z\"></path></svg>";

        /// <summary>
        /// Converts [chevroncard ordered] or [chevroncard unordered] with [card title="..." href="..."] items
        /// into an action list (ol or ul with class govuk-list dfe-action-list) — full-width links with chevron icon.
        /// </summary>
        private static string ApplyChevroncardActionListShortcodes(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            return Regex.Replace(
                markdown,
                @"\[chevroncard\s+(ordered|unordered)\]([\s\S]*?)\[/chevroncard\]",
                m => RenderChevroncardActionList(m.Groups[1].Value.Trim(), m.Groups[2].Value),
                RegexOptions.IgnoreCase);
        }

        private static string RenderChevroncardActionList(string orderedOrUnordered, string body)
        {
            var isOrdered = orderedOrUnordered.Equals("ordered", StringComparison.OrdinalIgnoreCase);
            var listTag = isOrdered ? "ol" : "ul";
            var cardPattern = @"\[card([^\]]*)\]";
            var cards = Regex.Matches(body, cardPattern, RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m =>
                {
                    var a = m.Groups[1].Value;
                    var ext = Attr(a, "external", "").Equals("true", StringComparison.OrdinalIgnoreCase);
                    var target = ext ? " target=\"_blank\" rel=\"noopener noreferrer\"" : "";
                    return (Title: Attr(a, "title", ""), Href: Attr(a, "href", "#"), Target: target);
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Href))
                .ToList();

            var itemsHtml = string.Join("\n",
                cards.Select(c =>
                    $"  <li>\n    <a href=\"{HtmlEncode(c.Href)}\" class=\"govuk-link govuk-link--no-visited-state\"{c.Target}>{HtmlEncode(c.Title)}\n{ActionListChevronSvg}\n    </a>\n  </li>"));

            var listHtml = $"<{listTag} class=\"govuk-list dfe-action-list\">\n{itemsHtml}\n</{listTag}>";
            return $"\n<div class=\"dfe-action-list-block\">\n{listHtml}\n</div>\n";
        }

        // [pill] shortcode

        /// <summary>
        /// Converts [pill ...] shortcodes into standalone dfe-pill span elements.
        ///
        /// Shortcode syntax:
        ///   [pill label="Discovery" phase="discovery"]
        ///   [pill label="Mandatory" colour="blue"]
        ///
        /// Use either phase or colour — not both. If neither is set, a neutral grey style is used.
        /// Phase values: discovery | alpha | beta | live
        /// Colour values: blue | green | orange | red | yellow | grey
        /// </summary>
        private static string ApplyPillShortcodes(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"\[pill([^\]]*)\]",
                m =>
                {
                    var attrs = m.Groups[1].Value;
                    var label = Attr(attrs, "label", "");
                    var phase = Attr(attrs, "phase", "").ToLowerInvariant();
                    var colour = Attr(attrs, "colour", "").ToLowerInvariant();

                    if (string.IsNullOrWhiteSpace(label)) return string.Empty;

                    // Phase takes priority over colour
                    var modifier = !string.IsNullOrWhiteSpace(phase) ? $"dfe-pill--{phase}"
                                 : !string.IsNullOrWhiteSpace(colour) ? $"dfe-pill--{colour}"
                                 : "dfe-pill--grey";

                    return $"<span class=\"dfe-pill {modifier}\">{HtmlEncode(label)}</span>";
                },
                RegexOptions.IgnoreCase);
        }

        // Section break size variants (---xl, ---l, ---m).

        /// <summary>
        /// Pre-processes markdown to convert sized section break variants into raw HTML
        /// before Markdig runs. Plain --- is handled post-render by ApplyGovUkClasses.
        ///
        /// Syntax (on its own line):
        ///   ---xl  →  govuk-section-break--xl
        ///   ---l   →  govuk-section-break--l
        ///   ---m   →  govuk-section-break--m
        ///   ---    →  govuk-section-break (default, handled by ApplyGovUkClasses)
        /// </summary>
        private static string ApplySectionBreaks(string markdown)
        {
            return Regex.Replace(
                markdown,
                @"^---(xl|l|m)\s*$",
                m => $"<hr class=\"govuk-section-break govuk-section-break--{m.Groups[1].Value.ToLowerInvariant()} govuk-section-break--visible\">",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        // Sortable table marker: replaces line before table with sentinel comment.

        private const string SortableSentinel = "<!--dfe-sortable-table-->";

        /// <summary>
        /// Pre-processes markdown to replace {sortable} markers with a sentinel HTML comment
        /// placed immediately before the table. After Markdig renders the markdown to HTML,
        /// WrapSortableTables() replaces each sentinel + &lt;table&gt; pair with a
        /// &lt;div data-module="dfe-sortable-table"&gt;&lt;table&gt;...&lt;/table&gt;&lt;/div&gt;.
        ///
        /// This two-step approach is necessary because Markdig will not parse markdown tables
        /// that are nested inside a raw HTML block — the table must be rendered by Markdig first.
        ///
        /// Syntax — place {sortable} on the line immediately before the table header row:
        ///
        ///   {sortable}
        ///   | Name | Role | Phase |
        ///   |------|------|-------|
        ///   | Jane | Lead | Beta  |
        ///
        /// Without the marker, tables render as normal non-sortable GOV.UK tables.
        /// </summary>
        private static string ApplySortableTableMarkers(string markdown)
        {
            // Replace {sortable} on its own line with the sentinel comment.
            // The table lines that follow are left untouched so Markdig can parse them.
            return Regex.Replace(
                markdown,
                @"^\{sortable\}\r?\n",
                SortableSentinel + "\n",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Post-processes rendered HTML to wrap any &lt;table&gt; immediately preceded by
        /// the sortable sentinel comment in a &lt;div data-module="dfe-sortable-table"&gt;.
        /// </summary>
        private static string WrapSortableTables(string html)
        {
            // The sentinel comment may be inside a <p> if Markdig wrapped it — strip those first
            html = Regex.Replace(html,
                @"<p[^>]*>\s*" + Regex.Escape(SortableSentinel) + @"\s*</p>",
                SortableSentinel,
                RegexOptions.IgnoreCase);

            // Now wrap: sentinel immediately followed (possibly with whitespace) by <table...>...</table>
            html = Regex.Replace(html,
                Regex.Escape(SortableSentinel) + @"\s*(<table\b[\s\S]*?</table>)",
                "<div data-module=\"dfe-sortable-table\">$1</div>",
                RegexOptions.IgnoreCase);

            return html;
        }

        // New-tab link modifier (markdown pre-process).

        /// <summary>
        /// Pre-processes markdown to convert [text](url){new-tab} and [text](url){new-tab "suffix"}
        /// into raw HTML anchor tags before Markdig runs.
        ///
        /// This must run before Markdig because UseAdvancedExtensions enables the generic attributes
        /// extension, which would consume the {…} syntax and apply it as an HTML attribute rather
        /// than leaving it as text for post-processing.
        /// </summary>
        private static string ApplyNewTabLinks(string markdown)
        {
            // [text](url){new-tab "custom suffix"} → raw <a> with visible suffix span
            markdown = Regex.Replace(
                markdown,
                @"\[([^\]]+)\]\(([^)]+)\)\{new-tab\s+""([^""]*)""\}",
                m =>
                {
                    var text = m.Groups[1].Value;
                    var url = m.Groups[2].Value;
                    var suffix = HtmlEncode(m.Groups[3].Value.Trim());
                    return $"<a href=\"{HtmlEncode(url)}\" class=\"govuk-link\" target=\"_blank\" rel=\"noopener noreferrer\">{text} <span class=\"dfe-link-suffix\">({suffix})</span></a>";
                },
                RegexOptions.IgnoreCase);

            // [text](url){new-tab} → raw <a> with visually hidden screen reader notice
            markdown = Regex.Replace(
                markdown,
                @"\[([^\]]+)\]\(([^)]+)\)\{new-tab\}",
                m =>
                {
                    var text = m.Groups[1].Value;
                    var url = m.Groups[2].Value;
                    return $"<a href=\"{HtmlEncode(url)}\" class=\"govuk-link\" target=\"_blank\" rel=\"noopener noreferrer\">{text} <span class=\"govuk-visually-hidden\">(link opens in new tab)</span></a>";
                },
                RegexOptions.IgnoreCase);

            return markdown;
        }

        /// <summary>
        /// Post-processes rendered HTML to apply {new-tab} and {new-tab "custom text"} modifiers
        /// that appear immediately after a closing </a> tag.
        ///
        /// Syntax in markdown:
        ///   [Link text](https://example.com){new-tab}
        ///   [Link text](https://example.com){new-tab "opens on Intranet"}
        ///
        /// {new-tab} adds target="_blank" rel="noopener noreferrer" and appends a visually
        /// hidden "(link opens in new tab)" span for screen reader users.
        ///
        /// {new-tab "custom text"} adds the same attributes but appends a visible
        /// dfe-link-suffix span with the custom text instead.
        /// </summary>
        private static string ApplyLinkModifiers(string html)
        {
            // Match </a>{new-tab "custom text"} — visible custom suffix
            html = Regex.Replace(
                html,
                @"(<a\b[^>]*>)(.*?)(</a>)\{new-tab\s+""([^""]*)""\}",
                m =>
                {
                    var openTag = AddNewTabAttrs(m.Groups[1].Value);
                    var text = m.Groups[2].Value;
                    var suffix = HtmlEncode(m.Groups[4].Value.Trim());
                    return $"{openTag}{text} <span class=\"dfe-link-suffix\">({suffix})</span></a>";
                },
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Match </a>{new-tab} — screen-reader-only "(link opens in new tab)"
            html = Regex.Replace(
                html,
                @"(<a\b[^>]*>)(.*?)(</a>)\{new-tab\}",
                m =>
                {
                    var openTag = AddNewTabAttrs(m.Groups[1].Value);
                    var text = m.Groups[2].Value;
                    return $"{openTag}{text} <span class=\"govuk-visually-hidden\">(link opens in new tab)</span></a>";
                },
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return html;
        }

        /// <summary>Injects target="_blank" and rel="noopener noreferrer" into an opening anchor tag.</summary>
        private static string AddNewTabAttrs(string openTag)
        {
            // Remove any existing target or rel attributes first to avoid duplicates
            openTag = Regex.Replace(openTag, @"\s+target=""[^""]*""", "", RegexOptions.IgnoreCase);
            openTag = Regex.Replace(openTag, @"\s+rel=""[^""]*""", "", RegexOptions.IgnoreCase);
            // Insert before the closing >
            return Regex.Replace(openTag, @"\s*>$", " target=\"_blank\" rel=\"noopener noreferrer\">", RegexOptions.IgnoreCase);
        }

        private static string RenderPanelShortcode(string attrs, string body)
        {
            var id = Attr(attrs, "id", $"panel-{Guid.NewGuid():N}");
            var colour = Attr(attrs, "colour", "blue");
            var title = Attr(attrs, "title", "");
            var description = Attr(attrs, "description", "");
            var tagsRaw = Attr(attrs, "tags", "");
            var phasesRaw = Attr(attrs, "phases", "");
            var columnsAttr = Attr(attrs, "columns", "2");
            var singleCol = columnsAttr.Trim() == "1";

            var bodyId = $"gbody-{id}";
            var toggleId = $"gtog-{id}";

            // Tags
            var tagsHtml = "";
            if (!string.IsNullOrWhiteSpace(tagsRaw))
            {
                var tagItems = tagsRaw.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0);
                tagsHtml = $"<div class=\"dfe-f-panel-component__tags\">{string.Join("", tagItems.Select(t => $"<span class=\"dfe-f-panel-component__tag\">{HtmlEncode(t)}</span>"))}</div>";
            }

            // Title
            var titleHtml = string.IsNullOrWhiteSpace(title) ? "" : $"<h3 class=\"dfe-f-panel-component__title\">{HtmlEncode(title)}</h3>";

            // Description
            var descHtml = string.IsNullOrWhiteSpace(description) ? "" : $"<p class=\"dfe-f-panel-component__description\">{HtmlEncode(description)}</p>";

            // Phases
            var phaseClassMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Discovery", "dfe-f-panel-component__phase--discovery" },
                { "Alpha",     "dfe-f-panel-component__phase--alpha" },
                { "Beta",      "dfe-f-panel-component__phase--beta" },
                { "Live",      "dfe-f-panel-component__phase--live" },
            };
            var phasesHtml = "";
            if (!string.IsNullOrWhiteSpace(phasesRaw))
            {
                var phaseItems = phasesRaw.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0);
                var pills = phaseItems.Select(p =>
                {
                    var cls = phaseClassMap.TryGetValue(p, out var pc) ? $"dfe-f-panel-component__phase {pc}" : "dfe-f-panel-component__phase";
                    return $"<span class=\"{cls}\">{HtmlEncode(p)}</span>";
                });
                phasesHtml = $"<div class=\"dfe-f-panel-component__phases\">{string.Join("", pills)}</div>";
            }

            // Body sections
            // [steps] and [checks] support an optional label="..." attribute
            var stepsMatch = Regex.Match(body, @"\[steps([^\]]*)\]([\s\S]*?)\[/steps\]", RegexOptions.IgnoreCase);
            var checksMatch = Regex.Match(body, @"\[checks([^\]]*)\]([\s\S]*?)\[/checks\]", RegexOptions.IgnoreCase);
            var linksMatch = Regex.Match(body, @"\[links\]([\s\S]*?)\[/links\]", RegexOptions.IgnoreCase);

            var hasBody = stepsMatch.Success || checksMatch.Success;

            // Steps column
            var stepsHtml = "";
            if (stepsMatch.Success)
            {
                var stepsAttrs = stepsMatch.Groups[1].Value;
                var stepsLabel = Attr(stepsAttrs, "label", "What your team needs to do");
                var stepsRaw = stepsMatch.Groups[2].Value;

                // Try numbered-list mode first (lines matching "1. text")
                var numberedItems = stepsRaw
                    .Split('\n')
                    .Select(l => Regex.Match(l.Trim(), @"^\d+\.\s+(.+)$"))
                    .Where(sm => sm.Success)
                    .ToList();

                string stepsBodyHtml;
                if (numberedItems.Count > 0)
                {
                    // Render as numbered step chips
                    stepsBodyHtml = string.Join("", numberedItems.Select((sm, i) =>
                        $"<div class=\"dfe-f-panel-component__step\"><span class=\"dfe-f-panel-component__step-num\">{i + 1}</span><span>{InlineMarkdown(sm.Groups[1].Value.Trim())}</span></div>"));
                }
                else
                {
                    // Render as rich markdown (paragraphs, bullets, etc.)
                    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                    stepsBodyHtml = ApplyGovUkClasses(Markdown.ToHtml(stepsRaw.Trim(), pipeline));
                }

                stepsHtml = $"<div><div class=\"dfe-f-panel-component__col-label\">{HtmlEncode(stepsLabel)}</div><div class=\"dfe-f-panel-component__steps-body\">{stepsBodyHtml}</div></div>";
            }

            // Checks column
            var checksHtml = "";
            if (checksMatch.Success)
            {
                var checksAttrs = checksMatch.Groups[1].Value;
                var checksLabel = Attr(checksAttrs, "label", "What assessors and reviewers check");
                var checkLines = checksMatch.Groups[2].Value
                    .Split('\n')
                    .Select(l => Regex.Match(l.Trim(), @"^[-*]\s+(.+)$"))
                    .Where(cm => cm.Success)
                    .Select(cm => $"<div class=\"dfe-f-panel-component__bullet\">{InlineMarkdown(cm.Groups[1].Value.Trim())}</div>");
                checksHtml = $"<div><div class=\"dfe-f-panel-component__col-label\">{HtmlEncode(checksLabel)}</div><div>{string.Join("", checkLines)}</div></div>";
            }

            // Single column: only one block present, or columns="1" explicitly set
            var effectivelySingleCol = singleCol || (stepsMatch.Success ^ checksMatch.Success);
            var columnsCls = effectivelySingleCol
                ? "dfe-f-panel-component__columns dfe-f-panel-component__columns--single"
                : "dfe-f-panel-component__columns";

            var columnsHtml = hasBody
                ? $"<div class=\"{columnsCls}\">{stepsHtml}{checksHtml}</div>"
                : "";

            // Links row
            var linksHtml = "";
            if (linksMatch.Success)
            {
                var linkItems = linksMatch.Groups[1].Value
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && l.Contains('|'))
                    .Select(l =>
                    {
                        var parts = l.Split('|').Select(p => p.Trim()).ToArray();
                        var label = parts.Length > 0 ? parts[0] : "";
                        var href = parts.Length > 1 ? parts[1] : "#";
                        var isPrimary = parts.Length > 2 && parts[2].Equals("primary", StringComparison.OrdinalIgnoreCase);
                        var cls = isPrimary ? "dfe-f-panel-component__link dfe-f-panel-component__link--primary" : "dfe-f-panel-component__link";
                        return $"<a class=\"{cls}\" href=\"{HtmlEncode(href)}\" rel=\"noopener noreferrer\">{HtmlEncode(label)}</a>";
                    });
                linksHtml = $"<div class=\"dfe-f-panel-component__links\">{string.Join("", linkItems)}</div>";
            }

            // Assemble
            var hasToggle = hasBody || linksMatch.Success;

            var toggleButton = hasToggle
                ? $"<button class=\"dfe-f-panel-component__toggle\" id=\"{toggleId}\" aria-expanded=\"false\" aria-controls=\"{bodyId}\"><span class=\"dfe-f-panel-component__toggle-label\">Detail</span><svg class=\"dfe-f-panel-component__toggle-icon\" width=\"12\" height=\"8\" viewBox=\"0 0 12 8\" fill=\"none\" aria-hidden=\"true\" focusable=\"false\"><path d=\"M1 1L6 6L11 1\" stroke=\"#0b0c0c\" stroke-width=\"2\" stroke-linecap=\"round\"></path></svg></button>"
                : "";

            var bodySection = hasToggle
                ? $"<div class=\"dfe-f-panel-component__body\" id=\"{bodyId}\">{columnsHtml}{linksHtml}</div>"
                : "";

            return $"\n<div class=\"dfe-f-panel-component dfe-f-panel-component--{colour}\">" +
                   $"<div class=\"dfe-f-panel-component__header\">" +
                   $"<div class=\"dfe-f-panel-component__header-main\">{tagsHtml}{titleHtml}{descHtml}{phasesHtml}</div>" +
                   $"{toggleButton}" +
                   $"</div>" +
                   $"{bodySection}" +
                   $"</div>\n";
        }

        private static string HtmlEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

        /// <summary>
        /// Renders markdown as inline HTML (no wrapping block element). Use for headers or short text
        /// that may contain **bold**, *italic*, `code`, or [label](url) links. Applies govuk-link to links.
        /// </summary>
        public static string ToGovUkInlineHtml(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
            markdown = NormaliseReversedMarkdownLinks(markdown.Trim());
            var html = Markdown.ToHtml(markdown).Trim();
            html = Regex.Replace(html, @"^<p>([\s\S]*?)<\/p>$", "$1", RegexOptions.IgnoreCase).Trim();
            html = Regex.Replace(html, @"<a\b(?![^>]*class=)([^>]*?)href=""([^""]*?)""([^>]*?)>",
                "<a$1href=\"$2\"$3 class=\"govuk-link\">", RegexOptions.IgnoreCase);
            return html;
        }

        /// <summary>
        /// Renders a single line of text as inline markdown — supports **bold**, *italic*,
        /// `code`, and [label](url) links. Returns an HTML fragment (no wrapping block element).
        /// </summary>
        private static string InlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Use Markdig to convert, then strip the wrapping <p>...</p> it adds
            var html = Markdown.ToHtml(text.Trim()).Trim();
            // Remove outer <p> wrapper
            html = Regex.Replace(html, @"^<p>([\s\S]*?)<\/p>$", "$1", RegexOptions.IgnoreCase).Trim();
            // Apply govuk-link class to any links produced
            html = Regex.Replace(html, @"<a\b(?![^>]*class=)([^>]*?)href=""([^""]*?)""([^>]*?)>",
                "<a$1href=\"$2\"$3 class=\"govuk-link\">", RegexOptions.IgnoreCase);
            return html;
        }

        // >x> / >!> inline panels

        /// <summary>
        /// Pre-processes custom panel syntax. Consecutive lines sharing the same
        /// prefix are grouped into a single panel block:
        ///
        ///   >x> **Warning**
        ///   >x> Your service could be taken offline if it does not have an appropriate name.
        ///
        ///   >!> **Note**
        ///   >!> This is an informational callout.
        ///
        /// The prefix is stripped, the remaining content is rendered as markdown,
        /// and the result is wrapped in the appropriate panel div.
        /// </summary>
        private static string ApplyCustomPanels(string markdown)
        {
            var lines = markdown.Split('\n');
            var output = new System.Text.StringBuilder();
            string? currentType = null;
            var blockLines = new List<string>();

            void FlushBlock()
            {
                if (currentType is null || blockLines.Count == 0) return;
                var inner = string.Join("\n", blockLines).Trim();
                var innerHtml = Markdown.ToHtml(inner);
                output.AppendLine($"\n<div class=\"dfe-f-panel dfe-f-panel--{currentType}\">{innerHtml}</div>\n");
                blockLines.Clear();
                currentType = null;
            }

            foreach (var line in lines)
            {
                if (line.StartsWith(">x> ") || line == ">x>")
                {
                    if (currentType != "warning") FlushBlock();
                    currentType = "warning";
                    blockLines.Add(line.Length > 4 ? line[4..] : "");
                }
                else if (line.StartsWith(">!> ") || line == ">!>")
                {
                    if (currentType != "info") FlushBlock();
                    currentType = "info";
                    blockLines.Add(line.Length > 4 ? line[4..] : "");
                }
                else
                {
                    FlushBlock();
                    output.AppendLine(line);
                }
            }

            FlushBlock();
            return output.ToString();
        }

        /// <summary>
        /// Markdig sometimes wraps raw HTML blocks in a paragraph — strip those wrappers
        /// from around our panel divs.
        /// </summary>
        private static string RestoreCustomPanels(string html)
        {
            // Unwrap dfe-f-panel blocks (>x> / >!> panels)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-panel[^""]*"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap dfe-f-panel-component blocks ([panel] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-panel-component[^""]*"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap dfe-f-metric blocks ([metric] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-metric[^""]*"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap dfe-f-metric-grid blocks ([metric-grid] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-metric-grid"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap dfe-f-stat-card blocks ([stat-card] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-stat-card[^""]*"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap dfe-f-stat-card-grid blocks ([stat-card-grid] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-f-stat-card-grid"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap card list blocks ([chevron-cards] / [card-list] shortcodes)
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-card-list-block"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap [noBullets] blocks
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-no-bullets"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            // Unwrap [chevroncard ordered/unordered] action list blocks
            html = Regex.Replace(html,
                @"<p[^>]*>\s*(<div class=""dfe-action-list-block"">[\s\S]*?</div>)\s*</p>",
                "$1",
                RegexOptions.IgnoreCase);

            return html;
        }

        /// <summary>
        /// Extracts headings from markdown for use in a contents list.
        /// Returns h2 and h3 only (so contents start at level 2; h1 is assumed to be the page title above).
        /// </summary>
        public static List<HeadingEntry> ExtractHeadings(string? markdown)
        {
            var entries = new List<HeadingEntry>();

            if (string.IsNullOrEmpty(markdown))
                return entries;

            var html = Markdown.ToHtml(markdown);
            var matches = Regex.Matches(html, @"<(h[23])[^>]*>(.*?)<\/h[23]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var tag = match.Groups[1].Value.ToLower();
                var level = int.Parse(tag[1].ToString());
                var rawText = Regex.Replace(match.Groups[2].Value, @"<[^>]+>", "").Trim();
                var id = GenerateId(rawText);
                entries.Add(new HeadingEntry(id, rawText, level));
            }

            return entries;
        }

        /// <summary>
        /// Extracts h2 and h3 headings from already-rendered HTML (e.g. from ToGovUkHtmlForBody).
        /// Use when the body is rendered with demoted headings so the contents list matches the on-page structure (starting at level 2).
        /// </summary>
        public static List<HeadingEntry> ExtractHeadingsFromHtml(string? html)
        {
            var entries = new List<HeadingEntry>();

            if (string.IsNullOrEmpty(html))
                return entries;

            var matches = Regex.Matches(html, @"<(h[23])[^>]*>(.*?)<\/h[23]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var tag = match.Groups[1].Value.ToLower();
                var level = int.Parse(tag[1].ToString());
                var rawText = Regex.Replace(match.Groups[2].Value, @"<[^>]+>", "").Trim();
                var id = GenerateId(rawText);
                entries.Add(new HeadingEntry(id, rawText, level));
            }

            return entries;
        }

        /// <summary>
        /// Generates a URL-safe ID from heading text, matching the IDs added to headings in the HTML output.
        /// </summary>
        public static string GenerateId(string text)
        {
            var id = text.ToLower();
            id = Regex.Replace(id, @"[^\w\s-]", "");
            id = Regex.Replace(id, @"\s+", "-");
            id = Regex.Replace(id, @"-+", "-");
            id = id.Trim('-');
            return id;
        }

        private static string ApplyGovUkClasses(string html)
        {
            // Apply GOV.UK heading classes and add id attributes
            html = Regex.Replace(html, @"<h1(?![^>]*class=)[^>]*>(.*?)<\/h1>",
                m => $"<h1 class=\"govuk-heading-xl\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h1>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<h2(?![^>]*class=)[^>]*>(.*?)<\/h2>",
                m => $"<h2 class=\"govuk-heading-l\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h2>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<h3(?![^>]*class=)[^>]*>(.*?)<\/h3>",
                m => $"<h3 class=\"govuk-heading-m\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h3>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<h4(?![^>]*class=)[^>]*>(.*?)<\/h4>",
                m => $"<h4 class=\"govuk-heading-s\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h4>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<h5(?![^>]*class=)[^>]*>(.*?)<\/h5>",
                m => $"<h5 class=\"govuk-heading-s\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h5>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<h6(?![^>]*class=)[^>]*>(.*?)<\/h6>",
                m => $"<h6 class=\"govuk-heading-s\" id=\"{GenerateId(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", "").Trim())}\">{m.Groups[1].Value}</h6>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Apply GOV.UK paragraph classes (only if no class exists)
            // Use \b after "p" so <path>, <pre> etc. are not matched
            html = Regex.Replace(html, @"<p\b(?![^>]*class=)[^>]*>", "<p class=\"govuk-body\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK link classes — \b ensures <a > not <abbr> etc.
            html = Regex.Replace(html, @"<a\b(?![^>]*class=)([^>]*?)href=""([^""]*?)""([^>]*?)>",
                "<a$1href=\"$2\"$3 class=\"govuk-link\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK list classes
            html = Regex.Replace(html, @"<ul\b(?![^>]*class=)[^>]*>", "<ul class=\"govuk-list govuk-list--bullet\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<ol\b(?![^>]*class=)[^>]*>", "<ol class=\"govuk-list govuk-list--number\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK table classes
            html = Regex.Replace(html, @"<table\b(?![^>]*class=)[^>]*>", "<table class=\"govuk-table\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<thead\b(?![^>]*class=)[^>]*>", "<thead class=\"govuk-table__head\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<tbody\b(?![^>]*class=)[^>]*>", "<tbody class=\"govuk-table__body\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<tr\b(?![^>]*class=)[^>]*>", "<tr class=\"govuk-table__row\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<th\b(?![^>]*class=)[^>]*>", "<th class=\"govuk-table__header\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<td\b(?![^>]*class=)[^>]*>", "<td class=\"govuk-table__cell\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK blockquote classes
            html = Regex.Replace(html, @"<blockquote\b(?![^>]*class=)[^>]*>", "<blockquote class=\"govuk-inset-text\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK code classes
            html = Regex.Replace(html, @"<code\b(?![^>]*class=)[^>]*>", "<code class=\"govuk-code\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<pre\b(?![^>]*class=)[^>]*>", "<pre class=\"govuk-code\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK text formatting classes
            html = Regex.Replace(html, @"<strong\b(?![^>]*class=)[^>]*>", "<strong class=\"govuk-!-font-weight-bold\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<em\b(?![^>]*class=)[^>]*>", "<em class=\"govuk-!-font-style-italic\">", RegexOptions.IgnoreCase);

            // Apply GOV.UK horizontal rule
            html = Regex.Replace(html, @"<hr\b(?![^>]*class=)[^>]*>", "<hr class=\"govuk-section-break govuk-section-break--visible\">", RegexOptions.IgnoreCase);

            // Handle definition lists
            html = Regex.Replace(html, @"<dl\b(?![^>]*class=)[^>]*>", "<dl class=\"govuk-summary-list\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<dt\b(?![^>]*class=)[^>]*>", "<dt class=\"govuk-summary-list__key\">", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<dd\b(?![^>]*class=)[^>]*>", "<dd class=\"govuk-summary-list__value\">", RegexOptions.IgnoreCase);

            // Handle address elements
            html = Regex.Replace(html, @"<address\b(?![^>]*class=)[^>]*>", "<address class=\"govuk-body\">", RegexOptions.IgnoreCase);

            // Handle small text
            html = Regex.Replace(html, @"<small\b(?![^>]*class=)[^>]*>", "<small class=\"govuk-body-s\">", RegexOptions.IgnoreCase);

            return html;
        }

        /// <summary>Alias for <see cref="ToGovUkHtmlForBody"/>.</summary>
        public static string ToBodyHtml(string? markdown) => ToGovUkHtmlForBody(markdown);

        /// <summary>
        /// Body content from the CMS may be markdown or an HTML fragment. When the trimmed value clearly opens as HTML
        /// (optional whitespace, then <c>&lt;</c> with a tag, comment, doctype, or XML declaration), Markdig is skipped and the same
        /// GOV.UK HTML post-processing as <see cref="ToGovUkHtml"/> is applied; otherwise content is treated as markdown.
        /// </summary>
        public static string ToBodyHtmlOrHtmlFragment(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var trimmedStart = content.TrimStart();
            if (!LooksLikeLeadingHtmlFragment(trimmedStart))
                return ToBodyHtml(content);

            try
            {
                var html = trimmedStart;
                html = ApplyGovUkClasses(html);
                html = WrapSortableTables(html);
                html = RestoreCustomPanels(html);
                html = ApplyLinkModifiers(html);
                return html;
            }
            catch (Exception ex)
            {
                return $"<div class=\"govuk-error-summary\"><h2 class=\"govuk-error-summary__title\">HTML Processing Error</h2><div class=\"govuk-error-summary__body\"><p class=\"govuk-body\">Error: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p></div></div>";
            }
        }

        private static bool LooksLikeLeadingHtmlFragment(string s)
        {
            var i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;

            if (i >= s.Length || s[i] != '<')
                return false;

            i++;
            if (i >= s.Length)
                return false;

            if (s[i] is '!' or '?')
                return true;

            if (s[i] == '/')
            {
                i++;
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                    i++;

                return i < s.Length && char.IsLetter(s[i]);
            }

            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;

            return i < s.Length && char.IsLetter(s[i]);
        }

        /// <summary>Inline markdown for short headings (new-tab syntax + GOV.UK emphasis classes).</summary>
        public static string ToInlineHtml(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;
            try
            {
                var md = markdown.Trim();
                md = NormaliseReversedMarkdownLinks(md);
                md = ApplyNewTabLinks(md);
                var html = Markdown.ToHtml(md, SharedMarkdownPipeline).Trim();
                html = Regex.Replace(html, @"^<p>([\s\S]*?)<\/p>$", "$1", RegexOptions.IgnoreCase).Trim();
                html = Regex.Replace(html, @"<a\b(?![^>]*class=)([^>]*?)href=""([^""]*?)""([^>]*?)>",
                    "<a$1href=\"$2\"$3 class=\"govuk-link\">", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<strong\b(?![^>]*class=)[^>]*>", "<strong class=\"govuk-!-font-weight-bold\">", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<em\b(?![^>]*class=)[^>]*>", "<em class=\"govuk-!-font-style-italic\">", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<code\b(?![^>]*class=)[^>]*>", "<code class=\"govuk-code\">", RegexOptions.IgnoreCase);
                return html;
            }
            catch (Exception ex)
            {
                return WebUtility.HtmlEncode(ex.Message);
            }
        }
    }
}
