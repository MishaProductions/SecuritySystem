namespace SecuritySystem.Utils
{
    public static class MusicPlayer
    {
        public static readonly List<string> MusicFiles = new();
        public static readonly List<string> AnncFiles = new();

        public static bool MusicPlaying { get { return musicProc.IsPlaying; } }
        public static bool AnncPlaying { get { return anncProc.IsPlaying; } }

        public static readonly Player musicProc = new();
        public static readonly Player anncProc = new();
        private static readonly Player alarmProc = new();
        private static readonly Player alarmAnncProc = new();
        private static readonly Player anncSfx = new();
        public static bool PlaylistMode { get; private set; }
        public static string CurrentSongName
        {
            get
            {
                if (PlaylistMode)
                {
                    return MusicFiles[(int)musicProc.PlaylistIndex];
                }

                if (MusicPlaying)
                {
                    return MusicFiles[MusicIdx];
                }
                else
                {
                    return "None";
                }
            }
        }
        public static int Anncvol
        {
            get
            {
                return (int)anncProc.Volume;
            }
            set
            {

                if (anncProc.Volume != value)
                {
                    anncProc.Volume = value;
                    OnAnncVolumeChanged?.Invoke(null, new EventArgs());
                }
                anncProc.Volume = value;
                anncSfx.Volume = value;
            }
        }
        public static int MusicVol
        {
            get
            {
                return (int)musicProc.Volume;
            }
            set
            {
                if (musicProc.Volume != value)
                {
                    musicProc.Volume = value;
                    OnMusicVolumeChanged?.Invoke(null, new EventArgs());
                }
                musicProc.Volume = value;
            }
        }
        private static int MusicFadeTime = 50;

        public static event EventHandler? OnMusicStop;
        public static event EventHandler? OnAnncStop;

        public static event EventHandler? OnMusicVolumeChanged;
        public static event EventHandler? OnAnncVolumeChanged;
        private static int MusicIdx = 0;
        static MusicPlayer()
        {
            musicProc.OnStop += MusicProc_OnStop;
            anncProc.OnStop += AnncProc_OnStop;

            // arlarm function
            alarmProc.OnStop += AlarmProc_OnStop;
            //alarmProc.Volume = 10; // debugging only

            SystemManager.OnAlarm += SystemManager_OnAlarm;
            SystemManager.OnSystemDisarm += SystemManager_OnSystemDisarm;

            MusicPlayer.ScanFiles();
        }

        private static void SystemManager_OnSystemDisarm(object? sender, EventArgs e)
        {
            Console.WriteLine("MusicPlayer: system disarm detection");
            Configuration.Instance.SystemAlarmState = false; // HACK
            alarmAnncProc.Stop();
            alarmProc.Stop();
        }

        private static void SystemManager_OnAlarm(int alarmZone)
        {
            alarmAnncProc.Play("/musics/annc/tts_onalarm.mp3");
            alarmAnncProc.OnStop += delegate (MpvEventEndFile data)
            {
                alarmAnncProc.Stop();
                Console.WriteLine("start alarm seq");
                alarmProc.Play("/musics/annc/sys_alarm.mp3");
            };
        }

        private static void AlarmProc_OnStop(MpvEventEndFile data)
        {
            if (Configuration.Instance.SystemAlarmState)
            {
                Console.WriteLine("restart alarm");
                alarmProc.Play("/musics/annc/sys_alarm.mp3");
            }
        }

        internal static void ScanFiles()
        {
            MusicFiles.Clear();
            AnncFiles.Clear();

            if (Directory.Exists("/musics/"))
            {
                List<string> items = [];
                foreach (var item in Directory.GetFiles("/musics/"))
                {
                    var name = Path.GetFileName(item);
                    if (name != "upload.bat" && !name.EndsWith(".jpg"))
                        items.Add(name);
                }
                items = items.OrderBy(q => q).ToList();
                foreach (var item in items)
                {
                    MusicFiles.Add(item);
                }
            }

            if (Directory.Exists("/musics/annc/"))
            {
                List<string> items = [];
                foreach (var item in Directory.GetFiles("/musics/annc/"))
                {
                    var name = Path.GetFileName(item);
                    if (name != "upload.bat")
                        items.Add(Path.GetFileName(item));
                }
                items = items.OrderBy(q => q).ToList();
                foreach (var item in items)
                {
                    AnncFiles.Add(item);
                }
            }
        }
        private static void AnncProc_OnStop(MpvEventEndFile data)
        {
            OnAnncStop?.Invoke(null, new());

            // If music was faded, set it back to normal volume
            if (FadeBackMusic && MusicPlaying)
            {
                FadeBackMusic = false;

                if (MusicPrevVol != MusicVol)
                {
                    if (MusicPrevVol > MusicVol)
                    {
                        // fade it back now
                        Thread t = new(delegate ()
                        {
                            if (IsFadingMusic)
                            {
                                // we are already fading or something, ignore the annc request
                            }
                            else
                            {
                                IsFadingMusic = true;
                                while (MusicVol != MusicPrevVol)
                                {
                                    MusicVol += 1;
                                    Thread.Sleep(MusicFadeTime);
                                }

                                IsFadingMusic = false;
                                FadeBackMusic = false;
                                Console.WriteLine("music: fade in ended");
                            }
                        });
                        t.Start();
                    }
                    else
                    {
                        Console.WriteLine("music: after playing annc, music vol changed.");
                    }
                }
            }
        }
        private static void MusicProc_OnStop(MpvEventEndFile data)
        {
            Console.WriteLine("MusicProc_OnStop with " + data.Reason);
            OnMusicStop?.Invoke(null, new());
        }
        internal static void PlayAllMusic(List<string> paths)
        {
            PlaylistMode = true;
            string[] strings = new string[paths.Count];
            for (int i = 0; i < strings.Length i++)
            {
                strings[i] = "/musics/" + paths[i];
                Console.WriteLine(strings[i]);
            }
            musicProc.PlaylistPlay(strings, true);
        }
        internal static void PlayMusic(int a)
        {
            if (MusicPlaying)
            {
                musicProc.Stop();
            }
            MusicIdx = a;
            PlaylistMode = false;
            Console.WriteLine("[music] PlayMusic() with " + MusicFiles[a]);
            musicProc.Play("/musics/" + MusicFiles[a]);
        }
        private static bool IsFadingMusic = false;
        private static bool FadeBackMusic;
        private static int MusicPrevVol = 0;
        internal static void StopAsyncMicAnnc()
        {
            anncSfx.Play("/musics/annc/anncend.mp3");
            AnncProc_OnStop(new MpvEventEndFile());
        }
        internal static void StartAsyncMicAnnc()
        {
            if (AnncPlaying)
            {
                anncProc.Stop();
            }
            if (!MusicPlaying)
            {
                anncSfx.Play("/musics/annc/anncstart.mp3");
                return;
            }

            // begin fading music

            Thread t = new(delegate ()
            {
                if (IsFadingMusic)
                {
                    // we are already fading or something, ignore the annc request
                }
                else
                {
                    IsFadingMusic = true;
                    MusicPrevVol = MusicVol;
                    if (MusicVol > 20)
                    {
                        while (MusicVol > 20)
                        {
                            MusicVol -= 1;
                            Thread.Sleep(MusicFadeTime);
                        }
                        Console.WriteLine("music: fade out ended");
                    }
                    else
                    {
                        Console.WriteLine("music: not fading music as volume is " + MusicVol);
                    }

                    IsFadingMusic = false;
                    FadeBackMusic = true;

                    // Play the annc start
                    anncSfx.Play("/musics/annc/anncstart.mp3");
                }
            });
            t.Start();
        }
        internal static void PlayAnnc(int idx)
        {
            if (AnncPlaying)
            {
                anncProc.Stop();
            }
            if (!MusicPlaying)
            {
                anncProc.Play("/musics/annc/" + AnncFiles[idx]);
                return;
            }

            // begin fading music

            Thread t = new(delegate ()
            {
                if (IsFadingMusic)
                {
                    // we are already fading or something, ignore the annc request
                }
                else
                {
                    IsFadingMusic = true;
                    MusicPrevVol = MusicVol;
                    if (MusicVol > 20)
                    {
                        while (MusicVol > 20)
                        {
                            MusicVol -= 1;
                            Thread.Sleep(MusicFadeTime);
                        }
                        Console.WriteLine("music: fade out ended");
                    }
                    else
                    {
                        Console.WriteLine("music: not fading music as volume is " + MusicVol);
                    }

                    IsFadingMusic = false;
                    FadeBackMusic = true;

                    // Play the annoucement
                    anncProc.Play("/musics/annc/" + AnncFiles[idx]);
                }
            });
            t.Start();
        }
        internal static void StopMusic()
        {
            musicProc.Stop();
        }
        internal static void StopAnnc()
        {
            anncProc.Stop();
        }

        public static void PlaylistBack()
        {
            musicProc.PlaylistBack();
        }

        public static void PlaylistForward()
        {
            musicProc.PlaylistNext();
        }
    }
}
