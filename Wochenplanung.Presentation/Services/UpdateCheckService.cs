using System.Net.Http;
using System.Text.Json;
using System.IO;

namespace Wochenplanung.Presentation.Services;

public sealed class UpdateCheckService
{
    private readonly string _settingsPath;
    private readonly HttpClient _httpClient = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Meisterfenster/2.0");
        return client;
    }

    public UpdateCheckService()
    {
        _settingsPath = AppPaths.Combine("update_settings.json");
    }

    public UpdateSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new UpdateSettings();
                SaveSettings(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_settingsPath);
            var parsed = JsonSerializer.Deserialize<UpdateSettings>(json);
            return parsed ?? new UpdateSettings();
        }
        catch
        {
            return new UpdateSettings();
        }
    }

    public void SaveSettings(UpdateSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion)
    {
        var settings = LoadSettings();
        if (!settings.Enabled)
            return UpdateCheckResult.Disabled();

        if (string.IsNullOrWhiteSpace(settings.ReleaseApiUrl))
            return UpdateCheckResult.NotConfigured();

        try
        {
            var json = await _httpClient.GetStringAsync(settings.ReleaseApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagName) ? tagName.GetString() : null;
            var htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
                return UpdateCheckResult.Failed("Release-Tag konnte nicht gelesen werden.");

            var versionText = tag.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(versionText, out var latest))
                return UpdateCheckResult.Failed($"Ungültige Versionsangabe: {tag}");

            if (latest > currentVersion)
                return UpdateCheckResult.UpdateAvailable(latest, htmlUrl ?? string.Empty);

            return UpdateCheckResult.UpToDate(latest);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
    public string ReleaseApiUrl { get; set; } = "https://api.github.com/repos/fankifiction/Meisterfenster/releases/latest";
}

public sealed class UpdateCheckResult
{
    public bool IsEnabled { get; private init; }
    public bool IsConfigured { get; private init; }
    public bool HasUpdate { get; private init; }
    public Version? LatestVersion { get; private init; }
    public string Url { get; private init; } = string.Empty;
    public string Message { get; private init; } = string.Empty;

    public static UpdateCheckResult Disabled() => new() { IsEnabled = false, IsConfigured = true, Message = "Auto-Update ist deaktiviert." };
    public static UpdateCheckResult NotConfigured() => new() { IsEnabled = true, IsConfigured = false, Message = "ReleaseApiUrl fehlt in update_settings.json." };
    public static UpdateCheckResult UpdateAvailable(Version latest, string url) => new() { IsEnabled = true, IsConfigured = true, HasUpdate = true, LatestVersion = latest, Url = url, Message = $"Neue Version verfügbar: {latest}" };
    public static UpdateCheckResult UpToDate(Version latest) => new() { IsEnabled = true, IsConfigured = true, HasUpdate = false, LatestVersion = latest, Message = $"Aktuell: {latest}" };
    public static UpdateCheckResult Failed(string message) => new() { IsEnabled = true, IsConfigured = true, Message = $"Update-Prüfung fehlgeschlagen: {message}" };
}
