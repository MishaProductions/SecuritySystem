using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public enum MessageType
    {
        None = 0,

        // Authentication process
        ServerHello = 1, // sent by server
        ClientWelcomeReply = 2, // sent by client
        AuthError = 3, // sent by server
        AuthOK = 4, // sent by server

        // Security system core
        SystemStateChange = 8,
        ZoneUpdate = 9,

        // Music Manager
        MusicVolumeChange = 10,
        AnncVolumeChange = 11,
        MusicStarted = 13,
        MusicStopped = 14,

        // Devices
        FwUpdate = 12,
    }
}
