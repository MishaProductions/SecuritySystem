using System.Text.Json.Serialization;
using MHSApi.WebSocket;
using SecuritySystemApi;

namespace MHSApi.API
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ApiResponse))]
    [JsonSerializable(typeof(ApiResponseWithContent<Hello>))]
    [JsonSerializable(typeof(ApiResponseWithContent<UpdateInformationContent>))]
    [JsonSerializable(typeof(ApiResponseWithContent<ShortWeatherDataContent>))]
    [JsonSerializable(typeof(ApiResponseWithContent<AlarmHistoryInfoContent[]>))]
    [JsonSerializable(typeof(ApiResponseWithContent<EventLogEntry[]>))]
    [JsonSerializable(typeof(ApiResponseWithContent<MusicListResponse>))]
    [JsonSerializable(typeof(ApiResponseWithContent<NotificationSettings>))]
    [JsonSerializable(typeof(ApiResponseWithContent<SystemInfoResponse>))]
    [JsonSerializable(typeof(ApiResponseWithContent<ApiUser>))]
    [JsonSerializable(typeof(ApiResponseWithContent<ApiUser[]>))]
    [JsonSerializable(typeof(ApiResponseWithContent<LoginResponse>))]
    [JsonSerializable(typeof(ClientWelcomeReply))]
    [JsonSerializable(typeof(AuthenticationFail))]
    [JsonSerializable(typeof(AuthenticationOK))]
    [JsonSerializable(typeof(SystemStateChange))]
    [JsonSerializable(typeof(ZoneUpdate))]
    [JsonSerializable(typeof(AnncPlayerVolumeChange))]
    [JsonSerializable(typeof(MusicPlayerVolumeChange))]
    [JsonSerializable(typeof(MusicStarted))]
    [JsonSerializable(typeof(MusicStopped))]
    [JsonSerializable(typeof(AnncStarted))]
    [JsonSerializable(typeof(AnncStopped))]
    [JsonSerializable(typeof(FwUpdateMsg))]
    [JsonSerializable(typeof(LoginRequest))]
    [JsonSerializable(typeof(UserUpdatePasswordRequest))]
    [JsonSerializable(typeof(UserUpdatePermissionRequest))]
    [JsonSerializable(typeof(UserUpdateRequest))]
    [JsonSerializable(typeof(string))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
