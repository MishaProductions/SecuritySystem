
namespace SecuritySystem.Modules.Brinks;
/// <summary>
/// Represents a page on the BHS4000 keypad LCD
/// </summary>
/// <param name="Controller"></param>
public abstract class KeypadPage(LcdDevice Controller)
{
    public LcdDevice Controller { get; } = Controller;
    public abstract void KeyUp(LcdKey key);
    public abstract void KeyDown(LcdKey key);
    public abstract void Refresh();
    public abstract void Start();
    public abstract void Stop();
}