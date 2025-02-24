﻿using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using HttpMultipartParser;
using MHSApi.API;
using Newtonsoft.Json.Linq;
using SecuritySystem.Modules;
using SecuritySystem.Utils;
using System.Text;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        [Route(HttpVerbs.Post, Endpoints.UploadFirmware)]
        public async Task UpdateKeypadFirmware()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            var parser = await MultipartFormDataParser.ParseAsync(Request.InputStream);
            byte[]? fw = null;
            Console.WriteLine("parser found:" + parser.Files.Count);
            foreach (var file in parser.Files)
            {
                Console.WriteLine("file:" + file.Name);
                if (file.Name == "fw")
                {
                    SystemManager.WriteToEventLog("User has began nextion keypad firmware update", currentUser);

                    MemoryStream ms = new();
                    file.Data.CopyTo(ms);
                    fw = ms.ToArray();
                    Console.WriteLine(fw[0]);
                }
            }

            if (fw == null)
            {
                await SendGenericResponse(SecurityApiResult.WrongFirmware);
            }
            else
            {

                var disp = ModuleController.GetDisplays();
                if (disp.Length > 0)
                {
                    // todo don't hardcode index
                    await SendGenericResponse(SecurityApiResult.Success);

                    disp[0].UpdateFirmware(fw);
                    return;
                }


                await SendGenericResponse(SecurityApiResult.NoDevicesToUpdate);
            }

        }
        [Route(HttpVerbs.Get, Endpoints.FirmwareUpdateStatus)]
        public async Task GetUpdProgress()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            if (currentUser.Permissions != UserPermissions.Admin)
            {
                await SendGenericResponse(SecurityApiResult.NoPermission);
                return;
            }

            // todo don't hardcode index

            var disp = ModuleController.GetDisplays();
            if (disp.Length > 0)
            {
                // todo don't hardcode index
                var display = disp[0];

                FirmwareUpdateProgressResponseContent result = new()
                {
                    fail = display.UpdateFail,
                    progress = display.UpdateProgress,
                    progressstring = display.UpdateProgressString,
                    finished = display.UpdateFinish,
                    inprogress = display.UpdateInProgress
                };
                await SendSuccessfulResponseWithContent(result);
                return;
            }


            await SendGenericResponse(SecurityApiResult.NoDevicesToUpdate);
        }

        [Route(HttpVerbs.Get, Endpoints.QueryClientUpdateInfo)]
        public async Task GetWindowsClientVersionV2Windows()
        {
            await SendSuccessfulResponseWithContent(JObject.Parse(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/www/client/mhsclientversion.json")));
        }
    }
}
