using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    /// <summary>
    /// Sent by server whenever a new music file starts playing
    /// </summary>
    public class MusicStarted : WebSocketMessage
    {
        public override MessageType type { get; } = MessageType.MusicStarted;
        public string MusicFileName { get; set; }
    }
}
