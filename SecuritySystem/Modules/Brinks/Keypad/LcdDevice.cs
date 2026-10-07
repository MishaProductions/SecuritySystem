using System.Timers;
using SecuritySystem.Utils;

namespace SecuritySystem.Modules.Brinks;

/// <summary>
/// Represents a BHS 4000 "Icon Keypad" or Premium keypad
/// </summary>
public class LcdDevice : Device
{
    private System.Timers.Timer displayRefresh;

    public KeypadPage? currentPage = null!;

    public LcdDevice(Interface i) : base(i)
    {
        displayRefresh = new();
        displayRefresh.Elapsed += DisplayRefresh_Elapsed;
        displayRefresh.Interval = 1000;
    }
    public override void Cycle()
    {
        // unused
    }

    /// <summary>
    /// takes key byte and returns key and down/up value
    /// </summary>
    /// <param name="key"></param>
    /// <returns>(Key, IsDown)</returns>
    private (LcdKey, bool) ProcessKey(byte key)
    {
        switch (key)
        {
            case 0xE6: return (LcdKey.Fire, true);
            case 0x66: return (LcdKey.Fire, false);
            case 0xED: return (LcdKey.Hospital, true);
            case 0x6D: return (LcdKey.Hospital, false);
            case 0xF0: return (LcdKey.Fire, true);
            case 0x70: return (LcdKey.Fire, false);

            case 0xB1: return (LcdKey.One, true);
            case 0x31: return (LcdKey.One, false);
            case 0xB2: return (LcdKey.Two, true);
            case 0x32: return (LcdKey.Two, false);
            case 0xB3: return (LcdKey.Three, true);
            case 0x33: return (LcdKey.Three, false);
            case 0xB4: return (LcdKey.Four, true);
            case 0x34: return (LcdKey.Four, false);
            case 0xB5: return (LcdKey.Five, true);
            case 0x35: return (LcdKey.Five, false);
            case 0xB6: return (LcdKey.Six, true);
            case 0x36: return (LcdKey.Six, false);
            case 0xB7: return (LcdKey.Seven, true);
            case 0x37: return (LcdKey.Seven, false);
            case 0xB8: return (LcdKey.Eight, true);
            case 0x38: return (LcdKey.Eight, false);
            case 0xB9: return (LcdKey.Nine, true);
            case 0x39: return (LcdKey.Nine, false);
            case 0xB0: return (LcdKey.Zero, true);
            case 0x30: return (LcdKey.Zero, false);

            case 0x98: return (LcdKey.Cancel, true);
            case 0x18: return (LcdKey.Cancel, false);
            case 0xEF: return (LcdKey.On, true);
            case 0x6F: return (LcdKey.On, false);
            case 0xE4: return (LcdKey.MotionOff, true);
            case 0x64: return (LcdKey.MotionOff, false);
            case 0xE9: return (LcdKey.Instant, true);
            case 0x69: return (LcdKey.Instant, false);
            case 0x8d: return (LcdKey.Options, true);
            case 0x0d: return (LcdKey.Options, false);
        }

        return (LcdKey.None, false);
    }

    public override void OnMessage(Command cmd, byte[] args)
    {
        if (cmd == Command.KeypadButton)
        {
            // note: args may contain multiple keycodes from the keypad buffer
            // some keys have a 0x1b prefix, ignore it as there are no collisions
            foreach (var item in args)
            {
                if (item == 0x1B) continue;

                var key = ProcessKey(item);

                if (key.Item2) currentPage?.KeyDown(key.Item1);
                else currentPage?.KeyUp(key.Item1);
            }
        }
    }

    /// <summary>
    /// queues a display update
    /// </summary>
    /// <param name="dev"></param>
    public void UpdateLcd(KeypadLcd dev)
    {
        SendMessage(Command.LCDOutput, dev.Build());
    }

    public override void Start()
    {
        // Unknown as to what the arguments do
        SendMessage(Command.ConfigureKeypad, [0, 0, 0x1f, 0, 0, 0]);

        SendMessage(Command.LCDOutput, [0x00, 0x1F, 0x7F, 0x7F, 0x7F, 0x0FF, 0x0FF, 0xFF, 0x07]); // Display all segments
        SendMessage(Command.StandardLcdOutput, [0x0, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1]);
        BeepOutput(0, 0, 0, 0, 0, 0, 0, 0); // Mute speaker
        SendMessage(Command.CfgLedState, [0, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1]);

        Thread.Sleep(5000); // wait for the keypad to finish initialization

        displayRefresh.Start();
        NavigateToPage(LcdPageType.Home);

        SystemManager.OnSysTimerEvent += SysMan_OnSysTimerEvent;
        SystemManager.OnSystemDisarm += SysMan_OnSysDisarmed;
        SystemManager.OnAlarm += SysMan_OnAlarm;


        if (Configuration.Instance.SystemArmed)
        {
            UpdateLedState(LcdLedArmedState.OnSolid);
        }
        else
        {
            UpdateLedState(LcdLedArmedState.Off);
        }
    }

