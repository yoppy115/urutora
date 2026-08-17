using Simulation.Core.Domain;

namespace Simulation.Core;

public sealed record NpcProjection(
    long Id,
    Position Position,
    IReadOnlySet<ConceptKind> ConceptMarks,
    IReadOnlySet<ConceptKind> ActiveAuras,
    int? SettlementId,
    int? InvasionId);

public sealed record LandmarkProjection(ConceptKind Concept, Position Position);

public sealed record SettlementProjection(
    int Id,
    Position Center,
    int CoreRadius,
    int InfluenceRadius,
    int FormedTick,
    bool IsActive,
    int Population,
    double CrowdingPressure);

public sealed record InvasionProjection(
    int Id,
    int AttackSettlementId,
    int DefenseSettlementId,
    int StartTick,
    bool IsActive);

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
    IReadOnlySet<ConceptKind> ActiveAuras,
    int? SettlementId,
    IReadOnlyList<SettlementAffinityProjection> SettlementAffinities,
    int? InvasionId,
    InvasionRole? InvasionRole,
    long KillCount,
    int HeldInformationCount,
    IReadOnlyList<NpcActionRecord> ActionHistory)
{
    public double AgeYears => (double)AgeDays / DaysPerYear;
}

public sealed record SettlementAffinityProjection(int SettlementId, double Affinity, bool IsActiveMembership);

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

public sealed record WorldPhaseStatistics(
    WorldPhase CurrentPhase,
    int GenerationStartTick,
    int? OrderStartTick,
    double PopulationCv,
    double DemographicImbalance,
    int StabilityConsecutiveDays);

public sealed record SettlementStatistics(
    int Id,
    Position Center,
    int FormedTick,
    int FounderCount,
    bool IsActive,
    int Population,
    double WorldPopulationRatio,
    double CoreOccupancy,
    double CrowdingPressure,
    int CrowdingConsecutiveDays,
    int? DissolvedTick,
    string? DissolutionReason,
    int? IntegratedIntoSettlementId);

public sealed record FrictionStatistics(
    int FirstSettlementId,
    int SecondSettlementId,
    double CurrentFriction,
    long CollisionEvents,
    long ExplicitThreatEvents,
    double LifetimeDecay,
    int LastFrictionEventTick);

public sealed record AffiliationGroupStatistics(
    string Group,
    int Population,
    double AverageAgeYears,
    double AverageDeathAgeYears,
    double AverageHp,
    long CombatDeaths,
    long VitalityDeaths,
    long RestActions,
    double RestActionRate,
    long ReproductionAttempts,
    long ReproductionSuccesses,
    long Births,
    long ConceptMarkAcquisitions);

public sealed record ViolenceStatistics(
    long Collisions,
    long CollisionAttacks,
    long SameSettlementSuppressions,
    long UnaffiliatedProtectionCollisions,
    long OtherSettlementCollisions,
    long FrictionIncreases,
    long ExplicitAttacks,
    long Counterattacks,
    long PursuitAttacks,
    long AttackCandidateSuppressions,
    long AttackResolutionSuppressions,
    long ThreatExceptionAttacks);

public sealed record ReproductionScopeStatistics(
    string Scope,
    long Attempts,
    long Successes,
    long Failures);

public sealed record InvasionStatistics(
    int Id,
    int AttackSettlementId,
    int DefenseSettlementId,
    int CreatedTick,
    int EffectiveTick,
    int? EndTick,
    InvasionOutcome Outcome,
    double TriggerCrowdingPressure,
    string TargetReason,
    int InitialForceSize,
    int CoreCohortSize,
    int FrontierCohortSize,
    int RestWithdrawals,
    int DeathWithdrawals,
    double MaximumCoreOccupationRate,
    bool CenterOccupied,
    int TotalUsableCoreCells,
    int AttackOccupiedUsableCoreCells,
    int FleeingParticipants);

public sealed record AuraStatistics(
    long Applied,
    long Expired,
    long SelfMarkOverlapSuppressions,
    long SurvivalApplied,
    long SurvivalExpired,
    int CurrentRecipients,
    int InvasionHolders);

public sealed record PhaseWindowStatistics(
    string Window,
    int Days,
    double AveragePopulation,
    long Births,
    long Deaths,
    long CombatDeaths,
    long CollisionAttacks,
    long ReproductionAttempts,
    long ReproductionSuccesses,
    double AverageAgeYears,
    double AverageAffiliationRate);

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
    IReadOnlyList<ConceptMarkStatistics> ConceptMarks,
    WorldPhaseStatistics WorldPhase,
    int AffiliatedPopulation,
    int UnaffiliatedPopulation,
    IReadOnlyList<SettlementStatistics> Settlements,
    IReadOnlyList<FrictionStatistics> Frictions,
    IReadOnlyList<AffiliationGroupStatistics> AffiliationGroups,
    ViolenceStatistics Violence,
    IReadOnlyList<ReproductionScopeStatistics> ReproductionScopes,
    IReadOnlyList<InvasionStatistics> Invasions,
    AuraStatistics Auras,
    IReadOnlyList<PhaseWindowStatistics> OrderTransitionWindows,
    long HotspotCandidates,
    long HotspotConflicts,
    long HotspotRejections);

public sealed record SimulationSnapshot(
    int Tick,
    int DaysPerYear,
    int Width,
    int Height,
    WorldPhase Phase,
    IReadOnlyList<NpcProjection> Npcs,
    IReadOnlyList<LandmarkProjection> Landmarks,
    IReadOnlyList<SettlementProjection> Settlements,
    IReadOnlyList<InvasionProjection> Invasions,
    IReadOnlyList<SimulationEvent> RecentEvents)
{
    public int Year => Tick / DaysPerYear;
    public int Day => Tick % DaysPerYear + 1;
}

public sealed record TickResult(int CompletedTick, IReadOnlyList<SimulationEvent> Events);
