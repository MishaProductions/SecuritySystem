using System.IO;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Utils
{
    public abstract class AudioPlaybackDriver
    {
        public abstract Task PlayWav(Stream wavFile);
    }
}