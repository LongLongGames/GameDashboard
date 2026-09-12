using GameDashboard.Api.Infrastructure.Db;
using Npgsql;

namespace GameDashboard.Api.Services;

public sealed class AuditRepo(Db db)
{
    public async Task LogAsync(int userId, string username, string action,
        string? gameId = null, string? target = null, string? detail = null,
        bool success = true, string? error = null, string? ip = null)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO audit_logs (user_id, username, action, game_id, target, detail, success, error_message, ip_address)
              VALUES (@uid,@u,@a,@g,@t,@d,@s,@e,@ip)", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("a", action);
        cmd.Parameters.AddWithValue("g", (object?)gameId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("t", (object?)target ?? DBNull.Value);
        cmd.Parameters.AddWithValue("d", (object?)detail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("s", success);
        cmd.Parameters.AddWithValue("e", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ip", (object?)ip ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditRow>> RecentAsync(int take = 200)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, username, action, game_id, target, detail, success, created_at
              FROM audit_logs ORDER BY created_at DESC LIMIT @n", conn);
        cmd.Parameters.AddWithValue("n", take);
        var list = new List<AuditRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AuditRow(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetBoolean(6), r.GetDateTime(7)));
        }
        return list;
    }
}
