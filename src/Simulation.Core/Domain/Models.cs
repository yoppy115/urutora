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

public enum WorldPhase
{
    Generation,
    Order
}

public enum InvasionRole
{
    Attacker,
    Defender
}

public enum InvasionOutcome
{
    None,
    AttackVictory,
    DefenseVictory
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
    public Dictionary<int, double> SettlementAffinity { get; } = new();
    public int? SettlementId { get; set; }
    public int? InvasionId { get; set; }
    public InvasionRole? InvasionRole { get; set; }
    public bool HasAdvanceBias { get; set; }
    public bool HasDefenseBias { get; set; }
    public HashSet<int> WithdrawnInvasionIds { get; } = new();
    public HashSet<ConceptKind> ActiveAuras { get; } = new();
    public int? SettlementAtDeathId { get; set; }
    public int? DeathAgeDays { get; set; }
    public string? DeathCause { get; set; }
    public long? ParentAId { get; init; }
    public long? ParentBId { get; init; }

    public EffectiveStats EffectiveStats(SimulationConfig config)
    {
        var markMultiplier = config.Concept.EffectiveMultiplier;
        double Multiplier(ConceptKind concept) => ConceptMarks.Contains(concept)
            ? markMultiplier
            : ActiveAuras.Contains(concept) ? config.Aura.EffectiveMultiplier : 1;
        return new EffectiveStats(
            BaseStats.MaxHp * Multiplier(ConceptKind.Survival),
            BaseStats.Action * Multiplier(ConceptKind.Struggle),
            BaseStats.Combat * Multiplier(ConceptKind.Struggle),
            BaseStats.Communication * Multiplier(ConceptKind.Communication));
    }

    public bool IsMature(SimulationConfig config) =>
        AgeDays >= config.Reproduction.MatureAgeDays;
}

public sealed record Landmark(ConceptKind Concept, Position Position);

public sealed class WorldState
{
    public int Tick { get; set; }
    public WorldPhase Phase { get; set; } = WorldPhase.Generation;
    public WorldPhase? PendingPhase { get; set; }
    public int GenerationStartTick { get; set; }
    public int? OrderStartTick { get; set; }
    public int StabilityConsecutiveDays { get; set; }
    public double PopulationCv { get; set; }
    public double DemographicImbalance { get; set; }
    public Dictionary<long, NpcState> Npcs { get; } = new();
    public List<Landmark> Landmarks { get; } = new();
    public List<BirthRequest> BirthRequests { get; } = new();
    public Dictionary<int, SettlementState> Settlements { get; } = new();
    public Dictionary<SettlementPair, SettlementFriction> Frictions { get; } = new();
    public HashSet<HostilityEdge> Hostilities { get; } = new();
    public Dictionary<int, InvasionState> Invasions { get; } = new();
    public List<ReproductionSuccessRecord> ReproductionSuccesses { get; } = new();
    public List<DailyPopulationRecord> PopulationHistory { get; } = new();
    public long NextNpcId { get; set; } = 1;
    public int NextSettlementId { get; set; } = 1;
    public int NextInvasionId { get; set; } = 1;
    public long SettlementCandidateCount { get; set; }
    public long SettlementCandidateConflictCount { get; set; }
    public long SettlementCandidateRejectionCount { get; set; }
    public long AuraSelfMarkSuppressionCount { get; set; }
    public long AttackCandidateSuppressionCount { get; set; }
    public long UnaffiliatedThreatExceptionAttackCount { get; set; }
}

public sealed class SettlementState
{
    public required int Id { get; init; }
    public required Position Center { get; init; }
    public required int FormedTick { get; init; }
    public required int EffectiveTick { get; init; }
    public required IReadOnlyList<long> FounderIds { get; init; }
    public int? DissolvedTick { get; set; }
    public string? DissolutionReason { get; set; }
    public int? IntegratedIntoSettlementId { get; set; }
    public double CoreOccupancy { get; set; }
    public double BlockedMovementRate { get; set; }
    public double CrowdingPressure { get; set; }
    public List<double> CrowdingHistory { get; } = new();
    public int CrowdingConsecutiveDays { get; set; }
    public int LowPopulationConsecutiveDays { get; set; }

