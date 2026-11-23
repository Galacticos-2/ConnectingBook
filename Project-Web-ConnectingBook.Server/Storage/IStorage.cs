namespace Project_Web_ConnectingBook.Server.Storage
{
    public record StoredObject(long Size, string ETag);
    public interface IStorage
    {
        Task PutAsync(string key, Stream input, long size, string contentType, CancellationToken ct);
        Task<StoredObject?> StatAsync(string key, CancellationToken ct);
        Task CopyToAsync(string key, Stream output, long offset, long length, CancellationToken ct);
        Task DeleteAsync(string key, CancellationToken ct);
        Task<bool> IsReadyAsync(CancellationToken ct);
    }
}
