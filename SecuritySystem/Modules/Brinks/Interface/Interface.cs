using System.IO.Ports;

/*
    NOTICE: This file is licensed under GPLv3.
    This code is based on: https://github.com/flarn2006/BHSTools/blob/master/intellibus/main.py
*/

namespace SecuritySystem.Modules.Brinks;

public class Interface
{
    public byte Counter = 1;
    public SerialPort Port;
    private Queue<Packet> ToSend = new Queue<Packet>();
    private Dictionary<ushort, SyncState> Syncs = new Dictionary<ushort, SyncState>();
    public Interface(string ComPort)
    {
        Port = new SerialPort(ComPort, 38400, Parity.None, 8, StopBits.One);
        Port.Open();
    }
    #region Serial Stuff
    public byte GetByte()
    {
        byte[] buffer = new byte[1];
        Port.Read(buffer, 0, 1);

        return buffer[0];
    }
    #endregion

    public Packet? Read()
    {
        var packet = InternalPacketRead();
        bool isSynced = true;
        if (packet is SyncPing p)
        {
            // Should NEVER be recieved, only sent by the panel itself!
        }
        else if (packet is SyncReply r)
        {
            Sync(r.Address).Receive(r);

            // sync reply flag is set if the device's MasterSync is set.
            // this flips each time the device recieves a message from the panel.
            // TODO: check if the device recieved a sent message from the panel
        }
        else if (packet is Message m)
        {
            if (0x7001 <= m.Dest && m.Dest <= 0x707F)
            {
                if ((m.Dest & 0xFF) == Counter)
                {
                    Counter = (byte)(Counter % 0x7F + 1);
                }
                else
                {
                    isSynced = false;
                }
            }
            else if (m.Dest == 0)
            {
                isSynced = Sync(m.Source).Receive(m);
            }
            else if (m.Dest != 0x7FFF)
            {
                isSynced = Sync(m.Dest).Receive(m);
            }

            // HACK
            if (m.Command == (ushort)Command.DeviceAddressAccepted)
            {
                isSynced = true;
            }
        }
        if (!isSynced)
        {
            return null;
        }
        return packet;
    }

    public void Dequeue()
    {
        while (ToSend.Count > 0)
        {
            var pkt = ToSend.Dequeue();
            FormatPacket(pkt, true);

            SendRawMessageNow(pkt.Data);
        }
    }

    public void DoSyncPing(ushort address)
    {
        var flag = address != 0x7FFE ? ((Sync(address).Flags & 1) != 0) : false;
        SyncPing x = SyncPing.Create(address, Counter, flag);
        SendRaw(x);
    }
    private Packet? InternalPacketRead()
    {
        while (true)
        {
            while (GetByte() != '\x1e')
            {
            }
            List<byte> data = new List<byte>();
            bool found_non_1e = false;
            while (true)
            {
                var b = GetByte();

                if (b == 0x1E)
                {
                    if (found_non_1e)
                        break;
                }
                else
                {
                    found_non_1e = true;
                    data.Add(b);
                }
            }
            for (int i = 0; i < data.Count; i++)
            {
                if (i != data.Count - 1)
                {
                    var current = data[i];
                    var next = data[i + 1];

                    if (current == 0x7D && next == 0x3E)
                    {
                        data.RemoveAt(i);
                        data.RemoveAt(i);

                        data.Insert(i, 0x1e);
                    }

                    if (current == 0x7D && next == 0x5D)
                    {
                        data.RemoveAt(i + 1);
                    }
                }
            }


            if (data.Count == 0 || data.Count == 1)
                return null;
            var a = data[data.Count - 2];
            var bb = data[data.Count - 1];
            //remove last 2 bytes (they are a checksum)
            data.RemoveAt(data.Count - 1);
            data.RemoveAt(data.Count - 1);
            return Packet.GetPacketFromByteArray(data.ToArray(), a, bb);
        }
    }

