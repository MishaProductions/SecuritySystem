/*
 * PROJECT: SecuritySystem
 * DATE: 2/6/2024
 * DESCRIPTION: Represents API endpoints
*/

namespace MHSApi.API
{
    public static class Endpoints
    {
        public const string ApiBase = "/api";

        public const string Login = "/auth/login-modern";
        public const string Logout = "/auth/logout";

        public const string Arm = "/armsystem";
        public const string Disarm = "/disarmsystem";
        public const string SystemStatus = "/system/status";
        public const string SystemInfo = "/system/info";
        public const string CompleteSystemOobe = "/system/SaveSysConfig";
        public const string SystemEventLog = "/system/eventlog";
        public const string GetAlarmHistory = "/ReadAlarmHistory";

        public const string NotificationSettings = "/settings/notifications";
        public const string NotificationsTest = "/settings/notifications-test";

        public const string ZoneSettings = "/settings/zones";

        public const string UploadFirmware = "/firmware/keypad/upload";
        public const string FirmwareUpdateStatus = "/firmware/keypad/status";
        public const string QueryClientUpdateInfo = "/getWindowsClientVersion-v2-windows";

        public const string StartAnnc = "/music/startannc";
        public const string StartMusic = "/music/startmusic";
        public const string StopMusic = "/music/stopmusic";
        public const string PlayAllMusic = "/music/playall";
        public const string PlayNextMusic = "/music/playnext";
        public const string PlayPreviousMusic = "/music/playprevious";
        public const string StopAnnc = "/music/stopannc";
        public const string ListMusicAndAnnoucements = "/music/list";
        public const string CurrentUser = "/user/current";
        public const string AllUser = "/user/all";
        public const string UserMod = "/user/";
    }
}
