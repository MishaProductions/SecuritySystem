using SecuritySystem.Modules;
using SecuritySystem.Modules.NXDisplay;

namespace SecuritySystem.Utils
{
    public static class MusicPlayer
    {
        public static readonly List<string> MusicFiles = [];
        public static readonly List<string> AnncFiles = [];

        public static bool MusicPlaying { get { return musicProc.IsPlaying; } }
        public static bool AnncPlaying { get { return anncProc.IsPlaying; } }

        public static readonly Player musicProc = new();
        public static readonly Player anncProc = new();
        private static readonly Player alarmProc = new();
        private static readonly Player alarmAnncProc = new();
        private static readonly Player anncSfx = new();
        private static readonly Player tickSfx = new();
        private static readonly Player armAnnc = new();
        private static int MusicFadeTime = 30;
        private static int MusicFadeToVolume = 50;
        private static bool ShuffleMusic = false;


        public static bool PlaylistMode { get; private set; }
        public static event EventHandler? OnMusicStop;
        public static event EventHandler? OnAnncStop;

        public static event EventHandler? OnMusicVolumeChanged;
        public static event EventHandler? OnAnncVolumeChanged;
        public static event MusicStartedEventArgs? OnMusicStarted;
        public static event MusicStartedEventArgs? OnAnncStarted;

        public static string CurrentSongName
        {
            get
            {
                if (MusicPlaying)
                {
                    return musicProc.GetPlaylistFileNameByIndex(musicProc.PlaylistIndex);
                }
                else
                {
                    return "None";
                }
            }
        }
        public static string CurrentAnnc
        {
            get
            {
                if (AnncPlaying)
                {
                    return anncProc.GetPlaylistFileNameByIndex(anncProc.PlaylistIndex);
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
        static MusicPlayer()
        {
            musicProc.OnStop += MusicProc_OnStop;
            anncProc.OnStop += AnncProc_OnStop;

            musicProc.OnStart += MusicProc_OnStart;

            // arlarm function
            alarmProc.OnStop += AlarmProc_OnStop;
            //alarmProc.Volume = 10; // debugging only

            SystemManager.OnAlarm += SystemManager_OnAlarm;
            SystemManager.OnSystemDisarm += SystemManager_OnSystemDisarm;
            SystemManager.OnSysTimerEvent += SystemManager_OnSysTimer;
            SystemManager.OnZoneUpdate += SystemManager_OnZoneUpdate;

            ScanFiles();
        }

        private static void SystemManager_OnZoneUpdate(bool single, int zone, string name, ZoneState ready)
        {
            //tickSfx.Play("/musics/annc/systimer.mp3");
        }

        private static void SystemManager_OnSysTimer(bool entry, int timer)
        {
            tickSfx.Play("/musics/annc/systimer.mp3");

            if (!entry && timer == 14)
            {
                armAnnc.Play("/musics/annc/standclear.mp3");
                ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/standclear.mp3");
            }
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
                items = [.. items.OrderBy(q => q)];
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
                items = [.. items.OrderBy(q => q)];
                foreach (var item in items)
                {
                    AnncFiles.Add(item);
                }
            }
        }

        private static void FadeIn()
        {
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
        private static void AnncProc_OnStop(MpvEventEndFile data)
        {
            OnAnncStop?.Invoke(null, new());
            FadeIn();
        }
        private static void MusicProc_OnStop(MpvEventEndFile data)
        {
            Console.WriteLine("MusicProc_OnStop with " + data.Reason);
            OnMusicStop?.Invoke(null, new());
        }
        private static void MusicProc_OnStart(long id)
        {
            var fileName = musicProc.GetPlaylistFileNameByIndex(musicProc.PlaylistIndex);
            Console.WriteLine("got " + fileName + " id of " + id);
            OnMusicStarted?.Invoke(fileName, false);
        }
        internal static void PlayAllMusic(List<string> paths)
        {
            PlaylistMode = true;
            string[] strings = new string[paths.Count];
            musicProc.ClearPlaylist();
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = "/musics/" + paths[i];
                Console.WriteLine(strings[i]);
            }

            musicProc.PlaylistPlay(strings, true, ShuffleMusic);
        }
        internal static void PlayMusic(int a)
        {
            if (MusicPlaying)
            {
                musicProc.Stop();
            }
            musicProc.ClearPlaylist();
            string fileName = MusicFiles[a];
            PlaylistMode = false;
            Console.WriteLine("[music] PlayMusic() with " + fileName);
            ModuleController.GetDisplays().First().PlayMusic("/musics/" + fileName);
            Thread.Sleep(5);
            musicProc.Play("/musics/" + fileName);
        }
        private static bool IsFadingMusic = false;
        private static bool FadeBackMusic;
        private static int MusicPrevVol = 0;
        internal static void StopAsyncMicAnnc()
        {
            anncSfx.Play("/musics/annc/anncend.mp3");
            ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/anncend.mp3");
            AnncProc_OnStop(new MpvEventEndFile());
        }
        internal static void StartAsyncMicAnnc()
        {
            if (AnncPlaying)
            {
                anncProc.Stop();
            }

            OnAnncStarted?.Invoke("", true);

            if (!MusicPlaying)
            {
                anncSfx.Play("/musics/annc/anncstart.mp3");
                ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/anncstart.mp3");
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
                    if (MusicVol > MusicFadeToVolume)
                    {
                        while (MusicVol > MusicFadeToVolume)
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
                    ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/anncstart.mp3");
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
                ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/" + AnncFiles[idx]);
                anncProc.Play("/musics/annc/" + AnncFiles[idx]);
                OnAnncStarted?.Invoke(AnncFiles[idx], false);
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
                    if (MusicVol > MusicFadeToVolume)
                    {
                        while (MusicVol > MusicFadeToVolume)
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
                    OnAnncStarted?.Invoke(AnncFiles[idx], false);

                    // Play the annoucement
                    ModuleController.GetDisplays().First().PlayAnnc("/musics/annc/" + AnncFiles[idx]);
                    anncProc.Play("/musics/annc/" + AnncFiles[idx]);
                }
            });
            t.Start();
        }
        internal static void StopMusic()
        {
            musicProc.Stop();
            ModuleController.GetDisplays().First().StopMusic();
            OnMusicStop?.Invoke(null, new());
        }
        internal static void StopAnnc()
        {
            FadeIn();
            anncProc.Stop();
            OnAnncStop?.Invoke(null, new());
            ModuleController.GetDisplays().First().StopAnnc();
        }

        public static void PlaylistBack()
        {
            musicProc.PlaylistBack();
        }

        public static void PlaylistForward()
        {
            musicProc.PlaylistNext();
        }

        public static void SetMusicShuffle(bool should)
        {
            ShuffleMusic = should;
            musicProc.UpdatePlaylistShuffle(should);
        }

        public static void SetMusicLoop(bool should)
        {
            musicProc.LoopPlaylist = should;
        }

        public delegate void MusicStartedEventArgs(string fileName, bool isLive);
    }
}
