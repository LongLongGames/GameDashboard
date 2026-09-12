using System.Text.Json.Serialization;

namespace GameDashboard.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(OkResponse))]
[JsonSerializable(typeof(MsgResponse))]
[JsonSerializable(typeof(IdResponse))]
[JsonSerializable(typeof(TempPasswordResponse))]
[JsonSerializable(typeof(MeResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(SendMailRequest))]
[JsonSerializable(typeof(UserRow))]
[JsonSerializable(typeof(List<UserRow>))]
[JsonSerializable(typeof(GameEndpointRow))]
[JsonSerializable(typeof(List<GameEndpointRow>))]
[JsonSerializable(typeof(AuditRow))]
[JsonSerializable(typeof(List<AuditRow>))]
[JsonSerializable(typeof(PlayerInfo))]
[JsonSerializable(typeof(List<PlayerInfo>))]
[JsonSerializable(typeof(LeaderboardEntry))]
[JsonSerializable(typeof(List<LeaderboardEntry>))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(BugItem))]
[JsonSerializable(typeof(List<BugItem>))]
[JsonSerializable(typeof(Match3StatusResponse))]
[JsonSerializable(typeof(Dictionary<string, long>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(GameUpsertRequest))]
[JsonSerializable(typeof(SetActiveRequest))]
[JsonSerializable(typeof(ReportSummary))]
[JsonSerializable(typeof(List<ReportSummary>))]
[JsonSerializable(typeof(ReportListResponse))]
[JsonSerializable(typeof(StatusUpdateRequest))]
internal partial class AppJsonContext : JsonSerializerContext;

public sealed class GameUpsertRequest
{
    public string GameId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string? AdminKey { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class SetActiveRequest
{
    public bool Active { get; set; }
}
