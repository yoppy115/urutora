using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Concepts;
using Simulation.Core.Decision;
using Simulation.Core.Domain;
using Simulation.Core.Lifecycle;
using Simulation.Core.Needs;
using Simulation.Core.Perception;
using Simulation.Core.Randomness;
using Simulation.Core.Reproduction;
using Simulation.Core.Resolution;
using Simulation.Core.Social;
using Simulation.Core.Statistics;
using Simulation.Core.World;

namespace Simulation.Core;

public sealed class SimulationEngine
{
    private readonly object _gate = new();
    private readonly RandomStreamFactory _random;
    private readonly PerceptionSystem _perception;
    private readonly UtilityDecisionSystem _decision;
    private readonly ReproductionSystem _reproduction;
    private readonly VitalitySystem _vitality;
    private readonly NeedsSystem _needs;
    private readonly ConceptExposureSystem _conceptExposure;
    private readonly ActionResolutionSystem _actionResolution;
    private readonly ConceptAuraSystem _aura;
    private readonly InvasionSystem _invasion;
    private readonly SettlementMaintenanceCoordinator _maintenance;
    private readonly List<SimulationEvent> _events = new();
    private readonly List<DecisionTrace> _lastDecisionTraces = new();
    private readonly Dictionary<ActionKind, long> _selectedActionCounts = Enum
        .GetValues<ActionKind>()
        .ToDictionary(action => action, _ => 0L);
    private readonly Dictionary<long, Dictionary<ActionKind, long>> _selectedActionCountsByNpc = new();
    private int _minimumPopulation;
    private int _eventSequence;
    private WorldStatisticsProjection? _cachedWorldStatistics;
    private long _restActions;
    private double _selectedRestNeedTotal;
    private double _selectedRestPressureTotal;

    public SimulationEngine(SimulationConfig config, long runSeed)
        : this(config, runSeed, null)
    {
    }

