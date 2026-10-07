
namespace SecuritySystem.Modules.Brinks;

public class IconKeypadModule(string comport) : Module
{
    private VirtualPanel panel = null!;
    public override void OnRegister()
    {
        panel = new();
        Thread panelThread = new Thread(delegate ()
        {
            panel.Begin(comport);
        });
        panelThread.Start();
    }

    public override void OnUnregister()
    {

    }
}