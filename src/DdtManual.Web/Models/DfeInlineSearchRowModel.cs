namespace DdtManual.Web.Models;

/// <summary>Plain search input + square blue icon submit for filter/search GET forms.</summary>
public sealed class DfeInlineSearchRowModel
{
    public required string InputId { get; init; }

    public required string InputName { get; init; }

    public string? Value { get; init; }

    /// <summary>Optional <c>aria-describedby</c> (e.g. hint element id).</summary>
    public string? DescribedBy { get; init; }

    /// <summary>Accessible name for the icon-only submit control.</summary>
    public string SubmitAriaLabel { get; init; } = "Search";

    public bool Spellcheck { get; init; }
}
