using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class SaveSysConfig
    {
        public string NewUsername;
        public string NewPassword;
        public bool IsOrangePiDriver;
        public SaveSysConfig(string newUsername, string newPassword, bool isOrangePiDriver)
        {
            NewUsername = newUsername;
            NewPassword = newPassword;
            IsOrangePiDriver = isOrangePiDriver;
        }
    }
}
