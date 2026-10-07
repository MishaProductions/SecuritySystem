using MHSApi.API;
using SecuritySystem.Modules.NXDisplay;
using SecuritySystem.Utils;

namespace SecuritySystem.Modules.Brinks;

public class HomePage : KeypadPage
{
    int hour = 0;
    int minute = 0;
    bool colon = false;
    bool onMenuOpen = false;

    int onMenuTimeout = 0;
    string keyboardBuffer = "";

    public KeypadLcd keypadLcd = new();
    public KeypadLcd keypadLcd2 = new();

    public int index = 0;
    public int state = 0; // 0: time, 1: zone display, 2: trouble display
    public bool flip = false;
    public int counter = 0;
    public HomePage(LcdDevice Controller) : base(Controller)
    {
    }

    public override void KeyDown(LcdKey key)
    {

    }

    private void CloseOnMenu()
    {
        onMenuOpen = false;
        keypadLcd.Set(LcdBit.IEnter, false);
        keypadLcd.Set(LcdBit.IMaster, false);
        keypadLcd.Set(LcdBit.ICode, false);
    }

    public override void KeyUp(LcdKey key)
    {
        if (key == LcdKey.Options && !Configuration.Instance.SystemArmed)
        {
            Controller.NavigateToPage(LcdPageType.Options);
        }
        else if (key == LcdKey.Cancel)
        {
            if (onMenuOpen)
            {
                CloseOnMenu();
                Refresh();
            }
        }
        else if (key == LcdKey.On)
        {
            if (string.IsNullOrEmpty(keyboardBuffer))
            {
                onMenuOpen = true;
                keypadLcd.Set(LcdBit.IEnter, true);
                keypadLcd.Set(LcdBit.IMaster, true);
                keypadLcd.Set(LcdBit.ICode, true);
                Controller.TimeoutSound();
            }
            else
            {
                // SecuritySystem: verify keyboard buffer
                if (!ZoneController.IsReady)
                {
                    Controller.TimeoutSound();
                }
                else if (Configuration.CheckIfCodeCorrect(keyboardBuffer))
                {
                    if (Configuration.Instance.SystemArmed)
                        SystemManager.DisarmSystem();
                    else
                        SystemManager.ArmSystem();
                }
                else
                {
                    Controller.BeepOutput(1, 2507, 0, 1, 0, 1, 1, 0);
                }
                keyboardBuffer = "";
            }
        }
        else if (key == LcdKey.Zero) keyboardBuffer += "0";
        else if (key == LcdKey.One) keyboardBuffer += "1";
        else if (key == LcdKey.Two) keyboardBuffer += "2";
        else if (key == LcdKey.Three) keyboardBuffer += "3";
        else if (key == LcdKey.Four) keyboardBuffer += "4";
        else if (key == LcdKey.Five) keyboardBuffer += "5";
        else if (key == LcdKey.Six) keyboardBuffer += "6";
        else if (key == LcdKey.Seven) keyboardBuffer += "7";
        else if (key == LcdKey.Eight) keyboardBuffer += "8";
        else if (key == LcdKey.Nine) keyboardBuffer += "9";
        else if (key == LcdKey.Fire)
        {
            var idx = MusicPlayer.AnncFiles.IndexOf("eat_ploff.mp3");
            if (idx != -1)
                MusicPlayer.PlayAnnc(idx);
        }
        else if (key == LcdKey.Hospital)
        {
            var idx = MusicPlayer.AnncFiles.IndexOf("wasps.mp3");
            if (idx != -1)
                MusicPlayer.PlayAnnc(idx);
        }
        else if (key == LcdKey.Police)
        {
            var idx = MusicPlayer.AnncFiles.IndexOf("rain.mp3");
            if (idx != -1)
                MusicPlayer.PlayAnnc(idx);
        }
        else
        {
            Controller.TimeoutSound();
        }
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        colon = !colon;
        keypadLcd.Set(LcdBit.Colon, colon);
        if (hour != now.Hour)
        {
            hour = now.Hour;

            int displayHour = now.Hour % 12;

            if (displayHour == 0)
                displayHour = 12;

            // First digit: only on for 10, 11, 12
            keypadLcd.SetHour1st(displayHour >= 10);

            // Second digit
            keypadLcd.SetDigit(0, displayHour % 10);

            // AM / PM
            keypadLcd.Set(LcdBit.Am, now.Hour < 12);
            keypadLcd.Set(LcdBit.Pm, now.Hour >= 12);
        }
        if (minute != now.Minute)
        {
            minute = now.Minute;

            var x = minute.ToString("00");

            keypadLcd.SetDigit(1, int.Parse(x[0].ToString()));
            keypadLcd.SetDigit(2, int.Parse(x[1].ToString()));
        }
    }

