using Simulation.Core.Configuration;

namespace Simulation.Core.Domain;

public readonly record struct Position(int X, int Y) : IComparable<Position>
{
    public int ChebyshevDistance(Position other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public IEnumerable<Position> Neighbors()
    {
        for (var y = Y - 1; y <= Y + 1; y++)
        {
            for (var x = X - 1; x <= X + 1; x++)
            {
                if (x != X || y != Y)
                {
                    yield return new Position(x, y);
                }
            }
        }
    }

    public int CompareTo(Position other)
    {
        var byY = Y.CompareTo(other.Y);
        return byY != 0 ? byY : X.CompareTo(other.X);
    }

    public override string ToString() => $"({X},{Y})";
}

public enum ConceptKind
{
    Struggle,
    Survival,
    Communication
}

public static class ConceptKindParser
{
    public static ConceptKind Parse(string value) => value switch
    {
        "struggle" => ConceptKind.Struggle,
        "survival" => ConceptKind.Survival,
        "communication" => ConceptKind.Communication,
        _ => throw new ConfigurationException($"Unknown concept id: {value}")
    };
}

public sealed class BaseStats
{
    public double MaxHp { get; init; }
    public double Action { get; init; }
    public double Combat { get; init; }
    public double Communication { get; init; }

    public BaseStats Copy() => new()
    {
        MaxHp = MaxHp,
        Action = Action,
        Combat = Combat,
        Communication = Communication
    };
}

public readonly record struct EffectiveStats(double MaxHp, double Action, double Combat, double Communication);

public sealed class NeedsState
{
    public double Survival { get; set; }
    public double Rest { get; set; }
    public double Activity { get; set; }
    public double Communication { get; set; }
    public double Reproduction { get; set; }

    public void ClampAll()
    {
        Survival = Math.Clamp(Survival, 0, 10);
        Rest = Math.Clamp(Rest, 0, 10);
        Activity = Math.Clamp(Activity, 0, 10);
        Communication = Math.Clamp(Communication, 0, 10);
        Reproduction = Math.Clamp(Reproduction, 0, 10);
    }

    public NeedsSnapshot Snapshot() => new(Survival, Rest, Activity, Communication, Reproduction);
}

public readonly record struct NeedsSnapshot(
    double Survival,
    double Rest,
    double Activity,
    double Communication,
    double Reproduction);

public enum InformationProperty
{
    PositionX,
    PositionY,
    Alive,
    CurrentHp,
    Combat,
    LifeStage,
    MarkStruggle,
    MarkSurvival,
    MarkCommunication
}

public enum PerceivedLifeStage
{
    Child = 0,
    Mature = 1
}

public enum InformationAcquisition
{
    Observation,
    Communication,
    DirectOutcome
}

public sealed record InformationRecord(
    string InformationId,
    long SubjectId,
    InformationProperty Property,
    double EstimatedValue,
    double Confidence,
    long SourceId,
    InformationAcquisition AcquiredBy,
    int AcquiredTick);

public sealed record ThreatMemory(long SubjectId, int LastThreatTick);

public sealed class NpcState
{
    public required long Id { get; init; }
    public required Position Position { get; set; }
    public required BaseStats BaseStats { get; init; }
    public required double RiskPreference { get; init; }
    public required double CurrentHp { get; set; }
    public required int AgeDays { get; set; }
    public bool IsAlive { get; set; } = true;
    public int ReproductionCooldownDays { get; set; }
    public NeedsState Needs { get; } = new();
    public HashSet<ConceptKind> ConceptMarks { get; } = new();
    public Dictionary<ConceptKind, double> ConceptExposure { get; } = new();
    public List<InformationRecord> HeldInformation { get; } = new();
    public long NextInformationSequence { get; set; }
    public Dictionary<long, ThreatMemory> ThreatMemory { get; } = new();
    public long? ParentAId { get; init; }
    public long? ParentBId { get; init; }

    public EffectiveStats EffectiveStats(SimulationConfig config)
    {
        var multiplier = config.Concept.EffectiveMultiplier;
        return new EffectiveStats(
            BaseStats.MaxHp * (ConceptMarks.Contains(ConceptKind.Survival) ? multiplier : 1),
            BaseStats.Action * (ConceptMarks.Contains(ConceptKind.Struggle) ? multiplier : 1),
            BaseStats.Combat * (ConceptMarks.Contains(ConceptKind.Struggle) ? multiplier : 1),
            BaseStats.Communication * (ConceptMarks.Contains(ConceptKind.Communication) ? multiplier : 1));
    }

    public bool IsMature(SimulationConfig config) =>
        AgeDays >= config.Reproduction.MatureAgeDays;
}

public sealed record Landmark(ConceptKind Concept, Position Position);

public sealed class WorldState
{
    public int Tick { get; set; }
    public Dictionary<long, NpcState> Npcs { get; } = new();
    public List<Landmark> Landmarks { get; } = new();
    public List<BirthRequest> BirthRequests { get; } = new();
    public long NextNpcId { get; set; } = 1;
}

public sealed record GeneticSnapshot(BaseStats BaseStats, double RiskPreference);

public sealed record BirthRequest(
    string RequestId,
    long ParentAId,
    long ParentBId,
    Position ParentAPositionAtConception,
    Position ParentBPositionAtConception,
    GeneticSnapshot ParentAGenetics,
    GeneticSnapshot ParentBGenetics,
    int ConceptionTick);

public enum ActionKind
{
    Idle,
    Move,
    Rest,
    Communication,
    Attack,
    Flee,
    Reproduction
}

public enum SimulationEventType
{
    Idle,
    Move,
    MoveFailed,
    Communication,
    Attack,
    CollisionAttack,
    Counterattack,
    Flee,
    Pursuit,
    ReproductionAttempt,
    ReproductionSuccess,
    ReproductionFailure,
    Birth,
    BirthFailure,
    Death,
    ConceptMarkAcquired,
    TargetPositionInvalidated,
    IntentReplaced
}

public sealed record SimulationEvent(
    string EventId,
    int Tick,
    int MicroRound,
    SimulationEventType Type,
    long? ActorId,
    long? TargetId,
    Position? Position,
    bool Success,
    string Detail)
{
    public string Fingerprint() => string.Join("|",
        EventId,
        Tick,
        MicroRound,
        Type,
        ActorId?.ToString() ?? "-",
        TargetId?.ToString() ?? "-",
        Position?.ToString() ?? "-",
        Success ? "1" : "0",
        Detail);
}

public static class DomainMath
{
    public static double ClampNeed(double value) => Math.Clamp(value, 0, 10);
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
