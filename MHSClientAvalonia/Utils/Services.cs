using MHSClientAvalonia.Client;
using System;
using System.Runtime.InteropServices;

namespace MHSClientAvalonia.Utils
{
    public static class Services
    {
        private static IPreferences? _preferences;
        private static SecurityClient? _securityClient;

        public static SecurityClient SecurityClient
        {
            get
            {
                if (_securityClient == null)
                {
                    _securityClient = new SecurityClient();
                }

                return _securityClient;
            }
        }
        public static IPreferences Preferences
        {
            get
            {
                if (_preferences == null)
                {
                    if (BrowserUtils.IsBrowser)
                    {
                        _preferences = new BrowserStorage();
                    }
                    else
                    {
                        _preferences = new DesktopStorage();
                    }
                }

                return _preferences;
            }
        }
        public static MainWindow MainWindow { get; set; } = null!;
        public static MainView MainView { get; set; } = null!;
        public static AudioCaptureDriver AudioCapture { get; set; } = null!;
        public static DesktopNotifications.INotificationManager NotificationManager = null!;
    }
}
