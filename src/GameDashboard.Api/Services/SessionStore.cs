using System.Collections.Concurrent;

namespace GameDashboard.Api.Services;

public sealed class SessionInfo
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string[] Roles { get; init; }
    public required bool MustChangePassword { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>进程内会话（单实例 Dashboard 足够；多副本可换 Redis）。</summary>
public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, SessionInfo> _map = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    public string Create(SessionInfo info)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        info.ExpiresAt = DateTime.UtcNow.Add(Ttl);
        _map[token] = info;
        return token;
    }

    public SessionInfo? Get(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        if (!_map.TryGetValue(token, out var s)) return null;
        if (s.ExpiresAt < DateTime.UtcNow)
        {
            _map.TryRemove(token, out _);
            return null;
        }
        s.ExpiresAt = DateTime.UtcNow.Add(Ttl);
        return s;
    }

    public void Remove(string? token)
    {
        if (!string.IsNullOrEmpty(token)) _map.TryRemove(token, out _);
    }

    public void Update(string token, SessionInfo info) => _map[token] = info;
}
