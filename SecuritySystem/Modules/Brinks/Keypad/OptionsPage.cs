namespace SecuritySystem.Modules.Brinks;
public class OptionsPage : KeypadPage
{
    int timeout = 0;
    public KeypadLcd keypadLcd = new();
    public OptionsPage(LcdDevice Controller) : base(Controller)
    {
    }

    public override void KeyDown(LcdKey key)
    {

    }

    public override void KeyUp(LcdKey key)
    {
        if (key == LcdKey.Cancel)
        {
            Controller.NavigateToPage(LcdPageType.Home);
        }
        else
        {
            Controller.TimeoutSound();
        }
    }

    public override void Refresh()
    {
        timeout++;
        if (timeout >= 45)
        {
            Controller.TimeoutSound();
            Controller.NavigateToPage(LcdPageType.Home);
        }
        else
        {
             Controller.UpdateLcd(keypadLcd);
        }
    }

    public override void Start()
    {
        keypadLcd.Clear();
        keypadLcd.Set(LcdBit.ICallBrinks, true);
        Controller.UpdateLcd(keypadLcd);
    }

    public override void Stop()
    {

    }
}