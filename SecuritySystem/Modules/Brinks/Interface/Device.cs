namespace SecuritySystem.Modules.Brinks;

/// <summary>
/// high level representation of a brinks device
/// </summary>
public abstract class Device
{
    /// <summary>
    /// current device address. If no address has been assigned, it is 0xFFFF.
    /// </summary>
    public ushort Address { get; set; } = 0xFFFF;
    /// <summary>
    /// Indicates if the device is on the RS485 bus
    /// </summary>
    public bool Responsive { get; set; }

    /// <summary>
    /// Pending packets queued to the device
    /// </summary>
    public Queue<Message> PendingPackets { get; set; } = new();

    /// <summary>
    /// The brinks interface that the device belongs to
    /// </summary>
    public Interface Interface { get; }
    /// <summary>
    /// The serial number of the device
    /// </summary>
    public byte[] SerialNumber = [];
    /// <summary>
    /// True if the device is present on the RS485 bus, false otherwise
    /// </summary>
    public bool Accepted { get; set; }
    public DateTime LastPing { get; set; }

    public Device(Interface i)
    {
        Interface = i;
    }

    /// <summary>
    /// Indicates when the device begins communication
    /// </summary>
    public abstract void Start();

    /// <summary>
    /// Indicates when the device is disconnected from the bus. Clean up anything here.
    /// </summary>
    public abstract void Stop();

    /// <summary>
    /// Called every communication cycle to queue packets
    /// </summary>
    public abstract void Cycle();

    /// <summary>
    /// Called when the device sends a message
    /// </summary>
    /// <param name="cmd">Command</param>
    /// <param name="args">Command arguments</param>
    public abstract void OnMessage(Command cmd, byte[] args);


    /// <summary>
    /// Queues a command to send to the device
    /// </summary>
    protected void SendMessage(Command cmd, byte[] args)
    {
        PendingPackets.Enqueue(new Message(cmd, args));
    }
}