    public void SendMessage(ushort src, ushort dest, Command command, byte[] args, int repeatCount = 1, bool doSync = true)
    {
        ushort flags = 0;

        if (dest == 0x7FFF) doSync = false; // Broadcast does not sync
        if (doSync)
        {
            flags = Sync(src == 0 ? dest : src).Next(src == 0);
        }
        Message message = Message.CreateMessage(src, dest, command, flags, args);
        for (int i = 0; i < repeatCount; i++)
        {
            SendRaw(message);
        }
    }
    public object Locker { get; set; } = new();
    private (Command command, byte[] bytes)[] broadcasts = new (Command command, byte[] bytes)[127];
    internal void SendBroadcast(Command command, byte[] bytes)
    {
        lock (Locker)
        {
            var dest = (ushort)(0x7000 + Counter);
            //  broadcasts[Counter] = (command, bytes); //todo solve this for good
            Message message = Message.CreateMessage(0, dest, command, 0, bytes);
            SendRaw(message);
           // Console.WriteLine("broadcast " + Counter);
            Counter = (byte)(Counter % 0x7F + 1);
        }
    }
    public void SendMessage(ushort src, ushort dest, Command command, byte[] args, ushort flags, int repeatCount = 1)
    {
        Message message = Message.CreateMessage(src, dest, command, flags, args);
        for (int i = 0; i < repeatCount; i++)
        {
            SendRaw(message);
        }
    }
    private void SendRaw(Packet p)
    {
        //ToSend.Enqueue(p);

        FormatPacket(p, true);
        SendRawMessageNow(p.Data);
    }
    public void SendRawMessageNow(byte[] message)
    {
        var c = Crc(message); //mcrf4xx
        var chars = message.ToList();

        for (int i = 0; i < chars.Count; i++)
        {
            if (i != chars.Count - 1)
            {
                var current = chars[i];

                if (current == 0x7D)
                {
                    chars.Insert(i + 1, 0x5d);
                }

                if (current == 0x1E)
                {
                    chars[i] = 0x7d;
                    chars.Insert(i + 1, 0x3e);
                }
            }
        }
        List<byte> toSend =
        [
            0x1E,
                //add the packet contents
                .. chars,
                //add the checksum
                c[0],
                c[1],
                0x1E
        ];

        Port.Write(toSend.ToArray(), 0, toSend.Count);
    }
    public SyncState Sync(ushort address)
    {
        if (0x7001 <= address && address <= 0x707F) throw new Exception("sync should not be called for broadcast address");

        if (Syncs.ContainsKey(address))
        {
            return Syncs[address];
        }

        Syncs.Add(address, new(address, address == 0, address != 0));
        return Syncs[address];
    }

    public delegate void PacketRecieved(Packet p);
    public event PacketRecieved? OnPacket;
    public void Run()
    {
        while (true)
        {
            try
            {
                var p = Read();
                if (p != null)
                {
                    FormatPacket(p, false);

                    OnPacket?.Invoke(p);
                }
                else
                {
                    // Console.WriteLine("Null!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }

    private void FormatPacket(Packet p, bool tx)
    {
        return; // disabled in production
        if (p is SyncPing || p is SyncReply) return;
        string marker = tx ? "TX" : "RX";
        var old = Console.ForegroundColor;
        Console.ForegroundColor = tx ? ConsoleColor.Green : ConsoleColor.Red;
        if (p is Message m)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss:FFF}] {marker}: " + m.ToString());
        }
        else if (p is SyncReply r)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss:FFF}] {marker}: SyncReply" + r.ToString());
        }
        else if (p is SyncPing pi)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss:FFF}] {marker}: SyncPing" + pi.ToString());
        }
        Console.ForegroundColor = old;
    }
    #region CRC
    private static byte[] Crc(byte[] message)
    {
        ushort crc = 0xffff;
        for (int i = 0; i < message.Length; i++)
        {
            crc = CRCOfByte(crc, message[i]);
        }
        return BitConverter.GetBytes((ushort)crc);
    }

    private static ushort CRCOfByte(ushort Crc, byte Byte)
    {
        //make byte 16 bit format
        byte TempByte = Byte;

        for (byte i = 0; i < 8; i++)
        {
            if ((Crc & 0x0001) == (TempByte & 0x0001))
            {
                //right shift crc
                Crc >>= 1;
                //right shift data
                TempByte >>= 1;
            }
            else
            {
                Crc >>= 1;
                TempByte >>= 1;
                Crc = (ushort)(Crc ^ 0x8408);
            }
        }

        return Crc;
    }
    #endregion
}
