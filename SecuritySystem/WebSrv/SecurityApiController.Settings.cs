using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using MHSApi.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecuritySystem.Modules;
using SecuritySystem.Utils;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        [Route(HttpVerbs.Get, Endpoints.NotificationSettings)]
        public async Task GetNotificationSettings()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            await SendSuccessfulResponseWithContent(new NotificationSettings(Configuration.Instance.SmtpEnabled, Configuration.Instance.SmtpSendTo, Configuration.Instance.SmtpHost, Configuration.Instance.SmtpUsername, "", Configuration.Instance.NotificationLevel));
        }
        [Route(HttpVerbs.Post, Endpoints.NotificationSettings)]
        public async Task SetNotificationSettings()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            var data = await ParseRequestJson<NotificationSettings>();


            Configuration.Instance.SmtpEnabled = data.smtpEnabled;
            Configuration.Instance.SmtpSendTo = data.smtpSendTo;
            Configuration.Instance.NotificationLevel = data.notificationLevel;
            Configuration.Instance.SmtpHost = data.smtpHost;
            Configuration.Instance.SmtpUsername = data.smtpUsername;
            if (!string.IsNullOrEmpty(data.smtpPassword))
                Configuration.Instance.SmtpPassword = data.smtpPassword;
            Configuration.Save();

            await SendGenericResponse(SecurityApiResult.Success);
        }
        [Route(HttpVerbs.Post, Endpoints.NotificationsTest)]
        public async Task TestNotificationSettings()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            var json = await HttpContext.GetRequestBodyAsStringAsync();
            Console.WriteLine("Sending test email");

            try
            {
                MailClass.SendMailNonThreaded($"If this was not you, take steps. Test email sent from security system.<br><small>Generated on {DateTime.Now}</small>", $"Someone accessed your security system and clicked on test email button.", false);
            }
            catch (Exception ex)
            {
                await SendUnSuccessfulResponseWithCustomMessage(ex.ToString());
                return;
            }
            await SendGenericResponse(SecurityApiResult.Success);
        }
        [Route(HttpVerbs.Post, Endpoints.ZoneSettings)]
        public async Task ModifyZones()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            var data = await ParseRequestJson<JsonZones>();

            //We need to convert the JsonZones into proper zone class
            Dictionary<int, Zone> newZones = new();
            int i = 0;
            foreach (var item in data.zones)
            {
                newZones.Add(i, new Zone() { Name = item.name, ZoneNumber = item.idx, Type = (ZoneType)item.type });
                i++;
            }

            Configuration.Instance.Zones = newZones;
            Configuration.Save();

            SystemManager.SendZoneUpdateSingleToAll(false, 0, "", Modules.NXDisplay.ZoneState.Unconfigured);

            await SendGenericResponse(SecurityApiResult.Success);
        }


        [Route(HttpVerbs.Post, Endpoints.CompleteSystemOobe)]
        public async Task CompleteSystemOobe()
        {
            if (Configuration.Instance.SystemSetUp)
            {
                // we cannot reconfigure system again
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            // do not check user token because its a dummy user anyways
            var data = await ParseRequestJson<SaveSysConfig>();

            // do basic validation
            if (string.IsNullOrEmpty(data.NewUsername) || string.IsNullOrEmpty(data.NewPassword) || data.NewUsername == "System Installer")
            {
                await SendGenericResponse(SecurityApiResult.BadRequest);
                return;
            }

            Configuration.Instance.Users.Clear();
            Configuration.Instance.Tokens.Clear();
            Configuration.Instance.UseOrangePiDriver = data.IsOrangePiDriver;
            Configuration.Instance.Users.Add(new User() { Username = data.NewUsername, PasswordHash = Sha256(data.NewPassword), Permissions = UserPermissions.Admin, ID = -1 });
            Configuration.Instance.SystemSetUp = true; // once this is set system gets started fully
            Configuration.Save();

            await SendGenericResponse(SecurityApiResult.Success);
        }
    }
}
