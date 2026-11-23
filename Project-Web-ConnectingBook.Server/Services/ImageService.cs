using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Storage;
using SkiaSharp;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class ImageService(ConnectingBookDbContext db, IStorage storage)
    {
        public async Task<object> UploadAsync(Guid owner, IFormFile file, bool avatar, CancellationToken ct)
        {
            var limit = (avatar ? 2 : 5) * 1024 * 1024;
            if (file.Length > limit) throw new ApiException(413, "image_too_large", "Ảnh vượt dung lượng cho phép.");
            await using var input = file.OpenReadStream();
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (memory.Length + count > limit) throw new ApiException(413, "image_too_large", "Ảnh vượt dung lượng cho phép.");
                memory.Write(buffer, 0, count);
            }
            var bytes = memory.ToArray();
            var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
            var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var webp = bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
            if (!jpeg && !png && !webp) throw new ApiException(415, "invalid_image_type", "Chỉ chấp nhận JPEG, PNG và WebP thực tế.");
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 4096 || codec.Info.Height > 4096)
                throw new ApiException(400, "invalid_image", "Ảnh hỏng hoặc vượt kích thước 4096 px.");
            if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
                throw new ApiException(415, "invalid_image_type", "Định dạng ảnh không hợp lệ.");
            using var decoded = new SKBitmap(codec.Info.Width, codec.Info.Height);
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw new ApiException(400, "invalid_image", "Không thể đọc ảnh.");
            using var oriented = Orient(decoded, codec.EncodedOrigin);
            using var normalized = avatar ? Square(oriented, Math.Min(512, Math.Min(oriented.Width, oriented.Height))) : oriented.Copy();
            using var thumbnail = Square(oriented, 256);
            using var encoded = SKImage.FromBitmap(normalized);
            using var encodedThumb = SKImage.FromBitmap(thumbnail);
            using var fullData = encoded.Encode(SKEncodedImageFormat.Png, 100);
            using var thumbData = encodedThumb.Encode(SKEncodedImageFormat.Png, 100);
            var media = new Media { OwnerId = owner, Kind = avatar ? "Avatar" : "Image", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "image/png", Width = normalized.Width, Height = normalized.Height, SizeBytes = fullData.Size };
            var thumb = new Media { OwnerId = owner, Kind = "Thumbnail", ParentMediaId = media.Id, StorageKey = Guid.NewGuid().ToString("N"), ContentType = "image/png", Width = thumbnail.Width, Height = thumbnail.Height, SizeBytes = thumbData.Size };
            var stored = new List<string>();
            try
            {
                await using var fullStream = fullData.AsStream();
                await storage.PutAsync(media.StorageKey, fullStream, media.SizeBytes, media.ContentType, ct); stored.Add(media.StorageKey);
                await using var thumbStream = thumbData.AsStream();
                await storage.PutAsync(thumb.StorageKey, thumbStream, thumb.SizeBytes, thumb.ContentType, ct); stored.Add(thumb.StorageKey);
                db.Media.AddRange(media, thumb);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                foreach (var key in stored) await storage.DeleteAsync(key, CancellationToken.None);
                throw;
            }
            return new { ImageId = media.Id, ThumbnailId = thumb.Id, media.Width, media.Height };
        }

        private static SKBitmap Square(SKBitmap source, int size)
        {
            var result = new SKBitmap(size, size);
            using var canvas = new SKCanvas(result);
            var side = Math.Min(source.Width, source.Height);
            var x = (source.Width - side) / 2f; var y = (source.Height - side) / 2f;
            canvas.DrawBitmap(source, new SKRect(x, y, x + side, y + side), new SKRect(0, 0, size, size));
            return result;
        }

        private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var result = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
            using var canvas = new SKCanvas(result);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(result.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, result.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, result.Height); canvas.RotateDegrees(270); break;
            }
            canvas.DrawBitmap(bitmap, 0, 0);
            return result;
        }
    }
}
