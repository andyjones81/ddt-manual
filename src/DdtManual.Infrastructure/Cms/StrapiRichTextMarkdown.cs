using System.Text;
using System.Text.Json;

namespace DdtManual.Infrastructure.Cms;

/// <summary>
/// Converts Strapi Rich Text (blocks JSON) to markdown-ish text so downstream markdown rendering
/// (including <c>[[ServiceStandardList]]</c>) runs on the same pipeline as plain string fields.
/// </summary>
internal static class StrapiRichTextMarkdown
{
    public static string? FromJsonElement(JsonElement obj, params string[] fieldNames)
    {
        foreach (var name in fieldNames)
        {
            if (!TryGetPropertyFlexible(obj, name, out var p))
                continue;

            if (p.ValueKind == JsonValueKind.String)
                return p.GetString();

            if (p.ValueKind == JsonValueKind.Array)
                return BlocksToMarkdown(p);
        }

        return null;
    }

    private static bool TryGetPropertyFlexible(JsonElement obj, string name, out JsonElement prop)
    {
        if (obj.TryGetProperty(name, out prop))
            return true;

        if (name.Length > 0)
        {
            var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
            if (obj.TryGetProperty(camel, out prop))
                return true;
        }

        return false;
    }

    private static string BlocksToMarkdown(JsonElement blocksArray)
    {
        var sb = new StringBuilder();
        foreach (var block in blocksArray.EnumerateArray())
            AppendBlock(block, sb);

        var result = sb.ToString().Trim();
        if (string.IsNullOrEmpty(result) && blocksArray.GetArrayLength() > 0)
            result = FlattenAllTextNodes(blocksArray).Trim();

        return result;
    }

    private static void AppendBlock(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        if (!node.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            AppendTextLeaf(node, sb);
            AppendChildren(node, sb);
            return;
        }

        var type = typeEl.GetString();
        switch (type)
        {
            case "text":
                AppendTextLeaf(node, sb);
                return;

            case "paragraph":
                AppendChildren(node, sb);
                sb.Append("\n\n");
                return;

            case "heading":
                var level = 2;
                if (node.TryGetProperty("level", out var lv))
                {
                    if (lv.ValueKind == JsonValueKind.Number && lv.TryGetInt32(out var n))
                        level = Math.Clamp(n, 1, 6);
                }

                sb.Append(new string('#', level)).Append(' ');
                AppendChildren(node, sb);
                sb.Append("\n\n");
                return;

            case "list":
                AppendList(node, sb);
                return;

            case "quote":
                sb.Append("> ");
                AppendChildren(node, sb);
                sb.Append("\n\n");
                return;

            case "code":
                sb.Append("```\n");
                AppendChildren(node, sb);
                sb.Append("\n```\n\n");
                return;

            case "link":
                var url = "";
                if (node.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    url = urlEl.GetString() ?? "";

                var inner = new StringBuilder();
                AppendChildren(node, inner);
                sb.Append('[').Append(inner).Append("](").Append(url).Append(')');
                return;

            case "list-item":
                AppendChildren(node, sb);
                return;

            default:
                AppendChildren(node, sb);
                return;
        }
    }

    private static void AppendList(JsonElement listBlock, StringBuilder sb)
    {
        var ordered = false;
        if (listBlock.TryGetProperty("format", out var fmt) && fmt.ValueKind == JsonValueKind.String)
            ordered = string.Equals(fmt.GetString(), "ordered", StringComparison.OrdinalIgnoreCase);

        if (!listBlock.TryGetProperty("children", out var items) || items.ValueKind != JsonValueKind.Array)
            return;

        var i = 1;
        foreach (var item in items.EnumerateArray())
        {
            var line = new StringBuilder();
            AppendBlock(item, line);
            var lineStr = line.ToString().Trim();
            if (string.IsNullOrEmpty(lineStr))
                continue;

            if (ordered)
                sb.Append(i++).Append(". ").AppendLine(lineStr);
            else
                sb.Append("- ").AppendLine(lineStr);
        }

        sb.Append('\n');
    }

    private static void AppendChildren(JsonElement block, StringBuilder sb)
    {
        if (!block.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            return;

        foreach (var child in children.EnumerateArray())
            AppendBlock(child, sb);
    }

    private static void AppendTextLeaf(JsonElement node, StringBuilder sb)
    {
        if (!node.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
            return;

        var text = textEl.GetString() ?? "";
        var bold = node.TryGetProperty("bold", out var b) && b.ValueKind == JsonValueKind.True;
        var italic = node.TryGetProperty("italic", out var it) && it.ValueKind == JsonValueKind.True;
        var code = node.TryGetProperty("code", out var cd) && cd.ValueKind == JsonValueKind.True;

        if (code)
        {
            sb.Append('`').Append(text).Append('`');
            return;
        }

        if (bold && italic)
            sb.Append("***").Append(text).Append("***");
        else if (bold)
            sb.Append("**").Append(text).Append("**");
        else if (italic)
            sb.Append('*').Append(text).Append('*');
        else
            sb.Append(text);
    }

    /// <summary>Last resort: collect every <c>text</c> property in document order (preserves shortcodes).</summary>
    private static string FlattenAllTextNodes(JsonElement root)
    {
        var sb = new StringBuilder();
        FlattenRecursive(root, sb);
        return sb.ToString();
    }

    private static void FlattenRecursive(JsonElement el, StringBuilder sb)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (el.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    sb.Append(t.GetString());
                    return;
                }

                foreach (var p in el.EnumerateObject())
                    FlattenRecursive(p.Value, sb);
                return;

            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    FlattenRecursive(item, sb);
                break;
        }
    }
}
