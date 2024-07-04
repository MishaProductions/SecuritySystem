using MHSApi.API;
using SecuritySystem.Modules.NXDisplay;
using SecuritySystem.Utils;
using System.Device.Gpio;
using System.Net;
using System.Net.Mail;

namespace SecuritySystem.Modules
{
    public class MailClass : Module
    {
        public override void OnRegister()
        {
            SystemManager.OnZoneUpdate += SystemManager_OnZoneUpdate;
            SystemManager.OnAlarm += SystemManager_OnAlarm;
        }

        public override void OnUnregister()
        {
            SystemManager.OnZoneUpdate -= SystemManager_OnZoneUpdate;
            SystemManager.OnAlarm -= SystemManager_OnAlarm;
        }
        private void SystemManager_OnAlarm(int alarmZone)
        {
            if (Configuration.Instance.NotificationLevel >= 1)
            {
                SendMail($"<h1>CRITICAL: ALARM ON ZONE #{alarmZone}: {Configuration.Instance.Zones[alarmZone - 1].Name}</h1><br>" + MailClass.GenerateZoneBlock(), "[security system] alarm");
            }
        }
        private void SystemManager_OnZoneUpdate(bool single, int zone, string name, ZoneState ready)
        {
            if (Configuration.Instance.NotificationLevel == 2)
            {
                SendMail($"<h1>Zone #{zone}: {name} is {ready}</h1><br>" + GenerateZoneBlock(), "[security system] zone state change");
            }
        }
        public static void SendMail(string contents, string subject)
        {
            Thread mailThread = new(delegate ()
            {
                SendMailNonThreaded(contents, subject);
            });
            mailThread.Start();
        }
        public static void SendMailNonThreaded(string contents, string subject, bool tryCatch = true)
        {
            if (Configuration.Instance.SmtpEnabled)
            {
                Console.WriteLine("[mail] sending email: " + contents);

                var smtpClient = new SmtpClient(Configuration.Instance.SmtpHost)
                {
                    Port = 587,
                    Credentials = new NetworkCredential(Configuration.Instance.SmtpUsername, Configuration.Instance.SmtpPassword),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(Configuration.Instance.SmtpUsername),
                    Subject = subject,
                    Body = contents,
                    IsBodyHtml = true,
                };
                foreach (var item in Configuration.Instance.SmtpSendTo.Split(";"))
                {
                    mailMessage.To.Add(item);
                }
                if (tryCatch)
                {
                    try
                    {
                        smtpClient.Send(mailMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[mail] sending email failure: " + ex.ToString());
                        SystemManager.WriteToEventLog("The following error occured while sending email: " + ex.ToString());
                    }
                }
                else
                {
                    smtpClient.Send(mailMessage);
                }
            }
        }
        public static string GenerateZoneBlock()
        {
            string thing = "";
            foreach (var item in Configuration.Instance.Zones)
            {
                string color = ZoneController.ZoneStates[item.Key] == PinValue.Low ? "green" : "red";
                if (item.Value.Type == ZoneType.None)
                    color = "gray";
                thing += "<p style=\"color:" + color + ";\"" + $">Zone #{item.Key + 1}: {item.Value.Name}</p>";
            }

            return thing;
        }

    }
}
