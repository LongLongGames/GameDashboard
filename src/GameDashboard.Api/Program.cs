using System.Text.Json;
using GameDashboard.Api;
using GameDashboard.Api.Infrastructure.Db;
using GameDashboard.Api.Services;
using Npgsql;


// ---- migrate only ----
if (args.Contains("--migrate") ||
    string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATION_ONLY"), "true", StringComparison.OrdinalIgnoreCase))
{
    var cfg = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    var cs = cfg.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("缺少 ConnectionStrings:PostgreSQL");
    Environment.Exit(Migrator.Run(cs));
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var connStr = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings:PostgreSQL");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connStr).Build());
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddScoped<UserRepo>();
builder.Services.AddScoped<AuditRepo>();
builder.Services.AddScoped<GameRepo>();
builder.Services.AddScoped<GameAdminClient>();
builder.Services.AddHttpClient();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        .SetIsOriginAllowed(_ => true))); // 开发代理；生产同域静态即可

var app = builder.Build();

app.UseExceptionHandler(err =>
{
    err.Run(async ctx =>
    {
        var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Unhandled");
        log.LogError(ex, "Unhandled {Path}", ctx.Request.Path);
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ErrorResponse(ex?.Message ?? "error"), AppJsonContext.Default.ErrorResponse);
    });
});

// seed after migrate job in compose; also safe on API start for first-run
{
    var ds = app.Services.GetRequiredService<NpgsqlDataSource>();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
    await Seed.EnsureAsync(ds, app.Configuration, logger);
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// -------- helpers --------
static string? TokenOf(HttpRequest req)
{
    if (req.Headers.TryGetValue("Authorization", out var h))
    {
        var v = h.ToString();
        if (v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return v["Bearer ".Length..].Trim();
    }
    if (req.Cookies.TryGetValue("gd_token", out var c)) return c;
    return null;
}

static SessionInfo? TrySession(HttpRequest req, SessionStore store) => store.Get(TokenOf(req));

bool HasRole(SessionInfo s, params string[] roles) => s.Roles.Any(roles.Contains);

// -------- health --------
app.MapGet("/health", () => Results.Ok(new HealthDto("ok", "gamedashboard-api")));

// -------- auth --------
app.MapPost("/api/v1/auth/login", async (LoginRequest body, UserRepo users, AuditRepo audit, SessionStore sessions, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new ErrorResponse("用户名和密码必填"));

    var auth = await users.GetAuthAsync(body.Username.Trim());
    if (auth is null || !BCrypt.Net.BCrypt.Verify(body.Password, auth.Value.hash))
        return Results.Json(new ErrorResponse("用户名或密码错误"), AppJsonContext.Default.ErrorResponse, statusCode: 401);

    var u = auth.Value.row;
    await users.TouchLoginAsync(u.Id);
    var token = sessions.Create(new SessionInfo
    {
        UserId = u.Id,
        Username = u.Username,
        Roles = u.Roles,
        MustChangePassword = u.MustChangePassword
    });
    await audit.LogAsync(u.Id, u.Username, "Login", success: true, ip: ctx.Connection.RemoteIpAddress?.ToString());

    ctx.Response.Cookies.Append("gd_token", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromHours(12)
    });

    return Results.Ok(new LoginResponse(u.Id, u.Username, u.MustChangePassword, u.Roles, token));
});

app.MapPost("/api/v1/auth/logout", (SessionStore sessions, HttpContext ctx) =>
{
    sessions.Remove(TokenOf(ctx.Request));
    ctx.Response.Cookies.Delete("gd_token");
    return Results.Ok(new OkResponse());
});

app.MapGet("/api/v1/auth/me", (SessionStore sessions, HttpRequest req) =>
{
    var s = sessions.Get(TokenOf(req));
    if (s is null) return Results.Unauthorized();
    return Results.Ok(new MeResponse(s.UserId, s.Username, s.MustChangePassword, s.Roles));
});

app.MapPost("/api/v1/auth/change-password", async (ChangePasswordRequest body, SessionStore sessions, UserRepo users, AuditRepo audit, HttpContext ctx) =>
{
    var s = sessions.Get(TokenOf(ctx.Request));
    if (s is null) return Results.Unauthorized();
    if (string.IsNullOrEmpty(body.NewPassword) || body.NewPassword.Length < 8)
        return Results.BadRequest(new ErrorResponse("新密码至少 8 位"));
    if (body.NewPassword == "ChangeMe123!")
        return Results.BadRequest(new ErrorResponse("请勿使用默认密码"));

    var hash = await users.GetPasswordHashAsync(s.UserId);
    if (hash is null || !BCrypt.Net.BCrypt.Verify(body.CurrentPassword, hash))
        return Results.BadRequest(new ErrorResponse("当前密码不正确"));

    await users.ChangePasswordAsync(s.UserId, BCrypt.Net.BCrypt.HashPassword(body.NewPassword));
    s.MustChangePassword = false;
    var token = TokenOf(ctx.Request)!;
    sessions.Update(token, s);
    await audit.LogAsync(s.UserId, s.Username, "ChangePassword", ip: ctx.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new OkResponse());
});

