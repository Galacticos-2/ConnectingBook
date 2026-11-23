using System.Security.Cryptography;

namespace Project_Web_ConnectingBook.Server.Storage
{
    public class LocalStorage : IStorage
    {
        private readonly string root;
        public LocalStorage(IConfiguration config, IWebHostEnvironment environment)
        {
            root = Path.GetFullPath(config["Storage:LocalPath"] ?? Path.Combine(environment.ContentRootPath, ".local", "media"));
            Directory.CreateDirectory(root);
        }

        private string FilePath(string key)
        {
            if (key.Length != 32 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid storage key.");
            return Path.Combine(root, key);
        }

        public async Task PutAsync(string key, Stream input, long size, string contentType, CancellationToken ct)
        {
            var path = FilePath(key);
            try
            {
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await input.CopyToAsync(file, ct);
                if (file.Length != size) throw new IOException("Unexpected object size.");
            }
            catch
            {
                // Never remove an earlier immutable object if CreateNew failed.
                throw;
            }
        }

        public async Task<StoredObject?> StatAsync(string key, CancellationToken ct)
        {
            var path = FilePath(key);
            if (!File.Exists(path)) return null;
            await using var file = File.OpenRead(path);
            return new StoredObject(file.Length, Convert.ToHexString(await SHA256.HashDataAsync(file, ct)));
        }

        public async Task CopyToAsync(string key, Stream output, long offset, long length, CancellationToken ct)
        {
            await using var input = new FileStream(FilePath(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            input.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[81920];
            while (length > 0)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length)), ct);
                if (read == 0) throw new EndOfStreamException();
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                length -= read;
            }
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            File.Delete(FilePath(key));
            return Task.CompletedTask;
        }
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(Directory.Exists(root));
    }
}
