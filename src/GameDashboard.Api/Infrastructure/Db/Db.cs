using Npgsql;

namespace GameDashboard.Api.Infrastructure.Db;

public sealed class Db(NpgsqlDataSource ds)
{
    public NpgsqlDataSource DataSource => ds;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = await ds.OpenConnectionAsync(ct);
        return conn;
    }
}
