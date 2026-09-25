using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Remnant2UnlockerApp.Services;

/// <summary>
/// Finds an item picture on the Fextralife wiki, with remnant2.wiki.gg as a second source, and
/// caches it next to the app. An image is only used when it can be tied to the item (infobox,
/// og:image or a file name / page title matching the item) — no image is better than a wrong one.
/// </summary>
public sealed class WikiImageService
{
    private const string FextralifeBaseUrl = "https://remnant2.wiki.fextralife.com";
    private const string WikiGgApiUrl = "https://remnant2.wiki.gg/api.php";

    // Fextralife serves pages to browser-like clients; wiki.gg (MediaWiki) drops connections from
    // generic browser user agents and asks API clients to identify themselves.
    private static readonly HttpClient Http = CreateHttpClient("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    private static readonly HttpClient WikiGgHttp = CreateHttpClient(
        $"Remnant2ItemSpawner/{typeof(WikiImageService).Assembly.GetName().Version} (item image lookup)");

    // Words the wiki writes in lower case inside page titles ("Ankh of Power", "Beads of the Valorous").
    private static readonly HashSet<string> MinorWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "by", "for", "from", "in", "into", "of", "on", "or", "the", "to", "with"
    };

    private static readonly Regex OgImagePattern = new(
        "<meta\\s+(?:property=[\"']og:image[\"']\\s+content=[\"']([^\"']*)[\"']|content=[\"']([^\"']*)[\"']\\s+property=[\"']og:image[\"'])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InfoboxImagePattern = new(
        "class=[\"'][^\"']*infobox_a_image_background[^\"']*[\"'][^>]*>(.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ImgTagPattern = new("<img\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SrcPattern = new("\\bsrc=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AltPattern = new("\\balt=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // items.json suffixes such as "Alien Device (Engineer*)" or "Prismatic Stone (Blue*)"
    private static readonly Regex MarkerSuffixPattern = new("\\s*\\([^)]*\\*\\)\\s*$", RegexOptions.Compiled);

    // Fextralife thumbnails: .../file/remnant2/thumb/f/f9/Name.png/200px-Name.png → .../file/remnant2/f/f9/Name.png
    private static readonly Regex ThumbPattern = new(
        "^(.*/file/remnant2)/thumb/(.+?\\.(?:png|jpe?g|webp|gif))/[^/]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "v2": earlier versions could cache the wiki logo or a menu icon for pages without og:image,
    // so the old cache folder is discarded once instead of showing those wrong images forever.
    private readonly string _cacheDir =
        Path.Combine(AppContext.BaseDirectory, "Cache", "Images", "v2");

    public WikiImageService()
    {
        Directory.CreateDirectory(_cacheDir);
        DeleteLegacyCache();
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
            var fextralife = await FindFextralifeImageAsync(itemName);

            // A Fextralife image whose file name matches the item is taken as is. Otherwise the page
            // may show a different item's picture (e.g. "Dull Steel Ring" uses the Bright Steel Ring
            // image), so wiki.gg's image for the exact title wins, with Fextralife's as the fallback.
            var candidates = new List<string>();

            if (fextralife is { NameMatches: true })
                candidates.Add(fextralife.Url);

            if (candidates.Count == 0 && await FindWikiGgImageAsync(itemName) is { } wikiGgUrl)
                candidates.Add(wikiGgUrl);

            if (fextralife is { NameMatches: false })
                candidates.Add(fextralife.Url);

            foreach (var imageUrl in candidates)
            {
                if (await TryDownloadAsync(imageUrl, localPath))
                    return localPath;
            }

            Debug.WriteLine($"[WikiImage] No image found for {itemName}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WikiImage] Failed for {itemName}: {ex.Message}");
            return null;
        }
    }

    private static HttpClient CreateHttpClient(string userAgent)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    private static HttpClient ClientFor(string url) =>
        url.Contains("wiki.gg", StringComparison.OrdinalIgnoreCase) ? WikiGgHttp : Http;

    /// <summary>GET with up to two retries on timeouts, 429 and 5xx; other results return at once.</summary>
    private static async Task<HttpResponseMessage> GetWithRetryAsync(string url)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await ClientFor(url).GetAsync(url);

                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (!transient || attempt == 3)
                    return response;

                response.Dispose();
            }
            catch (Exception ex) when (attempt < 3 && ex is HttpRequestException or TaskCanceledException)
            {
                Debug.WriteLine($"[WikiImage] Retry {attempt} for {url}: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }
    }

    private static async Task<bool> TryDownloadAsync(string imageUrl, string localPath)
    {
        try
        {
            using var response = await GetWithRetryAsync(NormalizeImageUrl(imageUrl));
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();

            if (bytes.Length < 500)
            {
                Debug.WriteLine($"[WikiImage] Image too small: {bytes.Length} bytes ({imageUrl})");
                return false;
            }

            await File.WriteAllBytesAsync(localPath, bytes);
            Debug.WriteLine($"[WikiImage] Saved: {localPath} ({imageUrl})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WikiImage] Download failed for {imageUrl}: {ex.Message}");
            return false;
        }
    }

    // ── Fextralife ──

    private sealed record FoundImage(string Url, bool NameMatches);

    private static async Task<FoundImage?> FindFextralifeImageAsync(string itemName)
    {
        var key = NameKey(BaseName(itemName));

        foreach (var pageName in GetFextralifePageNames(itemName))
        {
            var html = await TryGetFextralifePageAsync(pageName);

            if (html == null)
                continue;

            var found = ExtractFextralifeImage(html, key);

            Debug.WriteLine($"[WikiImage] Fextralife {pageName}: {found?.Url ?? "no image"}");

            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Wiki page titles are case-sensitive ("Ankh Of Power" in items.json, "Ankh of Power" on the
    /// wiki) and never carry the items.json markers or quotes ("AS-10 Bulldog").
    /// </summary>
    private static IEnumerable<string> GetFextralifePageNames(string itemName)
    {
        var baseName = BaseName(itemName).Replace("\"", "");
        baseName = Regex.Replace(baseName, "\\s+", " ").Trim();

        var wikiCased = string.Join(" ", baseName.Split(' ').Select((word, index) =>
            index > 0 && MinorWords.Contains(word) ? word.ToLowerInvariant() : word));

        return new[] { baseName, wikiCased }.Distinct(StringComparer.Ordinal);
    }

    private static async Task<string?> TryGetFextralifePageAsync(string pageName)
    {
        var pageUrl = $"{FextralifeBaseUrl}/{Uri.EscapeDataString(pageName).Replace("%20", "+")}";

        using var response = await GetWithRetryAsync(pageUrl);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>The infobox picture, else og:image, else a content image named after the item.</summary>
    private static FoundImage? ExtractFextralifeImage(string html, string key)
    {
        var infobox = InfoboxImagePattern.Match(html);
        if (infobox.Success && FirstWikiImage(infobox.Groups[1].Value) is { } infoboxUrl)
            return new FoundImage(ToFullSizeUrl(infoboxUrl), Matches(infoboxUrl, null, key));

        var og = OgImagePattern.Match(html);
        if (og.Success)
        {
            var ogUrl = og.Groups[1].Value.Length > 0 ? og.Groups[1].Value : og.Groups[2].Value;

            if (!string.IsNullOrWhiteSpace(ogUrl) && !ogUrl.Contains("/logo", StringComparison.OrdinalIgnoreCase))
                return new FoundImage(ogUrl, Matches(ogUrl, null, key));
        }

        if (key.Length == 0)
            return null;

        foreach (Match tag in ImgTagPattern.Matches(html))
        {
            var src = SrcPattern.Match(tag.Value);
            if (!src.Success || !IsWikiFile(src.Groups[1].Value))
                continue;

            var alt = AltPattern.Match(tag.Value);
            if (Matches(src.Groups[1].Value, alt.Success ? alt.Groups[1].Value : null, key))
                return new FoundImage(ToFullSizeUrl(src.Groups[1].Value), true);
        }

        return null;
    }

    private static string? FirstWikiImage(string html)
    {
        foreach (Match tag in ImgTagPattern.Matches(html))
        {
            var src = SrcPattern.Match(tag.Value);
            if (src.Success && IsWikiFile(src.Groups[1].Value))
                return src.Groups[1].Value;
        }

        return null;
    }

    private static bool IsWikiFile(string url) =>
        url.Contains("/file/remnant2/", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string url, string? alt, string key) =>
        key.Length > 0 && (NameKey(FileNameOf(url)).Contains(key) || (alt != null && NameKey(alt).Contains(key)));

    private static string FileNameOf(string url) =>
        Uri.UnescapeDataString(url.Split('?')[0].Split('/')[^1]);

    private static string ToFullSizeUrl(string url)
    {
        var match = ThumbPattern.Match(url.Split('?')[0]);
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : url;
    }

    // ── remnant2.wiki.gg (MediaWiki API) ──

    /// <summary>
    /// Page image for the item's title on wiki.gg: the exact name, the name without items.json
    /// markers, the Prism Fragment page ("Base Armor (fragment)"), then a search whose result
    /// title must equal the item name.
    /// </summary>
    private static async Task<string?> FindWikiGgImageAsync(string itemName)
    {
        var baseName = BaseName(itemName);

        // "Prismatic Stone (Blue*)" → "Prismatic Stone (blue)" names a variant page on wiki.gg
        var marker = MarkerSuffixPattern.Match(itemName.Trim()).Value.Trim().Trim('(', ')').TrimEnd('*');
        var variant = marker.Length > 0 ? $"{baseName} ({marker.ToLowerInvariant()})" : baseName;

        var titles = new[] { itemName.Trim(), variant, baseName, $"{baseName} (fragment)" }
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var byTitle = await QueryWikiGgAsync(
            $"titles={Uri.EscapeDataString(string.Join("|", titles))}");

        foreach (var title in titles)
        {
            if (byTitle.TryGetValue(title, out var url))
                return url;
        }

        var key = NameKey(baseName);
        var searched = await QueryWikiGgAsync(
            $"generator=search&gsrlimit=5&gsrsearch={Uri.EscapeDataString(baseName)}");

        return searched
            .Where(pair => NameKey(Regex.Replace(pair.Key, "\\s*\\(fragment\\)$", "")) == key)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    /// <summary>Requested title (after normalisation/redirects are mapped back) → thumbnail URL.</summary>
    private static async Task<Dictionary<string, string>> QueryWikiGgAsync(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var url = $"{WikiGgApiUrl}?action=query&format=json&redirects=1&prop=pageimages&piprop=thumbnail&pithumbsize=256&{query}";

        using var response = await GetWithRetryAsync(url);
        if (!response.IsSuccessStatusCode)
            return result;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!document.RootElement.TryGetProperty("query", out var root))
            return result;

        var imageByPage = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("title", out var title) &&
                    page.Value.TryGetProperty("thumbnail", out var thumbnail) &&
                    thumbnail.TryGetProperty("source", out var source))
                {
                    imageByPage[title.GetString()!] = source.GetString()!;
                }
            }
        }

        foreach (var (title, image) in imageByPage)
            result[title] = image;

        // Map the titles as requested ("Alpha/Omega", lower-case first letter, ...) onto the page found.
        foreach (var listName in new[] { "normalized", "redirects" })
        {
            if (!root.TryGetProperty(listName, out var list))
                continue;

            foreach (var entry in list.EnumerateArray())
            {
                var from = entry.GetProperty("from").GetString()!;
                var to = entry.GetProperty("to").GetString()!;

                if (result.TryGetValue(to, out var image))
                    result.TryAdd(from, image);
            }
        }

        return result;
    }

    // ── Shared ──

    private static string BaseName(string itemName) =>
        MarkerSuffixPattern.Replace(itemName.Trim(), "");

    // Letters and digits only, lower case: "Brewmaster's Cork" / "Brewmasters_cork_amulets..." → "brewmasterscork..."
    private static string NameKey(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string NormalizeImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return imageUrl;

        imageUrl = WebUtility.HtmlDecode(imageUrl.Trim());

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
            return FextralifeBaseUrl + imageUrl;

        return imageUrl;
    }

    private void DeleteLegacyCache()
    {
        var legacyDir = Path.GetDirectoryName(_cacheDir)!;

        try
        {
            foreach (var file in Directory.EnumerateFiles(legacyDir, "*.png"))
                File.Delete(file);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WikiImage] Could not clear legacy cache: {ex.Message}");
        }
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
