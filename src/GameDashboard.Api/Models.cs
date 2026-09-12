namespace GameDashboard.Api;

public record UserRow(int Id, string Username, bool MustChangePassword, bool IsActive, string[] Roles, string[] Games);
public record GameEndpointRow(int Id, string GameId, string DisplayName, string BaseUrl, bool Enabled);
public record AuditRow(long Id, string Username, string Action, string? GameId, string? Target, string? Detail, bool Success, DateTime CreatedAt);
public record PlayerInfo(string MpAccountId, string Nickname, bool IsBanned, Dictionary<string, long>? Currencies, string? Extra);
public record LeaderboardEntry(string MpAccountId, string? Nickname, long Score, int Rank);
public record HealthResult(bool Ok, string? Raw, string Message);
public record BugItem(string Id, string Title, string Status, string? GameId, string? Reporter, DateTime CreatedAt, string? Detail);

// BugReport service shapes (GET /api/v1/reports)
public record ReportSummary(
    string Id,
    string ProjectId,
    string Level,
    string Message,
    string Status,
    DateTime OccurredAt,
    DateTime CreatedAt,
    string? AppVersion);

public record ReportListResponse(
    List<ReportSummary> Items,
    int Page,
    int PageSize,
    int Total);

public record StatusUpdateRequest(string Status);

public record LoginRequest(string Username, string Password);
public record LoginResponse(int UserId, string Username, bool MustChangePassword, string[] Roles, string Token);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateUserRequest(string Username, string Password, string[] RoleNames, string[] GameIds);
public record SendMailRequest(string GameId, string MpAccountId, string Title, string Body, string? ItemsJson);
public record CurrencyRequest(string GameId, string MpAccountId, string Item, long Delta, string Reason);
public record BanRequest(string GameId, string MpAccountId, string Reason);

public record MsgResponse(string Message);
public record ErrorResponse(string Error);
public record OkResponse(bool Ok = true);
public record IdResponse(int Id);
public record TempPasswordResponse(string TemporaryPassword);
public record HealthDto(string Status, string Service);
public record MeResponse(int UserId, string Username, bool MustChangePassword, string[] Roles);
public record Match3StatusResponse(HealthResult Health, string? Version, List<LeaderboardEntry> Top, PlayerInfo? JwtPlayer);
