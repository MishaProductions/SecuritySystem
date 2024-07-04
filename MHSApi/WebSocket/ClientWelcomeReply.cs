using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace MHSApi.WebSocket
{
    public class ClientWelcomeReply : WebSocketMessage
    {
        public override MessageType type => MessageType.ClientWelcomeReply;
        public string Token { get; set; } = "";

        public ClientWelcomeReply() { }
        public ClientWelcomeReply(string token)
        {
            Token = token;
        }
    }
}
