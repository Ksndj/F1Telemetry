using F1Telemetry.Core.Models;

namespace F1Telemetry.Core.Formatting;

/// <summary>
/// Formats player-car damage snapshots into compact user-facing summaries.
/// </summary>
public static class DamageSummaryFormatter
{
    private const int MaxDisplayedComponents = 4;

    /// <summary>
    /// Formats the damage snapshot into a compact summary, or the supplied missing text when no packet has been observed.
    /// </summary>
    /// <param name="snapshot">The damage snapshot to summarize.</param>
    /// <param name="missingText">The text to return when the snapshot is unavailable.</param>
    public static string Format(DamageSnapshot? snapshot, string missingText = "")
    {
        if (snapshot is null)
        {
            return missingText;
        }

        var parts = new List<string>();
        if (snapshot.DrsFault)
        {
            parts.Add("DRS 故障");
        }

        if (snapshot.ErsFault)
        {
            parts.Add("ERS 故障");
        }

        if (snapshot.EngineBlown)
        {
            parts.Add("引擎爆缸");
        }

        if (snapshot.EngineSeized)
        {
            parts.Add("引擎卡死");
        }

        foreach (var item in snapshot.Components
                     .Select(pair => new
                     {
                         Component = pair.Key,
                         DamagePercent = pair.Value,
                         Severity = DamageSnapshot.Classify(pair.Value)
                     })
                     .Where(item => item.Severity > DamageSeverity.None)
                     .OrderBy(item => IsDrivetrainWearComponent(item.Component) ? 1 : 0)
                     .ThenByDescending(item => item.Severity)
                     .ThenByDescending(item => item.DamagePercent)
                     .Take(MaxDisplayedComponents))
        {
            parts.Add($"{FormatComponent(item.Component)} {item.DamagePercent}%（{FormatSeverity(item.Severity)}）");
        }

        return parts.Count == 0 ? "无明显损伤" : string.Join("；", parts);
    }

    private static string FormatComponent(DamageComponent component)
    {
        return component switch
        {
            DamageComponent.TyreDamage => "轮胎",
            DamageComponent.BrakeDamage => "刹车",
            DamageComponent.TyreBlister => "轮胎起泡",
            DamageComponent.FrontLeftWing => "前翼左侧",
            DamageComponent.FrontRightWing => "前翼右侧",
            DamageComponent.RearWing => "尾翼",
            DamageComponent.Floor => "底板",
            DamageComponent.Diffuser => "扩散器",
            DamageComponent.Sidepod => "侧箱",
            DamageComponent.Gearbox => "变速箱磨损",
            DamageComponent.Engine => "引擎磨损",
            DamageComponent.EngineMguhWear => "MGU-H 磨损",
            DamageComponent.EngineEsWear => "电池磨损",
            DamageComponent.EngineCeWear => "电控磨损",
            DamageComponent.EngineIceWear => "内燃机磨损",
            DamageComponent.EngineMgukWear => "MGU-K 磨损",
            DamageComponent.EngineTcWear => "涡轮磨损",
            _ => component.ToString()
        };
    }

    private static bool IsDrivetrainWearComponent(DamageComponent component)
    {
        return component is DamageComponent.Gearbox
            or DamageComponent.Engine
            or DamageComponent.EngineMguhWear
            or DamageComponent.EngineEsWear
            or DamageComponent.EngineCeWear
            or DamageComponent.EngineIceWear
            or DamageComponent.EngineMgukWear
            or DamageComponent.EngineTcWear;
    }

    private static string FormatSeverity(DamageSeverity severity)
    {
        return severity switch
        {
            DamageSeverity.Minor => "轻微",
            DamageSeverity.Light => "轻度",
            DamageSeverity.Moderate => "中度",
            DamageSeverity.Severe => "严重",
            DamageSeverity.Critical => "危急",
            _ => "无"
        };
    }
}
