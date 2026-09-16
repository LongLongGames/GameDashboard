using System.Collections.Concurrent;

namespace GameDashboard.Api.Services;

/// <summary>
/// 从 config/{gameId}/Item.json 加载道具表（ExcelConfigCompiler 生成代码 + Runtime JsonReader）。
/// 不再使用 appsettings ItemCatalog。
/// </summary>
public sealed class GameCatalog(IConfiguration config, IHostEnvironment env, ILogger<GameCatalog> log)
{
    readonly ConcurrentDictionary<string, Item[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    string CatalogRoot
    {
        get
        {
            var configured = config["Mail:CatalogRoot"];
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            // 发布后与二进制同级 config/；开发时兼容仓库根 config/
            var beside = Path.Combine(AppContext.BaseDirectory, "config");
            if (Directory.Exists(beside)) return beside;
            var content = Path.Combine(env.ContentRootPath, "config");
            if (Directory.Exists(content)) return content;
            var up = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "config"));
            return up;
        }
    }

    public string ResolveItemJsonPath(string gameId)
    {
        var gid = string.IsNullOrWhiteSpace(gameId) ? "match3" : gameId.Trim();
        return Path.Combine(CatalogRoot, gid, "Item.json");
    }

    public Item[] GetItems(string gameId)
    {
        var gid = string.IsNullOrWhiteSpace(gameId) ? "match3" : gameId.Trim();
        return _cache.GetOrAdd(gid, LoadGame);
    }

    Item[] LoadGame(string gameId)
    {
        // 当前 Dashboard 仅嵌入 match3 生成代码（ItemTable）。其它 gameId 暂无同一表文件约定。
        var path = ResolveItemJsonPath(gameId);
        if (!File.Exists(path))
        {
            log.LogWarning("道具表不存在: {Path}", path);
            return Array.Empty<Item>();
        }

        try
        {
            // match3 使用生成的 ItemTable（namespace GameDashboard.Api）
            if (!string.Equals(gameId, "match3", StringComparison.OrdinalIgnoreCase))
            {
                log.LogWarning("gameId={GameId} 尚无独立生成代码，尝试用 match3 ItemTable 读 {Path}", gameId, path);
            }

            var items = ItemTable.LoadJsonFromFile(path);
            log.LogInformation("已加载道具表 {Path} count={Count}", path, items.Length);
            return items;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "加载道具表失败 {Path}", path);
            return Array.Empty<Item>();
        }
    }

    public bool IsKnownItem(string gameId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (!int.TryParse(itemId.Trim(), out var id)) return false;
        var items = GetItems(gameId);
        foreach (var it in items)
        {
            if (it.Id == id) return true;
        }
        return false;
    }

    public IReadOnlyList<ItemCatalogEntry> ToCatalogEntries(string gameId)
    {
        var items = GetItems(gameId);
        var list = new List<ItemCatalogEntry>(items.Length);
        foreach (var it in items)
        {
            list.Add(new ItemCatalogEntry(
                it.Id.ToString(),
                string.IsNullOrEmpty(it.Name) ? it.Id.ToString() : it.Name!,
                it.Icon));
        }
        return list;
    }

    public void Invalidate(string? gameId = null)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            _cache.Clear();
        else
            _cache.TryRemove(gameId.Trim(), out _);
    }
}
