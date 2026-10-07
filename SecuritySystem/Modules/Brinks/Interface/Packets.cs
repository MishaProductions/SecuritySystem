using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem.Modules.Brinks;

public abstract class Packet
{
    public byte[] Data { get; set; }
    public byte Hash1 { get; internal set; }
    public byte Hash2 { get; internal set; }

    public Packet(byte[] p)
    {
        Data = p;
    }
    public Packet() { Data = []; }

    public static Packet? GetPacketFromByteArray(byte[] packet, byte hashBitA, byte hashBitB)
    {
        if (packet.Length == 2)
        {
            return new SyncReply(packet) { Hash1 = hashBitA, Hash2 = hashBitB };
        }
        else if (packet.Length == 3)
        {
            return new SyncPing(packet) { Hash1 = hashBitA, Hash2 = hashBitB };
        }
        else if (packet.Length >= 7)
        {
            return new Message(packet) { Hash1 = hashBitA, Hash2 = hashBitB };
        }
        else
        {
            Console.WriteLine("Null packet! length is " + packet.Length);
        }

        return null;
    }
    public virtual byte[] GetContents()
    {
        return Data;
    }
}
public class Message : Packet
{
    public ushort Dest { get; set; }
    public ushort Source { get; set; }
    /// <summary>
    /// Unknown byte - always 4
    /// </summary>
    public byte N { get; set; }
    public ushort Length { get; set; }
    public ushort Flags { get; set; }
    public ushort Command { get; set; }
    public byte[] Args { get; set; }

    public Message(Command command, byte[] args)
    {
        this.Command = (ushort)command;
        Args = args;
    }

    public Message(byte[] p) : base(p)
    {
        BinaryReader packetReader = new BinaryReader(new MemoryStream(p));
        Dest = packetReader.ReadUInt16();
        Source = packetReader.ReadUInt16();

        N = packetReader.ReadByte(); //unsigned char
        if (N != 4)
            Console.WriteLine("N=" + N);
        Length = packetReader.ReadUInt16();

        if (Length + 5 != p.Length)
        {
            if (Length + 7 != p.Length)
            {
               // Console.WriteLine("Packet size error");
            }
        }

        packetReader.BaseStream.Position = 7;
        Command = packetReader.ReadUInt16();

        ushort flags = 0;
        if ((Dest & 0x8000) != 0)
        {
            Dest &= 0x7FFF;
            flags |= 1;
        }
        if ((Source & 0x8000) != 0)
        {
            Source &= 0x7FFF;
            flags |= 2;
        }
        Flags = flags;

        Args = new byte[packetReader.BaseStream.Length - packetReader.BaseStream.Position];

        var idx = 0;
        for (long i = packetReader.BaseStream.Position; i < packetReader.BaseStream.Length; i++)
        {
            Args[idx] = packetReader.ReadByte();
            idx++;
        }
    }

    public static Message CreateMessage(ushort src, ushort dest, Command command, ushort flags, byte[] args)
    {
        var dest_field = dest;
        if ((flags & 1) != 0)
        {
            dest_field = (ushort)(dest | 0x8000);
        }

        var src_field = src;
        if ((flags & 2) != 0)
        {
            src_field = (ushort)(src | 0x8000);
        }


        byte[] packet = new byte[1024];
        var ms = new MemoryStream(packet);
        BinaryWriter b = new BinaryWriter(ms);
        b.Write(dest_field);
        b.Write(src_field);

        //write N field
        b.Write((byte)4);

        //Write length
        b.Write((ushort)(args.Length + 4));

        //write cmd
        b.Write((ushort)command);

        //write arguments
        foreach (var item in args)
        {
            b.Write(item);
        }

        //write hash
        b.Flush();

        //the buffer is too big
        var ar = ms.ToArray();
        List<byte> bb = new List<byte>();
        for (int i = 0; i < b.BaseStream.Position; i++)
        {
            bb.Add(ar[i]);
        }


        return new Message(bb.ToArray());
    }


    public override string ToString()
    {
        var r = $"Message {Source:X2}->{Dest:X2} {(Command)Command} ({Flags:X2}): [ ";
        foreach (var bb in Args) r += bb.ToString("X2") + " ";
        return r + "]";
    }
}
public class SyncPing : Packet
{
    public ushort Address { get; set; }
    public bool Flag { get; set; } = false;
    public byte Counter { get; set; }
    public SyncPing(byte[] p) : base(p)
    {
        var value = BitConverter.ToUInt16(p, 0);
        Address = (ushort)(value & 0x7FFF);
        Counter = p[2];
        Flag = (value & 0x8000) != 0;
    }

    public override string ToString()
    {
        var f = Flag == true ? "1" : "0";
        return $"->0x{Address:X2},counter=0x{Counter:X2},flag={f}";
    }

    public static SyncPing Create(ushort address, byte counter, bool flag)
    {
        byte[] buffer = new byte[3];
        BinaryWriter br = new BinaryWriter(new MemoryStream(buffer));

        var addr = address;
        if (flag == true)
            addr = (ushort)(addr | 0x8000);
        br.Write(addr);
        br.Write(counter);
        br.Flush();

        return new SyncPing(buffer);
    }
}
public class SyncReply : Packet
{
    public ushort Address { get; set; }
    public bool Flag { get; set; }
    public SyncReply(byte[] p) : base(p)
    {
        var value = BitConverter.ToUInt16(p, 0);
        Address = (ushort)(value & 0x7FFF);
        Flag = (value & 0x8000) != 0;
    }

    public static SyncReply Create(ushort address, bool flag)
    {
        var addr = address;
        if (flag == true)
            addr = (ushort)(addr | 0x8000);

        return new SyncReply(BitConverter.GetBytes(addr));
    }
    public override string ToString()
    {
        var f = Flag == true ? "1" : "0";
        return $"<({f}) {Address:X2}->PONG>";
    }
}