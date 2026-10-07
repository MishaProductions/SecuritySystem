
namespace SecuritySystem.Modules.Brinks;

/// <summary>
/// List of commonly sent and recieved commands from BHS4000 keypad.
/// </summary>
public enum Command : ushort
{
    ResendBroadcast = 31,
    ConfigureKeypad = 2002,
    LCDOutput = 2010,
    StandardLcdOutput = 2011,
    CfgLedState = 2015,
    KeypadBeep = 2016,
    KeypadButton = 2020,
    EventAnnouncement = 2050,
    DeviceAddressRequest = 3001,
    DeviceAddressOffer = 3002,
    DeviceAddressAccepted = 3003,
    LearningDevices = 3004
}