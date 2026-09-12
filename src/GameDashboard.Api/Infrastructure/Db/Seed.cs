using Npgsql;

namespace GameDashboard.Api.Infrastructure.Db;

public static class Seed
{
    public const string RootUsername = "root";
    public const string DefaultRootPassword = "ChangeMe123!";

    public static async Task EnsureAsync(NpgsqlDataSource ds, IConfiguration config, ILogger logger)
    {
        await using var conn = await ds.OpenConnectionAsync();

        // root
        await using (var check = new NpgsqlCommand("SELECT 1 FROM users WHERE username=@u", conn))
        {
            check.Parameters.AddWithValue("u", RootUsername);
            var exists = await check.ExecuteScalarAsync();
            if (exists is null)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(DefaultRootPassword);
                await using var ins = new NpgsqlCommand(
                    @"INSERT INTO users (username, password_hash, must_change_password, is_active)
                      VALUES (@u, @h, TRUE, TRUE) RETURNING id", conn);
                ins.Parameters.AddWithValue("u", RootUsername);
                ins.Parameters.AddWithValue("h", hash);
                var id = Convert.ToInt32(await ins.ExecuteScalarAsync());

                await using var role = new NpgsqlCommand(
                    @"INSERT INTO user_roles (user_id, role_id)
                      SELECT @uid, id FROM roles WHERE name='SuperAdmin'", conn);
                role.Parameters.AddWithValue("uid", id);
                await role.ExecuteNonQueryAsync();
                logger.LogInformation("Created root account (must change password on first login).");
            }
        }

        // match3 endpoint
        var baseUrl = config["Games:Match3:BaseUrl"] ?? "http://localhost:13180";
        var adminKey = config["Games:Match3:AdminKey"] ?? "match3-dev-key";

        await using (var cmd = new NpgsqlCommand(
            @"INSERT INTO game_endpoints (game_id, display_name, base_url, admin_key, enabled)
              VALUES ('match3', 'Match3', @url, @key, TRUE)
              ON CONFLICT (game_id) DO UPDATE SET
                display_name = EXCLUDED.display_name,
                enabled = TRUE,
                base_url = CASE
                  WHEN game_endpoints.base_url IN ('mock','http://localhost:0')
                       OR game_endpoints.base_url LIKE '%game-xxx%'
                  THEN EXCLUDED.base_url
                  ELSE game_endpoints.base_url
                END", conn))
        {
            cmd.Parameters.AddWithValue("url", baseUrl);
            cmd.Parameters.AddWithValue("key", adminKey);
            await cmd.ExecuteNonQueryAsync();
        }

        // disable placeholders
        await using (var dis = new NpgsqlCommand(
            "UPDATE game_endpoints SET enabled=FALSE WHERE game_id IN ('demo-game','game-xxx')", conn))
        {
            await dis.ExecuteNonQueryAsync();
        }
    }
}
