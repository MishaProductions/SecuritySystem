using Avalonia.Input;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class MusicManager : SecurityPage
{
    private readonly ObservableCollection<string> MusicFiles = [];
    private readonly ObservableCollection<string> AnncFiles = [];
    private bool anncChanging = true;
    private bool musicChanging = true;

    public MusicManager()
    {
        InitializeComponent();
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        MusicFiles.Clear();
        AnncFiles.Clear();

        UpdateLoadingString("Loading music and annc info");
        var res = await Services.SecurityClient.GetMusicAndAnnoucements();
        if (res.IsFailure || res.Value == null)
        {
            MainView.ShowMessage("Failed to fetch data", res.ResultMessage);
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

        anncChanging = true;
        musicChanging = true;

        AnncVolumeSlider.Value = Services.SecurityClient.AnncVol;
        MusicVolumeSlider.Value = Services.SecurityClient.MusicVol;

        anncChanging = false;
        musicChanging = false;

        OnMusicFileChanged(Services.SecurityClient.CurrentlyPlayingMusic, false);
        OnAnncChanged(Services.SecurityClient.CurrentlyPlayingAnnc, false);

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
        if (Services.AudioCapture != null)
            await Services.AudioCapture.Stop();
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
        anncChanging = false;
    }

    public override void OnMusicVolChanged()
    {
        base.OnMusicVolChanged();
        musicChanging = true;
        MusicVolumeSlider.Value = Services.SecurityClient.MusicVol;
        musicChanging = false;
    }

    public override void OnMusicFileChanged(string? fileName, bool isLive)
    {
        if (isLive)
        {
            TxtCurrentlyPlayingMusic.Text = "Source: DirectStream";
            return;
        }

        if (fileName != null)
            TxtCurrentlyPlayingMusic.Text = "Playing: " + fileName;
        else
            TxtCurrentlyPlayingMusic.Text = "Playing: None";
    }
    public override void OnAnncChanged(string? fileName, bool isLive)
    {
        if (isLive)
        {
            TxtCurrentlyPlayingAnnc.Text = "Source: Microphone input";
            return;
        }

        if (fileName != null)
            TxtCurrentlyPlayingAnnc.Text = "Playing: " + fileName;
        else
            TxtCurrentlyPlayingAnnc.Text = "Playing: None";
    }

    private async void AnncVol_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (anncChanging) return;
        await Services.SecurityClient.SetAnncVolume((int)e.NewValue);
    }

    private async void MusicVol_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (musicChanging) return;
        await Services.SecurityClient.SetMusicVolume((int)e.NewValue);
    }


    private async void PlayAnncFromMic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Services.AudioCapture == null)
        {
            MainView.ShowMessage("Platform Error", "Audio capture is not yet implemented on this platform.");
            return;
        }
        if (Services.AudioCapture._audioOutSocket != null)
        {
            MainView.ShowMessage("System Error", "An audio input stream is already open on this device. Try speaking into the microphone, and then press Stop annc.");
            return;
        }

        BtnPlayAnncFromMic.IsEnabled = false;

        try
        {
            Services.AudioCapture._audioOutSocket = await Services.SecurityClient.OpenAnncStream(44100, 16, 2);
            if (Services.AudioCapture._audioOutSocket != null)
            {
                await Services.AudioCapture.Open();
            }
            else
            {
                MainView.ShowMessage("System Error", "Opening remote annc stream input failed. Check network/authentication");
                BtnPlayAnncFromMic.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            MainView.ShowMessage("System Error", "Try checking microphone permissions\n"+ ex.ToString());
            BtnPlayAnncFromMic.IsEnabled = true;
        }
    }
}