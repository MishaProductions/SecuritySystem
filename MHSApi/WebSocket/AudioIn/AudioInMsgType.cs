using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket.AudioIn
{
    public enum AudioInMsgType
    {
        DoAuth = 1,
        OpenAudioDevice = 2,
        WritePcm = 3,
        CloseAudioDevice = 4,

        OK = 101,
        NoAuth = 102,
        AuthFail = 103,
        CmdError = 104,
        LineBusy = 105,
    }
}
