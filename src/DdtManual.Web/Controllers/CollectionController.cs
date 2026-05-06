using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using DdtManual.Web.Helpers;
using DdtManual.Web.Models.Templates;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

[Route("collection")]
public sealed class CollectionController(ICmsContentClient cmsContentClient) : Controller
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var dto = await cmsContentClient.GetCollectionBySlugAsync(slug, cancellationToken);
        if (dto == null)
            return NotFound();

        var model = MapToPageModel(dto);
        return View("~/Views/Templates/Collection.cshtml", model);
    }

    private static ManualCollectionPageModel MapToPageModel(CollectionDetailDto dto)
    {
        var sections = dto.Sections.Select(s => new ManualCollectionSectionModel
        {
            Title = s.Title,
            Items = s.Items.Select(i => new ManualCollectionLinkModel
            {
                Title = i.Title,
                Url = i.Url,
                ContentType = i.ContentType,
                LinkType = i.LinkType,
                Grade = i.Grade,
                OpenInNewTab = i.OpenInNewTab,
                CollectionSlugForQuery = i.CollectionSlugForQuery,
            }).ToList(),
        }).ToList();

        var related = dto.RelatedContent.Select(r => new ManualRelatedContentModel
        {
            HeaderHtml = GovUkMarkdown.ToInlineHtml(r.HeaderMarkdown),
            ContentHtml = string.IsNullOrWhiteSpace(r.ContentMarkdown)
                ? null
                : GovUkMarkdown.ToGovUkHtmlForRelatedContent(r.ContentMarkdown),
        }).ToList();

        var files = dto.RelatedFiles.Select(f => new ManualRelatedFileModel
        {
            Name = f.Name,
            Url = f.Url,
            SizeDisplay = f.SizeDisplay,
            FileType = f.FileType,
            Caption = f.Caption,
        }).ToList();

        return new ManualCollectionPageModel
        {
            Title = dto.Title,
            Slug = dto.Slug,
            MetaDescription = dto.MetaDescription,
            BodyHtml = string.IsNullOrWhiteSpace(dto.BodyMarkdown)
                ? null
                : GovUkMarkdown.ToBodyHtml(dto.BodyMarkdown),
            Sections = sections,
            RelatedContent = related,
            RelatedFiles = files,
            ShowDraftContentBanner = dto.ShowDraftContentBanner,
            ShowLastReviewedDateOnPage = dto.ShowLastReviewedDateOnPage,
            LastReviewedDateDisplay = dto.LastReviewedDateDisplay,
            Owner = dto.Owner,
            OwnerUrl = dto.OwnerUrl,
            AudienceTags = dto.AudienceTags.Select(t => new ManualTagRefModel { Slug = t.Slug, Title = t.Title }).ToList(),
        };
    }
}
