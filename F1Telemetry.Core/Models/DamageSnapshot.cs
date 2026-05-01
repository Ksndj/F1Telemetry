namespace F1Telemetry.Core.Models;

/// <summary>
/// Represents the latest player-car damage state derived from a CarDamage packet.
/// </summary>
public sealed record DamageSnapshot
{
    /// <summary>
    /// Gets the time when the damage snapshot was observed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the average tyre wear across all four tyres.
    /// </summary>
    public float AverageTyreWear { get; init; }

    /// <summary>
    /// Gets percentage-based damage values by component.
    /// </summary>
    public IReadOnlyDictionary<DamageComponent, byte> Components { get; init; } =
        new Dictionary<DamageComponent, byte>();

    /// <summary>
    /// Gets a value indicating whether the DRS fault flag is active.
    /// </summary>
    public bool DrsFault { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ERS fault flag is active.
    /// </summary>
    public bool ErsFault { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine blown flag is active.
    /// </summary>
    public bool EngineBlown { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine seized flag is active.
    /// </summary>
    public bool EngineSeized { get; init; }

    /// <summary>
    /// Gets a value indicating whether any binary critical fault is active.
    /// </summary>
    public bool HasCriticalFault => DrsFault || ErsFault || EngineBlown || EngineSeized;

    /// <summary>
    /// Gets the highest normalized severity in this snapshot.
    /// </summary>
    public DamageSeverity HighestSeverity
    {
        get
        {
            var highest = HasCriticalFault ? DamageSeverity.Critical : DamageSeverity.None;

            foreach (var value in Components.Values)
            {
                var severity = Classify(value);
                if (severity > highest)
                {
                    highest = severity;
                }
            }

            return highest;
        }
    }

    /// <summary>
    /// Returns the percentage value for the requested component, or zero when the component is absent.
    /// </summary>
    /// <param name="component">The component to read.</param>
    public byte GetDamage(DamageComponent component)
    {
        return Components.TryGetValue(component, out var value) ? value : (byte)0;
    }

    /// <summary>
    /// Returns the normalized severity for the requested component.
    /// </summary>
    /// <param name="component">The component to classify.</param>
    public DamageSeverity GetSeverity(DamageComponent component)
    {
        return Classify(GetDamage(component));
    }

    /// <summary>
    /// Converts a percentage damage value into the normalized V1.5 severity band.
    /// </summary>
    /// <param name="damagePercent">The raw damage percentage.</param>
    public static DamageSeverity Classify(byte damagePercent)
    {
        return damagePercent switch
        {
            0 => DamageSeverity.None,
            <= 9 => DamageSeverity.Minor,
            <= 24 => DamageSeverity.Light,
            <= 49 => DamageSeverity.Moderate,
            <= 74 => DamageSeverity.Severe,
            _ => DamageSeverity.Critical
        };
    }
}
