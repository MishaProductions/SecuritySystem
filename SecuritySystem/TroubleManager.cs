using SecuritySystem.Utils;

namespace SecuritySystem;

public static class TroubleManager
{
    public static readonly List<TroubleCondition> ActiveConditions = [];
    public static event TroubleEventHandler? OnNewCondition;
    public static event TroubleEventHandler? OnConditionCleared;
    public static void InsertTroubleCondition(TroubleCondition condition)
    {
        if (ActiveConditions.Where(c => c.ID == condition.ID).Any())
            throw new InvalidOperationException("Cannot insert existing condition");

        condition.ID = Guid.NewGuid();
        ActiveConditions.Add(condition);

        OnNewCondition?.Invoke(condition);
    }

    public static void RemoveTroubleCondition(TroubleCondition condition)
    {
        if(!ActiveConditions.Contains(condition))
        {
            throw new InvalidOperationException($"no such condition {condition.ID} {condition.Title} {condition.GetDescription()} {condition.Type}");
        }

        ActiveConditions.Remove(condition);
        OnConditionCleared?.Invoke(condition);
    }

    internal static bool InactivityZoneFaultExists(int zone)
    {
        foreach(var item in ActiveConditions)
        {
            if (item.Type == TroubleConditionType.ZoneInactivity && item is ZoneInactivityTroubleCondition zitem && zitem.ZoneIndex == zone)
                return true;
        }
        return false;
    }

    internal static void RmTroubleByZoneIdxInactivity(int zone)
    {
        for (int i = 0; i < ActiveConditions.Count; i++)
        {
            var item = ActiveConditions[i];

            if (item.Type == TroubleConditionType.ZoneInactivity && item is ZoneInactivityTroubleCondition zitem && zitem.ZoneIndex == zone)
            {
                RemoveTroubleCondition(item);
                break;
            }
        }
    }
}

public delegate void TroubleEventHandler(TroubleCondition condition);

public class TroubleCondition(TroubleConditionType type)
{
    // Internal fields
    public Guid ID { get; set; }

    public TroubleConditionType Type { get; set; } = type;
    public virtual string Title => Type.ToString();
    public virtual string GetDescription()
    {
        return Type.ToString();
    }
    public virtual void HandleSilence()
    {
        
    }
}

public class DispayUnexpectedResetTroubleCondition() : TroubleCondition(TroubleConditionType.DisplayUnexpectedReset)
{
    public override string Title => "Display error";

    public override string GetDescription()
    {
        return "The display has unexpectedly restarted.\r\nVerify that the power supply voltage and\r\namperage is correct.";
    }

    public static DispayUnexpectedResetTroubleCondition Create()
    {
        return new();
    }
}

public class ZoneInactivityTroubleCondition(int zoneIndex, DateTime inactiveTime) : TroubleCondition(TroubleConditionType.ZoneFault)
{
    public int ZoneIndex { get; set; } = zoneIndex;
    public DateTime InactiveTime { get; set; } = inactiveTime;
    public override string Title => "Inactive Zone";

    public override string GetDescription()
    {
        var time = DateTime.Now - InactiveTime;
        return $"The zone {ZoneIndex + 1} has been not ready for {(int)time.TotalMinutes}mins";
    }

    public override void HandleSilence()
    {
        SystemManager.SendIgnoreInactiveZone(ZoneIndex);
    }

    public static ZoneInactivityTroubleCondition Create(int zoneIndex, DateTime inactiveTime)
    {
        return new(zoneIndex, inactiveTime);
    }
}

public enum TroubleConditionType
{
    DisplayUnexpectedReset,
    DisplayCommLoss,
    ZoneInactivity,
    ZoneFault,
    NetworkError,
    Temperature
}