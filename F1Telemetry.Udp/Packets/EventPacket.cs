namespace F1Telemetry.Udp.Packets;

public sealed record EventPacket(
    string RawEventCode,
    EventCode Code,
    EventDetail Detail) : IUdpPacket;

public enum EventCode
{
    Unknown = 0,
    SessionStarted,
    SessionEnded,
    FastestLap,
    Retirement,
    DrsEnabled,
    DrsDisabled,
    TeamMateInPits,
    ChequeredFlag,
    RaceWinner,
    Penalty,
    SpeedTrap,
    StartLights,
    LightsOut,
    DriveThroughPenaltyServed,
    StopGoPenaltyServed,
    Flashback,
    Buttons,
    RedFlag,
    Overtake,
    SafetyCar,
    Collision
}

public abstract record EventDetail;

public sealed record EmptyEventDetail() : EventDetail;

public sealed record UnknownEventDetail(
    byte[] RawData) : EventDetail;

public sealed record FastestLapEventDetail(
    byte VehicleIndex,
    float LapTime) : EventDetail;

public sealed record RetirementEventDetail(
    byte VehicleIndex,
    byte Reason) : EventDetail;

public sealed record DrsDisabledEventDetail(
    byte Reason) : EventDetail;

public sealed record TeamMateInPitsEventDetail(
    byte VehicleIndex) : EventDetail;

public sealed record RaceWinnerEventDetail(
    byte VehicleIndex) : EventDetail;

public sealed record PenaltyEventDetail(
    byte PenaltyType,
    byte InfringementType,
    byte VehicleIndex,
    byte OtherVehicleIndex,
    byte Time,
    byte LapNumber,
    byte PlacesGained) : EventDetail;

public sealed record SpeedTrapEventDetail(
    byte VehicleIndex,
    float Speed,
    bool IsOverallFastestInSession,
    bool IsDriverFastestInSession,
    byte FastestVehicleIndexInSession,
    float FastestSpeedInSession) : EventDetail;

public sealed record StartLightsEventDetail(
    byte NumLights) : EventDetail;

public sealed record DriveThroughPenaltyServedEventDetail(
    byte VehicleIndex) : EventDetail;

public sealed record StopGoPenaltyServedEventDetail(
    byte VehicleIndex,
    float StopTime) : EventDetail;

public sealed record FlashbackEventDetail(
    uint FlashbackFrameIdentifier,
    float FlashbackSessionTime) : EventDetail;

public sealed record ButtonsEventDetail(
    uint ButtonStatus) : EventDetail;

public sealed record OvertakeEventDetail(
    byte OvertakingVehicleIndex,
    byte BeingOvertakenVehicleIndex) : EventDetail;

public sealed record SafetyCarEventDetail(
    byte SafetyCarType,
    byte EventType) : EventDetail;

/// <summary>
/// 碰撞事件明细。F1 26 新增 Severity（严重程度）字段，F1 25 中恒为 0。
/// </summary>
public sealed record CollisionEventDetail(
    byte Vehicle1Index,
    byte Vehicle2Index,
    byte Severity = 0) : EventDetail;
