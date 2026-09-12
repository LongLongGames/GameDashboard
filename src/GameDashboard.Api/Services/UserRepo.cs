using GameDashboard.Api.Infrastructure.Db;
using Npgsql;

namespace GameDashboard.Api.Services;

public sealed class UserRepo(Db db)
{
    public async Task<(UserRow row, string hash)?> GetAuthAsync(string username)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, username, password_hash, must_change_password, is_active FROM users WHERE username=@u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var id = r.GetInt32(0);
        var uname = r.GetString(1);
        var hash = r.GetString(2);
        var must = r.GetBoolean(3);
        var active = r.GetBoolean(4);
        await r.CloseAsync();
        if (!active) return null;
        var roles = await RolesOfAsync(conn, id);
        var games = await GamesOfAsync(conn, id);
        return (new UserRow(id, uname, must, active, roles, games), hash);
    }

    public async Task TouchLoginAsync(int userId)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("UPDATE users SET last_login_at=NOW() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ChangePasswordAsync(int userId, string newHash, bool mustChange = false)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE users SET password_hash=@h, must_change_password=@m WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("h", newHash);
        cmd.Parameters.AddWithValue("m", mustChange);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetPasswordHashAsync(int userId)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT password_hash FROM users WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    public async Task<List<UserRow>> ListAsync()
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, username, must_change_password, is_active FROM users ORDER BY id", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var ids = new List<(int id, string u, bool m, bool a)>();
        while (await r.ReadAsync())
            ids.Add((r.GetInt32(0), r.GetString(1), r.GetBoolean(2), r.GetBoolean(3)));
        await r.CloseAsync();

        var list = new List<UserRow>();
        foreach (var x in ids)
        {
            var roles = await RolesOfAsync(conn, x.id);
            var games = await GamesOfAsync(conn, x.id);
            list.Add(new UserRow(x.id, x.u, x.m, x.a, roles, games));
        }
        return list;
    }

    public async Task<int> CreateAsync(string username, string hash, string[] roleNames, string[] gameIds)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using var ins = new NpgsqlCommand(
            @"INSERT INTO users (username, password_hash, must_change_password, is_active)
              VALUES (@u,@h,TRUE,TRUE) RETURNING id", conn, tx);
        ins.Parameters.AddWithValue("u", username);
        ins.Parameters.AddWithValue("h", hash);
        var id = Convert.ToInt32(await ins.ExecuteScalarAsync());

        foreach (var rn in roleNames.Distinct())
        {
            await using var ur = new NpgsqlCommand(
                @"INSERT INTO user_roles (user_id, role_id)
                  SELECT @uid, id FROM roles WHERE name=@n", conn, tx);
            ur.Parameters.AddWithValue("uid", id);
            ur.Parameters.AddWithValue("n", rn);
            await ur.ExecuteNonQueryAsync();
        }
        foreach (var g in gameIds.Distinct())
        {
            await using var ug = new NpgsqlCommand(
                "INSERT INTO user_game_scopes (user_id, game_id) VALUES (@uid,@g) ON CONFLICT DO NOTHING", conn, tx);
            ug.Parameters.AddWithValue("uid", id);
            ug.Parameters.AddWithValue("g", g);
            await ug.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return id;
    }

    public async Task SetActiveAsync(int id, bool active)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("UPDATE users SET is_active=@a WHERE id=@id AND username<>'root'", conn);
        cmd.Parameters.AddWithValue("a", active);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> ResetPasswordAsync(int id)
    {
        var temp = "Temp" + Guid.NewGuid().ToString("N")[..8] + "!";
        var hash = BCrypt.Net.BCrypt.HashPassword(temp);
        await using var conn = await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE users SET password_hash=@h, must_change_password=TRUE WHERE id=@id RETURNING username", conn);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("id", id);
        var uname = (string?)await cmd.ExecuteScalarAsync();
        return uname is null ? null : temp;
    }

    static async Task<string[]> RolesOfAsync(NpgsqlConnection conn, int userId)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT r.name FROM roles r
              INNER JOIN user_roles ur ON ur.role_id=r.id WHERE ur.user_id=@id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list.ToArray();
    }

    static async Task<string[]> GamesOfAsync(NpgsqlConnection conn, int userId)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT game_id FROM user_game_scopes WHERE user_id=@id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list.ToArray();
    }
}
