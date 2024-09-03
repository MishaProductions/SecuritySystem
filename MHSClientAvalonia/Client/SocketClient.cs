using MHSClientAvalonia.Utils;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Client
{
    public class PlugifyWebSocketClient
    {
        private string URL = "";
        public bool IsOpen { get; set; }
        private ClientWebSocket client = new ClientWebSocket();
        public event EventHandler<string>? OnMessage;
        public event EventHandler? OnClose;
        CancellationTokenSource disposalTokenSource = new CancellationTokenSource();

        public void SetUrl(string url)
        {
            this.URL = url;
        }
        public async Task Start(bool recieveLoop = true)
        {
            if (string.IsNullOrEmpty(URL))
                throw new Exception("url cannot be empty in PlugifyWebSocketClient");

            client = new ClientWebSocket();

            if (!BrowserUtils.IsBrowser)
            {
                // Please use this in a production environment
                client.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            await client.ConnectAsync(new Uri(URL), disposalTokenSource.Token);
            IsOpen = true;

            if (recieveLoop)
                _ = ReceiveLoop();
        }

        private async Task ReceiveLoop()
        {
            while (!disposalTokenSource.IsCancellationRequested)
            {
                if (client.State == WebSocketState.Open | client.State == WebSocketState.CloseSent)
                {
                    var message = await Receive();
                    if (message != null)
                    {
                        if (OnMessage != null)
                            OnMessage.Invoke(this, message);
                    }
                }
                else
                {
                    break;
                }
            }
        }
        public async Task Send(string data)
        {
            Console.WriteLine("TX: " + data);
            if (client.State == WebSocketState.Open)
            {
                await client.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                // make sure that onclose is called once
                if (IsOpen)
                {
                    IsOpen = false;
                    OnClose?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public async Task Send(byte[] data)
        {
            if (client.State == WebSocketState.Open)
            {
                await client.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            else
            {
                // make sure that onclose is called once
                if (IsOpen)
                {
                    IsOpen = false;
                    OnClose?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public async Task<byte[]?> ReceiveBytes()
        {
            try
            {
                var buffer = new ArraySegment<byte>(new byte[2048]);
                do
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await client.ReceiveAsync(buffer, CancellationToken.None);
                            if (buffer.Array != null)
                                ms.Write(buffer.Array, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        return ms.ToArray();
                    }
                } while (true);
                IsOpen = false;
                return null;
            }
            catch
            {
                if (IsOpen)
                    OnClose?.Invoke(this, EventArgs.Empty);
                IsOpen = false;
                client.Dispose();
                return null;
            }
        }
        public async Task<string?> Receive()
        {
            try
            {
                var buffer = new ArraySegment<byte>(new byte[2048]);
                do
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await client.ReceiveAsync(buffer, CancellationToken.None);
                            if (buffer.Array != null)
                                ms.Write(buffer.Array, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        ms.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                            return await reader.ReadToEndAsync();
                    }
                } while (true);
                IsOpen = false;
                return null;
            }
            catch
            {
                if (IsOpen)
                    OnClose?.Invoke(this, EventArgs.Empty);
                IsOpen = false;
                client.Dispose();
                return null;
            }
        }

        public void Close()
        {
            if (client != null)
            {
                try
                {
                    disposalTokenSource.Cancel();
                    client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
