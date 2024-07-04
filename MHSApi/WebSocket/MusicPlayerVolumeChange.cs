using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class MusicPlayerVolumeChange : WebSocketMessage
    {
        public override MessageType type => MessageType.MusicVolumeChange;

        public int MusicVolume { get; set; }

        public MusicPlayerVolumeChange() { }
        public MusicPlayerVolumeChange(int volume) { MusicVolume = volume; }
    }


    public class AnncPlayerVolumeChange : WebSocketMessage
    {
        public override MessageType type => MessageType.AnncVolumeChange;

        public int AnncVolume { get; set; }

        public AnncPlayerVolumeChange() { }
        public AnncPlayerVolumeChange(int volume) { AnncVolume = volume; }
    }
}
