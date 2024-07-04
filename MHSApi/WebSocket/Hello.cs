using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    /// <summary>
    /// Sent by server whenever a client is connected
    /// </summary>
    internal class Hello : WebSocketMessage
    {
        public override MessageType type { get; } = MessageType.ServerHello;
    }
}