    private void SysMan_OnAlarm(int alarmZone)
    {
        UpdateLedState(LcdLedArmedState.OnFlashing7);
        ContinousSound();
    }

    private void SysMan_OnSysDisarmed(object? sender, EventArgs e)
    {
        UpdateLedState(LcdLedArmedState.Off);
        MuteSpeaker();
    }

    private void SysMan_OnSysTimerEvent(bool entry, int timer)
    {
        if (timer != 0)
        {
            CountdownNoise();
            UpdateLedState(LcdLedArmedState.OnFlashing1);
        }
        else
        {
            MuteSpeaker();
            UpdateLedState(LcdLedArmedState.OnSolid);
        }
    }


    /// <summary>
    /// navigates to a lcd page of type
    /// </summary>
    /// <param name="type"></param>
    public void NavigateToPage(LcdPageType type)
    {
        currentPage?.Stop();
        currentPage = null;
        if (type == LcdPageType.Home)
        {
            currentPage = new HomePage(this);
            currentPage.Start();
        }
        else if (type == LcdPageType.Options)
        {
            currentPage = new OptionsPage(this);
            currentPage.Start();
        }
        currentPage?.Refresh();
    }
    private void DisplayRefresh_Elapsed(object? sender, ElapsedEventArgs e)
    {
        currentPage?.Refresh();
    }

    /// <summary>
    /// Update ON led status. Keeps LCD backlight and keyboard backlight on auto mode.
    /// </summary>
    /// <param name="armedLed"></param>
    public void UpdateLedState(LcdLedArmedState armedLed)
    {
        // 0th byte: auto mode, 5 = on key press
        // 1st byte: LCD backlight
        // 2nd byte: keypad led
        // 3rd byte: ARMED led, 7 - fast flash, 6 slower, 5 off, 4 1/2 speed, 
        //   Devices[m.Source].PendingPackets.Enqueue(new Message(Command.CfgLedState, [0, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1, 0x1]));

        SendMessage(Command.CfgLedState, [0, 0, 0, (byte)armedLed, 0, 0, 0, 0]); // set display backlight to auto
    }

    public override void Stop()
    {
        displayRefresh.Stop();
    }

    /// <summary>
    /// Plays time out noise
    /// </summary>
    public void TimeoutSound()
    {
        BeepOutput(6, 2500, 0, 3, 0, 1, 3, 0);
    }

    /// <summary>
    /// Tells the keypad to play a beep every second
    /// </summary>
    public void CountdownNoise()
    {
        BeepOutput(0, 2500, 0, 1, 0, 2, 0xa, 0, 1);
    }
    /// <summary>
    /// Long beep every 20 seconds or so
    /// </summary>
    public void TroubleSound()
    {
        BeepOutput(0, 3000, 0, 1, 0, 0x14, 0x1, 0x1c, 0);
    }
    /// <summary>
    /// Annoying noise
    /// </summary>
    public void ContinousSound()
    {
        BeepOutput(0, 3000, 0, 1, 0, 6, 1, 0);
    }
    /// <summary>
    /// Alarm cancel noise
    /// </summary>
    public void AlarmCancel()
    {
        BeepOutput(1, 2505, 2000, 0xf, 0xa, 3, 4, 0);
    }

    public void MuteSpeaker()
    {
        BeepOutput(0, 0, 0, 0, 0, 0, 0, 0);
    }
    /// <summary>
    /// Send beep command
    /// </summary>
    /// <param name="repeatCount">Repeat count</param>
    /// <param name="PrimaryFrequency">Primary Frequency</param>
    /// <param name="SecondaryFrequency">Secondary Frequency</param>
    /// <param name="PrimaryPattern">Main pattern (bit 1 on, bit 0 off)</param>
    /// <param name="TonePattern">Tone pattern (bit 1 on, bit 0 off)</param>
    /// <param name="PatternBitTime">Time each bit in pattern lasts for</param>
    /// <param name="PatternLength">Length of repeating pattern in bits</param>
    /// <param name="RepeatDelay">Delay after each repeat in double bits</param>
    public void BeepOutput(byte repeatCount, ushort PrimaryFrequency, ushort SecondaryFrequency, ushort PrimaryPattern, ushort TonePattern, byte PatternBitTime, byte PatternLength, byte RepeatDelay, byte Flag = 0)
    {
        SendMessage(Command.KeypadBeep,
            [0,
            repeatCount,
           (byte)(PrimaryFrequency & 0xFF),
           (byte)((PrimaryFrequency >> 8) & 0xFF),
            (byte)(SecondaryFrequency & 0xFF),
           (byte)((SecondaryFrequency >> 8) & 0xFF),
            (byte)(PrimaryPattern & 0xFF),
           (byte)((PrimaryPattern >> 8) & 0xFF),
            (byte)(TonePattern & 0xFF),
           (byte)((TonePattern >> 8) & 0xFF),
           PatternBitTime,
           PatternLength,
           RepeatDelay, Flag]);
    }
}
public enum LcdPageType
{
    Home,
    Options
}
