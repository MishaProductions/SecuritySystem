using Iot.Device.Media;
using NAudio.Wave;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TagLib.Id3v2;
using static SecuritySystem.Alsa.Interop;

namespace SecuritySystem.Alsa
{
    internal class UnixPCMDevice : IDisposable
    {
        private IntPtr _playbackPcm;
        private IntPtr _recordingPcm;
        private IntPtr _mixer;
        private IntPtr _elem;
        private int _errorNum;

        private static readonly object playbackInitializationLock = new object();
        private static readonly object recordingInitializationLock = new object();
        private static readonly object mixerInitializationLock = new object();

        /// <summary>
        /// The connection settings of the sound device.
        /// </summary>
        public SoundConnectionSettings Settings { get; }

        /// <summary>
        /// The playback volume of the sound device.
        /// </summary>
        public long PlaybackVolume
        {
            get => GetPlaybackVolume();
            set
            {
                SetPlaybackVolume(value);
            }
        }

        // The lib do not have a method of get all channels mute state.
        private bool _playbackMute;
        /// <summary>
        /// The playback mute of the sound device.
        /// </summary>
        public bool PlaybackMute
        {
            get => _playbackMute;
            set
            {
                SetPlaybackMute(value);
                _playbackMute = value;
            }
        }

        /// <summary>
        /// The recording volume of the sound device.
        /// </summary>
        public long RecordingVolume { get => GetRecordingVolume(); set => SetRecordingVolume(value); }

