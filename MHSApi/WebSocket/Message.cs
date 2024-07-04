using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public abstract class WebSocketMessage
    {
        public abstract MessageType type { get; }
    }
}
