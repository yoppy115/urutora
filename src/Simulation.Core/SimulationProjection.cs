using Simulation.Core.Domain;

namespace Simulation.Core;

public sealed record NpcProjection(
    long Id,
    Position Position,
    IReadOnlySet<ConceptKind> ConceptMarks,
    IReadOnlySet<ConceptKind> ActiveAuras,
    int? SettlementId,
    int? InvasionId);

public sealed record NpcStatusProjection(
    long Id,
    bool IsAlive,
    Position Position,
    int AgeDays,
    int DaysPerYear,
    double CurrentHp,
    double EffectiveMaxHp,
    NeedsSnapshot Needs,
    IReadOnlySet<ConceptKind> ConceptMarks,
    IReadOnlySet<ConceptKind> ActiveAuras,
    int? SettlementId,
    int? InvasionId,
    InvasionRole? InvasionRole,
    int HeldInformationCount,
    int EventBeliefCount,
    int SettlementBeliefCount)
{
    public double AgeYears => (double)AgeDays / DaysPerYear;
    public int PersonBeliefCount => HeldInformationCount;
}

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
    int EventBeliefCount,
    int SettlementBeliefCount,
    IReadOnlyList<NpcActionRecord> ActionHistory)
{
    public double AgeYears => (double)AgeDays / DaysPerYear;
    public int PersonBeliefCount => HeldInformationCount;
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
    int HeldInformationMaximum,
    int EventBeliefTotal,
    int SettlementBeliefTotal,
    double AveragePersonBeliefCapacity,
    long PersonBeliefTtlRemovals,
    long PersonBeliefDeathRemovals)
{
    public long PersonBeliefCapacityEvictions => HeldInformationEvictions;
    public int PersonBeliefTotal => HeldInformationTotal;
    public double PersonBeliefAverage => HeldInformationAverage;
    public int PersonBeliefMaximum => HeldInformationMaximum;
}

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
    int CorePopulation,
    int InfluenceOnlyPopulation,
    int OutsidePopulation,
    double WorldPopulationRatio,
    double CoreOccupancy,
    double CrowdingPressure,
    int UsableInfluenceCells,
    int NominalResidentialCapacity,
    double ResidentLoad,
    double MovementCongestion,
    double ReturnFailure,
    int CrowdingConsecutiveDays,
    int? LastInvasionStartedTick,
    int InvasionCooldownDaysRemaining,
    double SupportPotential,
    double DailySupportDelta,
    double Support,
    double SupportPopulationComponent,
    double SupportReproductionComponent,
    double SupportSocialComponent,
    int LowSupportDays,
    int SaturatedDays,
    int RenewalCount,
    int? LastRenewalTick,
    int FissionPressureDays,
    int? ParentSettlementId,
    IReadOnlyList<int> ChildSettlementIds,
    int SupportWindowDays,
    int FoundingResidentBaseline,
    double AverageAffiliatedResidentsInInfluence,
    int ReproductionSuccessesInSupportWindow,
    int SocialActionsInSupportWindow,
    int MemberDaysInSupportWindow,
    double TargetSocialActions,
    long HomeBiasApplications,
    long StrongHomeBiasApplications,
    long StrongHomeRestApplications,
    long StrongHomeHpApplications,
    long HomewardMoves,
    long CoreReturns,
    long ForeignApproaches,
    long ForeignDepartures,
    int? DissolvedTick,
    string? DissolutionReason,
    int? IntegratedIntoSettlementId);

public sealed record FatigueContributionStatistics(
    string Cause,
    long Applications,
    double RequestedTotal,
    double AppliedTotal);

public sealed record RestDiagnosticsStatistics(
    long RestActions,
    double RestActionRate,
    double AverageRestNeed,
    double AverageSelectedRestNeed,
    double AverageSelectedRestPressure,
    long InvasionRestActions,
    long AttackerRestActions,
    long DefenderRestActions,
    long InvasionWithdrawals,
    IReadOnlyList<FatigueContributionStatistics> FatigueContributions);

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
    int FleeingParticipants,
    int AdvancingParticipants,
    int DefendingParticipants,
    int FieldRestParticipants,
    int RetreatingParticipants,
    int AttackOccupationDays,
    int AttackCollapseDays,
    int InfluenceClearDays,
    int CenterDistance,
    int InfluenceClearRequiredDays);

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

public sealed record PhaseEcologyStatistics(
    WorldPhase Phase,
    int Days,
    double AveragePopulation,
    double AverageAgeYears,
    double AverageHp,
    long Births,
    long ReproductionAttempts,
    long ReproductionSuccesses,
    long CombatDeaths,
    long VitalityDeaths,
    double CollisionDamage,
    double ExplicitAttackDamage);

public sealed record EventLayerStatistics(
    int RecentEventCount,
    long IncrementalStatisticsUpdates,
    long TotalEvents,
    long FullHistoryRescans);

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
    RestDiagnosticsStatistics RestDiagnostics,
    ViolenceStatistics Violence,
    IReadOnlyList<ReproductionScopeStatistics> ReproductionScopes,
    IReadOnlyList<InvasionStatistics> Invasions,
    AuraStatistics Auras,
    IReadOnlyList<PhaseWindowStatistics> OrderTransitionWindows,
    IReadOnlyList<PhaseEcologyStatistics> PhaseEcology,
    EventLayerStatistics EventLayers,
    long HotspotCandidates,
    long HotspotConflicts,
    long HotspotRejections,
    long InvasionStartPrevented);

public sealed record DailyObservationProjection(
    int Tick,
    int Population,
    int MinimumPopulation,
    double AverageAgeYears,
    WorldPhase CurrentPhase,
    int ActiveSettlementCount,
    int AffiliatedPopulation,
    double PopulationCv,
    double DemographicImbalance,
    int StabilityConsecutiveDays,
    IReadOnlyList<ActionSelectionCount> ActionSelections,
    PerceptionStatistics Perception,
    double RestActionRate,
    double AverageRestNeed,
    double AverageSelectedRestNeed,
    double AverageSelectedRestPressure,
    double ActiveSettlementAverageSupport,
    int TotalLowSupportDays,
    int CoolingDownSettlementCount,
    long InvasionStartPrevented);

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