        private bool _recordingMute;
        /// <summary>
        /// The recording mute of the sound device.
        /// </summary>
        public bool RecordingMute
        {
            get => _recordingMute;
            set
            {
                SetRecordingMute(value);
                _recordingMute = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnixPCMDevice"/> class that will use the specified settings to communicate with the sound device.
        /// </summary>
        /// <param name="settings">The connection settings of a sound device.</param>
        public UnixPCMDevice(SoundConnectionSettings settings)
        {
            Settings = settings;

            PlaybackMute = false;
            RecordingMute = false;
        }
        private IntPtr @params = new IntPtr();
        private int dir = 0;
        private WavHeader h = new WavHeader();

        public unsafe void Open(ushort bitsPerSample, uint samplingRate, ushort blockAlighment)
        {
            Console.WriteLine($"Begin audio playback. Bits: {bitsPerSample}, sampling rate: {samplingRate}, block aligment: {blockAlighment}");
            OpenPlaybackPcm();
            h = new WavHeader() { BitsPerSample = bitsPerSample, SampleRate = samplingRate, BlockAlign = blockAlighment, NumChannels = 1 };
            PcmInitialize(_playbackPcm, h);

            ulong frames, bufferSize;

            fixed (int* dirP = &dir)
            {
                _errorNum = snd_pcm_hw_params_get_period_size(@params, &frames, dirP);
                ThrowErrorMessage("Can not get period size.");
            }

            bufferSize = frames * h.BlockAlign;
            Console.WriteLine("buffer size: " + bufferSize);

        }

        public void Close()
        {
            ClosePlaybackPcm();
        }
        private readonly BufferedWaveProvider waveProvider;
        private unsafe bool WriteStream(Stream wavStream, WavHeader header)
        {
            ulong frames, bufferSize;

            fixed (int* dirP = &dir)
            {
                _errorNum = snd_pcm_hw_params_get_period_size(@params, &frames, dirP);
                ThrowErrorMessage("Can not get period size.");
            }

            bufferSize = (ulong)frames * header.BlockAlign;
            // In Interop, the frames is defined as ulong. But actucally, the value of bufferSize won't be too big.
            byte[] readBuffer = new byte[(int)bufferSize];

            try
            {
                fixed (byte* buffer = readBuffer)
                {
                    while (true)
                    {
                        ulong offset = 0;

                        // normally there will be only one iteration of this loop but
                        // ReadAsync doesn't guarantee that 'received' will always match
                        // requested bytes amount

                        while (offset < bufferSize)
                        {
                            // read to the buffer until we read *bufferSize*
                            int received = wavStream.Read(readBuffer, (int)offset, (int)(bufferSize - offset));
                            if (received == 0)
                            {
                                Console.WriteLine("failed to read wav stream: client disconnected");
                                return false;
                            }
                            offset += (ulong)received;
                        }

                        Console.WriteLine("readed " + offset + " from buffer");
                        _errorNum = snd_pcm_writei(_playbackPcm, (IntPtr)buffer, (ulong)frames);

                        if (_errorNum == -32)
                        {
                            Console.WriteLine("buffer overran");
                            snd_pcm_prepare(_playbackPcm);
                            _errorNum = snd_pcm_writei(_playbackPcm, (IntPtr)buffer, (ulong)frames);
                        }
                        else
                        {
                            ThrowErrorMessage("Can not write data to the device.");
                        }
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception in audio rx: " + ex.ToString());
            }

            return false;
        }

        internal unsafe bool Write(Stream s)
        {
            return WriteStream(s, h);
        }

        private unsafe void PcmInitialize(IntPtr pcm, WavHeader header)
        {
            _errorNum = snd_pcm_hw_params_malloc(ref @params);
            ThrowErrorMessage("Cannot allocate parameters object.");

            _errorNum = snd_pcm_hw_params_any(pcm, @params);
            ThrowErrorMessage("Cannot fill parameters object.");

            _errorNum = snd_pcm_hw_params_set_access(pcm, @params, snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED);
            ThrowErrorMessage("Cannot set access mode.");

            _errorNum = (int)(header.BitsPerSample / 8) switch
            {
                1 => snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_U8),
                2 => snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S16_LE),
                3 => snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S24_LE),
                _ => throw new Exception("Bits per sample error. Please reset the value of RecordingBitsPerSample."),
            };
            ThrowErrorMessage("Cannot set format.");

            _errorNum = snd_pcm_hw_params_set_channels(pcm, @params, header.NumChannels);
            ThrowErrorMessage("Cannot set channel.");

            uint val = header.SampleRate;
            fixed (int* dirP = &dir)
            {
                _errorNum = snd_pcm_hw_params_set_rate_near(pcm, @params, &val, dirP);
                ThrowErrorMessage("Cannot set rate.");
            }
            Console.WriteLine("pcmdevice: Set sampling rate to " + val);

            _errorNum = snd_pcm_hw_params(pcm, @params);
            ThrowErrorMessage("Cannot set hardware parameters.");
        }

        private unsafe void SetPlaybackVolume(long volume)
        {
            OpenMixer();

            // The snd_mixer_selem_set_playback_volume_all method in Raspberry Pi is invalid.
            // So here we adjust the volume by setting the left and right channels separately.
            _errorNum = snd_mixer_selem_set_playback_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume);
            _errorNum = snd_mixer_selem_set_playback_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume);
            ThrowErrorMessage("Error while setting playback volume");

            CloseMixer();
        }

