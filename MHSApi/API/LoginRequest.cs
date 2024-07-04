using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class LoginRequest
    {
        public string username { get; set; } = "";
        public string password { get; set; } = "";

        public LoginRequest(string username, string password)
        {
            this.username = username;
            this.password = password;
        }
        public LoginRequest() { }
    }
}
