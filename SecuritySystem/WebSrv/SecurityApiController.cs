using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using MHSApi.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecuritySystem.Utils;
using SecuritySystemApi;
using System.Device.Gpio;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        public const string AuthEndpoint = "/api/auth/login";
        private static readonly Random random = new();
        [Route(HttpVerbs.Post, Endpoints.Arm)]
        public async Task ArmSystem()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            RequestWithCode c = await ParseRequestJson<RequestWithCode>();

            if (!ZoneController.IsReady)
            {
                await SendGenericResponse(SecurityApiResult.NotReady);
                return;
            }

            if (Configuration.CheckIfCodeCorrect(c.code))
            {
                await SendSuccessfulResponseWithCustomMessage($"Success. You have {Configuration.Instance.Timer} seconds to leave");
            }
            else
            {
                await SendGenericResponse(SecurityApiResult.IncorrectCode);
                return;
            }
            Configuration.Instance.Timer = 15;
            Configuration.Instance.InExitDelay = true;
            Configuration.Instance.SystemArmed = true;
        }
        [Route(HttpVerbs.Post, Endpoints.Disarm)]
        public async Task DisarmSystem()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            RequestWithCode c = await ParseRequestJson<RequestWithCode>();

            if (Configuration.CheckIfCodeCorrect(c.code))
            {
                await SendGenericResponse(SecurityApiResult.Success);
                SystemManager.DisarmSystem();
                Console.WriteLine("system disarmed");
            }
            else
            {
                await SendGenericResponse(SecurityApiResult.IncorrectCode);
                Console.WriteLine("incorrect code");
            }
        }

        #region System apis
        [Route(HttpVerbs.Get, Endpoints.SystemStatus)]
        public async Task GetSystemStatus()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            await SendSuccessfulResponseWithContent(new SystemStatusResponse(Configuration.Instance.SystemArmed, Configuration.Instance.Timer, Configuration.Instance.InEntryDelay, Configuration.Instance.InExitDelay));
        }
        [Route(HttpVerbs.Get, "/system/info")]
        public async Task GetInfo()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            await SendSuccessfulResponseWithContent(new SystemInfoResponse("Security System (MHS-1000P)", new DateTime(Builtin.CompileTime)));
        }
        [Route(HttpVerbs.Get, Endpoints.GetAlarmHistory)]
        public async Task ReadAlarmHistory()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            List<AlarmHistoryInfoContent> array = [];
            foreach (var item in Configuration.Instance.AlarmHistory.Reverse())
            {
                AlarmHistoryInfoContent alarm = new()
                {
                    date = item.Key.ToString(),
                    zone = item.Value
                };
                array.Add(alarm);
            }
            await SendSuccessfulResponseWithContent(array.ToArray());
        }
        [Route(HttpVerbs.Get, Endpoints.SystemEventLog)]
        public async Task ReadSystemEventLog()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            await SendSuccessfulResponseWithContent(Configuration.Instance.EventLog.ToArray());
        }
        #endregion
        #region Weather
        [Route(HttpVerbs.Get, Endpoints.QueryWeatherShort)]
        public async Task QueryWeatherShort()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            await SendSuccessfulResponseWithContent(new ShortWeatherDataContent(await WeatherService.GetWeather()));
        }
        #endregion
        #region Zones

        [Route(HttpVerbs.Get, "/zones/status")]
        public async Task GetZoneStatus()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            var result = new JsonZones();
            result.zones = new JsonZone[Configuration.Instance.Zones.Count];

            int i = 0;
            foreach (var item in Configuration.Instance.Zones)
            {
                JsonZoneWithReady obj = new()
                {
                    idx = item.Value.ZoneNumber,
                    name = item.Value.Name,
                    type = item.Value.Type,
                    ready = ZoneController.ZoneStates[item.Key] == PinValue.Low
                };
                result.zones[i] = obj;
            }

            await SendSuccessfulResponseWithContent(result);
        }
        #endregion
    }
}