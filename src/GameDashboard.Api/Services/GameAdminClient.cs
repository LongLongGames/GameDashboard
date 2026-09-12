using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GameDashboard.Api.Services;

namespace GameDashboard.Api.Services;

public sealed class GameAdminClient(GameRepo games, IHttpClientFactory http, IConfiguration config, ILogger<GameAdminClient> log)
{
    public async Task<HealthResult> HealthAsync(string gameId)
    {
        var ep = await games.GetAsync(gameId);
        if (ep is null) return new(false, null, "未配置或未启用");
        try
        {
            var c = Create(ep.BaseUrl);
            var resp = await c.GetAsync("/health");
            var body = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? new(true, body, "OK") : new(false, body, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) { return new(false, null, ex.Message); }
    }

    public async Task<List<LeaderboardEntry>> LeaderboardTopAsync(string gameId, int limit = 20)
    {
        var ep = await games.GetAsync(gameId);
        if (ep is null) return [];
        try
        {
            var c = Create(ep.BaseUrl);
            var resp = await c.GetAsync($"/api/v1/leaderboard/top?game_id={Uri.EscapeDataString(gameId)}&limit={limit}");
            if (!resp.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var arr = root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("entries", out var e) ? e
                : root.TryGetProperty("items", out var i) ? i : default;
            if (arr.ValueKind != JsonValueKind.Array) return [];
            var list = new List<LeaderboardEntry>();
            var rank = 1;
            foreach (var item in arr.EnumerateArray())
            {
                var id = Prop(item, "mp_account_id") ?? Prop(item, "accountId");
                if (id is null) continue;
                var nick = Prop(item, "nickname");
                long score = item.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                list.Add(new(id, nick, score, rank++));
            }
            return list;
        }
        catch (Exception ex) { log.LogWarning(ex, "leaderboard"); return []; }
    }

    public async Task<string?> VersionCheckAsync(string gameId)
    {
        var ep = await games.GetAsync(gameId);
        if (ep is null) return null;
        try
        {
            var c = Create(ep.BaseUrl);
            var url = $"/api/v1/game/version-check?game_id={gameId}&channel=official&platform=android&region=cn&client_version_code=10000&resource_version=0";
            var resp = await c.GetAsync(url);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return ex.Message; }
    }

    public async Task<List<PlayerInfo>> SearchPlayersAsync(string gameId, string? query)
    {
        var top = await LeaderboardTopAsync(gameId, 50);
        var q = query?.Trim();
        IEnumerable<LeaderboardEntry> src = top;
        if (!string.IsNullOrEmpty(q))
            src = top.Where(t => t.MpAccountId.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || (t.Nickname?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        return src.Select(t => new PlayerInfo(t.MpAccountId, t.Nickname ?? t.MpAccountId, false,
            new Dictionary<string, long> { ["score"] = t.Score }, "from leaderboard")).ToList();
    }

    public async Task<PlayerInfo?> GetPlayerViaJwtAsync(string gameId)
    {
        var jwt = config["Games:Match3:DebugJwt"];
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var ep = await games.GetAsync(gameId);
        if (ep is null) return null;
        try
        {
            var c = Create(ep.BaseUrl);
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.Trim());
            var profile = await c.GetAsync($"/api/v1/user/profile?game_id={gameId}");
            var state = await c.GetAsync($"/api/v1/user/state?game_id={gameId}&map_id=1");
            string id = "(jwt)", nick = "(jwt)";
            var cur = new Dictionary<string, long>();
            if (profile.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("mp_account_id", out var a)) id = a.GetString() ?? id;
                if (doc.RootElement.TryGetProperty("nickname", out var n)) nick = n.GetString() ?? nick;
            }
            if (state.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await state.Content.ReadAsStringAsync());
                foreach (var key in new[] { "gold", "energy", "energy_max", "unlocked_map" })
                    if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number)
                        cur[key] = v.GetInt64();
            }
            return new PlayerInfo(id, nick, false, cur, null);
        }
        catch (Exception ex) { log.LogWarning(ex, "jwt player"); return null; }
    }

