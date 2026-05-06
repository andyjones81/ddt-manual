using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace DdtManual.Web.Helpers;

public static class SearchHighlightHelper
{
    /// <summary>Returns HTML with keywords wrapped in &lt;mark class="dfe-f-search-highlight"&gt;. Plain text is HTML-encoded.</summary>
    public static IHtmlContent HighlightKeywords(string? text, string? keywords)
    {
        if (string.IsNullOrEmpty(text))
            return new HtmlString(System.Net.WebUtility.HtmlEncode(text ?? ""));

        if (string.IsNullOrWhiteSpace(keywords))
            return new HtmlString(System.Net.WebUtility.HtmlEncode(text));

        var words = keywords.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return new HtmlString(System.Net.WebUtility.HtmlEncode(text));

        var ranges = new List<(int Start, int End)>();
        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word))
                continue;

            var pattern = Regex.Escape(word);
            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
                ranges.Add((m.Index, m.Index + m.Length));
        }

        if (ranges.Count == 0)
            return new HtmlString(System.Net.WebUtility.HtmlEncode(text));

        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(int Start, int End)> { ranges[0] };
        for (var i = 1; i < ranges.Count; i++)
        {
            var last = merged[^1];
            if (ranges[i].Start <= last.End)
                merged[^1] = (last.Start, Math.Max(last.End, ranges[i].End));
            else
                merged.Add(ranges[i]);
        }

        var sb = new StringBuilder();
        var pos = 0;
        foreach (var (start, end) in merged)
        {
            sb.Append(System.Net.WebUtility.HtmlEncode(text[pos..start]));
            sb.Append("<mark class=\"dfe-f-search-highlight\">");
            sb.Append(System.Net.WebUtility.HtmlEncode(text[start..end]));
            sb.Append("</mark>");
            pos = end;
        }

        sb.Append(System.Net.WebUtility.HtmlEncode(text[pos..]));
        return new HtmlString(sb.ToString());
    }
}
