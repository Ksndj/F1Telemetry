namespace F1Telemetry.Core.Models;

/// <summary>
/// Defines the car components tracked by the live damage snapshot.
/// </summary>
public enum DamageComponent
{
    /// <summary>
    /// Represents the maximum tyre damage across all four tyres.
    /// </summary>
    TyreDamage,

    /// <summary>
    /// Represents the maximum brake damage across all four wheels.
    /// </summary>
    BrakeDamage,

    /// <summary>
    /// Represents the maximum tyre blister value across all four tyres.
    /// </summary>
    TyreBlister,

    /// <summary>
    /// Represents front-left wing damage.
    /// </summary>
    FrontLeftWing,

    /// <summary>
    /// Represents front-right wing damage.
    /// </summary>
    FrontRightWing,

    /// <summary>
    /// Represents rear wing damage.
    /// </summary>
    RearWing,

    /// <summary>
    /// Represents floor damage.
    /// </summary>
    Floor,

    /// <summary>
    /// Represents diffuser damage.
    /// </summary>
    Diffuser,

    /// <summary>
    /// Represents sidepod damage.
    /// </summary>
    Sidepod,

    /// <summary>
    /// Represents gearbox damage.
    /// </summary>
    Gearbox,

    /// <summary>
    /// Represents overall engine damage.
    /// </summary>
    Engine,

    /// <summary>
    /// Represents MGU-H wear.
    /// </summary>
    EngineMguhWear,

    /// <summary>
    /// Represents energy store wear.
    /// </summary>
    EngineEsWear,

    /// <summary>
    /// Represents control electronics wear.
    /// </summary>
    EngineCeWear,

    /// <summary>
    /// Represents internal combustion engine wear.
    /// </summary>
    EngineIceWear,

    /// <summary>
    /// Represents MGU-K wear.
    /// </summary>
    EngineMgukWear,

    /// <summary>
    /// Represents turbocharger wear.
    /// </summary>
    EngineTcWear
}
