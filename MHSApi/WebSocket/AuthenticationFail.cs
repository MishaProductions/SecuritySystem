using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class AuthenticationFail : WebSocketMessage
    {
        public override MessageType type { get { return MessageType.AuthError; } }
        public string Message { get; set; } = "";
        public AuthenticationFail()
        {

        }

        public AuthenticationFail(string message)
        {
            Message = message;
        }
    }
}
