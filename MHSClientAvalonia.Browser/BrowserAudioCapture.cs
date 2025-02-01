using System.Threading.Tasks;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Browser
{
    public class BrowserAudioCapture : AudioCaptureDriver
    {
        public override Task Open()
        {
            // Todo: open device and start capturing sound
            return Task.CompletedTask;
        }

        public override async Task Stop()
        {
            _shouldCapture = false;

            // TODO: Close device

            await CloseSocket();
        }
    }
}