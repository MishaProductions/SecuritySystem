using System;
using System.Threading.Tasks;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Client;

namespace MHSClientAvalonia.Utils
{
    public abstract class AudioCaptureDriver
    {
        public PlugifyWebSocketClient? _audioOutSocket;
        public bool _shouldCapture = false;
        public byte[] buffer = new byte[100 * 41000];

        public abstract Task Open();
        public abstract Task Stop();

        protected async Task CloseSocket()
        {
            if (_audioOutSocket != null)
            {
                try
                {
                    if (_audioOutSocket.IsOpen)
                    {
                        await _audioOutSocket.Send([(byte)AudioInMsgType.CloseAudioDevice]);
                    }


                    _audioOutSocket.Close();
                    _audioOutSocket = null;
                }
                catch
                {
                    _audioOutSocket = null;
                }
            }
        }
    }
}