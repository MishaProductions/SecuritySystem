using SecuritySystemApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class LoginResponse : ApiResponseContent
    {
        public string token { get; set; } = "";

        public LoginResponse(string token)
        {
            this.token = token;
        }
        public LoginResponse() { }
    }
}
