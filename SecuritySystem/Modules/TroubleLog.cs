namespace SecuritySystem.Modules;
public class TroubleLog
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TroubleType Type { get; set; }
    public int ZoneIndex { get; set; } = -1;
}

public enum TroubleType
{
    Unknown = 0,
    InactiveZone = 1,
    DisplayPowerLoss = 2
}