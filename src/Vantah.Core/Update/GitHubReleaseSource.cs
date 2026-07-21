using System.Text.Json;

namespace Vantah.Core.Update;

/// <summary>
/// Последний релиз Vantah с GitHub API. Единственное место в приложении, ходящее в сеть ради
/// обновлений. Эндпоинт /releases/latest сам исключает черновики и предрелизы.
/// </summary>
public sealed class GitHubReleaseSource : IAppReleaseSource
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/bolikcraft/vantah/releases/latest";

    private const string FallbackReleaseUrl =
        "https://github.com/bolikcraft/vantah/releases/latest";

    private readonly HttpClient _http;

    /// <param name="appVersion">Версия Vantah для User-Agent.</param>
    /// <param name="handler">Подменяется в тестах; в приложении — null (обычный HTTP).</param>
    public GitHubReleaseSource(string appVersion, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(10);
        // Без User-Agent GitHub API отвечает 403.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Vantah/{appVersion}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(LatestReleaseUrl, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String)
                return null;

            var url = root.TryGetProperty("html_url", out var html) && html.ValueKind == JsonValueKind.String
                ? html.GetString()!
                : FallbackReleaseUrl;

            return new AppUpdateInfo(tag.GetString()!, url);
        }
        catch
        {
            // Сеть, таймаут, битый JSON — проверка просто не состоялась. Молча: сбой проверки
            // не повод показывать пользователю ошибку.
            return null;
        }
    }
}
