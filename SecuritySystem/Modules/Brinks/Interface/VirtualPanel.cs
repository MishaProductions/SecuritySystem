using System.Collections;
using System.Runtime.InteropServices;
using System.Timers;


namespace SecuritySystem.Modules.Brinks;

internal class VirtualPanel
{
    const int BROADCAST = 0x7FFF;
    static Interface i = null!;

    public Device[] Devices = new Device[3];

    public void Begin(string comport)
    {
        try
        {
            i = new(comport);
            var busThead = new Thread(new ThreadStart(i.Run));
            busThead.Start();

            i.OnPacket += OnPacket;

            for (int j = 0; j < 5; j++)
            {
                i.DoSyncPing(0x7FFE);
                i.DoSyncPing(1);
                i.Dequeue();
                Thread.Sleep(50);
            }

            for (int j = 0; j < 1; j++)
            {
                i.SendBroadcast(Command.EventAnnouncement, [01, 00, 00, 00, 00, 00, 00]);
                i.Dequeue();
                Thread.Sleep(50);
            }
            DateTime lastAttempt = DateTime.Now;


            Devices[1] = new LcdDevice(i)
            {
                Address = 1,
                SerialNumber = [0, 0, 0, 3, 0x77, 0xA2]
            };

            while (true)
            {
                foreach (var item in Devices)
                {
                    if (item != null)
                    {
                        if (!item.Accepted)
                        {
                            if ((DateTime.Now - lastAttempt).TotalMilliseconds >= 500)
                            {
                                List<byte> msg = [.. item.SerialNumber, .. BitConverter.GetBytes(item.Address)];
                                i.SendMessage(0, BROADCAST, Command.DeviceAddressOffer, msg.ToArray());
                                lastAttempt = DateTime.Now;
                            }
                        }

                        item.Cycle();
                        if (item.PendingPackets.Count > 0)
                        {
                            while (item.PendingPackets.Count > 0)
                            {
                                var b = item.PendingPackets.Dequeue();
                                i.SendMessage(0, item.Address, (Command)b.Command, b.Args);
                                break; // we can only send one packet per cycle
                            }
                        }
                        else
                        {
                            i.DoSyncPing(item.Address);
                        }

                        if ((DateTime.Now - item.LastPing).TotalSeconds >= 2)
                        {
                            item.Accepted = false;
                            item.Stop();
                            i.Sync(item.Address).Flags = 1;
                        }
                    }
                }

                i.DoSyncPing(0x7FFE);
                Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void OnPacket(Packet p)
    {
        if (p is Message m)
        {
            if (Devices[m.Source] == null) return;
            Devices[m.Source].LastPing = DateTime.Now; // message also counts as a ping
            if (m.Command == (ushort)Command.DeviceAddressAccepted)
            {
                if (m.Source >= Devices.Length) return;

                Devices[m.Source].Accepted = true;
                Task.Run(Devices[m.Source].Start);
            }
            else
            {
                Devices[m.Source].OnMessage((Command)m.Command, m.Args);
            }
        }

        else if (p is SyncReply s)
        {
            if (Devices[s.Address] == null) return;
            if (Devices.Length > s.Address)
            {
                Devices[s.Address].LastPing = DateTime.Now;
            }
        }
    }
}