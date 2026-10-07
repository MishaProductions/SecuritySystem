
namespace SecuritySystem.Modules.Brinks;

/*
    NOTICE: This file is licensed under GPLv3.
    This code is based on: https://github.com/flarn2006/BHSTools/blob/master/intellibus/main.py
*/

public class SyncState
{
    public ushort Flags { get; set; }
    public ushort MyAddress { get; set; }
    public SyncState(ushort Address, bool Master = false, bool Slave = false)
    {
        this.MyAddress = Address;
        Flags = 0;
        if (Master)
        {
            Flags |= 2;
        }
        if (Slave)
        {
            Flags |= 1;
        }
    }

    public override string ToString()
    {
        return $"0x{Flags:X2},MyAddress=0x{MyAddress:X2}";
    }

    public bool Check(Packet p)
    {
        if (p is Message m)
        {
            if (m.Source == MyAddress)
            {
                return (Flags ^ m.Flags) == 1;
            }
            else if (m.Dest == MyAddress)
            {
                return (Flags ^ m.Flags) == 2;
            }
        }
        return true;
    }

    public bool Receive(Packet p)
    {
        if (p is SyncPing ping)
        {
            if (ping.Address == MyAddress)
            {
                if (ping.Flag)
                {
                    Flags |= 1;
                }
                else
                {
                    Flags &= 2;
                }
            }
            return true;
        }
        else if (p is SyncReply r)
        {
            if (r.Address == MyAddress)
            {
                if (r.Flag)
                {
                    Flags |= 2;
                }
                else
                {
                    Flags &= 1;
                }
            }
            return true;
        }
        else if (p is Message m)
        {
            bool accept = Check(m);
            if (accept)
                Flags = m.Flags;

            return accept;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public ushort Next(bool as_master)
    {
        Flags ^= (ushort)(as_master ? 2 : 1);
        return Flags;
    }

    public SyncReply Reply()
    {
        return SyncReply.Create(MyAddress, Convert.ToBoolean(Flags & 2));
    }
}
