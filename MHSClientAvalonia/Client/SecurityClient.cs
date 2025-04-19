using MHSApi.API;
using MHSApi.WebSocket;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Utils;
using SecuritySystemApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Client
{
    public class SecurityClient
    {
        /// <summary>
        /// HTTP client for Security Client. SSL is bypassed.
        /// </summary>
        public HttpClient Client;
        private PlugifyWebSocketClient ws = new();
        /// <summary>
        /// Gets the system model
        /// </summary>
        public string Model { get; private set; } = "";
        public bool IsSysArmed { get; private set; } = false;
        public bool IsAlarmState { get; private set; } = false;
        public bool IsReady { get; private set; } = false;
        public string Endpoint = "not set";
        public JsonZoneWithReady[]? Zones { get; set; }
        public ApiUser? CurrentUser { get; private set; }

        public event EventHandler? OnAuthenticationFailure;
        public event EventHandler? OnConnected;
        public event EventHandler? OnSystemDisarm;
        public event OnSystemTimer? OnSystemTimerEvent;
        public event EventHandler? OnZoneUpdate;
        public event EventHandler? OnWSClose;
        public event EventHandler? OnMusicVolChanged;
        public event EventHandler? OnAnncVolChanged;
        public event OnSystemUpdateProgress? OnFwUpdateProgress;
        public event MusicStartedEventHandler? OnMusicStarted;
        public event EventHandler? OnMusicStopped;
        public event MusicStartedEventHandler? OnAnncStarted;
        public event EventHandler? OnAnncStopped;

        private string Token = ""; //insecure!
        public bool IsConnected { get; private set; } = false;
        public int Timer { get; set; }

        public int AnncVol { get; set; }
        public int MusicVol { get; set; }
        public SecurityClient()
        {
            Console.WriteLine("SecurityClient cctor");

            var handler = new HttpClientHandler();
            if (!BrowserUtils.IsBrowser)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        return true;
                    };
            }

            Client = new HttpClient(handler);
        }

        public void SetHost(string endpoint)
        {
            this.Endpoint = endpoint;
            Console.WriteLine("set endpoint to " + Endpoint);
            ws = new PlugifyWebSocketClient();
            ws.SetUrl(endpoint.Replace("https://", "wss://") + "/wsv2");
            ws.OnMessage += WebsocketClient_OnMessage;
            ws.OnClose += Ws_OnClose;
        }

        private void Ws_OnClose(object? sender, EventArgs e)
        {
            OnWSClose?.Invoke(sender, e);
        }
        private async void WebsocketClient_OnMessage(object? sender, string e)
        {
            try
            {
                WebSocketMessage? msg = APIUtils.DeserializeWebsocketMessage(e);
                if (msg != null)
                {
                    if (msg.type == MessageType.ServerHello)
                    {
                        await ws.Send(JsonSerializer.Serialize(new ClientWelcomeReply(Token), SourceGenerationContext.Default.ClientWelcomeReply));
                    }
                    else if (msg.type == MessageType.AuthError)
                    {
                        OnAuthenticationFailure?.Invoke(this, new EventArgs());
                    }
                    else if (msg.type == MessageType.AuthOK)
                    {
                        IsConnected = true;
                        ProcessStateChange(((AuthenticationOK)msg).State, true);
                        ProcessZoneUpdate(((AuthenticationOK)msg).Zones, true);
                        await SendMusicManagerThings();
                        OnConnected?.Invoke(this, new EventArgs());
                    }
                    else if (msg.type == MessageType.SystemStateChange)
                    {
                        ProcessStateChange((SystemStateChange)msg, false);
                    }
                    else if (msg.type == MessageType.ZoneUpdate)
                    {
                        ProcessZoneUpdate((ZoneUpdate)msg, false);
                    }
                    else if (msg.type == MessageType.MusicVolumeChange)
                    {
                        MusicVol = ((MusicPlayerVolumeChange)msg).MusicVolume;
                        OnMusicVolChanged?.Invoke(this, new EventArgs());
                    }
                    else if (msg.type == MessageType.AnncVolumeChange)
                    {
                        AnncVol = ((AnncPlayerVolumeChange)msg).AnncVolume;
                        OnAnncVolChanged?.Invoke(this, new EventArgs());
                    }
                    else if (msg.type == MessageType.FwUpdate)
                    {
                        OnFwUpdateProgress?.Invoke((FwUpdateMsg)msg);
                    }
                    else if (msg.type == MessageType.MusicStarted)
                    {
                        var msg2 = ((MusicStarted)msg) ?? throw new Exception("cannot be null");
                        OnMusicStarted?.Invoke(msg2.MusicFileName, msg2.IsLive);
                    }
                    else if (msg.type == MessageType.MusicStopped)
                    {
                        OnMusicStopped?.Invoke(this, new EventArgs());
                    }
                    else if (msg.type == MessageType.AnncStarted)
                    {
                        var msg2 = ((AnncStarted)msg) ?? throw new Exception("cannot be null");
                        OnAnncStarted?.Invoke(msg2.AnncFileName ?? "null", msg2.IsLive);
                    }
                    else if (msg.type == MessageType.AnncStopped)
                    {
                        OnAnncStopped?.Invoke(this, new EventArgs());
                    }
                }
            }
            catch
            {
                // TODO: fix this correctly when secsys service is restarted:
            }
        }


        public async Task SetAnncVolume(int newValue)
        {
            await ws.Send(JsonSerializer.Serialize(new AnncPlayerVolumeChange(newValue), SourceGenerationContext.Default.AnncPlayerVolumeChange));
        }

        public async Task SetMusicVolume(int newValue)
        {
            await ws.Send(JsonSerializer.Serialize(new MusicPlayerVolumeChange(newValue), SourceGenerationContext.Default.MusicPlayerVolumeChange));
        }

        private async Task SendMusicManagerThings()
        {
            // request music/annc volume
            await SetAnncVolume(-1);
            await SetMusicVolume(-1);
        }

        private void ProcessStateChange(SystemStateChange state, bool initial)
        {
            if (state.IsSystemArmed != IsSysArmed)
            {
                IsSysArmed = state.IsSystemArmed;

                // Invoke OnSystemDisarmed event when disarmed
                if (!initial && !IsSysArmed)
                    OnSystemDisarm?.Invoke(this, new EventArgs());
            }

            IsAlarmState = state.IsAlarmState;
            IsReady = state.IsReady;

            if (state.IsCountdownInProgress)
            {
                OnSystemTimerEvent?.Invoke(!state.IsEntryDelay, state.SystemTimer);
            }
        }
        private void ProcessZoneUpdate(ZoneUpdate zones, bool initial)
        {
            Zones = zones.Zones;

            // calculate if all zones are ready client side
            if (zones.Zones != null)
            {
                bool ready = true;

                foreach (JsonZoneWithReady? r in zones.Zones)
                {
                    if (r != null)
                    {
                        if (r.type != ZoneType.None && !r.ready)
                        {
                            ready = false;
                        }
                    }
                }
                IsReady = ready;
            }

            if (!initial)
                OnZoneUpdate?.Invoke(this, new EventArgs());
        }
        public async Task<SecurityApiResult> Start(string token)
        {
            Token = token;
            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.Add("authorization", token);
            //check if the token is correct
            try
            {
                var sysinforequest = await GetSystemInfo();
                if (!sysinforequest.IsSuccess)
                    return sysinforequest.ResultCode;

                if (sysinforequest.IsSuccess && sysinforequest.Value != null)
                {
                    SystemInfoResponse resp = (SystemInfoResponse)sysinforequest.Value;
                    Model = resp.model;
                }
                else
                {
                    return SecurityApiResult.ConnectionFailed;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return SecurityApiResult.ConnectionFailed;
            }

            // get current user. some older fw might not support this.
            try
            {
                var res = await GetCurrentUser();
                if (res.IsSuccess && res.Value != null)
                    CurrentUser = (ApiUser?)res.Value;
            }
            catch
            {

            }
            await ws.Start();

            return SecurityApiResult.Success;
        }

        internal void Stop()
        {
            Token = "";
            IsConnected = false;
            ws.Close();
        }
        /// <summary>
        /// Creates session token. Returns LoginResponse
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<(SecurityApiResult, string)> Login(string username, string password)
        {
            var result = await DoSimplePost<ApiResponseWithContent<LoginResponse>>(Endpoints.Login, new LoginRequest(username, password));

            if (!result.IsSuccess || result.Value == null)
            {
                return (result.ResultCode, result.ResultMessage);
            }
            else
            {
                var fsdf = (ApiResponseWithContent<LoginResponse>)result.Value;

                var resp = fsdf.content;
                if (resp == null)
                {
                    // this really should not be null
                    return (SecurityApiResult.WrongFirmware, "");
                }
                return (SecurityApiResult.Success, resp.token);
            }
        }

        public async Task<Result> ArmOrDisarm(string code)
        {
            return await DoSimplePost<ApiResponse>(IsSysArmed ? Endpoints.Disarm : Endpoints.Arm, new RequestWithCode(code));
        }
        /// <summary>
        /// UpdateInformationContent?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> FetchUpdateData()
        {
            return await DoSimpleGet<UpdateInformationContent?>(Endpoints.QueryClientUpdateInfo);
        }
        /// <summary>
        /// ShortWeatherDataContent?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> FetchWeatherShortData()
        {
            return await DoSimpleGet<ShortWeatherDataContent?>(Endpoints.QueryWeatherShort);
        }
        /// <summary>
        /// AlarmHistoryInfoContent[]?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetAlarmHistory()
        {
            return await DoSimpleGet<AlarmHistoryInfoContent[]?>(Endpoints.GetAlarmHistory);
        }
        /// <summary>
        /// EventLogEntry[]?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetEventLog()
        {
            return await DoSimpleGet<EventLogEntry[]?>(Endpoints.SystemEventLog);
        }
        /// <summary>
        /// Result value: MusicListResponse?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetMusicAndAnnoucements()
        {
            return await DoSimpleGet<MusicListResponse?>(Endpoints.ListMusicAndAnnoucements);
        }
        public async Task<Result> PlayAnnoucement(string fileName)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.StartAnnc, new StartAnncRequest(fileName));
        }
        public async Task<Result> PlayMusic(string fileName)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.StartMusic, new StartAnncRequest(fileName));
        }
        public async Task<Result> StopCurrentAnnoucement()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.StopAnnc, "");
        }
        public async Task<Result> StopCurrentMusic()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.StopMusic, "");
        }
        public async Task<Result> PlayAllMusic()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.PlayAllMusic, "");
        }
        public async Task<Result> PlayPreviousMusic()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.PlayPreviousMusic, "");
        }
        public async Task<Result> PlayNextMusic()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.PlayNextMusic, "");
        }
        
        /// <summary>
        /// NotificationSettings?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetNotificationSettings()
        {
            return await DoSimpleGet<NotificationSettings>(Endpoints.NotificationSettings);
        }
        internal async Task<Result> SaveNotificationSettings(NotificationSettings cfg)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.NotificationSettings, cfg);
        }

        /// <summary>
        /// Result: SystemInfoResponse?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetSystemInfo()
        {
            return await DoSimpleGet<SystemInfoResponse?>(Endpoints.SystemInfo);
        }

        public async Task<Result> SendTestEmail()
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.NotificationsTest, new object());
        }
        public async Task<Result> SetZoneSettings(JsonZones zoneCfg)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.ZoneSettings, zoneCfg);
        }

        /// <summary>
        /// returns ApiUser?
        /// </summary>
        /// <returns></returns>
        public async Task<Result> GetCurrentUser()
        {
            return await DoSimpleGet<ApiUser?>(Endpoints.CurrentUser);
        }

        public async Task<Result> SetCurrentUserPassword(string oldPw, string newPw)
        {
            return await DoSimplePatch(Endpoints.CurrentUser, new UserUpdatePasswordRequest(oldPw, newPw));
        }
        /// <summary>
        /// gets all users. returns ApiUser[]?
        /// </summary>
        /// <returns>ApiUser[]?</returns>
        public async Task<Result> GetAllUsers()
        {
            return await DoSimpleGet<ApiUser[]?>(Endpoints.AllUser);
        }

        public async Task<Result> ModUserPw(string userID, string newPW)
        {
            return await DoSimplePatch(Endpoints.UserMod + userID, new UserUpdatePasswordRequest("", newPW, userID));
        }

        public async Task<Result> ModUserPermission(string userID, UserPermissions newPermission)
        {
            return await DoSimplePatch(Endpoints.UserMod + userID, new UserUpdatePermissionRequest(newPermission));
        }
        public async Task<Result> DelUser(string userID)
        {
            return await DoSimpleDelete<ApiResponse?>(Endpoints.UserMod + userID);
        }
        public async Task<Result> CreateUser(string name, string pw, UserPermissions perm)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.UserMod + "new", new ApiUser() { Username = name, Password = pw, Permissions = perm });
        }
        public async Task<Result> CompleteSystemOobe(SaveSysConfig cfg)
        {
            return await DoSimplePost<ApiResponse?>(Endpoints.CompleteSystemOobe, cfg);
        }

        /// <summary>
        /// Preforms a simple GET request on requested endpoint. T is the response content type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        private async Task<Result> DoSimpleGet<T>(string endpoint)
        {
            string response;
            try
            {
                response = await Client.GetStringAsync(Endpoint + Endpoints.ApiBase + endpoint).ConfigureAwait(false);
                if (response == null)
                {
                    return Result.EmptyResponse;
                }
            }
            catch
            {
                return Result.Exception;
            }

            var jsonType = SourceGenerationContext.Default.GetTypeInfo(typeof(ApiResponseWithContent<T>));
            if (jsonType == null) return new(SecurityApiResult.InternalError, "Unknown JSON type for body: " + typeof(T).Name, null);

            ApiResponseWithContent<T>? responseJson = JsonSerializer.Deserialize(response, (JsonTypeInfo<ApiResponseWithContent<T>>)jsonType);
            if (responseJson == null)
                return Result.EmptyResponse;

            return new Result(responseJson.code, responseJson.message, responseJson.content);
        }
        /// <summary>
        /// Preforms a simple GET request on requested endpoint. T is the response content type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        private async Task<Result> DoSimpleGet(string endpoint)
        {
            string response;
            try
            {
                response = await Client.GetStringAsync(Endpoint + Endpoints.ApiBase + endpoint).ConfigureAwait(false);
                if (response == null)
                {
                    return Result.EmptyResponse;
                }
            }
            catch
            {
                return Result.Exception;
            }

            ApiResponse? responseJson = JsonSerializer.Deserialize(response, SourceGenerationContext.Default.ApiResponse);
            if (responseJson == null)
                return Result.EmptyResponse;
            Result res = new(responseJson.code, responseJson.message, null);
            if (responseJson != null)
            {
                if (responseJson.success)
                {
                    return res;
                }
                else
                {
                    return res;
                }
            }
            return res;
        }
        /// <summary>
        /// Preforms a POST request with the requested body object which gets automatically serialized, then returns the deserialized object as per type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        private async Task<Result> DoSimplePost<T>(string endpoint, object body)
        {
            try
            {
                var jsonType = SourceGenerationContext.Default.GetTypeInfo(body.GetType());
                if (jsonType == null) return new(SecurityApiResult.InternalError, "Unknown JSON type for body: " + body.GetType().Name, null);
                
                var response = await Client.PostAsync(Endpoint + Endpoints.ApiBase + endpoint, new StringContent(JsonSerializer.Serialize(body, jsonType))).ConfigureAwait(false);
                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ApiResponse? contents = JsonSerializer.Deserialize(responseStr, SourceGenerationContext.Default.ApiResponse);
                if (contents == null)
                {
                    return Result.Exception;
                }

                var jsonTypeResponse = SourceGenerationContext.Default.GetTypeInfo(typeof(T));
                if (jsonTypeResponse == null) return new(SecurityApiResult.InternalError, "Unknown JSON type for response", null);

                var contents2 = JsonSerializer.Deserialize(responseStr, jsonTypeResponse);

                Result res = new(contents.code, contents.message, contents2);

                return res;
            }
            catch
            {
                return Result.Exception;
            }
        }

        /// <summary>
        /// Preforms a PATCH request with the requested body object which gets automatically serialized, then returns the deserialized object as per type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        private async Task<Result> DoSimplePatch(string endpoint, object body)
        {
            try
            {
                var jsonType = SourceGenerationContext.Default.GetTypeInfo(body.GetType());
                if (jsonType == null) return new(SecurityApiResult.InternalError, "Unknown JSON type", null);

                var response = await Client.PatchAsync(Endpoint + Endpoints.ApiBase + endpoint, new StringContent(JsonSerializer.Serialize(body, jsonType))).ConfigureAwait(false);

                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ApiResponse? contents = JsonSerializer.Deserialize(responseStr, SourceGenerationContext.Default.ApiResponse);

                if (contents == null)
                {
                    return Result.EmptyResponse;
                }

                return new Result(contents.code, contents.message, null);
            }
            catch
            {
                return Result.Exception;
            }
        }
        private async Task<Result> DoSimpleDelete<T>(string endpoint)
        {
            try
            {
                var response = await Client.DeleteAsync(Endpoint + Endpoints.ApiBase + endpoint).ConfigureAwait(false);
                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ApiResponse? contents = JsonSerializer.Deserialize(responseStr, SourceGenerationContext.Default.ApiResponse);

                if (contents == null)
                    return Result.EmptyResponse;

                return new Result(contents.code, contents.message, null);
            }
            catch
            {
                return Result.Exception;
            }
        }

        public async Task<PlugifyWebSocketClient> OpenAnncStream(int sampleRate, int bitsPerSample, int blockAlign)
        {
            var client = new PlugifyWebSocketClient();
            string path = Endpoint.Replace("https://", "wss://") + "/mixer/audioin/ws";
            client.SetUrl(path);

            await client.Start(false);


            // send token
            var tokenBytes = Encoding.ASCII.GetBytes(Token);
            List<byte> TokenCmd = [(byte)AudioInMsgType.DoAuth, (byte)tokenBytes.Length];
            TokenCmd.AddRange(tokenBytes);

            await client.Send([.. TokenCmd]);

            // recieve auth status
            var authResult = await client.ReceiveBytes();
            if (authResult != null && (AudioInMsgType)authResult[0] != AudioInMsgType.OK)
            {
                throw new Exception("unexpected result " + (AudioInMsgType)authResult[0] + " after reading authentication response");
            }

            // open audio device
            List<byte> AudioDevCmd = [(byte)AudioInMsgType.OpenAudioDevice];
            AudioDevCmd.AddRange(BitConverter.GetBytes(sampleRate));
            AudioDevCmd.AddRange(BitConverter.GetBytes(bitsPerSample));
            AudioDevCmd.AddRange(BitConverter.GetBytes(blockAlign));

            await client.Send([.. AudioDevCmd]);

            // recieve audio device status
            var devResult = await client.ReceiveBytes();
            if (devResult != null && (AudioInMsgType)devResult[0] != AudioInMsgType.OK)
            {
                throw new Exception("unexpected result " + devResult[0] + " after reading OpenAudioDevice");
            }

            return client;
        }

        public async Task<Result> Logout()
        {
            var res = await DoSimpleGet(Endpoints.Logout);
            if (!res.IsSuccess)
                return res;
            Stop();

            return Result.Success;
        }

        public async Task<Result> UploadNextionKeypadFirmware(byte[] file)
        {
            try
            {
                MultipartFormDataContent form = new()
                {
                    { new ByteArrayContent(file), "fw", "fw" }
                };

                var contentTypeHeader = form.Headers.ContentType ?? throw new Exception("content-type header cannot be null");
                var contentType = contentTypeHeader.Parameters.First();
                contentType.Value = "multipart/form-data; " + contentType.Value;
                var response = await Client.PostAsync(Endpoint + Endpoints.ApiBase + Endpoints.UploadFirmware, form).ConfigureAwait(false);
                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ApiResponse? contents = JsonSerializer.Deserialize(responseStr, SourceGenerationContext.Default.ApiResponse);
                if (contents == null)
                {
                    return Result.EmptyResponse;
                }
                else
                {
                    return new Result(contents.code, contents.message, null);
                }
            }
            catch
            {
                return Result.Exception;
            }
        }

        public delegate void ProgressEventHandler(int progress);
        public delegate void OnSystemTimer(bool arming, int timer);
        public delegate void OnSystemUpdateProgress(FwUpdateMsg msg);
        public delegate void MusicStartedEventHandler(string fileName, bool isLive);
    }
    public class Result(SecurityApiResult result, string message, object? value)
    {
        public SecurityApiResult ResultCode = result;
        public string ResultMessage = message;
        public bool IsSuccess
        {
            get
            {
                return ResultCode == SecurityApiResult.Success;
            }
        }

        public bool IsFailure
        {
            get
            {
                return !IsSuccess;
            }
        }
        public object? Value = value;

        public static readonly Result Exception = new(SecurityApiResult.ConnectionFailed, "Network operation failed. Check internet connection.", default);
        public static readonly Result EmptyResponse = new(SecurityApiResult.ConnectionFailed, "Server returned invalid/empty response to the request", default);
        public static readonly Result Success = new(SecurityApiResult.Success, "Operation is successful", default);
    }
}
