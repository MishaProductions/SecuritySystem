using EmbedIO;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecuritySystem.Utils;
using SecuritySystemApi;
using System.Security.Cryptography;
using System.Text;
using MHSApi.API;

namespace SecuritySystem
{
    public sealed partial class SecurityApiController : WebApiController
    {
        private async Task SendGenericResponse(SecurityApiResult code)
        {
            var response = new ApiResponse() { code = code, success = APIUtils.IsSuccessfulResult(code), message = APIUtils.ErrorToString(code), };
            await HttpContext.SendStringAsync(JsonConvert.SerializeObject(response), "application/json", Encoding.Unicode);
        }
        private async Task SendSuccessfulResponseWithContent<T>(T content)
        {
            var response = new ApiResponseWithContent<T>() { code = SecurityApiResult.Success, message = APIUtils.ErrorToString(SecurityApiResult.Success), success = true, content = content };
            await HttpContext.SendStringAsync(JsonConvert.SerializeObject(response), "application/json", Encoding.Unicode);
        }
        private async Task SendSuccessfulResponseWithCustomMessage(string message)
        {
            var response = new JObject
                    {
                        { "success", new JValue(true) },
                        { "message", new JValue(message) },
                        { "code", new JValue(0) },
                        { "content", new JObject() }
                    };
            await HttpContext.SendStringAsync(response.ToString(), "application/json", Encoding.Unicode);
        }
        private async Task SendUnSuccessfulResponseWithCustomMessage(string message)
        {
            var response = new JObject
                    {
                        { "success", new JValue(false) },
                        { "message", new JValue(message) },
                        { "code", new JValue(-1) },
                        { "content", new JObject() }
                    };
            await HttpContext.SendStringAsync(response.ToString(), "application/json", Encoding.Unicode);
        }
        private static string CreateAuthToken(User user)
        {
            var tok = RandomString(128);
            Configuration.Instance.Tokens.Add(tok, user.Username);
            Configuration.Save();
            return tok;
        }
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        public static string Sha256(string secret)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var secretHash = SHA256.HashData(secretBytes);
            return Convert.ToHexString(secretHash);
        }

        /// <summary>
        /// Reads the request JSON, and parses it to object of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="EmbedIO.HttpException"></exception>
        public async Task<T> ParseRequestJson<T>()
        {
            string json = await HttpContext.GetRequestBodyAsStringAsync();
            if (json == null) throw new HttpException(System.Net.HttpStatusCode.BadRequest);

            T? c = JsonConvert.DeserializeObject<T>(json);
            if (c == null) throw new HttpException(System.Net.HttpStatusCode.BadRequest);

            return c;
        }
    }
}
