using System.Threading.Tasks;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Android
{
    public class AndroidAudioCapture : AudioCaptureDriver
    {
        public override void Open()
        {
            // Todo: open device and start capturing sound
        }

        public override async Task Stop()
        {
            _shouldCapture = false;

            // TODO: Close device

            await CloseSocket();
        }
    }
}