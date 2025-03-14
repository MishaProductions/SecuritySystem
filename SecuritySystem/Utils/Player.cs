using System.Runtime.InteropServices;
using System.Text;

namespace SecuritySystem.Utils
{
    //Most code from here is taken from https://github.com/hudec117/Mpv.NET-lib- I couldnt use it directly as it does not support Linux
    public partial class Player
    {
        // /usr/lib/arm-linux-gnueabihf/libmpv.so
        public bool IsPlaying { get; private set; } = false;
        public event EndfileEventHandler? OnStop;
        public event StartfileEventHandler? OnStart;
        private nint playerHandle;
        private Task? eventLoopTask;
        private bool IsEventLoopRunning = true;
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
                MpvError hr = GetPropertyString(playerHandle, "loop-playlist", MpvFormat.String, out string vol);
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
        public delegate void StartfileEventHandler(long index);
        public Player()
        {
            if (MPVExists())
            {
                playerHandle = mpv_create();
                if (mpv_initialize(playerHandle) < 0)
                {
                    throw new Exception("failed to initialize libmpv");
                }
                Volume = 100;

                eventLoopTask = new Task(EventLoopTaskHandler);
                eventLoopTask.Start();
            }
        }
        private bool currentlyShuffled = false;

        public void UpdatePlaylistShuffle(bool shuffle)
        {
            if (shuffle && !currentlyShuffled)
            {
                DoMpvCommand("playlist-shuffle");
                currentlyShuffled = true;
            }
            else if (currentlyShuffled)
            {
                DoMpvCommand("playlist-unshuffle");
                currentlyShuffled = false;
            }
        }

        private void EventLoopTaskHandler()
        {
            try
            {
                while (IsEventLoopRunning)
                {
                    var eventPtr = mpv_wait_event(playerHandle, Timeout.Infinite);
                    if (eventPtr != nint.Zero)
                    {
                        var @event = Marshal.PtrToStructure<MpvEvent>(eventPtr);
                        if (@event.ID != MpvEventID.None)
                        {
                            //Console.WriteLine("Got message: " + @event.ID);
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
                            else if (@event.ID == MpvEventID.StartFile)
                            {
                                Console.WriteLine("mpv: start file");
                                if (@event.Data != 0)
                                {
                                    var evntdata = Marshal.PtrToStructure<MpvEventStartFile>(@event.Data);
                                    IsPlaying = true;
                                    //Console.WriteLine("index changed to " + (evntdata.PlaylistEntryId - 1));
                                    OnStart?.Invoke(evntdata.PlaylistEntryId - 1);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during event loop: " + ex.ToString());
            }
        }

        public void Play(string path)
        {
            if (MPVExists())
            {
                if (IsPlaying)
                {
                    Stop();
                }

                IsPlaying = true;
                DoMpvCommand("loadfile", path);
            }
        }
        public void PlaylistPlay(string[] paths, bool force = false, bool shuffle = false)
        {
            if (IsPlaying)
            {
                Stop();
                ClearPlaylist();
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

            currentlyShuffled = false;
            if (shuffle)
            {
                UpdatePlaylistShuffle(true);
            }

            mpv_set_property_string(playerHandle, "pause", "no");
            DoMpvCommand("playlist-play-index", "0");
        }

        public string GetPlaylistFileNameByIndex(long idx)
        {
            MpvError hr = GetPropertyString(playerHandle, "playlist/" + idx + "/filename", MpvFormat.String, out string vol);
            if (hr != MpvError.Success)
            {
                return $"failed to get playlist filename property of {idx}: {hr}";
            }
            return vol;
        }

        public void ClearPlaylist()
        {
            Console.WriteLine(DoMpvCommand("playlist-clear"));
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
            if (MPVExists())
            {
                var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out nint[] byteArrayPointers);
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

        private static MpvError GetPropertyString(nint mpvHandle, string name, MpvFormat format, out string stringg)
        {
            var error = mpv_get_property(mpvHandle, name, format, out IntPtr ptr);
            if (error != MpvError.Success || ptr == IntPtr.Zero)
            {
                stringg = "";
                return error;
            }

            var result = Marshal.PtrToStringUTF8(ptr);
            if (result == null) stringg = "";
            else stringg = result;

            // free the MPV string
            mpv_free(ptr);

            return error;
        }

        private static bool MPVExists()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(LibmpvPath);
        }

        public const string LibmpvPath = "/usr/lib/arm-linux-gnueabihf/libmpv.so";

        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial nint mpv_create();
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial nint mpv_free(nint handle);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_initialize(nint mpvHandle);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_command(nint mpvHandle, nint strings);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_get_property(nint mpvHandle, string name, MpvFormat format, out double data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_get_property(nint mpvHandle, string name, MpvFormat format, out long data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_get_property(nint mpvHandle, string name, MpvFormat format, out IntPtr data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_set_property(nint mpvHandle, string name, MpvFormat format, ref double data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_set_property(nint mpvHandle, string name, MpvFormat format, ref long data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial MpvError mpv_set_property_string(nint mpvHandle, string name, string data);
        [LibraryImport(LibmpvPath, StringMarshalling = StringMarshalling.Utf8)]
        private static partial nint mpv_wait_event(nint mpvHandle, double timeout);
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
                default:
                    break;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct MpvEventStartFile
    {
        public long PlaylistEntryId;
    }
}
