
using MHSApi.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

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
                var obj = JObject.Parse(str);
                var type = obj["type"];
                if (type != null)
                {
                    switch ((MessageType)type.Value<int>())
                    {
                        case MessageType.None:
                            return null;
                        case MessageType.ServerHello:
                            return JsonConvert.DeserializeObject<Hello>(str);
                        case MessageType.ClientWelcomeReply:
                            return JsonConvert.DeserializeObject<ClientWelcomeReply>(str);
                        case MessageType.AuthError:
                            return JsonConvert.DeserializeObject<AuthenticationFail>(str);
                        case MessageType.AuthOK:
                            return JsonConvert.DeserializeObject<AuthenticationOK>(str);
                        case MessageType.SystemStateChange:
                            return JsonConvert.DeserializeObject<SystemStateChange>(str);
                        case MessageType.ZoneUpdate:
                            return JsonConvert.DeserializeObject<ZoneUpdate>(str);
                        case MessageType.AnncVolumeChange:
                            return JsonConvert.DeserializeObject<AnncPlayerVolumeChange>(str);
                        case MessageType.MusicVolumeChange:
                            return JsonConvert.DeserializeObject<MusicPlayerVolumeChange>(str);
                        case MessageType.MusicStarted:
                            return JsonConvert.DeserializeObject<MusicStarted>(str);
                        case MessageType.MusicStopped:
                            return JsonConvert.DeserializeObject<MusicStopped>(str);
                        case MessageType.AnncStarted:
                            return JsonConvert.DeserializeObject<AnncStarted>(str);
                        case MessageType.AnncStopped:
                            return JsonConvert.DeserializeObject<AnncStopped>(str);
                        case MessageType.FwUpdate:
                            return JsonConvert.DeserializeObject<FwUpdateMsg>(str);
                        default:
                            throw new Exception("unknown message type");
                    }
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