    public async Task<(bool ok, string msg)> EnergyRefillAsync(string gameId)
    {
        var jwt = config["Games:Match3:DebugJwt"];
        if (string.IsNullOrWhiteSpace(jwt)) return (false, "未配置 Games:Match3:DebugJwt");
        var ep = await games.GetAsync(gameId);
        if (ep is null) return (false, "游戏未启用");
        try
        {
            var c = Create(ep.BaseUrl);
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.Trim());
            var json = "{\"game_id\":\"" + gameId + "\"}";
            using var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var resp = await c.PostAsync("/api/v1/user/energy/cheat-refill", content);
            var body = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? (true, "体力已回满") : (false, body);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> SendMailAsync(string gameId, string mpAccountId, string title, string content, string? items)
    {
        var ep = await games.GetAsync(gameId);
        if (ep is null) return (false, "游戏未启用");
        var key = await games.GetAdminKeyAsync(gameId) ?? "";
        try
        {
            var c = Create(ep.BaseUrl);
            c.DefaultRequestHeaders.TryAddWithoutValidation("X-Dashboard-Key", key);
            var json = "{\"mp_account_id\":\"" + Esc(mpAccountId) + "\",\"title\":\"" + Esc(title) + "\",\"content\":\"" + Esc(content) + "\"}";
            using var body = new StringContent(json, Encoding.UTF8);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var resp = await c.PostAsync("/admin/v1/mail/send", body);
            if ((int)resp.StatusCode == 404)
                return (false, "match3 尚未暴露 /admin/v1/mail/send");
            var text = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? (true, "已发送") : (false, text);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>
    /// 调 BugReport GET /api/v1/reports（projectId = 游戏 game_id）。
    /// </summary>
    public async Task<ReportListResponse> ListReportsAsync(string? projectId, string? status, int page = 1, int pageSize = 50)
    {
        var baseUrl = config["BugReport:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl) || baseUrl == "mock")
        {
            return new ReportListResponse(
                [
                    new("00000000-0000-0000-0000-000000000001", projectId ?? "match3", "Error",
                        "（mock）未配置 BugReport:BaseUrl", "Open", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1), "0.0.0")
                ], 1, pageSize, 1);
        }

        try
        {
            var c = http.CreateClient();
            c.Timeout = TimeSpan.FromSeconds(15);
            var adminKey = config["BugReport:AdminKey"];
            if (!string.IsNullOrEmpty(adminKey) && adminKey != "replace-me")
                c.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Api-Key", adminKey);

            var qs = new List<string> { $"page={page}", $"pageSize={pageSize}" };
            if (!string.IsNullOrWhiteSpace(projectId))
                qs.Add($"projectId={Uri.EscapeDataString(projectId)}");
            if (!string.IsNullOrWhiteSpace(status))
                qs.Add($"status={Uri.EscapeDataString(status)}");

            var url = $"{baseUrl.TrimEnd('/')}/api/v1/reports?{string.Join("&", qs)}";
            var resp = await c.GetAsync(url);
            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("BugReport list failed {Status}: {Body}", (int)resp.StatusCode, raw);
                return new ReportListResponse([], page, pageSize, 0);
            }
            return JsonSerializer.Deserialize(raw, AppJsonContext.Default.ReportListResponse)
                   ?? new ReportListResponse([], page, pageSize, 0);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "BugReport list");
            return new ReportListResponse([], page, pageSize, 0);
        }
    }

    public async Task<(bool ok, string msg)> UpdateReportStatusAsync(string id, string status)
    {
        var baseUrl = config["BugReport:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl) || baseUrl == "mock")
            return (false, "BugReport 未配置");
        try
        {
            var c = http.CreateClient();
            var adminKey = config["BugReport:AdminKey"];
            if (!string.IsNullOrEmpty(adminKey) && adminKey != "replace-me")
                c.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Api-Key", adminKey);
            var json = "{\"status\":\"" + status + "\"}";
            using var body = new StringContent(json, Encoding.UTF8);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var resp = await c.PatchAsync($"{baseUrl.TrimEnd('/')}/api/v1/reports/{id}/status", body);
            var text = await resp.Content.ReadAsStringAsync();
            return resp.IsSuccessStatusCode ? (true, "已更新") : (false, text);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    static string? Prop(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetString() : null;

    HttpClient Create(string baseUrl)
    {
        var c = http.CreateClient();
        c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(15);
        return c;
    }
}
