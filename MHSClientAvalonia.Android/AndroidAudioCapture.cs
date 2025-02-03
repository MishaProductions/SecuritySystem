using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.Content.PM;
using Android.Media;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Android
{
    public class AndroidAudioCapture(MainActivity activity) : AudioCaptureDriver
    {
        private readonly AudioRecord recorder = new(AudioSource.Mic, 44100, ChannelIn.Mono, Encoding.Pcm16bit, 4096);
        private readonly MainActivity _activity = activity;

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

            if (ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.RecordAudio) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(_activity, [Manifest.Permission.RecordAudio], 1);
            }

            if (ContextCompat.CheckSelfPermission(_activity, Manifest.Permission.RecordAudio) != Permission.Granted)
            {
                // it is still not granted, throw exception

                _audioOutSocket?.Close();
                _audioOutSocket = null;

                throw new Exception("Android Record Audio/Microphone permission is required");
            }


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