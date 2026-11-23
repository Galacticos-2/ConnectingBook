using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Project_Web_ConnectingBook.Server.Storage
{
    public class MinioStorage : IStorage, IDisposable
    {
        private readonly IMinioClient client;
        private readonly string bucket;

        public MinioStorage(IConfiguration configuration)
        {
            bucket = configuration["Storage:Bucket"] ?? throw new InvalidOperationException("Missing Storage__Bucket.");
            client = new MinioClient()
                .WithEndpoint(configuration["Storage:Endpoint"] ?? throw new InvalidOperationException("Missing Storage__Endpoint."))
                .WithCredentials(configuration["Storage:AccessKey"] ?? throw new InvalidOperationException("Missing storage credentials."),
                    configuration["Storage:SecretKey"] ?? throw new InvalidOperationException("Missing storage credentials."))
                .WithSSL(configuration.GetValue("Storage:UseSsl", true)).Build();
        }

        public async Task PutAsync(string key, Stream input, long size, string contentType, CancellationToken ct) =>
            await client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(key).WithStreamData(input)
                .WithObjectSize(size).WithContentType(contentType), ct);

        public async Task<StoredObject?> StatAsync(string key, CancellationToken ct)
        {
            try
            {
                var value = await client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key), ct);
                return new StoredObject(value.Size, value.ETag);
            }
            catch (ObjectNotFoundException) { return null; }
        }

        public async Task CopyToAsync(string key, Stream output, long offset, long length, CancellationToken ct) =>
            await client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket).WithObject(key)
                .WithOffsetAndLength(offset, length)
                .WithCallbackStream((stream, cancellation) => stream.CopyToAsync(output, cancellation)), ct);

        public async Task DeleteAsync(string key, CancellationToken ct) =>
            await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), ct);

        public async Task<bool> IsReadyAsync(CancellationToken ct) =>
            await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);

        public void Dispose() => client.Dispose();
    }
}