    public override void Refresh()
    {
        if (counter >= 6) counter = 0;

        if (counter == 3 && state == 0 && TroubleManager.ActiveConditions.Count != 0)
        {
                state = 2;
                keypadLcd2 = keypadLcd;
                keypadLcd = new();

                keypadLcd.Set(LcdBit.ITrouble, true);
                keypadLcd.Set(LcdBit.ICallBrinks, true);
                counter = 0;
        }
        else if (counter == 3 && state == 0 && !ZoneController.IsReady)
        {
                state = 1;
                keypadLcd2 = keypadLcd;
                keypadLcd = new();

                keypadLcd.Set(LcdBit.INot, true);
                keypadLcd.Set(LcdBit.IReady, true);
                keypadLcd.Set(LcdBit.IZone, true);
                counter = 0;
        }

        if ((state == 1 || state == 2) && counter >= 2)
        {
            counter = 0;
            index++;
        }

        if (state == 1 && counter == 0)
        {
            bool found = false;
            // find the first zone not ready
            foreach (var item in Configuration.Instance.Zones)
            {
                if (item.Key >= index && ZoneController.ZoneStates[item.Key] == ZoneState.NotReady && item.Value.Type != ZoneType.None)
                {
                    keypadLcd.SetDigit(1, 0);
                    keypadLcd.SetDigit(2, item.Value.ZoneNumber + 1);
                    index = item.Key;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                state = 0;
                index = 0;
                keypadLcd = keypadLcd2;
            }
        }

        if (state == 2 && counter == 0)
        {
            bool found = false;
            // find the first zone not ready
            for (int i = 0; i < TroubleManager.ActiveConditions.Count; i++)
            {
                var item = TroubleManager.ActiveConditions[i];

                if (i >= index)
                {
                    index = i;
                    found = true;

                    if (item.Type == TroubleConditionType.DisplayUnexpectedReset)
                    {
                        keypadLcd.SetCharacter(0, 'D');
                        keypadLcd.SetCharacter(1, 'U');
                        keypadLcd.SetCharacter(2, 'R');
                    }
                    else if (item.Type == TroubleConditionType.ZoneInactivity)
                    {
                        keypadLcd.SetCharacter(0, 'C');
                        keypadLcd.SetCharacter(1, 'H');
                        keypadLcd.SetCharacter(2, 'P');
                    }
                }
            }

            if (!found)
            {
                state = 0;
                index = 0;
                keypadLcd = keypadLcd2;
            }
        }



        if (state == 0)
        {
            UpdateTime();

            if (onMenuOpen)
            {
                onMenuTimeout++;
                if (onMenuTimeout >= 3)
                {
                    CloseOnMenu();
                    onMenuTimeout = 0;
                }
            }
        }

        Controller.UpdateLcd(keypadLcd);
        counter++;
    }

    /// <summary>
    /// refresh state should not be called often
    /// </summary>
    private void UpdateState()
    {
        if (Configuration.Instance.SystemArmed)
        {
            keypadLcd.Set(LcdBit.INot, false);
            keypadLcd.Set(LcdBit.IReady, false);
            keypadLcd.Set(LcdBit.IAll, true);// the system does not support zone bypass
            keypadLcd.Set(LcdBit.IOn, true);
            keypadLcd.Set(LcdBit.IAlarm, Configuration.Instance.SystemAlarmState);
        }
        else
        {
            keypadLcd.Set(LcdBit.INot, !ZoneController.IsReady);
            keypadLcd.Set(LcdBit.IReady, true);
            keypadLcd.Set(LcdBit.IAll, false);
            keypadLcd.Set(LcdBit.IOn, false);
            keypadLcd.Set(LcdBit.IAlarm, false);
        }
        keypadLcd.Set(LcdBit.ITrouble, TroubleManager.ActiveConditions.Count != 0);
    }

    public override void Start()
    {
        SystemManager.OnZoneUpdate += SysMan_OnZoneUpdate;
        SystemManager.OnSysTimerEvent += SysMan_OnSysTimerEvent;
        SystemManager.OnAlarm += SysMan_OnAlarm;
        SystemManager.OnSystemDisarm += SysMan_Disarm;
        TroubleManager.OnNewCondition += TroubleManager_OnTrouble;
        TroubleManager.OnConditionCleared += TroubleManager_ConditionCleared;
        UpdateState();
    }

    private void TroubleManager_ConditionCleared(TroubleCondition condition)
    {
        if (TroubleManager.ActiveConditions.Count == 0)
        {
            keypadLcd.Set(LcdBit.ITrouble, false);
            Controller.MuteSpeaker();
            if (Configuration.Instance.SystemArmed)
             Controller.UpdateLedState(LcdLedArmedState.OnSolid);
             else   Controller.UpdateLedState(LcdLedArmedState.Off);
        }
    }

    private void TroubleManager_OnTrouble(TroubleCondition condition)
    {
        keypadLcd.Set(LcdBit.ITrouble, true);
        Controller.TroubleSound();
        Controller.UpdateLedState(LcdLedArmedState.OnFlashing1);
    }

    private void SysMan_Disarm(object? sender, EventArgs e)
    {
        UpdateState();
    }

    private void SysMan_OnAlarm(int alarmZone)
    {
        UpdateState();
    }

    private void SysMan_OnSysTimerEvent(bool entry, int timer)
    {
        UpdateState();
    }

    private void SysMan_OnZoneUpdate(bool single, int zone, string name, ZoneState ready)
    {
        UpdateState();
    }

    public override void Stop()
    {
        SystemManager.OnZoneUpdate -= SysMan_OnZoneUpdate;
        SystemManager.OnSysTimerEvent -= SysMan_OnSysTimerEvent;
        SystemManager.OnAlarm -= SysMan_OnAlarm;
        SystemManager.OnSystemDisarm -= SysMan_Disarm;
        TroubleManager.OnNewCondition -= TroubleManager_OnTrouble;
        TroubleManager.OnConditionCleared -= TroubleManager_ConditionCleared;
    }
}