namespace DdtManual.Web.Services;

/// <summary>
/// Provides file metadata for blob storage URLs used in [fileDownload] document blocks.
/// When not configured or for non-blob URLs, returns null and the UI keeps the placeholder meta line.
/// </summary>
public interface IBlobMetadataProvider
{
    Task<BlobMetadata?> GetMetadataAsync(string blobUrl, CancellationToken cancellationToken = default);
}

public record BlobMetadata(long SizeBytes, DateTimeOffset LastModified);
