using System.Globalization;
using System.Security.Cryptography;
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
    private readonly List<SimulationEvent> _events = new();
    private readonly List<DecisionTrace> _lastDecisionTraces = new();
    private readonly Dictionary<ActionKind, long> _selectedActionCounts = Enum
        .GetValues<ActionKind>()
        .ToDictionary(action => action, _ => 0L);
    private int _minimumPopulation;
    private int _eventSequence;

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
        _actionResolution = new ActionResolutionSystem(
            config, _random, _perception, _decision, communication, _reproduction, _needs);
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
            var eventStart = _events.Count;
            _eventSequence = 0;
            _lastDecisionTraces.Clear();

            _perception.Observe(State);
            _needs.UpdateDaily(State);

            var actionCounts = State.Npcs.Values.Where(item => item.IsAlive)
                .ToDictionary(item => item.Id, _ => 0);
            var eligible = actionCounts.Keys.ToHashSet();
            for (var microRound = 1; microRound <= Config.Action.MaximumActionsPerDay && eligible.Count > 0; microRound++)
            {
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
            ApplyVitalityAndAging();
            ResolveBirthQueue();
            _minimumPopulation = Math.Min(_minimumPopulation, State.Npcs.Values.Count(item => item.IsAlive));
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
                    (IReadOnlySet<ConceptKind>)item.ConceptMarks.ToHashSet()))
                .ToArray();
            var landmarks = State.Landmarks
                .OrderBy(item => item.Concept)
                .Select(item => new LandmarkProjection(item.Concept, item.Position))
                .ToArray();
            var recent = _events.TakeLast(recentEventLimit).ToArray();
            return new SimulationSnapshot(
                State.Tick,
                Config.World.DaysPerYear,
                Config.World.Width,
                Config.World.Height,
                npcs,
                landmarks,
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
                SchemaVersion = 1,
                State.Tick,
                State.NextNpcId,
                MinimumPopulation = _minimumPopulation,
                EventSequence = _eventSequence,
                SelectedActions = _selectedActionCounts
                    .OrderBy(item => item.Key)
                    .Select(item => new { Action = item.Key, item.Value })
                    .ToArray(),
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
                        Needs = item.Needs.Snapshot(),
                        ConceptMarks = item.ConceptMarks.OrderBy(value => value).ToArray(),
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
                        item.ConceptionTick
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
            var actionHistory = _events
                .Where(item => item.Type is not SimulationEventType.Move and not SimulationEventType.MoveFailed)
                .Where(item => item.ActorId == npcId || item.TargetId == npcId)
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
                npc.HeldInformation.Count,
                actionHistory);
        }
    }

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
            var alive = State.Npcs.Values.Where(item => item.IsAlive).ToArray();
            var averageAgeYears = alive.Length == 0
                ? 0
                : alive.Average(item => (double)item.AgeDays / Config.World.DaysPerYear);
            var selections = _selectedActionCounts
                .OrderBy(item => item.Key)
                .Select(item => new ActionSelectionCount(item.Key, item.Value))
                .ToArray();
            var deathCauses = _events
                .Where(item => item.Type == SimulationEventType.Death)
                .GroupBy(item => item.Detail, StringComparer.Ordinal)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    var ages = group
                        .Where(item => item.ActorId.HasValue && State.Npcs.ContainsKey(item.ActorId.Value))
                        .Select(item => (double)State.Npcs[item.ActorId!.Value].AgeDays / Config.World.DaysPerYear)
                        .ToArray();
                    return new DeathCauseStatistics(group.Key, group.LongCount(), ages.Length == 0 ? 0 : ages.Average());
                })
                .ToArray();
            var reproductionOutcomes = _events
                .Where(item => item.Type is SimulationEventType.ReproductionSuccess or SimulationEventType.ReproductionFailure)
                .GroupBy(item => item.Type == SimulationEventType.ReproductionSuccess ? "success" : item.Detail,
                    StringComparer.Ordinal)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(group => new ReproductionOutcomeStatistics(group.Key, group.LongCount()))
                .ToArray();
            var targetedActions = new[]
            {
                TargetedStatistics(ActionKind.Attack, SimulationEventType.Attack),
                TargetedStatistics(ActionKind.Reproduction, SimulationEventType.ReproductionAttempt),
                TargetedStatistics(ActionKind.Communication, SimulationEventType.Communication)
            };
            var combatTypes = new[]
                {
                    SimulationEventType.Attack,
                    SimulationEventType.CollisionAttack,
                    SimulationEventType.Counterattack,
                    SimulationEventType.Pursuit
                }
                .Select(type =>
                {
                    var results = _events.Where(item => item.Type == type &&
                        (item.Detail == "miss" || item.Detail.StartsWith("damage=", StringComparison.Ordinal))).ToArray();
                    var damage = results
                        .Select(item => ParseDamage(item.Detail))
                        .Where(item => item.HasValue)
                        .Select(item => item!.Value)
                        .ToArray();
                    return new CombatTypeStatistics(type, results.LongLength, damage.LongLength,
                        damage.Length == 0 ? 0 : damage.Average());
                })
                .ToArray();
            var heldCounts = alive.Select(item => item.HeldInformation.Count).ToArray();
            var perception = new PerceptionStatistics(
                _perception.PositionInvalidationCount,
                _perception.SubjectPurgeCount,
                _perception.EvictionCount,
                heldCounts.Sum(),
                heldCounts.Length == 0 ? 0 : heldCounts.Average(),
                heldCounts.Length == 0 ? 0 : heldCounts.Max());
            var conceptMarks = Enum.GetValues<ConceptKind>()
                .Select(concept =>
                {
                    var exposure = alive.Select(item => item.ConceptExposure.GetValueOrDefault(concept)).ToArray();
                    return new ConceptMarkStatistics(
                        concept,
                        alive.Count(item => item.ConceptMarks.Contains(concept)),
                        _events.LongCount(item => item.Type == SimulationEventType.ConceptMarkAcquired &&
                                                  item.Detail == concept.ToString()),
                        exposure.Sum(),
                        exposure.Length == 0 ? 0 : exposure.Average(),
                        exposure.Length == 0 ? 0 : exposure.Max());
                })
                .ToArray();
            return new WorldStatisticsProjection(
                State.Tick,
                alive.Length,
                _minimumPopulation,
                averageAgeYears,
                selections,
                deathCauses,
                reproductionOutcomes,
                targetedActions,
                combatTypes,
                perception,
                conceptMarks);

            TargetedActionStatistics TargetedStatistics(ActionKind action, SimulationEventType eventType)
            {
                var attempts = _events.Where(item => item.Type == eventType).ToArray();
                var absent = action == ActionKind.Reproduction
                    ? _events.LongCount(item => item.Type == SimulationEventType.ReproductionFailure &&
                                                item.Detail == "target-absent")
                    : attempts.LongCount(item => item.Detail == "target-absent");
                return new TargetedActionStatistics(action, attempts.LongLength, absent);
            }
        }
    }

    private static double? ParseDamage(string detail)
    {
        const string prefix = "damage=";
        return detail.StartsWith(prefix, StringComparison.Ordinal) &&
               double.TryParse(detail[prefix.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private IReadOnlyList<ActionIntent> CreateIntents(IReadOnlySet<long> eligible, int microRound)
    {
        var rules = new WorldDecisionRules(
            Config.World.Width,
            Config.World.Height,
            State.Landmarks.Select(item => item.Position).ToHashSet());
        var intents = new List<ActionIntent>();
        foreach (var id in eligible.OrderBy(item => item))
        {
            if (!State.Npcs.TryGetValue(id, out var npc) || !npc.IsAlive)
            {
                continue;
            }

            _needs.RefreshSurvival(npc);
            var context = CreateDecisionContext(npc, rules);
            var trace = _decision.Decide(context, State.Tick, microRound);
            _lastDecisionTraces.Add(trace);
            _selectedActionCounts[trace.Selected.Kind]++;
            intents.Add(_decision.CreateIntent(trace));
        }

        return intents;
    }

    private DecisionContext CreateDecisionContext(NpcState npc, WorldDecisionRules rules)
    {
        return new DecisionContext(
            npc.Id,
            npc.Position,
            npc.CurrentHp,
            npc.EffectiveStats(Config),
            npc.RiskPreference,
            npc.AgeDays,
            npc.ReproductionCooldownDays,
            npc.Needs.Snapshot(),
            _perception.CreateView(npc, State.Tick),
            rules);
    }

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
            if (_vitality.ApplyDailyChange(npc))
            {
                AddEvent(0, SimulationEventType.Death, npc.Id, null, npc.Position, true, "vitality");
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
                    $"parents={result.Request.ParentAId},{result.Request.ParentBId};request={result.Request.RequestId}");
            }
            else
            {
                AddEvent(0, SimulationEventType.BirthFailure, result.Request.ParentAId, result.Request.ParentBId, null, false,
                    $"request={result.Request.RequestId};no-cell");
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
        _events.Add(new SimulationEvent(
            StableHash.StableId("event", State.Tick, sequence, type, actorId, targetId),
            State.Tick,
            microRound,
            type,
            actorId,
            targetId,
            position,
            success,
            detail));
    }
}
