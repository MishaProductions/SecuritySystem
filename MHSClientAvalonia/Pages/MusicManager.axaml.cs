using Avalonia.Input;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using NAudio.Wave;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;

namespace MHSClientAvalonia.Pages;

public partial class MusicManager : SecurityPage
{
    private ObservableCollection<string> MusicFiles = new ObservableCollection<string>();
    private ObservableCollection<string> AnncFiles = new ObservableCollection<string>();
    private bool anncChanging = false;
    private bool musicChanging = false;
    static WaveInEvent? waveIn;
    static TcpClient? streaming;
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
        if (waveIn != null)
        {
            waveIn.StopRecording();
            waveIn.Dispose();
            waveIn = null;
        }
        if (streaming != null)
        {
            try
            {
                streaming.GetStream().Close();
                streaming.Close();
                streaming = null;
            }
            catch
            {
                streaming = null;
            }
        }
        BtnPlayAnncFromMic.IsEnabled = true;
        await Services.SecurityClient.StopCurrentAnnoucement();
    }

    private async void StopMusic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.SecurityClient.StopCurrentMusic();
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

    private async void PlayAnncFromMic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        waveIn = new WaveInEvent();
        try
        {
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
            waveIn.BufferMilliseconds = 50;
            waveIn.NumberOfBuffers = 3;
            streaming = await Services.SecurityClient.OpenAnncStream(waveIn.WaveFormat.SampleRate + "," + waveIn.WaveFormat.BitsPerSample + "," + waveIn.WaveFormat.BlockAlign);
            if (streaming != null)
            {
                BtnPlayAnncFromMic.IsEnabled = false;
                var ss = streaming.GetStream();
                waveIn.StartRecording();
                waveIn.DataAvailable += (s, a) =>
                {
                    Debug.WriteLine("wrote " + a.BytesRecorded);
                    ss.Write(a.Buffer, 0, a.BytesRecorded);
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}