using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public record MusicState(int MusicVolume, int AnnouncementVolume, string? CurrentlyPlayingMusic, string? CurrentlyPlayingAnnouncement);
}
