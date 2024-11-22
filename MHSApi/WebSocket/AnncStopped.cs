using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    /// <summary>
    /// Sent by server whenever a new announcement starts playing
    /// </summary>
    public class AnncStopped : WebSocketMessage
    {
        public override MessageType type { get; } = MessageType.AnncStopped;
    }
}
