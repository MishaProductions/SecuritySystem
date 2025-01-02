using Avalonia.Input;
using MHSApi.API;
using MHSApi.WebSocket.AudioIn;
using MHSClientAvalonia.Client;
using MHSClientAvalonia.Utils;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;

namespace MHSClientAvalonia.Pages;

public partial class MusicManager : SecurityPage
{
    private ObservableCollection<string> MusicFiles = new ObservableCollection<string>();
    private ObservableCollection<string> AnncFiles = new ObservableCollection<string>();
    private bool anncChanging = false;
    private bool musicChanging = false;
    static PlugifyWebSocketClient? _audioOutSocket;

    private static ALCaptureDevice _captureDevice;
    private static bool _shouldCapture = false;
    private static byte[] buffer = new byte[100 * 41000];
    public MusicManager()
    {
        InitializeComponent();
    }

    public override async void OnNavigateTo()
    {
        base.OnNavigateTo();
        MusicFiles.Clear();
        AnncFiles.Clear();

        UpdateLoadingString("Loading music and annc info");
        var res = await Services.SecurityClient.GetMusicAndAnnoucements();
        if (res.IsFailure || res.Value == null)
        {
            Services.MainView.ShowMessage("Failed to fetch data", res.ResultMessage);
            HideLoadingBar();
            return;
        }
        var files = (MusicListResponse)res.Value;

        foreach (var file in files.Music)
        {
            MusicFiles.Add(file.FileName);
        }
        foreach (var file in files.Annoucements)
        {
            AnncFiles.Add(file.FileName);
        }
        listMusic.ItemsSource = MusicFiles;
        listAnnc.ItemsSource = AnncFiles;

        AnncVolumeSlider.Value = Services.SecurityClient.AnncVol;
        MusicVolumeSlider.Value = Services.SecurityClient.MusicVol;

        HideLoadingBar();
    }
    private async void listMusic_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (listMusic.SelectedIndex != -1)
        {
            await Services.SecurityClient.PlayMusic(MusicFiles[listMusic.SelectedIndex]);
        }
    }
    private async void listAnnc_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (listAnnc.SelectedIndex != -1)
        {
            await Services.SecurityClient.PlayAnnoucement(AnncFiles[listAnnc.SelectedIndex]);
        }
    }

    private async void StopAnnc_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
#pragma warning disable CS8073 // The result of the expression is always 'true' since a value of type 'ALCaptureDevice' is never equal to 'null' of type 'ALCaptureDevice?'
        if (_captureDevice != null! && !BrowserUtils.IsBrowser)
#pragma warning restore CS8073 // The result of the expression is always 'true' since a value of type 'ALCaptureDevice' is never equal to 'null' of type 'ALCaptureDevice?'
        {
            _shouldCapture = false;
            ALC.CaptureStop(_captureDevice);

            ALC.CaptureCloseDevice(_captureDevice);
        }
        if (_audioOutSocket != null)
        {
            try
            {
                if (_audioOutSocket.IsOpen)
                {
                    await _audioOutSocket.Send([(byte)AudioInMsgType.CloseAudioDevice]);
                }


                _audioOutSocket.Close();
                _audioOutSocket = null;
            }
            catch
            {
                _audioOutSocket = null;
            }
        }
        BtnPlayAnncFromMic.IsEnabled = true;
        await Services.SecurityClient.StopCurrentAnnoucement();
    }

    private async void StopMusic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.SecurityClient.StopCurrentMusic();
    }

    private async void PlayAllMusic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.SecurityClient.PlayAllMusic();
    }
    private async void MusicBack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.SecurityClient.PlayPreviousMusic();
    }
    private async void MusicNext_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.SecurityClient.PlayNextMusic();
    }

    public override void OnAnncVolChanged()
    {
        base.OnAnncVolChanged();
        anncChanging = true;
        AnncVolumeSlider.Value = Services.SecurityClient.AnncVol;
    }

    public override void OnMusicVolChanged()
    {
        base.OnMusicVolChanged();
        musicChanging = true;
        MusicVolumeSlider.Value = Services.SecurityClient.MusicVol;
    }

    public override void OnMusicFileChanged(string? fileName)
    {
        if (fileName != null)
            TxtCurrentlyPlayingMusic.Text = "Playing: " + fileName;
        else
            TxtCurrentlyPlayingMusic.Text = "Playing: no file";
    }
    public override void OnAnncChanged(string? fileName, bool isLive)
    {
        if (isLive)
        {
            TxtCurrentlyPlayingAnnc.Text = "Microphone input";
            return;
        }

        if (fileName != null)
            TxtCurrentlyPlayingAnnc.Text = "Playing: " + fileName;
        else
            TxtCurrentlyPlayingAnnc.Text = "Playing: no file";
    }

    private async void AnncVol_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (anncChanging)
        {
            anncChanging = false;
            return;
        }
        await Services.SecurityClient.SetAnncVolume((int)e.NewValue);
    }

    private async void MusicVol_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (musicChanging)
        {
            musicChanging = false;
            return;
        }
        await Services.SecurityClient.SetMusicVolume((int)e.NewValue);
    }

    private static async void CapturingThread()
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

    private async void PlayAnncFromMic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_audioOutSocket != null)
        {
            Services.MainView.ShowMessage("System Error", "An audio input stream is already open on this device. Try speaking into the microphone, and then press Stop annc.");
            return;
        }

        try
        {
            _captureDevice = ALC.CaptureOpenDevice(null, 44100, ALFormat.Mono16, 50);//opens default mic //null specifies default 

            _audioOutSocket = await Services.SecurityClient.OpenAnncStream(44100, 16, 2);
            if (_audioOutSocket != null)
            {
                ALC.CaptureStart(_captureDevice);
                _shouldCapture = true;
                new Thread(CapturingThread).Start();
            }
            else
            {
                Services.MainView.ShowMessage("System Error", "Opening remote annc stream input failed. Check network/authentication");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Services.MainView.ShowMessage("System Error", ex.ToString());
        }
    }
}