    internal SimulationEngine(SimulationConfig config, long runSeed, WorldState? initialState)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Config.Validate();
        RunSeed = runSeed;
        _random = new RandomStreamFactory(runSeed);
        State = initialState ?? WorldFactory.Create(config, _random);
        _minimumPopulation = State.Npcs.Values.Count(item => item.IsAlive);
        _perception = new PerceptionSystem(config, _random);
        _decision = new UtilityDecisionSystem(config, _random);
        var communication = new CommunicationSystem(config, _random, _perception);
        _reproduction = new ReproductionSystem(config, _random);
        _vitality = new VitalitySystem(config);
        _needs = new NeedsSystem(config);
        _conceptExposure = new ConceptExposureSystem(config);
        _aura = new ConceptAuraSystem(config, _random);
        _invasion = new InvasionSystem(config, _random);
        _maintenance = new SettlementMaintenanceCoordinator(config, _random, _invasion);
        _actionResolution = new ActionResolutionSystem(
            config, _random, _perception, _decision, communication, _reproduction, _needs, _invasion, _aura);
    }

    public SimulationConfig Config { get; }
    public long RunSeed { get; }
    internal WorldState State { get; }
    public IReadOnlyList<DecisionTrace> LastDecisionTraces => _lastDecisionTraces.AsReadOnly();

    internal static SimulationEngine CreateForTesting(SimulationConfig config, long runSeed, WorldState state) =>
        new(config, runSeed, state);

    public TickResult AdvanceOneDay()
    {
        lock (_gate)
        {
            _cachedWorldStatistics = null;
            var eventStart = _events.Count;
            _eventSequence = 0;
            _lastDecisionTraces.Clear();

            _maintenance.ActivatePending(State, AddEvent);
            _aura.Refresh(State, AddEvent, 0);

            _perception.Observe(State);
            _needs.UpdateDaily(State);

            var actionCounts = State.Npcs.Values.Where(item => item.IsAlive)
                .ToDictionary(item => item.Id, _ => 0);
            var eligible = actionCounts.Keys.ToHashSet();
            for (var microRound = 1; microRound <= Config.Action.MaximumActionsPerDay && eligible.Count > 0; microRound++)
            {
                _aura.Refresh(State, AddEvent, microRound);
                var intents = CreateIntents(eligible, microRound);
                _actionResolution.ResolveRound(
                    State,
                    intents,
                    microRound,
                    draft => AddEvent(
                        draft.MicroRound,
                        draft.Type,
                        draft.ActorId,
                        draft.TargetId,
                        draft.Position,
                        draft.Success,
                        draft.Detail),
                    trace => _lastDecisionTraces.Add(trace));
                _aura.Refresh(State, AddEvent, microRound);

                foreach (var intent in intents)
                {
                    if (actionCounts.ContainsKey(intent.ActorId))
                    {
                        actionCounts[intent.ActorId]++;
                    }
                }

                eligible = DetermineRepeatParticipants(intents, actionCounts, microRound);
            }

            foreach (var acquisition in _conceptExposure.Apply(State))
            {
                AddEvent(0, SimulationEventType.ConceptMarkAcquired, acquisition.EntityId, null,
                    acquisition.Position, true, acquisition.Concept.ToString());
                _needs.RefreshSurvival(State.Npcs[acquisition.EntityId]);
            }
            _aura.Refresh(State, AddEvent, 0);
            ApplyVitalityAndAging();
            ResolveBirthQueue();
            _minimumPopulation = Math.Min(_minimumPopulation, State.Npcs.Values.Count(item => item.IsAlive));
            var dayEvents = _events.Skip(eventStart).ToArray();
            _maintenance.RunEndOfDay(State, dayEvents, AddEvent);
            State.Tick++;

            return new TickResult(
                State.Tick - 1,
                _events.Skip(eventStart).ToArray());
        }
    }

    public void AdvanceDays(int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        for (var index = 0; index < days; index++)
        {
            AdvanceOneDay();
        }
    }

    public SimulationSnapshot GetSnapshot(int recentEventLimit = 200)
    {
        if (recentEventLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentEventLimit));
        }

        lock (_gate)
        {
            var npcs = State.Npcs.Values
                .Where(item => item.IsAlive)
                .OrderBy(item => item.Id)
                .Select(item => new NpcProjection(
                    item.Id,
                    item.Position,
                    (IReadOnlySet<ConceptKind>)item.ConceptMarks.ToHashSet(),
                    (IReadOnlySet<ConceptKind>)item.ActiveAuras.ToHashSet(),
                    item.SettlementId,
                    item.InvasionId))
                .ToArray();
            var landmarks = State.Landmarks
                .OrderBy(item => item.Concept)
                .Select(item => new LandmarkProjection(item.Concept, item.Position))
                .ToArray();
            var settlements = State.Settlements.Values.OrderBy(item => item.Id)
                .Select(item => new SettlementProjection(
                    item.Id,
                    item.Center,
                    Config.Settlement.CoreRadius,
                    Config.Settlement.InfluenceRadius,
                    item.FormedTick,
                    item.IsActive(State.Tick),
                    State.Npcs.Values.Count(npc => npc.IsAlive && npc.SettlementId == item.Id),
                    item.CrowdingPressure))
                .ToArray();
            var invasions = State.Invasions.Values.OrderBy(item => item.Id)
                .Select(item => new InvasionProjection(
                    item.Id,
                    item.AttackSettlementId,
                    item.DefenseSettlementId,
                    item.EffectiveTick,
                    item.IsActive(State.Tick)))
                .ToArray();
            var recent = _events.TakeLast(recentEventLimit).ToArray();
            return new SimulationSnapshot(
                State.Tick,
                Config.World.DaysPerYear,
                Config.World.Width,
                Config.World.Height,
                State.Phase,
                npcs,
                landmarks,
                settlements,
                invasions,
                recent);
        }
    }

    public IReadOnlyList<string> EventFingerprints()
    {
        lock (_gate)
        {
            return _events.Select(item => item.Fingerprint()).ToArray();
        }
    }

    public string DeterministicStateFingerprint()
    {
        lock (_gate)
        {
            var checkpoint = new
            {
                SchemaVersion = 3,
                State.Tick,
                State.Phase,
                State.PendingPhase,
                State.GenerationStartTick,
                State.OrderStartTick,
                State.StabilityConsecutiveDays,
                State.PopulationCv,
                State.DemographicImbalance,
                State.NextNpcId,
                State.NextSettlementId,
                State.NextInvasionId,
                MinimumPopulation = _minimumPopulation,
                EventSequence = _eventSequence,
                SelectedActions = _selectedActionCounts
                    .OrderBy(item => item.Key)
                    .Select(item => new { Action = item.Key, item.Value })
                    .ToArray(),
                SelectedActionsByNpc = _selectedActionCountsByNpc.OrderBy(item => item.Key)
                    .Select(item => new
                    {
                        NpcId = item.Key,
                        Counts = item.Value.OrderBy(value => value.Key)
                            .Select(value => new { Action = value.Key, value.Value }).ToArray()
                    }).ToArray(),
                Perception = new
                {
                    _perception.PositionInvalidationCount,
                    _perception.SubjectPurgeCount,
                    _perception.EvictionCount
                },
                Landmarks = State.Landmarks
                    .OrderBy(item => item.Concept)
                    .ThenBy(item => item.Position)
                    .Select(item => new { item.Concept, item.Position.X, item.Position.Y })
                    .ToArray(),
                Npcs = State.Npcs.Values
                    .OrderBy(item => item.Id)
                    .Select(item => new
                    {
                        item.Id,
                        item.IsAlive,
                        item.Position.X,
                        item.Position.Y,
                        BaseStats = new
                        {
                            item.BaseStats.MaxHp,
                            item.BaseStats.Action,
                            item.BaseStats.Combat,
                            item.BaseStats.Communication
                        },
                        item.RiskPreference,
                        item.CurrentHp,
                        item.AgeDays,
                        item.ReproductionCooldownDays,
                        item.SettlementId,
                        item.InvasionId,
                        item.InvasionRole,
                        item.HasAdvanceBias,
                        item.HasDefenseBias,
                        item.SettlementAtDeathId,
                        item.DeathAgeDays,
                        item.DeathCause,
                        Needs = item.Needs.Snapshot(),
                        ConceptMarks = item.ConceptMarks.OrderBy(value => value).ToArray(),
                        ActiveAuras = item.ActiveAuras.OrderBy(value => value).ToArray(),
                        SettlementAffinity = item.SettlementAffinity.OrderBy(value => value.Key).ToArray(),
                        WithdrawnInvasionIds = item.WithdrawnInvasionIds.OrderBy(value => value).ToArray(),
                        ConceptExposure = item.ConceptExposure
                            .OrderBy(value => value.Key)
                            .Select(value => new { Concept = value.Key, value.Value })
                            .ToArray(),
                        HeldInformation = item.HeldInformation.Select((value, index) => new
                        {
                            Index = index,
                            value.InformationId,
                            value.SubjectId,
                            value.Property,
                            value.EstimatedValue,
                            value.Confidence,
                            value.SourceId,
                            value.AcquiredBy,
                            value.AcquiredTick
                        }).ToArray(),
                        item.NextInformationSequence,
                        ThreatMemory = item.ThreatMemory
                            .OrderBy(value => value.Key)
                            .Select(value => new
                            {
                                SubjectId = value.Key,
                                value.Value.LastThreatTick
                            })
                            .ToArray(),
                        item.ParentAId,
                        item.ParentBId
                    })
                    .ToArray(),
                Settlements = State.Settlements.Values.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id,
                    item.Center,
                    item.FormedTick,
                    item.EffectiveTick,
                    item.FounderIds,
                    item.DissolvedTick,
                    item.DissolutionReason,
                    item.IntegratedIntoSettlementId,
                    item.CoreOccupancy,
                    item.BlockedMovementRate,
                    item.CrowdingPressure,
                    CrowdingHistory = item.CrowdingHistory.ToArray(),
                    item.CrowdingConsecutiveDays,
                    item.CrowdingInvasionArmed,
                    item.CrowdingRearmConsecutiveDays,
                    item.CrowdingRearmCount,
                    item.FoundingResidentBaseline,
                    SupportHistory = item.SupportHistory.ToArray(),
                    item.SupportPopulationComponent,
                    item.SupportReproductionComponent,
                    item.SupportSocialComponent,
                    item.Support,
                    item.LowSupportDays
                }).ToArray(),
                Frictions = State.Frictions.Values.OrderBy(item => item.Pair.FirstId).ThenBy(item => item.Pair.SecondId)
                    .Select(item => new
                    {
                        item.Pair,
                        item.CurrentFriction,
                        item.LastFrictionEventTick,
                        item.LifetimeFrictionEvents,
                        item.CollisionEvents,
                        item.ExplicitThreatEvents,
                        item.LifetimeDecay
                    }).ToArray(),
                Hostilities = State.Hostilities.OrderBy(item => item.SourceSettlementId)
                    .ThenBy(item => item.TargetSettlementId).ToArray(),
                Invasions = State.Invasions.Values.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id,
                    item.AttackSettlementId,
                    item.DefenseSettlementId,
                    item.CreatedTick,
                    item.EffectiveTick,
                    item.TriggerCrowdingPressure,
                    item.TargetReason,
                    item.AttackParticipantIds,
                    item.CoreCohortIds,
                    item.FrontierCohortIds,
                    DefenseParticipantIds = item.DefenseParticipantIds.OrderBy(value => value).ToArray(),
                    item.EndTick,
                    item.Outcome,
                    item.MaximumCoreOccupationRate,
                    item.CenterOccupied,
                    item.RestWithdrawals,
                    item.DeathWithdrawals
                }).ToArray(),
                ReproductionSuccesses = State.ReproductionSuccesses.OrderBy(item => item.EventId, StringComparer.Ordinal).ToArray(),
                PopulationHistory = State.PopulationHistory.ToArray(),
                State.SettlementCandidateCount,
                State.SettlementCandidateConflictCount,
                State.SettlementCandidateRejectionCount,
                State.AuraSelfMarkSuppressionCount,
                State.AttackCandidateSuppressionCount,
                State.UnaffiliatedThreatExceptionAttackCount,
                State.InvasionStartPreventedCount,
                BirthRequests = State.BirthRequests
                    .OrderBy(item => item.RequestId, StringComparer.Ordinal)
                    .Select(item => new
                    {
                        item.RequestId,
                        item.ParentAId,
                        item.ParentBId,
                        ParentAPosition = item.ParentAPositionAtConception,
                        ParentBPosition = item.ParentBPositionAtConception,
                        ParentAGenetics = new
                        {
                            item.ParentAGenetics.BaseStats.MaxHp,
                            item.ParentAGenetics.BaseStats.Action,
                            item.ParentAGenetics.BaseStats.Combat,
                            item.ParentAGenetics.BaseStats.Communication,
                            item.ParentAGenetics.RiskPreference
                        },
                        ParentBGenetics = new
                        {
                            item.ParentBGenetics.BaseStats.MaxHp,
                            item.ParentBGenetics.BaseStats.Action,
                            item.ParentBGenetics.BaseStats.Combat,
                            item.ParentBGenetics.BaseStats.Communication,
                            item.ParentBGenetics.RiskPreference
                        },
                        item.ConceptionTick,
                        item.BirthSettlementId,
                        item.SettlementPlacement
                    })
                    .ToArray(),
                Events = _events.Select(item => item.Fingerprint()).ToArray()
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
    }

    public NpcDetailsProjection? GetNpcDetails(long npcId, int actionHistoryLimit = int.MaxValue)
    {
        if (actionHistoryLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actionHistoryLimit));
        }

        lock (_gate)
        {
            if (!State.Npcs.TryGetValue(npcId, out var npc))
            {
                return null;
            }

            var children = State.Npcs.Values
                .Where(item => item.ParentAId == npcId || item.ParentBId == npcId)
                .OrderBy(item => item.Id)
                .ToArray();
            var relatedEvents = _events
                .Where(item => item.ActorId == npcId || item.TargetId == npcId)
                .ToArray();
            var actionHistory = relatedEvents
                .Where(item => item.Type is not SimulationEventType.Move and not SimulationEventType.MoveFailed)
                .OrderBy(item => item.Tick)
                .ThenBy(item => item.MicroRound)
                .ThenBy(item => item.EventId, StringComparer.Ordinal)
                .Select(item => new NpcActionRecord(
                    item.Tick,
                    item.MicroRound,
                    item.Type,
                    item.ActorId == npcId ? item.TargetId : item.ActorId,
                    item.ActorId == npcId,
                    item.Success,
                    item.Detail))
                .TakeLast(actionHistoryLimit)
                .ToArray();
            var effective = npc.EffectiveStats(Config);

            return new NpcDetailsProjection(
                npc.Id,
                npc.IsAlive,
                npc.Position,
                npc.AgeDays,
                Config.World.DaysPerYear,
                npc.CurrentHp,
                new StatsProjection(
                    npc.BaseStats.MaxHp,
                    npc.BaseStats.Action,
                    npc.BaseStats.Combat,
                    npc.BaseStats.Communication),
                new StatsProjection(
                    effective.MaxHp,
                    effective.Action,
                    effective.Combat,
                    effective.Communication),
                npc.RiskPreference,
                npc.Needs.Snapshot(),
                npc.ReproductionCooldownDays,
                npc.IsMature(Config),
                npc.ParentAId,
                npc.ParentBId,
                children.Select(item => item.Id).ToArray(),
                (IReadOnlySet<ConceptKind>)npc.ConceptMarks.ToHashSet(),
                (IReadOnlySet<ConceptKind>)npc.ActiveAuras.ToHashSet(),
                npc.SettlementId,
                npc.SettlementAffinity.OrderBy(item => item.Key)
                    .Select(item => new SettlementAffinityProjection(item.Key, item.Value, npc.SettlementId == item.Key))
                    .ToArray(),
                npc.InvasionId,
                npc.InvasionRole,
                CountKills(relatedEvents, npcId),
                npc.HeldInformation.Count,
                actionHistory);
        }
    }

    internal static long CountKills(IEnumerable<SimulationEvent> events, long npcId) => events.LongCount(item =>
        item.Type == SimulationEventType.Death &&
        item.TargetId == npcId &&
        item.Detail.StartsWith("combat:", StringComparison.Ordinal));

    public AgeDistributionProjection GetCurrentAgeDistribution(int bucketSizeDays)
    {
        if (bucketSizeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSizeDays));
        }

        lock (_gate)
        {
            var ages = State.Npcs.Values
                .Where(item => item.IsAlive)
                .Select(item => item.AgeDays)
                .ToArray();
            if (ages.Length == 0)
            {
                return new AgeDistributionProjection(0, bucketSizeDays, Array.Empty<AgeDistributionBucket>());
            }

            var maximumBucket = ages.Max() / bucketSizeDays;
            var counts = new int[maximumBucket + 1];
            foreach (var age in ages)
            {
                counts[age / bucketSizeDays]++;
            }

            var buckets = counts
                .Select((count, index) => new AgeDistributionBucket(
                    checked(index * bucketSizeDays),
                    checked((index + 1) * bucketSizeDays),
                    count))
                .ToArray();
            return new AgeDistributionProjection(ages.Length, bucketSizeDays, buckets);
        }
    }

    public WorldStatisticsProjection GetWorldStatistics()
    {
        lock (_gate)
        {
            _cachedWorldStatistics ??= new WorldStatisticsProjector(
                Config,
                State,
                _events,
                _selectedActionCounts,
                _perception,
                _minimumPopulation).GetWorldStatistics();
            return _cachedWorldStatistics;
        }
    }

    public DailyObservationProjection GetDailyObservation()
    {
        lock (_gate)
        {
            var alive = State.Npcs.Values.Where(item => item.IsAlive).ToArray();
            var affiliatedPopulation = alive.Count(item =>
                SettlementQueries.ActiveSettlement(State, item.SettlementId) is not null);
            var activeSettlements = SettlementQueries.ActiveSettlements(State);
            var selections = _selectedActionCounts
                .OrderBy(item => item.Key)
                .Select(item => new ActionSelectionCount(item.Key, item.Value))
                .ToArray();
            var heldCounts = alive.Select(item => item.HeldInformation.Count).ToArray();
            var selectedActions = _selectedActionCounts.Values.Sum();

            return new DailyObservationProjection(
                State.Tick,
                alive.Length,
                _minimumPopulation,
                alive.Length == 0 ? 0 : alive.Average(item => (double)item.AgeDays / Config.World.DaysPerYear),
                State.Phase,
                activeSettlements.Count,
                affiliatedPopulation,
                State.PopulationCv,
                State.DemographicImbalance,
                State.StabilityConsecutiveDays,
                selections,
                new PerceptionStatistics(
                    _perception.PositionInvalidationCount,
                    _perception.SubjectPurgeCount,
                    _perception.EvictionCount,
                    heldCounts.Sum(),
                    heldCounts.Length == 0 ? 0 : heldCounts.Average(),
                    heldCounts.Length == 0 ? 0 : heldCounts.Max()),
                selectedActions == 0 ? 0 : (double)_restActions / selectedActions,
                alive.Length == 0 ? 0 : alive.Average(item => item.Needs.Rest),
                _restActions == 0 ? 0 : _selectedRestNeedTotal / _restActions,
                _restActions == 0 ? 0 : _selectedRestPressureTotal / _restActions,
                activeSettlements.Count == 0 ? 0 : activeSettlements.Average(item => item.Support),
                activeSettlements.Sum(item => item.LowSupportDays),
                activeSettlements.Count(item => item.CrowdingInvasionArmed),
                State.InvasionStartPreventedCount);
        }
    }

    private IReadOnlyList<ActionIntent> CreateIntents(IReadOnlySet<long> eligible, int microRound)
    {
        var ids = eligible.OrderBy(item => item).ToArray();
        var plans = new PlannedIntent?[ids.Length];
        var activeSettlements = SettlementQueries.ActiveSettlements(State);
        var activeCores = activeSettlements
            .Select(item => new SettlementCoreRule(item.Id, item.Center, Config.Settlement.CoreRadius))
            .ToArray();
        var settlementRegions = activeSettlements
            .Select(item => new SettlementMovementRule(
                item.Id,
                item.Center,
                Config.Settlement.CoreRadius,
                Config.Settlement.InfluenceRadius))
            .ToArray();
        var landmarkPositions = State.Landmarks.Select(item => item.Position).ToHashSet();
        var degree = ParallelExecutionPolicy.ResolveDegree(Config.Performance, ids.Length);

        void Plan(int index)
        {
            var id = ids[index];
            if (!State.Npcs.TryGetValue(id, out var npc) || !npc.IsAlive)
            {
                return;
            }

            _needs.RefreshSurvival(npc);
            var perception = _perception.CreateView(npc, State.Tick);
            var rules = CreateWorldDecisionRules(npc, perception, activeCores, settlementRegions, landmarkPositions);
            var context = CreateDecisionContext(npc, perception, rules, out var suppressedAttackCandidates);
            var trace = _decision.Decide(context, State.Tick, microRound);
            plans[index] = new PlannedIntent(trace, suppressedAttackCandidates);
        }

        if (degree == 1)
        {
            for (var index = 0; index < ids.Length; index++)
            {
                Plan(index);
            }
        }
        else
        {
            Parallel.For(0, ids.Length, new ParallelOptions { MaxDegreeOfParallelism = degree }, Plan);
        }

        var intents = new List<ActionIntent>();
        foreach (var plan in plans)
        {
            if (plan is null)
            {
                continue;
            }

            var trace = plan.Trace;
            State.AttackCandidateSuppressionCount += plan.SuppressedAttackCandidates;
            _lastDecisionTraces.Add(trace);
            _selectedActionCounts[trace.Selected.Kind]++;
            if (!_selectedActionCountsByNpc.TryGetValue(trace.EntityId, out var npcCounts))
            {
                npcCounts = Enum.GetValues<ActionKind>().ToDictionary(action => action, _ => 0L);
                _selectedActionCountsByNpc.Add(trace.EntityId, npcCounts);
            }
            npcCounts[trace.Selected.Kind]++;
            intents.Add(_decision.CreateIntent(trace));
        }

        return intents;
    }

    private WorldDecisionRules CreateWorldDecisionRules(
        NpcState npc,
        PerceptionView perception,
        IReadOnlyList<SettlementCoreRule> activeCores,
        IReadOnlyList<SettlementMovementRule> settlementRegions,
        IReadOnlySet<Position> landmarkPositions)
    {
        var suppressedTargets = perception.Threats
            .Select(item => item.EntityId)
            .Distinct()
            .Where(id => State.Npcs.TryGetValue(id, out var target) && target.IsAlive && target.Id != npc.Id &&
                         SettlementQueries.ExplicitAttackProtection(State, npc, target, Config) is not null)
            .ToHashSet();
        var movementTarget = _invasion.MovementTarget(State, npc);
        return new WorldDecisionRules(
            Config.World.Width,
            Config.World.Height,
            landmarkPositions,
            activeCores,
            suppressedTargets,
            movementTarget,
            npc.HasAdvanceBias ? Config.Invasion.AdvanceBiasWeight :
                npc.HasDefenseBias ? Config.Invasion.DefenseBiasWeight : 0,
            _aura.FindCohesionTarget(State, npc),
            Config.Invasion.AuraCohesionWeight,
            State.Phase == WorldPhase.Order,
            npc.SettlementId,
            settlementRegions,
            InvasionSystem.IsActiveParticipant(State, npc));
    }

    private DecisionContext CreateDecisionContext(
        NpcState npc,
        PerceptionView perception,
        WorldDecisionRules rules,
        out long suppressedAttackCandidates)
    {
        suppressedAttackCandidates = perception.Threats.LongCount(item =>
            rules.IsAttackSuppressed(item.EntityId));
        return new DecisionContext(
            npc.Id,
            npc.Position,
            npc.CurrentHp,
            npc.EffectiveStats(Config),
            npc.RiskPreference,
            npc.AgeDays,
            npc.ReproductionCooldownDays,
            npc.Needs.Snapshot(),
            perception,
            rules);
    }

    private sealed record PlannedIntent(DecisionTrace Trace, long SuppressedAttackCandidates);

    private HashSet<long> DetermineRepeatParticipants(
        IReadOnlyList<ActionIntent> intents,
        IReadOnlyDictionary<long, int> actionCounts,
        int microRound)
    {
        var result = new HashSet<long>();
        foreach (var actorId in intents.Select(item => item.ActorId).Distinct().OrderBy(item => item))
        {
            if (!State.Npcs.TryGetValue(actorId, out var npc) || !npc.IsAlive ||
                actionCounts[actorId] >= Config.Action.MaximumActionsPerDay)
            {
                continue;
            }

            var action = npc.EffectiveStats(Config).Action;
            var probability = action / (action + Config.Action.RepeatDenominator);
            if (_random.Create("scheduling", State.Tick, actorId, "repeat-action", microRound.ToString()).NextDouble() < probability)
            {
                result.Add(actorId);
            }
        }

        return result;
    }

    private void ApplyVitalityAndAging()
    {
        foreach (var npc in State.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id).ToArray())
        {
            var dailyChange = _vitality.DailyVitalChange(npc.AgeDays);
            var activeCore = SettlementQueries.FindActiveCore(State, npc.Position, Config) is not null;
            var multiplier = !activeCore
                ? 1
                : State.Phase == WorldPhase.Generation && dailyChange > 0
                    ? Config.Settlement.GenerationPositiveVitalityMultiplier
                    : State.Phase == WorldPhase.Order && dailyChange > 0
                        ? Config.Settlement.PositiveVitalityMultiplier
                        : State.Phase == WorldPhase.Order && dailyChange < 0
                            ? Config.Settlement.NegativeVitalityMultiplier
                            : 1;
            if (_vitality.ApplyDailyChange(npc, multiplier))
            {
                npc.SettlementAtDeathId = npc.SettlementId;
                npc.DeathAgeDays = npc.AgeDays;
                npc.DeathCause = "vitality";
                AddEvent(0, SimulationEventType.Death, npc.Id, null, npc.Position, true, "vitality");
                _invasion.NotifyDeath(State, npc, AddEvent, 0);
            }
            else
            {
                _needs.RefreshSurvival(npc);
            }
        }
    }

    private void ResolveBirthQueue()
    {
        foreach (var result in _reproduction.ResolveBirths(State))
        {
            if (result.Success)
            {
                AddEvent(0, SimulationEventType.Birth, result.Child!.Id, result.Request.ParentAId, result.Position, true,
                    $"parents={result.Request.ParentAId},{result.Request.ParentBId};request={result.Request.RequestId};" +
                    $"settlement={result.Child.SettlementId?.ToString() ?? "-"};" +
                    $"placement={SettlementQueries.BirthPlacementLabel(result.AppliedPlacement)}");
            }
            else
            {
                AddEvent(0, SimulationEventType.BirthFailure, result.Request.ParentAId, result.Request.ParentBId, null, false,
                    $"request={result.Request.RequestId};no-cell;" +
                    $"placement={SettlementQueries.BirthPlacementLabel(result.AppliedPlacement)}");
            }
        }
    }

    private void AddEvent(
        int microRound,
        SimulationEventType type,
        long? actorId,
        long? targetId,
        Position? position,
        bool success,
        string detail)
    {
        var sequence = _eventSequence++;
        var actorSettlementId = actorId.HasValue && State.Npcs.TryGetValue(actorId.Value, out var actor)
            ? actor.SettlementId
            : null;
        var targetSettlementId = targetId.HasValue && State.Npcs.TryGetValue(targetId.Value, out var target)
            ? target.SettlementId
            : null;
        var simulationEvent = new SimulationEvent(
            StableHash.StableId("event", State.Tick, sequence, type, actorId, targetId),
            State.Tick,
            microRound,
            type,
            actorId,
            targetId,
            position,
            success,
            detail,
            actorSettlementId,
            targetSettlementId);
        _events.Add(simulationEvent);
        if (type == SimulationEventType.Rest && success)
        {
            _restActions++;
            _selectedRestNeedTotal += DetailDouble(detail, "selectedRestNeed");
            _selectedRestPressureTotal += DetailDouble(detail, "restPressure");
        }
        if (type == SimulationEventType.ReproductionSuccess && actorId.HasValue && targetId.HasValue && position.HasValue)
        {
            State.ReproductionSuccesses.Add(new ReproductionSuccessRecord(
                simulationEvent.EventId,
                State.Tick,
                position.Value,
                actorId.Value,
                targetId.Value,
                ActiveSettlementMembership(actorId.Value),
                ActiveSettlementMembership(targetId.Value)));
            var minimumTick = State.Tick - Config.Settlement.HotspotWindowDays + 1;
            State.ReproductionSuccesses.RemoveAll(item => item.Tick < minimumTick);
        }

        int? ActiveSettlementMembership(long npcId) =>
            State.Npcs.TryGetValue(npcId, out var npc) &&
            SettlementQueries.ActiveSettlement(State, npc.SettlementId) is not null
                ? npc.SettlementId
                : null;
    }

    private static double DetailDouble(string detail, string key)
    {
        foreach (var part in detail.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && string.Equals(part[..separator], key, StringComparison.Ordinal) &&
                double.TryParse(part[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }
        return 0;
    }
}
