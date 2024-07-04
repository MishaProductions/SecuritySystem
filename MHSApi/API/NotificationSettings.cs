/*
 * PROJECT: SecuritySystem
 * DATE: 2/6/2024
 * DESCRIPTION: Describes the notificationsettings api request and response
*/

using SecuritySystemApi;

namespace MHSApi.API
{
    /// <summary>
    /// Represents the object sent and returned to /settings/notifications
    /// </summary>
    public class NotificationSettings : ApiResponseContent
    {
        public bool smtpEnabled { get; set; }
        public string smtpSendTo { get; set; } = "";
        public string smtpHost { get; set; } = "";
        public string smtpUsername { get; set; } = "";
        public string smtpPassword { get; set; } = "";
        public int notificationLevel { get; set; }

        public NotificationSettings(bool smtpEnabled, string smtpSendTo, string smtpHost, string smtpUsername, string smtpPassword, int notificationLevel)
        {
            this.smtpEnabled = smtpEnabled;
            this.smtpSendTo = smtpSendTo;
            this.smtpHost = smtpHost;
            this.smtpUsername = smtpUsername;
            this.smtpPassword = smtpPassword;
            this.notificationLevel = notificationLevel;
        }

        public NotificationSettings() { }
    }
}