// -------- users (SuperAdmin) --------
app.MapGet("/api/v1/users", async (SessionStore sessions, UserRepo users, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    return Results.Ok(await users.ListAsync());
});

app.MapPost("/api/v1/users", async (CreateUserRequest body, SessionStore sessions, UserRepo users, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new ErrorResponse("用户名和密码必填"));
    try
    {
        var id = await users.CreateAsync(body.Username.Trim(), BCrypt.Net.BCrypt.HashPassword(body.Password),
            body.RoleNames ?? [], body.GameIds ?? []);
        await audit.LogAsync(s.UserId, s.Username, "CreateUser", target: body.Username, ip: ctx.Connection.RemoteIpAddress?.ToString());
        return Results.Ok(new IdResponse(id));
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.Conflict(new ErrorResponse("用户名已存在"));
    }
});

app.MapPost("/api/v1/users/{id:int}/active", async (int id, SetActiveRequest body, SessionStore sessions, UserRepo users, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    await users.SetActiveAsync(id, body.Active);
    await audit.LogAsync(s.UserId, s.Username, body.Active ? "EnableUser" : "DisableUser", target: id.ToString());
    return Results.Ok(new OkResponse());
});

app.MapPost("/api/v1/users/{id:int}/reset-password", async (int id, SessionStore sessions, UserRepo users, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    var temp = await users.ResetPasswordAsync(id);
    if (temp is null) return Results.NotFound();
    await audit.LogAsync(s.UserId, s.Username, "ResetPassword", target: id.ToString());
    return Results.Ok(new TempPasswordResponse(temp));
});

// -------- games --------
app.MapGet("/api/v1/games", async (SessionStore sessions, GameRepo games, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    return Results.Ok(await games.ListEnabledAsync());
});

app.MapGet("/api/v1/games/all", async (SessionStore sessions, GameRepo games, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    return Results.Ok(await games.ListAllAsync());
});

app.MapPost("/api/v1/games", async (GameUpsertRequest body, SessionStore sessions, GameRepo games, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin")) return Results.Forbid();
    await games.UpsertAsync(body.GameId, body.DisplayName, body.BaseUrl, body.AdminKey, body.Enabled);
    await audit.LogAsync(s.UserId, s.Username, "SaveGameEndpoint", body.GameId);
    return Results.Ok(new OkResponse());
});

// -------- match3 / players --------
app.MapGet("/api/v1/match3/status", async (SessionStore sessions, GameAdminClient client, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    var health = await client.HealthAsync("match3");
    var ver = await client.VersionCheckAsync("match3");
    var top = await client.LeaderboardTopAsync("match3");
    var jwtPlayer = await client.GetPlayerViaJwtAsync("match3");
    return Results.Ok(new Match3StatusResponse(health, ver, top.ToList(), jwtPlayer));
});

app.MapPost("/api/v1/match3/energy-refill", async (SessionStore sessions, GameAdminClient client, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin", "GM", "Operator")) return Results.Forbid();
    var (ok, msg) = await client.EnergyRefillAsync("match3");
    await audit.LogAsync(s.UserId, s.Username, "Match3EnergyRefill", "match3", detail: msg, success: ok);
    return ok ? Results.Ok(new MsgResponse(msg)) : Results.BadRequest(new ErrorResponse(msg));
});

app.MapGet("/api/v1/players", async (string? gameId, string? query, SessionStore sessions, GameAdminClient client, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin", "GM")) return Results.Forbid();
    gameId ??= "match3";
    return Results.Ok(await client.SearchPlayersAsync(gameId, query));
});

app.MapPost("/api/v1/mail/send", async (SendMailRequest body, SessionStore sessions, GameAdminClient client, AuditRepo audit, HttpContext ctx) =>
{
    var s = TrySession(ctx.Request, sessions); if (s is null) return Results.Unauthorized();
    if (!HasRole(s, "SuperAdmin", "GM", "Operator")) return Results.Forbid();
    var (ok, msg) = await client.SendMailAsync(body.GameId, body.MpAccountId, body.Title, body.Body, body.ItemsJson);
    await audit.LogAsync(s.UserId, s.Username, "SendMail", body.GameId, body.MpAccountId, body.Title, ok, ok ? null : msg);
    return ok ? Results.Ok(new MsgResponse(msg)) : Results.BadRequest(new ErrorResponse(msg));
});

app.MapGet("/api/v1/bugs", async (string? gameId, string? status, SessionStore sessions, GameAdminClient client, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    return Results.Ok(await client.ListBugsAsync(gameId, status));
});

app.MapGet("/api/v1/audit", async (SessionStore sessions, AuditRepo audit, HttpRequest req) =>
{
    var s = TrySession(req, sessions); if (s is null) return Results.Unauthorized();
    return Results.Ok(await audit.RecentAsync());
});

// SPA fallback
app.MapFallback(async ctx =>
{
    var path = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (File.Exists(path))
    {
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(path);
    }
    else
        ctx.Response.StatusCode = 404;
});

app.Run();
