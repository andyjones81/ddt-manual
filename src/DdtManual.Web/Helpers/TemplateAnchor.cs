using System.Text.RegularExpressions;

namespace DdtManual.Web.Helpers;

public static class TemplateAnchor
{
    public static string SectionIdFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "section";

        var slug = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "section" : "section-" + slug;
    }
}
