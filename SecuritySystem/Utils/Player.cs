using System.Runtime.InteropServices;
using System.Text;

namespace SecuritySystem.Utils
{
    //Most code from here is taken from https://github.com/hudec117/Mpv.NET-lib- I couldnt use it directly as it does not support Linux
    public class Player
    {
        // /usr/lib/arm-linux-gnueabihf/libmpv.so
        public bool IsPlaying { get; private set; } = false;
        public event EndfileEventHandler? OnStop;
        private nint playerHandle;
        Task eventLoopTask;
        bool IsEventLoopRunning = true;
        public long PlaylistIndex
        {
            get
            {
                MpvError hr = mpv_get_property(playerHandle, "playlist-pos", MpvFormat.Int64, out long idx);
                if (hr != MpvError.Success)
                {
                    throw new Exception("failed to get playlist position property");
                }
                return idx;
            }
            set
            {
                var hr = mpv_set_property(playerHandle, "playlist-pos", MpvFormat.Int64, ref value);
                if (hr != MpvError.Success)
                {
                    throw new Exception("failed to set playlist position property");
                }
            }
        }

        public double Volume
        {
            get
            {
                double vol = 0; ;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    MpvError hr = mpv_get_property(playerHandle, "volume", MpvFormat.Double, out vol);
                    if (hr != MpvError.Success)
                    {
                        throw new Exception("failed to get volume property");
                    }
                }
                return vol;
            }
            set
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var hr = mpv_set_property(playerHandle, "volume", MpvFormat.Double, ref value);
                    if (hr != MpvError.Success)
                    {
                        throw new Exception("failed to set volume property");
                    }
                }
            }
        }
        public bool LoopPlaylist
        {
            get
            {
                MpvError hr = mpv_get_property(playerHandle, "loop-playlist", MpvFormat.String, out string vol);
                if (hr != MpvError.Success)
                {
                    throw new Exception("failed to get loop playlist property");
                }
                return vol != "no";
            }
            set
            {
                var stringValue = value ? "inf" : "no";
                var hr = mpv_set_property_string(playerHandle, "loop-playlist", stringValue);
                if (hr != MpvError.Success)
                {
                    throw new Exception("failed to set loop playlist property");
                }
            }
        }
        public delegate void EndfileEventHandler(MpvEventEndFile data);
        public Player()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                playerHandle = mpv_create();
                if (mpv_initialize(playerHandle) < 0)
                {
                    throw new Exception("failed to initialize libmpv");
                }
            }
            Volume = 100;

            eventLoopTask = new Task(EventLoopTaskHandler);
            eventLoopTask.Start();
        }

        private void EventLoopTaskHandler()
        {
            while (IsEventLoopRunning)
            {
                var eventPtr = mpv_wait_event(playerHandle, Timeout.Infinite);
                if (eventPtr != nint.Zero)
                {
                    var @event = Marshal.PtrToStructure<MpvEvent>(eventPtr);
                    if (@event.ID != MpvEventID.None)
                    {
                        if (@event.ID == MpvEventID.EndFile)
                        {
                            Console.WriteLine("mpv: end of file");
                            if (@event.Data != 0)
                            {
                                var evntdata = Marshal.PtrToStructure<MpvEventEndFile>(@event.Data);
                                if (evntdata.Reason == MpvEndFileReason.EndOfFile)
                                {
                                    IsPlaying = false;
                                    OnStop?.Invoke(evntdata);
                                }
                            }

                        }
                        else
                        {
                            //Console.WriteLine("unknown event: " + @event.ID);
                        }
                    }
                }
            }
        }

        public void Play(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (IsPlaying)
                {
                    Stop();
                }

                IsPlaying = true;
                DoMpvCommand("loadfile", path);
            }
        }
        public void PlaylistPlay(string[] paths, bool force = false)
        {
            if (IsPlaying)
            {
                Stop();

            }
            IsPlaying = true;
            Console.WriteLine(mpv_set_property_string(playerHandle, "pause", "no"));
            bool first = true;

            foreach (string path in paths)
            {
                var loadMethod = LoadMethod.Append;

                if (first && (!force || !IsPlaying))
                    loadMethod = LoadMethod.Replace;

                var loadMethodString = LoadMethodHelper.ToString(loadMethod);
                Console.WriteLine(loadMethodString);
                DoMpvCommand("loadfile", path, loadMethodString);

                first = false;
            }
            mpv_set_property_string(playerHandle, "pause", "no");
            DoMpvCommand("playlist-play-index", "0");
        }

        internal void Stop()
        {
            DoMpvCommand("stop");
            IsPlaying = false;
        }
        public void PlaylistBack()
        {
            Console.WriteLine("PlaylistBack()");
            DoMpvCommand("playlist-prev");
        }

        public void PlaylistNext()
        {
            Console.WriteLine("PlaylistNext()");
            DoMpvCommand("playlist-next");
        }
        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }
        public static nint AllocateUtf8IntPtrArrayWithSentinel(string[] arr, out nint[] byteArrayPointers)
        {
            int numberOfStrings = arr.Length + 1; // add extra element for extra null pointer last (sentinel)
            byteArrayPointers = new nint[numberOfStrings];
            nint rootPointer = Marshal.AllocCoTaskMem(nint.Size * numberOfStrings);
            for (int index = 0; index < arr.Length; index++)
            {
                var bytes = GetUtf8Bytes(arr[index]);
                nint unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                byteArrayPointers[index] = unmanagedPointer;
            }
            Marshal.Copy(byteArrayPointers, 0, rootPointer, numberOfStrings);
            return rootPointer;
        }
        private MpvError DoMpvCommand(params string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                nint[] byteArrayPointers;
                var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out byteArrayPointers);
                var result = mpv_command(playerHandle, mainPtr);
                foreach (var ptr in byteArrayPointers)
                {
                    Marshal.FreeHGlobal(ptr);
                }
                Marshal.FreeHGlobal(mainPtr);
                return result;
            }
            else
            {
                return MpvError.InvalidParameter;
            }
        }

        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern nint mpv_create();
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_initialize(nint mpvHandle);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_command(nint mpvHandle, nint strings);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_get_property(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format, out double data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_get_property(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format, out long data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_get_property(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format, [MarshalAs(UnmanagedType.LPStr)] out string data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_set_property(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format, ref double data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_set_property(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format, ref long data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern MpvError mpv_set_property_string(nint mpvHandle, [MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.LPStr)] string data);
        [DllImport("/usr/lib/arm-linux-gnueabihf/libmpv.so")]
        private static extern nint mpv_wait_event(nint mpvHandle, double timeout);
    }
    public enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
        NodeArray = 7,
        NodeMap = 8,
        ByteArray = 9
    }
    public enum MpvEventID
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        GetPropertyReply = 3,
        SetPropertyReply = 4,
        CommandReply = 5,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        TracksChanged = 9,
        TrackSwitched = 10,
        Idle = 11,
        Pause = 12,
        Unpause = 13,
        Tick = 14,
        ScriptInputDispatch = 15,
        ClientMessage = 16,
        VideoReconfig = 17,
        AudioReconfig = 18,
        MetadataUpdate = 19,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22,
        ChapterChange = 23,
        QueueOverflow = 24
    }
    public enum MpvError
    {
        Success = 0,
        EventQueueFull = -1,
        NoMem = -2,
        Uninitialised = -3,
        InvalidParameter = -4,
        OptionNotFound = -5,
        OptionFormat = -6,
        OptionError = -7,
        PropertyNotFound = -8,
        PropertyFormat = -9,
        PropertyUnavailable = -10,
        PropertyError = -11,
        Command = -12,
        LoadingFailed = -13,
        AoInitFailed = -14,
        VoInitFailed = -15,
        NothingToPlay = -16,
        UnknownFormat = -17,
        Unsupported = -18,
        NotImplemented = -19,
        Generic = -20
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MpvEvent
    {
        public MpvEventID ID;

        public MpvError Error;

        public ulong ReplyUserData;

        public nint Data;
    }
    public enum LoadMethod
    {
        /// <summary>
        /// Stop playback of current media and start new one.
        /// </summary>
        Replace,

        /// <summary>
        /// Append media to playlist.
        /// </summary>
        Append,

        /// <summary>
        /// Append media to playlist and play if nothing else is playing.
        /// </summary>
        AppendPlay
    }
    internal static class LoadMethodHelper
    {
        public static string ToString(LoadMethod loadMethod)
        {
            switch (loadMethod)
            {
                case LoadMethod.Replace:
                    return "replace";
                case LoadMethod.Append:
                    return "append";
                case LoadMethod.AppendPlay:
                    return "append-play";
            }

            throw new ArgumentException("Invalid enumeration value.");
        }
    }
    public enum MpvEndFileReason
    {
        EndOfFile = 0,
        Stop = 2,
        Quit = 3,
        Error = 4,
        Redirect = 5
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MpvEventEndFile
    {
        public MpvEndFileReason Reason;

        public MpvError Error;
    }
}
