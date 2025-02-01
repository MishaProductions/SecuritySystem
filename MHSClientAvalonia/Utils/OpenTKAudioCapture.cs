using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Client;
using OpenTK.Audio.OpenAL;

namespace MHSClientAvalonia.Utils
{
    public class OpenTKAudioCapture : AudioCaptureDriver
    {
        private ALCaptureDevice _captureDevice;
        public override async Task Stop()
        {
            _shouldCapture = false;
            ALC.CaptureStop(_captureDevice);

            ALC.CaptureCloseDevice(_captureDevice);

            await CloseSocket();
        }
        public async void CapturingThread()
        {
            while (_shouldCapture)
            {
                if (_audioOutSocket == null)
                    break;

                int samplesAvailable = ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples);

                if (samplesAvailable >= 2000)
                {
                    ALC.CaptureSamples(_captureDevice, buffer, samplesAvailable);

                    byte[] cmd = new byte[1 + samplesAvailable * 2];
                    cmd[0] = (byte)AudioInMsgType.WritePcm;
                    Array.Copy(buffer, 0, cmd, 1, samplesAvailable * 2);

                    try
                    {
                        await _audioOutSocket.Send(cmd);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        return;
                    }
                    Debug.WriteLine("Sent " + (samplesAvailable * 2) + " bytes");
                }
            }
        }

        public override void Open()
        {
            _captureDevice = ALC.CaptureOpenDevice(null, 44100, ALFormat.Mono16, 50);//opens default mic //null specifies default 
            ALC.CaptureStart(_captureDevice);
            _shouldCapture = true;
            new Thread(CapturingThread).Start();
        }

    }
}