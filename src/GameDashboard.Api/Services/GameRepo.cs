using GameDashboard.Api.Infrastructure.Db;
using Npgsql;

namespace GameDashboard.Api.Services;

public sealed class GameRepo(Db db)
{
    public async Task<List<GameEndpointRow>> ListEnabledAsync()
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, game_id, display_name, base_url, enabled FROM game_endpoints WHERE enabled ORDER BY display_name", conn);
        var list = new List<GameEndpointRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new GameEndpointRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4)));
        return list;
    }

    public async Task<List<GameEndpointRow>> ListAllAsync()
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, game_id, display_name, base_url, enabled FROM game_endpoints ORDER BY game_id", conn);
        var list = new List<GameEndpointRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new GameEndpointRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4)));
        return list;
    }

    public async Task<GameEndpointRow?> GetAsync(string gameId)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, game_id, display_name, base_url, enabled, admin_key FROM game_endpoints WHERE game_id=@g AND enabled", conn);
        cmd.Parameters.AddWithValue("g", gameId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new GameEndpointRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4));
    }

    public async Task<string?> GetAdminKeyAsync(string gameId)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT admin_key FROM game_endpoints WHERE game_id=@g", conn);
        cmd.Parameters.AddWithValue("g", gameId);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    public async Task UpsertAsync(string gameId, string displayName, string baseUrl, string? adminKey, bool enabled)
    {
        await using var conn = await db.OpenAsync();
        if (string.IsNullOrWhiteSpace(adminKey))
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO game_endpoints (game_id, display_name, base_url, admin_key, enabled)
                  VALUES (@g,@n,@u,'',@e)
                  ON CONFLICT (game_id) DO UPDATE SET display_name=@n, base_url=@u, enabled=@e", conn);
            cmd.Parameters.AddWithValue("g", gameId);
            cmd.Parameters.AddWithValue("n", displayName);
            cmd.Parameters.AddWithValue("u", baseUrl);
            cmd.Parameters.AddWithValue("e", enabled);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO game_endpoints (game_id, display_name, base_url, admin_key, enabled)
                  VALUES (@g,@n,@u,@k,@e)
                  ON CONFLICT (game_id) DO UPDATE SET display_name=@n, base_url=@u, admin_key=@k, enabled=@e", conn);
            cmd.Parameters.AddWithValue("g", gameId);
            cmd.Parameters.AddWithValue("n", displayName);
            cmd.Parameters.AddWithValue("u", baseUrl);
            cmd.Parameters.AddWithValue("k", adminKey);
            cmd.Parameters.AddWithValue("e", enabled);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
