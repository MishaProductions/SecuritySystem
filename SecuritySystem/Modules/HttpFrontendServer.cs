using EmbedIO;
using EmbedIO.WebApi;
using SecuritySystem.Utils;
using SecuritySystem.WebSrv.Websocket;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SecuritySystem.Modules
{
    public static class HttpFrontendServer
    {
        private static bool _isWebserverRunning = false;
        public static X509Certificate2 GenerateSelfSignedCertificate()
        {
            string secp256r1Oid = "1.2.840.10045.3.1.7";  //oid for prime256v1(7)  other identifier: secp256r1

            string subjectName = "Security System";

            var ecdsa = ECDsa.Create(ECCurve.CreateFromValue(secp256r1Oid));

            var certRequest = new CertificateRequest($"CN={subjectName}", ecdsa, HashAlgorithmName.SHA256);

            //add extensions to the request (just as an example)
            //add keyUsage
            certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));

            var san = new SubjectAlternativeNameBuilder();
            san.AddIpAddress(IPAddress.Loopback);
            san.AddIpAddress(IPAddress.IPv6Loopback);
            san.AddDnsName("localhost");
            san.AddDnsName(Environment.MachineName);
            san.AddDnsName(GetLocalIPAddress());
            certRequest.CertificateExtensions.Add(san.Build());

            // generate the cert and sign!
            X509Certificate2 generatedCert = certRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));

            return generatedCert;
        }
        private static void HttpsRedirectThead()
        {
            var server = new WebServer(o => o
            .WithUrlPrefixes("http://*:80/")
              .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithAction(HttpVerbs.Any, HandleHttpRequest);

            server.RunAsync().Wait();
        }

        private static Task HandleHttpRequest(IHttpContext context)
        {
            context.Redirect("https://" + context.Request.Url.Host + context.Request.Url.PathAndQuery);

            return Task.CompletedTask;
        }

        private static void WebServerThread()
        {
            // generate certificate
            X509Certificate2 cert;

            var certPath = AppDomain.CurrentDomain.BaseDirectory + "cert.pfx";
            if (!File.Exists(certPath))
            {
                cert = GenerateSelfSignedCertificate();

                var exportedCert = cert.Export(X509ContentType.Pkcs12);
                File.WriteAllBytes(certPath, exportedCert);

                cert = X509CertificateLoader.LoadPkcs12(exportedCert, null);
            }
            else
            {
                var certBytes = File.ReadAllBytes(certPath);

                cert = X509CertificateLoader.LoadPkcs12(certBytes, null);
            }

            var server = new WebServer(o => o
                 .WithUrlPrefixes("https://*:443/", "http://*:80/")
                 .WithMode(HttpListenerMode.EmbedIO).WithCertificate(cert))
             // First, we will configure our web server by adding Modules.
             .WithModule(new CorsFixer("/"))
             .WithCors()
             .WithLocalSessionManager()
             .WithStaticFolder("/client/", AppDomain.CurrentDomain.BaseDirectory + "/www/client/", true)
             .WithWebApi("/api", m => m
             .WithController<SecurityApiController>())
             .WithModule(new SecurityWebSocketModuleV2("/wsv2"))
             .WithModule(new AudioInputWebSocketModule("/mixer/audioin/ws"));
            server = server.WithStaticFolder("/", AppDomain.CurrentDomain.BaseDirectory + "/www/modernclient/", false);
            server.HandleHttpException(async (ctx, ex) =>
            {
                ctx.Response.StatusCode = ex.StatusCode;

                if (!HttpStatusDescription.TryGet(ex.StatusCode, out string description))
                {
                    description = "Status code has no standard description.";
                }

                if (ctx.Request.Url.PathAndQuery.StartsWith("/api/"))
                {
                    await ctx.SendStringAsync("{\"sucesss\":false,\"message\":\"" + description + "\",\"code\":" + ex.StatusCode + "}", "application/json", Encoding.UTF8);
                }
                else
                {
                    var msg = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/www/err.html");
                    msg = msg.Replace("{ErrorMsgTitle}", ex.StatusCode.ToString());
                    msg = msg.Replace("{ErrorMsgDesc}", description);
                    await ctx.SendStringAsync(msg, "text/html", Encoding.Unicode);
                }
            });

            server.RunAsync().Wait();
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                string ipa = ip.ToString();
                if (ip.AddressFamily == AddressFamily.InterNetwork && !ipa.StartsWith("127.") && !ipa.StartsWith("169."))
                {
                    return ipa;
                }
            }
            return "<No Network Adapters/Connection>";
        }
        /// <summary>
        /// Starts HTTP and HTTPS server
        /// </summary>
        public static void Start()
        {
            if (_isWebserverRunning)
                return;
            _isWebserverRunning = true;

            new Thread(WebServerThread).Start();
            //new Thread(HttpsRedirectThead).Start();
        }
    }
}
