
using MHSApi.WebSocket;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MHSApi.API
{
    public enum SecurityApiResult
    {
        // General stuff
        Success = 0,
        InternalError = 1,
        MissingInvaildAuthToken = 2,
        // Login API
        IncorrectUsernameOrPassword = 3,
        NoUsernameOrPassword = 4,
        // Arm/Disarm API
        IncorrectCode = 5,
        NotReady = 6,
        JsonError = 7,
        // Firmware
        NoDevicesToUpdate = 8,
        WrongFirmware = 9,
        // Music Manager
        FileNotFound = 10,
        // General errors
        NotImplemented = 11,
        BadRequest = 12,
        NoPermission = 13,

        // Client (not returned by server)
        ConnectionFailed = 100,
    }
    public static class APIUtils
    {
        public static string ErrorToString(SecurityApiResult result)
        {
            switch (result)
            {
                case SecurityApiResult.Success:
                    return "The operation completed successfully";
                case SecurityApiResult.InternalError:
                    return "An internal server error has occured";
                case SecurityApiResult.MissingInvaildAuthToken:
                    return "The authentication token is invailid or missing";
                case SecurityApiResult.IncorrectUsernameOrPassword:
                    return "Incorrect username or password";
                case SecurityApiResult.NoUsernameOrPassword:
                    return "No username or password was provided";
                case SecurityApiResult.IncorrectCode:
                    return "The passcode is incorrect";
                case SecurityApiResult.NotReady:
                    return "Security system is not ready";
                case SecurityApiResult.JsonError:
                    return "JSON parse error";
                case SecurityApiResult.NoDevicesToUpdate:
                    return "There are no devices to update";
                case SecurityApiResult.WrongFirmware:
                    return "The firmware file provided was not intended for the selected device";
                case SecurityApiResult.FileNotFound:
                    return "The requested file was not found";
                case SecurityApiResult.NotImplemented:
                    return "The operation is not implemented - update controller firmware";
                case SecurityApiResult.BadRequest:
                    return "Bad request";
                case SecurityApiResult.NoPermission:
                    return "Your account does not have permission to do this";
                default:
                    return "Unknown error";
            }
        }

        public static bool IsSuccessfulResult(SecurityApiResult code)
        {
            return code == SecurityApiResult.Success;
        }

        public static WebSocketMessage? DeserializeWebsocketMessage(string str)
        {
            try
            {
                var obj = JsonNode.Parse(str) ?? throw new Exception("failed to parse websocket");
                var type = obj["type"];
                if (type != null)
                {
                    return (MessageType)type.AsValue().GetValue<int>() switch
                    {
                        MessageType.None => null,
                        MessageType.ServerHello => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.Hello),
                        MessageType.ClientWelcomeReply => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.ClientWelcomeReply),
                        MessageType.AuthError => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.AuthenticationFail),
                        MessageType.AuthOK => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.AuthenticationOK),
                        MessageType.SystemStateChange => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.SystemStateChange),
                        MessageType.ZoneUpdate => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.ZoneUpdate),
                        MessageType.AnncVolumeChange => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.AnncPlayerVolumeChange),
                        MessageType.MusicVolumeChange => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.MusicPlayerVolumeChange),
                        MessageType.MusicStarted => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.MusicStarted),
                        MessageType.MusicStopped => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.MusicStopped),
                        MessageType.AnncStarted => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.AnncStarted),
                        MessageType.AnncStopped => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.AnncStopped),
                        MessageType.FwUpdate => JsonSerializer.Deserialize(str, SourceGenerationContext.Default.FwUpdateMsg),
                        _ => throw new Exception("unknown message type"),
                    };
                }
            }
            catch
            {
                return null;
            }
            return null;
        }
    }
}
