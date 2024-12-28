
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
                        MessageType.ServerHello => JsonSerializer.Deserialize<Hello>(str),
                        MessageType.ClientWelcomeReply => JsonSerializer.Deserialize<ClientWelcomeReply>(str),
                        MessageType.AuthError => JsonSerializer.Deserialize<AuthenticationFail>(str),
                        MessageType.AuthOK => JsonSerializer.Deserialize<AuthenticationOK>(str),
                        MessageType.SystemStateChange => JsonSerializer.Deserialize<SystemStateChange>(str),
                        MessageType.ZoneUpdate => JsonSerializer.Deserialize<ZoneUpdate>(str),
                        MessageType.AnncVolumeChange => JsonSerializer.Deserialize<AnncPlayerVolumeChange>(str),
                        MessageType.MusicVolumeChange => JsonSerializer.Deserialize<MusicPlayerVolumeChange>(str),
                        MessageType.MusicStarted => JsonSerializer.Deserialize<MusicStarted>(str),
                        MessageType.MusicStopped => JsonSerializer.Deserialize<MusicStopped>(str),
                        MessageType.AnncStarted => JsonSerializer.Deserialize<AnncStarted>(str),
                        MessageType.AnncStopped => JsonSerializer.Deserialize<AnncStopped>(str),
                        MessageType.FwUpdate => JsonSerializer.Deserialize<FwUpdateMsg>(str),
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
