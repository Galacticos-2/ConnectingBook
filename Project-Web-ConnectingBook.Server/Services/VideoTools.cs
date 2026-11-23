using System.Diagnostics;
using System.Text.Json;
using Project_Web_ConnectingBook.Server.Infrastructure;

namespace Project_Web_ConnectingBook.Server.Services
{
    public record VideoInfo(int Width, int Height, double Duration);
    public class VideoTools(IConfiguration configuration, IWebHostEnvironment environment)
    {
        public string WorkDirectory { get; } = Path.Combine(environment.ContentRootPath, ".local", "video-work");

        public async Task<string> RunAsync(bool probe, IEnumerable<string> arguments, CancellationToken ct)
        {
            var start = new ProcessStartInfo(configuration[probe ? "Video:FfprobePath" : "Video:FfmpegPath"] ?? (probe ? "ffprobe" : "ffmpeg"))
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(probe ? TimeSpan.FromSeconds(20) : TimeSpan.FromMinutes(5));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Video processor unavailable.");
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            var text = await output;
            _ = await errors; // Never persist process stderr, uploaded names or file contents.
            if (process.ExitCode != 0) throw new ApiException(400, "invalid_video", "Không thể đọc hoặc xử lý video.");
            return text;
        }

        public async Task<VideoInfo> ProbeAsync(string path, CancellationToken ct)
        {
            var header = new byte[12];
            await using (var stream = File.OpenRead(path))
            {
                if (await stream.ReadAsync(header, ct) != header.Length || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8) || header.AsSpan(8, 4).SequenceEqual("qt  "u8))
                    throw new ApiException(415, "invalid_video_type", "Chỉ chấp nhận video MP4 thực tế.");
            }
            var output = await RunAsync(true, ["-v", "error", "-protocol_whitelist", "file", "-show_entries", "format=format_name,duration:stream=codec_type,width,height", "-of", "json", path], ct);
            using var json = JsonDocument.Parse(output);
            var format = json.RootElement.GetProperty("format");
            var video = json.RootElement.GetProperty("streams").EnumerateArray().FirstOrDefault(x => x.GetProperty("codec_type").GetString() == "video");
            if (video.ValueKind == JsonValueKind.Undefined || !format.GetProperty("format_name").GetString()!.Contains("mp4", StringComparison.Ordinal))
                throw new ApiException(415, "invalid_video_type", "Tệp không phải video MP4.");
            if (!double.TryParse(format.GetProperty("duration").GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var duration) || !double.IsFinite(duration) || duration <= 0 || duration > 60)
                throw new ApiException(400, "invalid_video_duration", "Video phải có thời lượng tối đa 60 giây.");
            var width = video.GetProperty("width").GetInt32(); var height = video.GetProperty("height").GetInt32();
            if (width < 2 || height < 2) throw new ApiException(400, "invalid_video", "Kích thước video không hợp lệ.");
            return new VideoInfo(width, height, duration);
        }
    }
}
