using SecuritySystem.Alsa;
using SecuritySystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem
{
    public class MusicStreamServer
    {
        public static void StartAnncHandlerServer()
        {
            Thread handler = new Thread(AnncHandlerServer);
            handler.Start();
        }

        private static void AnncHandlerServer(object? obj)
        {
            TcpListener srv = new TcpListener(IPAddress.Any, 1234);
            srv.Start();

            while (true)
            {
                try
                {
                    var client = srv.AcceptTcpClient();

                    //client.ReceiveTimeout = 2000;
                    var network = client.GetStream();
                    BinaryReader br = new BinaryReader(network);

                    var auth = br.ReadString().Replace("AUTHORIZATION=", "");
                    Console.WriteLine("RX: " + auth);

                    bool found = false;
                    //verify token from auth header
                    foreach (var item in Configuration.Instance.Tokens)
                    {
                        if (item.Key == auth)
                        {
                            found = true;
                        }
                    }

                    if (found)
                    {
                        // user provided authentication token is correct
                        network.WriteByte(1);
                        br = new BinaryReader(network);

                        MusicPlayer.StartAsyncMicAnnc();

                        string wavData = br.ReadString(); // samplerate,bits

                        string[] keys = wavData.Split(",");
                        int sampleRate = int.Parse(keys[0]);
                        int bits = int.Parse(keys[1]);
                        int blockalightn = int.Parse(keys[2]);
                        Console.WriteLine("Sample rate: " + sampleRate + ", PCM bits: " + bits);
                        UnixPCMDevice sound = new UnixPCMDevice(new Iot.Device.Media.SoundConnectionSettings());
                        sound.Open((ushort)bits, (uint)sampleRate, (ushort)blockalightn);


                        while (true)
                        {
                            try
                            {
                                if (!sound.Write(network))
                                {
                                    Console.WriteLine("broken connection");
                                    client.Close();

                                    sound.Close();
                                    sound.Dispose();
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("annc CLOSE: " + ex.Message);
                                break;
                            }
                        }

                        MusicPlayer.StopAsyncMicAnnc();
                    }
                    else
                    {
                        network.WriteByte(0);
                        network.Close();
                        network.Dispose();
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exceptition during music stream server: " + e);
                    SystemManager.WriteToEventLog("Exceptition during music stream server: " + e);
                }
            }
        }
    }
}
