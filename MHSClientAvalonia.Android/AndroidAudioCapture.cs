using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Android
{
    public class AndroidAudioCapture : AudioCaptureDriver
    {
        private AudioRecord recorder = new AudioRecord(AudioSource.Mic, 44100, ChannelIn.Mono, Encoding.Pcm16bit, 4096);

        public async void CapturingThread()
        {
            while (_shouldCapture)
            {
                if (_audioOutSocket == null)
                    break;

                var byteCount = recorder.Read(buffer, 0, 4096);

                Debug.WriteLine("read " + byteCount + " bytes from android microphone");

                byte[] cmd = new byte[1 + byteCount];
                cmd[0] = (byte)AudioInMsgType.WritePcm;
                Array.Copy(buffer, 0, cmd, 1, byteCount);

                try
                {
                    await _audioOutSocket.Send(cmd);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return;
                }
            }
        }

        public override Task Open()
        {
            _shouldCapture = true;
            

            // open audio device
            recorder.StartRecording();

            // start capture thread
            new Thread(CapturingThread).Start();

            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            _shouldCapture = false;

            recorder.Stop();

            await CloseSocket();
        }
    }
}