        private unsafe long GetPlaybackVolume()
        {
            long volumeLeft, volumeRight;

            OpenMixer();

            _errorNum = snd_mixer_selem_get_playback_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft);
            _errorNum = snd_mixer_selem_get_playback_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight);
            ThrowErrorMessage("Get playback volume error.");

            CloseMixer();

            return (volumeLeft + volumeRight) / 2;
        }

        private unsafe void SetRecordingVolume(long volume)
        {
            OpenMixer();

            _errorNum = snd_mixer_selem_set_capture_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume);
            _errorNum = snd_mixer_selem_set_capture_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume);
            ThrowErrorMessage("Set recording volume error.");

            CloseMixer();
        }

        private unsafe long GetRecordingVolume()
        {
            long volumeLeft, volumeRight;

            OpenMixer();

            _errorNum = Interop.snd_mixer_selem_get_capture_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft);
            _errorNum = Interop.snd_mixer_selem_get_capture_volume(_elem, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight);
            ThrowErrorMessage("Get recording volume error.");

            CloseMixer();

            return (volumeLeft + volumeRight) / 2;
        }

        private void SetPlaybackMute(bool isMute)
        {
            OpenMixer();

            _errorNum = Interop.snd_mixer_selem_set_playback_switch_all(_elem, isMute ? 0 : 1);
            ThrowErrorMessage("Set playback mute error.");

            CloseMixer();
        }

        private void SetRecordingMute(bool isMute)
        {
            OpenMixer();

            _errorNum = Interop.snd_mixer_selem_set_playback_switch_all(_elem, isMute ? 0 : 1);
            ThrowErrorMessage("Set recording mute error.");

            CloseMixer();
        }

        private void OpenPlaybackPcm()
        {
            if (_playbackPcm != default)
            {
                return;
            }

            lock (playbackInitializationLock)
            {
                //1: SND_PCM_NONBLOCK
                _errorNum = Interop.snd_pcm_open(ref _playbackPcm, Settings.PlaybackDeviceName, snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK, 0);
                ThrowErrorMessage("Can not open playback device.");
            }
        }

        private void ClosePlaybackPcm()
        {
            if (_playbackPcm != default)
            {
                _errorNum = Interop.snd_pcm_drop(_playbackPcm);
                ThrowErrorMessage("Drop playback device error.");

                _errorNum = Interop.snd_pcm_close(_playbackPcm);
                ThrowErrorMessage("Close playback device error.");

                _playbackPcm = default;
            }
        }

        private void OpenRecordingPcm()
        {
            if (_recordingPcm != default)
            {
                return;
            }

            lock (recordingInitializationLock)
            {
                _errorNum = Interop.snd_pcm_open(ref _recordingPcm, Settings.RecordingDeviceName, snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE, 0);
                ThrowErrorMessage("Can not open recording device.");
            }
        }

        private void CloseRecordingPcm()
        {
            if (_recordingPcm != default)
            {
                _errorNum = Interop.snd_pcm_drop(_recordingPcm);
                ThrowErrorMessage("Drop recording device error.");

                _errorNum = Interop.snd_pcm_close(_recordingPcm);
                ThrowErrorMessage("Close recording device error.");

                _recordingPcm = default;
            }
        }

        private void OpenMixer()
        {
            if (_mixer != default)
            {
                return;
            }

            lock (mixerInitializationLock)
            {
                _errorNum = Interop.snd_mixer_open(ref _mixer, 0);
                ThrowErrorMessage("Can not open sound device mixer.");

                _errorNum = Interop.snd_mixer_attach(_mixer, Settings.MixerDeviceName);
                ThrowErrorMessage("Can not attach sound device mixer.");

                _errorNum = Interop.snd_mixer_selem_register(_mixer, IntPtr.Zero, IntPtr.Zero);
                ThrowErrorMessage("Can not register sound device mixer.");

                _errorNum = Interop.snd_mixer_load(_mixer);
                ThrowErrorMessage("Can not load sound device mixer.");

                _elem = Interop.snd_mixer_first_elem(_mixer);
            }
        }

        private void CloseMixer()
        {
            if (_mixer != default)
            {
                _errorNum = Interop.snd_mixer_close(_mixer);
                // ThrowErrorMessage("Close sound device mixer error.");

                _mixer = default;
                _elem = default;
            }
        }

        public void Dispose()
        {
            ClosePlaybackPcm();
            CloseRecordingPcm();
            CloseMixer();
        }

        private void ThrowErrorMessage(string message)
        {
            if (_errorNum < 0)
            {
                int code = _errorNum;
                string errorMsg = Marshal.PtrToStringAnsi(Interop.snd_strerror(_errorNum));

                throw new Exception($"{message}\nError {code}. {errorMsg}.");
                Dispose();
            }
        }

    }
}
