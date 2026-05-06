namespace DdtManual.Application.Content;

public sealed class ContentByProfessionViewModel
{
    public string ProfessionTitle { get; init; } = string.Empty;
    public string ProfessionSlug { get; init; } = string.Empty;
    public IReadOnlyList<ContentIndexItemDto> Items { get; init; } = [];
}
