using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    /// <summary>
    /// Sent by server whenever a new announcement starts playing
    /// </summary>
    public class AnncStarted : WebSocketMessage
    {
        public override MessageType type { get; } = MessageType.AnncStarted;
        public string? AnncFileName { get; set; }
        public bool IsLive { get; set; }

        public AnncStarted(string? filename, bool islive)
        {
            AnncFileName = filename;
            IsLive = islive;
        }

        public AnncStarted()
        {

        }
    }
}