    public bool IsActive(int tick) => DissolvedTick is null && tick >= EffectiveTick;
}

public readonly record struct SettlementPair(int FirstId, int SecondId)
{
    public static SettlementPair Create(int firstId, int secondId)
    {
        if (firstId == secondId)
        {
            throw new ArgumentException("A Settlement pair requires distinct IDs.");
        }

        return firstId < secondId ? new SettlementPair(firstId, secondId) : new SettlementPair(secondId, firstId);
    }
}

public sealed class SettlementFriction
{
    public required SettlementPair Pair { get; init; }
    public double CurrentFriction { get; set; }
    public int LastFrictionEventTick { get; set; }
    public long LifetimeFrictionEvents { get; set; }
    public long CollisionEvents { get; set; }
    public long ExplicitThreatEvents { get; set; }
    public double LifetimeDecay { get; set; }
}

public readonly record struct HostilityEdge(int SourceSettlementId, int TargetSettlementId);

public sealed class InvasionState
{
    public required int Id { get; init; }
    public required int AttackSettlementId { get; init; }
    public required int DefenseSettlementId { get; init; }
    public required int CreatedTick { get; init; }
    public required int EffectiveTick { get; init; }
    public required double TriggerCrowdingPressure { get; init; }
    public required string TargetReason { get; init; }
    public required IReadOnlyList<long> AttackParticipantIds { get; init; }
    public required IReadOnlyList<long> CoreCohortIds { get; init; }
    public required IReadOnlyList<long> FrontierCohortIds { get; init; }
    public List<long> DefenseParticipantIds { get; } = new();
    public int? EndTick { get; set; }
    public InvasionOutcome Outcome { get; set; }
    public double MaximumCoreOccupationRate { get; set; }
    public bool CenterOccupied { get; set; }
    public int RestWithdrawals { get; set; }
    public int DeathWithdrawals { get; set; }

    public bool IsPending(int tick) => EndTick is null && tick < EffectiveTick;
    public bool IsActive(int tick) => EndTick is null && tick >= EffectiveTick;
}

public sealed record ReproductionSuccessRecord(
    string EventId,
    int Tick,
    Position Position,
    long ParentAId,
    long ParentBId);

public sealed record DailyPopulationRecord(
    int Tick,
    int Population,
    int Births,
    int Deaths,
    int CombatDeaths,
    int CollisionAttacks,
    int ReproductionAttempts,
    int ReproductionSuccesses,
    double AverageAgeYears,
    double AffiliationRate);

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
    Rest,
    Birth,
    BirthFailure,
    Death,
    ConceptMarkAcquired,
    TargetPositionInvalidated,
    IntentReplaced,
    SettlementCandidateEvaluated,
    SettlementCandidateRejected,
    SettlementFormed,
    SettlementDissolved,
    SettlementIntegrated,
    AffinityChanged,
    AffiliationChanged,
    WorldPhaseChanged,
    CollisionSuppressed,
    AttackSuppressed,
    SettlementFrictionChanged,
    InitialHostilityEstablished,
    InvasionStarted,
    InvasionParticipantJoined,
    InvasionParticipantWithdrew,
    InvasionEnded,
    AuraApplied,
    AuraExpired,
    TemporaryMaxHpNormalized,
    SettlementMaintenance
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
    string Detail,
    int? ActorSettlementId = null,
    int? TargetSettlementId = null)
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
        Detail,
        ActorSettlementId?.ToString() ?? "-",
        TargetSettlementId?.ToString() ?? "-");
}

public delegate void DomainEventEmitter(
    int microRound,
    SimulationEventType type,
    long? actorId,
    long? targetId,
    Position? position,
    bool success,
    string detail);

public static class DomainMath
{
    public static double ClampNeed(double value) => Math.Clamp(value, 0, 10);
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
