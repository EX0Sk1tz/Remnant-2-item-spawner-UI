using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Remnant2UnlockerApp.Services;

public sealed class WikiImageService
{
    private static readonly HttpClient Http = new();

    private readonly string _cacheDir =
        Path.Combine(AppContext.BaseDirectory, "Cache", "Images");

    public WikiImageService()
    {
        Directory.CreateDirectory(_cacheDir);

        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public async Task<string?> GetImageAsync(string itemName)
    {
        var localPath = GetLocalPath(itemName);

        if (File.Exists(localPath))
        {
            Debug.WriteLine($"[WikiImage] Cache hit: {itemName}");
            return localPath;
        }

        try
        {
            var pageUrl = BuildFextralifeUrl(itemName);

            Debug.WriteLine($"[WikiImage] Loading page: {pageUrl}");

            var html = await Http.GetStringAsync(pageUrl);

            var imageUrl = ExtractImageUrl(html);

            Debug.WriteLine($"[WikiImage] Image URL for {itemName}: {imageUrl}");

            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            imageUrl = NormalizeImageUrl(imageUrl);

            var bytes = await Http.GetByteArrayAsync(imageUrl);

            if (bytes.Length < 500)
            {
                Debug.WriteLine($"[WikiImage] Image too small: {bytes.Length} bytes");
                return null;
            }

            await File.WriteAllBytesAsync(localPath, bytes);

            Debug.WriteLine($"[WikiImage] Saved: {localPath}");

            return localPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WikiImage] Failed for {itemName}: {ex.Message}");
            return null;
        }
    }

    private static string BuildFextralifeUrl(string itemName)
    {
        var pageName = itemName.Trim().Replace(" ", "+");
        return $"https://remnant2.wiki.fextralife.com/{pageName}";
    }

    private static string? ExtractImageUrl(string html)
    {
        var patterns = new[]
        {
            "<meta\\s+property=[\"']og:image[\"']\\s+content=[\"']([^\"']+)[\"']",
            "<meta\\s+content=[\"']([^\"']+)[\"']\\s+property=[\"']og:image[\"']",
            "<img[^>]+class=[\"'][^\"']*wiki_link[^\"']*[\"'][^>]+src=[\"']([^\"']+)[\"']",
            "<img[^>]+src=[\"']([^\"']+)[\"'][^>]+class=[\"'][^\"']*wiki_link[^\"']*[\"']",
            "<img[^>]+src=[\"']([^\"']+\\.png[^\"']*)[\"']",
            "<img[^>]+src=[\"']([^\"']+\\.webp[^\"']*)[\"']",
            "<img[^>]+src=[\"']([^\"']+\\.jpg[^\"']*)[\"']"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    private static string NormalizeImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return imageUrl;

        imageUrl = imageUrl.Trim();

        if (imageUrl.StartsWith("https:////", StringComparison.OrdinalIgnoreCase))
            return "https://" + imageUrl["https:////".Length..];

        if (imageUrl.StartsWith("https:///", StringComparison.OrdinalIgnoreCase))
            return "https://" + imageUrl["https:///".Length..];

        if (imageUrl.StartsWith("http:////", StringComparison.OrdinalIgnoreCase))
            return "http://" + imageUrl["http:////".Length..];

        if (imageUrl.StartsWith("http:///", StringComparison.OrdinalIgnoreCase))
            return "http://" + imageUrl["http:///".Length..];

        if (imageUrl.StartsWith("//"))
            return "https:" + imageUrl;

        if (imageUrl.StartsWith("/"))
            return "https://remnant2.wiki.fextralife.com" + imageUrl;

        return imageUrl;
    }

    private string GetLocalPath(string itemName)
    {
        return Path.Combine(_cacheDir, SafeFileName(itemName) + ".png");
    }

    private static string SafeFileName(string value)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value;
    }
}