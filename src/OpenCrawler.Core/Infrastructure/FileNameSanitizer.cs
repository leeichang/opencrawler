using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenCrawler.Core.Infrastructure;

public static class FileNameSanitizer
{
    private static readonly char[] Invalid =
        Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            .Distinct()
            .ToArray();

    public static string Sanitize(string input, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(input)) return "untitled";
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (Array.IndexOf(Invalid, ch) >= 0 || char.IsControl(ch)) sb.Append('-');
            else sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim(' ', '.', '-');
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength];
        return string.IsNullOrWhiteSpace(cleaned) ? "untitled" : cleaned;
    }

    public static string GenerateArticleFolderName(string? title)
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        var slug = Sanitize(title ?? "untitled", 40);
        return $"{ts}_{hash}_{slug}";
    }

    public static string GenerateAssetFileName(string url, string? contentType)
    {
        // 唯一性永遠以 URL 的 hash 為主,避免 WeChat 類網站的 /640 造成重名覆蓋。
        // 副檔名優先用 URL 路徑的副檔名,其次看 Content-Type,最後 fallback。
        string ext = "";
        try
        {
            var u = new Uri(url, UriKind.RelativeOrAbsolute);
            if (u.IsAbsoluteUri)
            {
                var name = Path.GetFileName(u.LocalPath);
                var e = Path.GetExtension(name);
                if (!string.IsNullOrEmpty(e) && e.Length <= 6) ext = e.ToLowerInvariant();
            }
        }
        catch { }

        if (string.IsNullOrEmpty(ext))
        {
            ext = contentType switch
            {
                var c when c?.Contains("jpeg") == true => ".jpg",
                var c when c?.Contains("png") == true => ".png",
                var c when c?.Contains("gif") == true => ".gif",
                var c when c?.Contains("webp") == true => ".webp",
                var c when c?.Contains("svg") == true => ".svg",
                var c when c?.Contains("css") == true => ".css",
                _ => ".bin"
            };
        }

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..12].ToLowerInvariant();
        return $"asset_{hash}{ext}";
    }
}
