using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    /// <summary>
    /// Sent by server whenever music is stopped
    /// </summary>
    public class MusicStopped : WebSocketMessage
    {
        public override MessageType type { get; } = MessageType.MusicStopped;
    }
}
