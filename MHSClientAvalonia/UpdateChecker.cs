using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FluentAvalonia.UI.Controls;
using MHSClientAvalonia.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SecuritySystemApi;

namespace MHSClientAvalonia
{
    internal partial class UpdateChecker
    {
        private static bool UpdateDialogShowing = false;
        private static TaskDialog? dialog;
        private static UpdateInformationContent? info;
        public static async void DoCheck(bool noUpdatesFoundDialog = true, Action? onDataFetchedCb = null)
        {
            var res = await Services.SecurityClient.FetchUpdateData();
            if (res.IsFailure || res.Value == null)
            {
                if (noUpdatesFoundDialog)
                {
                    await new ContentDialog()
                    {
                        Title = "Failed to fetch",
                        Content = "Server error: " + res.ResultMessage,
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
                return;
            }

            info = (UpdateInformationContent)res.Value;
            onDataFetchedCb?.Invoke();
            if (info != null && info.serverVersion != null)
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var serverVersion = Version.Parse(info.serverVersion);
                if (serverVersion != null)
                {
                    if (serverVersion > currentVersion)
                    {
                        if (UpdateDialogShowing)
                            return;

                        if (OperatingSystem.IsOSPlatform("windows") || OperatingSystem.IsLinux())
                        {
                            UpdateDialogShowing = true;

                            if (info.changelog == null)
                                info.changelog = "";

                            var result = await new TaskDialog()
                            {
                                Title = "An update was found",
                                Content = $"You are running {currentVersion}, while the latest version is {serverVersion}.\nChangelog: {info.changelog.Replace("\\n", Environment.NewLine)}\nWould you like to update?",
                                Buttons = { TaskDialogButton.YesButton, TaskDialogButton.NoButton },
                                XamlRoot = Services.MainView.VisualForUpdate
                            }.ShowAsync();

                            if (result == TaskDialogButton.YesButton.DialogResult)
                            {
                                await StartUpdate();
                            }
                            UpdateDialogShowing = false;
                        }
                        else
                        {
                            UpdateDialogShowing = true;

                            string updateHint = BrowserUtils.IsBrowser ? "Please clear cache and refresh page!" : "Please update your client!";
                            await new ContentDialog()
                            {
                                Title = "An update was found",
                                Content = $"You are running {currentVersion}, while the latest version is {serverVersion}. {updateHint}",
                                CloseButtonText = "OK"
                            }.ShowAsync();
                            UpdateDialogShowing = false;
                        }
                    }
                    else
                    {
                        if (noUpdatesFoundDialog)
                        {
                            await new ContentDialog()
                            {
                                Title = "No updates were found",
                                CloseButtonText = "OK"
                            }.ShowAsync();
                        }
                    }
                }
                else
                {
                    if (noUpdatesFoundDialog)
                    {
                        await new ContentDialog()
                        {
                            Title = "Server error",
                            Content = "Malformed JSON response from server - serverVersion field is invaild or missing from the response",
                            CloseButtonText = "OK"
                        }.ShowAsync();
                    }
                }
            }
            else
            {
                if (noUpdatesFoundDialog)
                {
                    await new ContentDialog()
                    {
                        Title = "No valid update response was returned from server",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
        }

        private static async Task StartUpdate()
        {
            string installationPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\MHS Security Center\";

            dialog = new TaskDialog
            {
                Title = "Update",
                ShowProgressBar = true,
                IconSource = new SymbolIconSource { Symbol = Symbol.Download },
                SubHeader = "Downloading",
                Content = "Staging update...",
                Buttons = { }
            };

            dialog.Opened += Dialog_Opened;

            dialog.XamlRoot = Services.MainView.VisualForUpdate;
            await dialog.ShowAsync(true);
        }
        /// <summary>
        /// This function downloads/installs the update
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static async void Dialog_Opened(TaskDialog sender, EventArgs args)
        {
            string[]? files = OperatingSystem.IsWindows() ? info.files.win64 : info.files.linux64;
            string arch = OperatingSystem.IsWindows() ? "win64" : "linux64";
            // ensure that update info is valid
            if (info == null || info.files == null || files == null || files.Length == 0)
            {
                sender.Hide();
                var error = new TaskDialog
                {
                    Title = "Error",
                    SubHeader = "We have encountered a error",
                    Content = "Failed to retrieve update information - no valid response from server. Try updating the server firmware.",
                    Buttons = { TaskDialogButton.OKButton }
                };
                error.XamlRoot = Services.MainView.VisualForUpdate;
                await error.ShowAsync(true);
                return;
            }

            string installationPath = AppDomain.CurrentDomain.BaseDirectory;

            // Instalation process:
            // 1. Delete backup* files in install path
            // 2. Download new files from server to download_
            // 3. Rename current files to backup_* (ie MHSClient.exe to backup_MHSClient.exe
            // 4. Move download files to new path (ie update_MHSClient.exe to MHSClient.exe)

            // Renaming/moving files avoids dealing with a seperate program to update those files.
            // Also might provide a rollback function in the future, but I don't think that is needed.

            sender.Content = "Purging backups";
            sender.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);

            // Purge the backups. If this fails we can't continue
            foreach (var item in Directory.GetFiles(installationPath, "backup_*"))
            {
                try
                {
                    File.Delete(item);
                }
                catch
                {
                    sender.Hide();
                    var error = new TaskDialog
                    {
                        Title = "Error",
                        SubHeader = "We have encountered a error",
                        Content = $"Failed to delete backup of previous version of MHS.\nFaulting file name:{item}\nTry closing all instances of MHSClient and try again.",
                        Buttons = { TaskDialogButton.OKButton }
                    };
                    error.XamlRoot = Services.MainView.VisualForUpdate;
                    await error.ShowAsync(true);
                    return;
                }
            }

            int i = 0;
            foreach (var item in files)
            {
                // update progress
                sender.Content = $"Downloading files ({i + 1}/{files.Length})\nCurrent file: {item}\nBytes downloaded: 0";
                sender.SetProgressBarState((i / files.Length) * 100, TaskDialogProgressState.Indeterminate);

                string downloadPath = installationPath + "update_" + item;
                HttpResponseMessage response = await Services.SecurityClient.Client.GetAsync(new Uri(Services.SecurityClient.Endpoint + "/client/" + arch + "/" + item), HttpCompletionOption.ResponseHeadersRead);


                var fs = File.Create(downloadPath);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    sender.Hide();
                    var error = new TaskDialog
                    {
                        Title = "Error",
                        SubHeader = "We have encountered a error",
                        Content = $"Failed to start download of file {item}. Check your connection to the router and security system and try again",
                        Buttons = { TaskDialogButton.OKButton }
                    };
                    error.XamlRoot = Services.MainView.VisualForUpdate;
                    await error.ShowAsync(true);
                    return;
                }

                // write file
                var stream = await response.Content.ReadAsStreamAsync();
                ulong totalBytesRead = 0;
                while (true)
                {
                    // Read from the web.
                    byte[] buffer = new byte[4096];
                    int read = await stream.ReadAsync(buffer);

                    if (read == 0)
                    {
                        // There is nothing else to read.
                        break;
                    }

                    // Report progress.
                    totalBytesRead += (ulong)read;


                    // Write to file.
                    await fs.WriteAsync(buffer, 0, read);

                    sender.Content = $"Downloading files ({i}/{files.Length})\nCurrent file: {item}\nBytes downloaded: {totalBytesRead}";
                }
                fs.Close();

                i++;

                sender.Content = $"Downloading files ({i}/{files.Length})\nCurrent file: {item}\nBytes downloaded: {totalBytesRead}";
            }

            sender.Content = $"Installing update";

            // Now its time for the actual update

            // move old files to backup
            foreach (var item in files)
            {
                string realPath = installationPath + item;
                string backupPath = installationPath + "backup_" + item;

                if (OperatingSystem.IsWindows())
                {
                    var result = MoveFileExW(realPath, backupPath, 0);

                    // ignore errors if current file doesnt exist because it might be a new file introduced in update
                    if (result == 0 && Marshal.GetLastWin32Error() != 2)
                    {
                        sender.Hide();
                        var error = new TaskDialog
                        {
                            Title = "Error",
                            SubHeader = "We have encountered a error",
                            Content = $"When we have tried to rename {realPath} to {backupPath}, we failed. Kill all other instances of MHSClient. DO NOT CLOSE THIS INSTACNE AS IT WILL LEAVE YOU WITH A BROKEN INSTALLATION AS I WAS TOO LAZY TO IMPLEMENT THE ROLL BACK FUNCTION!!!!!!!!!!!",
                            Buttons = { TaskDialogButton.OKButton }
                        };
                        error.XamlRoot = Services.MainView.VisualForUpdate;
                        await error.ShowAsync(true);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        File.Move(realPath, backupPath);
                    }
                    catch (Exception ex)
                    {
                        Services.MainView.ShowMessage("Update error", ex.ToString());
                    }
                }
            }

            // rename update_* files to the real ones.

            foreach (var item in files)
            {
                string realPath = installationPath + item;
                string newFilePath = installationPath + "update_" + item;

                if (OperatingSystem.IsWindows())
                {
                    var result = MoveFileExW(newFilePath, realPath, 0);
                    if (result == 0)
                    {
                        sender.Hide();
                        var error = new TaskDialog
                        {
                            Title = "Error",
                            SubHeader = "We have encountered a error",
                            Content = $"When we have tried to move {newFilePath} to {realPath}, we failed. This was part of step 3. Kill all other instances of MHSClient. DO NOT CLOSE THIS INSTACNE AS IT WILL LEAVE YOU WITH A BROKEN INSTALLATION AS I WAS TOO LAZY TO IMPLEMENT THE ROLL BACK FUNCTION!!!!!!!!!!!",
                            Buttons = { TaskDialogButton.OKButton }
                        };
                        error.XamlRoot = Services.MainView.VisualForUpdate;
                        await error.ShowAsync(true);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        File.Move(newFilePath, realPath);
                    }
                    catch (Exception ex)
                    {
                        Services.MainView.ShowMessage("Update error (phase 2)", ex.ToString());
                    }
                }
            }

            await new ContentDialog() { Title = "Update complete. Press OK to restart MHSClient", CloseButtonText = "OK" }.ShowAsync(Services.MainWindow);

            if (OperatingSystem.IsWindows())
                Process.Start(installationPath + "MHSClientAvalonia.Desktop.exe");
            else
                Process.Start(installationPath + "MHSClientAvalonia.Desktop");

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            {
                desktopApp.Shutdown();
            }
            else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime viewApp)
            {
                viewApp.MainView = null;
            }
        }

        [LibraryImport("kernel32", SetLastError = true, EntryPoint = "MoveFileExW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int MoveFileExW(string oldpath, string newpath, int flags);
    }
}
