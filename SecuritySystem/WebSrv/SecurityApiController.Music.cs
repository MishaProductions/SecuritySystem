using EmbedIO.Routing;
using EmbedIO;
using EmbedIO.WebApi;
using SecuritySystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MHSApi.API;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        [Route(HttpVerbs.Get, "/music/list")]
        public async Task ListMusicEntities()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;
            MusicPlayer.ScanFiles();
            MusicListResponse resp = new();

            foreach (var item in MusicPlayer.MusicFiles)
            {
                resp.Music.Add(new MusicListEntity(item));
            }
            foreach (var item in MusicPlayer.AnncFiles)
            {
                resp.Annoucements.Add(new MusicListEntity(item));
            }

            await SendSuccessfulResponseWithContent(resp);
        }

        [Route(HttpVerbs.Post, "/music/startannc")]
        public async Task PlayAnnc()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            var data = await ParseRequestJson<StartAnncRequest>();
            if (MusicPlayer.AnncFiles.Contains(data.AnncFileName))
            {
                MusicPlayer.PlayAnnc(MusicPlayer.AnncFiles.IndexOf(data.AnncFileName));
                await SendGenericResponse(SecurityApiResult.Success);
            }
            else
            {
                await SendGenericResponse(SecurityApiResult.FileNotFound);
            }
        }

        [Route(HttpVerbs.Post, "/music/startmusic")]
        public async Task PlayMusic()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            var data = await ParseRequestJson<StartAnncRequest>();
            if (MusicPlayer.MusicFiles.Contains(data.AnncFileName))
            {
                MusicPlayer.PlayMusic(MusicPlayer.MusicFiles.IndexOf(data.AnncFileName));
                await SendGenericResponse(SecurityApiResult.Success);
            }
            else
            {
                await SendGenericResponse(SecurityApiResult.FileNotFound);
            }
        }


        [Route(HttpVerbs.Post, "/music/stopmusic")]
        public async Task StopMusic()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            MusicPlayer.StopMusic();
            await SendGenericResponse(SecurityApiResult.Success);
        }

        [Route(HttpVerbs.Post, "/music/stopannc")]
        public async Task StopAnnc()
        {
            User? currentUser = await GetUserFromToken();
            if (currentUser == null) return;

            MusicPlayer.StopAnnc();
            await SendGenericResponse(SecurityApiResult.Success);
        }
    }
}
