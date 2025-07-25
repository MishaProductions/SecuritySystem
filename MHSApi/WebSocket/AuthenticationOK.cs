﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class AuthenticationOK : WebSocketMessage
    {
        public override MessageType type { get { return MessageType.AuthOK; } }
        public SystemStateChange State { get; set; } = new();
        public ZoneUpdate Zones { get; set; } = new();
        public MusicState MusicState { get; set; } = new(100, 100, null, null);
        public AuthenticationOK() { }
        public AuthenticationOK(SystemStateChange state, ZoneUpdate zones, MusicState musicState) { State = state; Zones = zones; MusicState = musicState; }
    }
}
