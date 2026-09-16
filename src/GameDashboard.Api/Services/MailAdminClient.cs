using System.Text;
using System.Text.Json;

namespace GameDashboard.Api.Services;

/// <summary>
/// 对接 LongLongGames Mail Admin API（X-Admin-Api-Key）。AOT 友好：不用 Dictionary/匿名类型反射序列化。
/// </summary>
public sealed class MailAdminClient(IHttpClientFactory http, IConfiguration config, ILogger<MailAdminClient> log)
{
    string BaseUrl => (config["Mail:BaseUrl"] ?? "http://localhost:12081").TrimEnd('/');
    string AdminKey => config["Mail:AdminApiKey"] ?? "";

    HttpClient Create()
    {
        var c = http.CreateClient();
        c.BaseAddress = new Uri(BaseUrl + "/");
        c.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(AdminKey) && AdminKey != "replace-me")
            c.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Api-Key", AdminKey);
        return c;
    }

    public async Task<(bool ok, string msg, Guid? mailId)> SendAsync(
        string projectId,
        string title,
        string content,
        IReadOnlyList<string>? targetUserIds,
        bool broadcast,
        IReadOnlyList<MailAttachmentDto>? attachments,
        string? senderName,
        DateTimeOffset? expireAt)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl) || BaseUrl == "mock")
            return (false, "未配置 Mail:BaseUrl", null);
        if (string.IsNullOrWhiteSpace(AdminKey) || AdminKey == "replace-me")
            return (false, "未配置 Mail:AdminApiKey", null);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return (false, "标题与内容必填", null);
        if (!broadcast && (targetUserIds is null || targetUserIds.Count == 0))
            return (false, "请填写收件人，或勾选全服发放", null);

        var attList = new List<MailAttachmentDto>();
        if (attachments != null)
        {
            foreach (var a in attachments)
            {
                if (string.IsNullOrWhiteSpace(a.ItemId))
                    return (false, "奖励道具 itemId 不能为空", null);
                if (a.Count < 1)
                    return (false, $"道具 {a.ItemId} 数量必须 ≥ 1", null);
                attList.Add(new MailAttachmentDto(a.ItemId.Trim(), a.Count));
            }
        }

        var targets = broadcast
            ? Array.Empty<string>()
            : targetUserIds!
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        // 手写 JSON，避免 AOT 下 Dictionary/object 反射序列化崩溃
        var json = BuildCreateMailJson(
            projectId,
            title.Trim(),
            content.Trim(),
            broadcast,
            string.IsNullOrWhiteSpace(senderName) ? "GM" : senderName.Trim(),
            targets,
            attList,
            expireAt);

        try
        {
            var c = Create();
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await c.PostAsync("api/v1/admin/mails", body);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Mail send failed {Code}: {Body}", (int)resp.StatusCode, text);
                return (false, string.IsNullOrWhiteSpace(text) ? $"HTTP {(int)resp.StatusCode}" : text, null);
            }

            Guid? id = null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                    Guid.TryParse(idEl.GetString(), out var g))
                    id = g;
            }
            catch { /* ignore */ }

            var who = broadcast ? "全服" : $"{targets.Length} 人";
            return (true, $"已发送（{who}）" + (id.HasValue ? $" mailId={id}" : ""), id);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail send exception");
            return (false, ex.Message, null);
        }
    }

    static string BuildCreateMailJson(
        string projectId,
        string title,
        string content,
        bool broadcast,
        string senderName,
        string[] targets,
        List<MailAttachmentDto> attachments,
        DateTimeOffset? expireAt)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("projectId", projectId);
            w.WriteString("title", title);
            w.WriteString("content", content);
            w.WriteBoolean("broadcast", broadcast);
            w.WriteString("senderName", senderName);

            w.WritePropertyName("attachments");
            w.WriteStartArray();
            foreach (var a in attachments)
            {
                w.WriteStartObject();
                w.WriteString("itemId", a.ItemId);
                w.WriteNumber("count", a.Count);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (!broadcast)
            {
                w.WritePropertyName("targetUserIds");
                w.WriteStartArray();
                foreach (var t in targets)
                    w.WriteStringValue(t);
                w.WriteEndArray();
            }

            if (expireAt.HasValue)
                w.WriteString("expireAt", expireAt.Value.UtcDateTime.ToString("o"));

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public async Task<(bool ok, string msg, List<MailAdminSummaryDto> items, int total)> ListAsync(
        string? projectId, int page = 1, int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl) || BaseUrl == "mock")
            return (false, "未配置 Mail:BaseUrl", [], 0);

        try
        {
            var c = Create();
            var qs = $"page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(projectId))
                qs += $"&projectId={Uri.EscapeDataString(projectId)}";
            var resp = await c.GetAsync($"api/v1/admin/mails?{qs}");
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return (false, text, [], 0);

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var list = new List<MailAdminSummaryDto>();
            if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in arr.EnumerateArray())
                {
                    list.Add(new MailAdminSummaryDto(
                        it.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        it.TryGetProperty("projectId", out var p) ? p.GetString() ?? "" : "",
                        it.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        it.TryGetProperty("targetCount", out var tc) && tc.ValueKind == JsonValueKind.Number ? tc.GetInt32() : 0,
                        it.TryGetProperty("isBroadcast", out var b) && b.ValueKind == JsonValueKind.True,
                        it.TryGetProperty("senderName", out var sn) ? sn.GetString() : null,
                        it.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null
                    ));
                }
            }
            var total = root.TryGetProperty("total", out var tot) && tot.ValueKind == JsonValueKind.Number
                ? tot.GetInt32() : list.Count;
            return (true, "OK", list, total);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, [], 0);
        }
    }

    public List<ItemCatalogEntry> GetItemCatalog(string gameId)
    {
        var section = config.GetSection($"Mail:ItemCatalog:{gameId}");
        if (!section.Exists())
            section = config.GetSection("Mail:ItemCatalog:match3");

        var list = new List<ItemCatalogEntry>();
        foreach (var child in section.GetChildren())
        {
            var id = child["Id"] ?? child["itemId"] ?? child.Key;
            var name = child["Name"] ?? child["name"] ?? id;
            var icon = child["Icon"] ?? child["icon"];
            if (string.IsNullOrWhiteSpace(id)) continue;
            list.Add(new ItemCatalogEntry(id.Trim(), name!.Trim(), icon));
        }

        if (list.Count == 0)
        {
            list.AddRange([
                new("1", "锤子", "item_hammer"),
                new("2", "横消", "item_rocket_h"),
                new("3", "竖消", "item_rocket_v"),
                new("4", "九宫格炸弹", "item_flower_5col"),
                new("5", "洗牌", "item_score_20"),
                new("6", "加五步", "item_steps_3"),
                new("7", "金币", "item_gold"),
                new("8", "体力", "item_energy_10"),
                new("9", "钻石", "item_diamond"),
            ]);
        }
        return list;
    }

    public bool IsKnownItem(string gameId, string itemId)
    {
        var cat = GetItemCatalog(gameId);
        return cat.Any(x => string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase));
    }
}

public record MailAttachmentDto(string ItemId, int Count);
public record ItemCatalogEntry(string Id, string Name, string? Icon);
public record MailAdminSummaryDto(
    string Id, string ProjectId, string Title, int TargetCount,
    bool IsBroadcast, string? SenderName, string? CreatedAt);
