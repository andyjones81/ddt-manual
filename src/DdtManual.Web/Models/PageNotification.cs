namespace DdtManual.Web.Models;

/// <summary>Optional CMS-driven banner (matches Service Manual <c>PageNotification</c>).</summary>
public sealed class PageNotification
{
    public string? Title { get; set; }
    public string? Message { get; set; }
}
