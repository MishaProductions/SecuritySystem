using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MHSClientAvalonia.Utils;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class FirmwareUpdate : SecurityPage
{
    public FirmwareUpdate()
    {
        InitializeComponent();
    }
    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        HideLoadingBar();
    }
    private static byte[]? Array;
    private async void NextionFlash_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        byte[] file;
        if (Array == null)
        {
            if (string.IsNullOrEmpty(TxtFilePath.Text))
            {
                MainView.ShowMessage("No disk", "Please select a file");
                return;
            }

            if (!File.Exists(TxtFilePath.Text) && !OperatingSystem.IsBrowser())
            {
                MainView.ShowMessage("No disk", "Specified file does not exist.");
                return;
            }

            file = File.ReadAllBytes(TxtFilePath.Text);
        }
        else
        {
            file = Array;
        }

        txtFwUpload.IsVisible = true;
        btnFlash.IsEnabled = false;

        var res = await Services.SecurityClient.UploadNextionKeypadFirmware(file);
        if (res.IsSuccess)
        {
            MainView.ShowMessage("Success", "Firmware uploaded successfully");
        }
        else
        {
            MainView.ShowMessage("Error", "Failed to upload firmware: " + res.ResultMessage);
        }
        txtFwUpload.IsVisible = false;
        btnFlash.IsEnabled = true;
    }

    private async void OpenFilePicker_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            MainView.ShowMessage("No disk", "Platform is not supported");
            return;
        }

        // Start async operation to open the dialog.
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Firmware TFT file",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            // Open reading stream from the first file.
            await using var stream = await files[0].OpenReadAsync();

            using var ms = new MemoryStream();

            // Copy file to MemoryStream. ReadAsync is used instead of CopyOf as Avalonia browser does not support it
            byte[] buffer = new byte[8192]; // 8 KB buffer size

            if (OperatingSystem.IsBrowser())
            {
                TxtFilePath.Text = "(selected in browser)";
                TxtFilePath.IsReadOnly = true;
            }

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await ms.WriteAsync(buffer, 0, bytesRead);
            }

            ms.Seek(0, SeekOrigin.Begin);

            Array = ms.ToArray();
            TxtFilePath.IsEnabled = false;
        }
    }
}