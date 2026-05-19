namespace FunctionLoadQueue.Core.DTOs
{
    public record FileQueueMessage(
        Guid Id,
        string FileName,
        string BlobName,
        string BlobUrl,
        long FileSizeInBytes,
        string ContentType,
        string Description,
        string UploadedAt
    );
}
