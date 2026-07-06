using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Remnant2UnlockerApp.Services;

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string CurrentVersion, string? LatestVersion, string? ReleaseUrl, string? DownloadUrl);

public static class UpdateCheckService
{
    private const string ApiUrl = "https://api.github.com/repos/EX0Sk1tz/Remnant-2-item-spawner-UI/releases/latest";

    private static readonly Lazy<HttpClient> LazyClient = new(CreateClient);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Remnant2UnlockerApp", GetCurrentVersion()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    public static string GetCurrentVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string? GetZipAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

            if (name != null &&
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("browser_download_url", out var urlProp))
            {
                return urlProp.GetString();
            }
        }

        return null;
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        var current = GetCurrentVersion();

        try
        {
            using var response = await LazyClient.Value.GetAsync(ApiUrl);

            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(false, current, null, null, null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var tagName = doc.RootElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            var releaseUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            var downloadUrl = GetZipAssetUrl(doc.RootElement);

            var latestVersionText = tagName.TrimStart('v', 'V');

            if (!Version.TryParse(latestVersionText, out var latestVersion) || !Version.TryParse(current, out var currentVersion))
                return new UpdateCheckResult(false, current, tagName, releaseUrl, downloadUrl);

            var isNewer = latestVersion > currentVersion;

            return new UpdateCheckResult(isNewer, current, tagName, releaseUrl, downloadUrl);
        }
        catch (Exception ex)
        {
            AppLogService.Warn("Update check failed", ex);
            return new UpdateCheckResult(false, current, null, null, null);
        }
    }
}
