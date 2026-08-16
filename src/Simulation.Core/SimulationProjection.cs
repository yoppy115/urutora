using Simulation.Core.Domain;

namespace Simulation.Core;

public sealed record NpcProjection(long Id, Position Position, IReadOnlySet<ConceptKind> ConceptMarks);

public sealed record LandmarkProjection(ConceptKind Concept, Position Position);

public readonly record struct StatsProjection(
    double MaxHp,
    double Action,
    double Combat,
    double Communication);

public sealed record NpcActionRecord(
    int Tick,
    int MicroRound,
    SimulationEventType Type,
    long? OtherNpcId,
    bool IsActor,
    bool Success,
    string Detail);

public sealed record NpcDetailsProjection(
    long Id,
    bool IsAlive,
    Position Position,
    int AgeDays,
    int DaysPerYear,
    double CurrentHp,
    StatsProjection BaseStats,
    StatsProjection EffectiveStats,
    double RiskPreference,
    NeedsSnapshot Needs,
    int ReproductionCooldownDays,
    bool IsMature,
    long? ParentAId,
    long? ParentBId,
    IReadOnlyList<long> ChildIds,
    IReadOnlySet<ConceptKind> ConceptMarks,
    int HeldInformationCount,
    IReadOnlyList<NpcActionRecord> ActionHistory)
{
    public double AgeYears => (double)AgeDays / DaysPerYear;
}

public sealed record AgeDistributionBucket(
    int MinimumAgeDays,
    int MaximumAgeDaysExclusive,
    int Count);

public sealed record AgeDistributionProjection(
    int Population,
    int BucketSizeDays,
    IReadOnlyList<AgeDistributionBucket> Buckets);

public sealed record ActionSelectionCount(ActionKind Action, long Count);

public sealed record DeathCauseStatistics(
    string Cause,
    long Count,
    double AverageAgeYears);

public sealed record ReproductionOutcomeStatistics(
    string Reason,
    long Count);

public sealed record TargetedActionStatistics(
    ActionKind Action,
    long Attempts,
    long TargetAbsent);

public sealed record CombatTypeStatistics(
    SimulationEventType Type,
    long Attempts,
    long Hits,
    double AverageDamage);

public sealed record PerceptionStatistics(
    long PositionInvalidations,
    long SubjectPurges,
    long HeldInformationEvictions,
    int HeldInformationTotal,
    double HeldInformationAverage,
    int HeldInformationMaximum);

public sealed record ConceptMarkStatistics(
    ConceptKind Concept,
    int Holders,
    long Acquisitions,
    double ExposureTotal,
    double ExposureAverage,
    double ExposureMaximum);

public sealed record WorldStatisticsProjection(
    int Tick,
    int Population,
    int MinimumPopulation,
    double AverageAgeYears,
    IReadOnlyList<ActionSelectionCount> ActionSelections,
    IReadOnlyList<DeathCauseStatistics> DeathCauses,
    IReadOnlyList<ReproductionOutcomeStatistics> ReproductionOutcomes,
    IReadOnlyList<TargetedActionStatistics> TargetedActions,
    IReadOnlyList<CombatTypeStatistics> CombatTypes,
    PerceptionStatistics Perception,
    IReadOnlyList<ConceptMarkStatistics> ConceptMarks);

public sealed record SimulationSnapshot(
    int Tick,
    int DaysPerYear,
    int Width,
    int Height,
    IReadOnlyList<NpcProjection> Npcs,
    IReadOnlyList<LandmarkProjection> Landmarks,
    IReadOnlyList<SimulationEvent> RecentEvents)
{
    public int Year => Tick / DaysPerYear;
    public int Day => Tick % DaysPerYear + 1;
}

public sealed record TickResult(int CompletedTick, IReadOnlyList<SimulationEvent> Events);
