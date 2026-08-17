using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Perception;
using Simulation.Core.Social;

namespace Simulation.Core.Statistics;

internal sealed class WorldStatisticsProjector
{
    private readonly object _gate = new();
    private readonly SimulationConfig _config;
    private readonly WorldState _state;
    private readonly IReadOnlyList<SimulationEvent> _events;
    private readonly IReadOnlyDictionary<ActionKind, long> _selectedActionCounts;
    private readonly PerceptionSystem _perception;
    private readonly int _minimumPopulation;

    public WorldStatisticsProjector(
        SimulationConfig config,
        WorldState state,
        IReadOnlyList<SimulationEvent> events,
        IReadOnlyDictionary<ActionKind, long> selectedActionCounts,
        PerceptionSystem perception,
        int minimumPopulation)
    {
        _config = config;
        _state = state;
        _events = events;
        _selectedActionCounts = selectedActionCounts;
        _perception = perception;
        _minimumPopulation = minimumPopulation;
    }

    public WorldStatisticsProjection GetWorldStatistics()
    {
        lock (_gate)
        {
            var alive = _state.Npcs.Values.Where(item => item.IsAlive).ToArray();
            var averageAgeYears = alive.Length == 0
                ? 0
                : alive.Average(item => (double)item.AgeDays / _config.World.DaysPerYear);
            var selections = _selectedActionCounts
                .OrderBy(item => item.Key)
                .Select(item => new ActionSelectionCount(item.Key, item.Value))
                .ToArray();
            var deathCauses = _events
                .Where(item => item.Type == SimulationEventType.Death)
                .GroupBy(item => DetailReason(item.Detail), StringComparer.Ordinal)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    var ages = group
                        .Where(item => item.ActorId.HasValue && _state.Npcs.ContainsKey(item.ActorId.Value))
                        .Select(item => (double)(_state.Npcs[item.ActorId!.Value].DeathAgeDays ??
                                                _state.Npcs[item.ActorId!.Value].AgeDays) / _config.World.DaysPerYear)
                        .ToArray();
                    return new DeathCauseStatistics(group.Key, group.LongCount(), ages.Length == 0 ? 0 : ages.Average());
                })
                .ToArray();
            var reproductionOutcomes = _events
                .Where(item => item.Type is SimulationEventType.ReproductionSuccess or SimulationEventType.ReproductionFailure)
                .GroupBy(item => item.Type == SimulationEventType.ReproductionSuccess ? "success" : DetailReason(item.Detail),
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
            var affiliatedPopulation = alive.Count(item => item.SettlementId.HasValue &&
                SettlementQueries.ActiveSettlement(_state, item.SettlementId) is not null);
            var settlementStatistics = _state.Settlements.Values.OrderBy(item => item.Id)
                .Select(item =>
                {
                    var population = alive.Count(npc => npc.SettlementId == item.Id);
                    return new SettlementStatistics(
                        item.Id,
                        item.Center,
                        item.FormedTick,
                        item.FounderIds.Count,
                        item.IsActive(_state.Tick),
                        population,
                        alive.Length == 0 ? 0 : (double)population / alive.Length,
                        item.CoreOccupancy,
                        item.CrowdingPressure,
                        item.CrowdingConsecutiveDays,
                        item.DissolvedTick,
                        item.DissolutionReason,
                        item.IntegratedIntoSettlementId);
                })
                .ToArray();
            var frictionStatistics = _state.Frictions.Values
                .OrderBy(item => item.Pair.FirstId)
                .ThenBy(item => item.Pair.SecondId)
                .Select(item => new FrictionStatistics(
                    item.Pair.FirstId,
                    item.Pair.SecondId,
                    item.CurrentFriction,
                    item.CollisionEvents,
                    item.ExplicitThreatEvents,
                    item.LifetimeDecay,
                    item.LastFrictionEventTick))
                .ToArray();
            var affiliationGroups = new[]
            {
                CreateAffiliationGroup("affiliated", true),
                CreateAffiliationGroup("unaffiliated", false)
            };
            var violence = new ViolenceStatistics(
                _events.LongCount(item => item.Type is SimulationEventType.CollisionAttack or SimulationEventType.CollisionSuppressed),
                _events.LongCount(item => item.Type == SimulationEventType.CollisionAttack),
                _events.LongCount(item => item.Type == SimulationEventType.CollisionSuppressed &&
                                          item.Detail.Contains("same-settlement", StringComparison.Ordinal)),
                _events.LongCount(item => item.Type == SimulationEventType.CollisionSuppressed &&
                                          item.Detail.Contains("unaffiliated-protected", StringComparison.Ordinal)),
                _events.LongCount(item => item.Type == SimulationEventType.CollisionSuppressed &&
                                          item.Detail.Contains("other-settlement-friction", StringComparison.Ordinal)),
                _events.LongCount(item => item.Type == SimulationEventType.SettlementFrictionChanged &&
                                          !item.Detail.Contains("reason=decay", StringComparison.Ordinal)),
                _events.LongCount(item => item.Type == SimulationEventType.Attack),
                _events.LongCount(item => item.Type == SimulationEventType.Counterattack),
                _events.LongCount(item => item.Type == SimulationEventType.Pursuit),
                _state.AttackCandidateSuppressionCount,
                _events.LongCount(item => item.Type == SimulationEventType.AttackSuppressed),
                _state.UnaffiliatedThreatExceptionAttackCount);
            var reproductionScopes = new[] { "same-core", "outside-penalty", "unknown" }
                .Select(scope => new ReproductionScopeStatistics(
                    scope,
                    _events.LongCount(item => item.Type == SimulationEventType.ReproductionAttempt &&
                                              item.Detail.Contains($"scope={scope}", StringComparison.Ordinal)),
                    _events.LongCount(item => item.Type == SimulationEventType.ReproductionSuccess &&
                                              item.Detail.Contains($"scope={scope}", StringComparison.Ordinal)),
                    _events.LongCount(item => item.Type == SimulationEventType.ReproductionFailure &&
                                              item.Detail.Contains($"scope={scope}", StringComparison.Ordinal))))
                .Where(item => item.Attempts + item.Successes + item.Failures > 0)
                .ToArray();
            var invasionStatistics = _state.Invasions.Values.OrderBy(item => item.Id)
                .Select(item =>
                {
                    var usable = _state.Settlements.TryGetValue(item.DefenseSettlementId, out var defense)
                        ? SettlementQueries.UsableCoreCells(_state, defense, _config)
                        : Array.Empty<Position>();
                    var occupied = _state.Npcs.Values.Where(npc => npc.IsAlive &&
                            npc.SettlementId == item.AttackSettlementId && usable.Contains(npc.Position))
                        .Select(npc => npc.Position).Distinct().Count();
                    var fleeing = _events.Where(simulationEvent => simulationEvent.Tick == _state.Tick - 1 &&
                            simulationEvent.Type == SimulationEventType.Flee && simulationEvent.ActorId.HasValue)
                        .Select(simulationEvent => simulationEvent.ActorId!.Value)
                        .Distinct()
                        .Count(id => _state.Npcs.TryGetValue(id, out var npc) && npc.InvasionId == item.Id);
                    return new InvasionStatistics(
                        item.Id,
                        item.AttackSettlementId,
                        item.DefenseSettlementId,
                        item.CreatedTick,
                        item.EffectiveTick,
                        item.EndTick,
                        item.Outcome,
                        item.TriggerCrowdingPressure,
                        item.TargetReason,
                        item.AttackParticipantIds.Count,
                        item.CoreCohortIds.Count,
                        item.FrontierCohortIds.Count,
                        item.RestWithdrawals,
                        item.DeathWithdrawals,
                        item.MaximumCoreOccupationRate,
                        item.CenterOccupied,
                        usable.Count,
                        occupied,
                        fleeing);
                })
                .ToArray();
            var auras = new AuraStatistics(
                _events.LongCount(item => item.Type == SimulationEventType.AuraApplied),
                _events.LongCount(item => item.Type == SimulationEventType.AuraExpired),
                _state.AuraSelfMarkSuppressionCount,
                _events.LongCount(item => item.Type == SimulationEventType.AuraApplied &&
                                          item.Detail.Contains("concept=Survival", StringComparison.Ordinal)),
                _events.LongCount(item => item.Type == SimulationEventType.AuraExpired &&
                                          item.Detail.Contains("concept=Survival", StringComparison.Ordinal)),
                alive.Count(item => item.ActiveAuras.Count > 0),
                alive.Count(item => item.InvasionId.HasValue && item.ConceptMarks.Count > 0));
            var transitionWindows = new List<PhaseWindowStatistics>();
            if (_state.OrderStartTick.HasValue)
            {
                var orderTick = _state.OrderStartTick.Value;
                transitionWindows.Add(AggregateWindow(
                    "before-order",
                    _state.PopulationHistory.Where(item =>
                        item.Tick >= orderTick - _config.Settlement.StabilityWindowDays && item.Tick < orderTick)));
                transitionWindows.Add(AggregateWindow(
                    "after-order",
                    _state.PopulationHistory.Where(item => item.Tick >= orderTick &&
                        item.Tick < orderTick + _config.Settlement.StabilityWindowDays)));
            }
            return new WorldStatisticsProjection(
                _state.Tick,
                alive.Length,
                _minimumPopulation,
                averageAgeYears,
                selections,
                deathCauses,
                reproductionOutcomes,
                targetedActions,
                combatTypes,
                perception,
                conceptMarks,
                new WorldPhaseStatistics(
                    _state.Phase,
                    _state.GenerationStartTick,
                    _state.OrderStartTick,
                    _state.PopulationCv,
                    _state.DemographicImbalance,
                    _state.StabilityConsecutiveDays),
                affiliatedPopulation,
                alive.Length - affiliatedPopulation,
                settlementStatistics,
                frictionStatistics,
                affiliationGroups,
                violence,
                reproductionScopes,
                invasionStatistics,
                auras,
                transitionWindows,
                _state.SettlementCandidateCount,
                _state.SettlementCandidateConflictCount,
                _state.SettlementCandidateRejectionCount);

            TargetedActionStatistics TargetedStatistics(ActionKind action, SimulationEventType eventType)
            {
                var attempts = _events.Where(item => item.Type == eventType).ToArray();
                var absent = action == ActionKind.Reproduction
                    ? _events.LongCount(item => item.Type == SimulationEventType.ReproductionFailure &&
                                                item.Detail == "target-absent")
                    : attempts.LongCount(item => item.Detail == "target-absent");
                return new TargetedActionStatistics(action, attempts.LongLength, absent);
            }

            AffiliationGroupStatistics CreateAffiliationGroup(string name, bool affiliated)
            {
                bool IsAffiliated(NpcState npc, bool atDeath) => atDeath
                    ? npc.SettlementAtDeathId.HasValue
                    : npc.SettlementId.HasValue && SettlementQueries.ActiveSettlement(_state, npc.SettlementId) is not null;
                var living = alive.Where(item => IsAffiliated(item, false)).Where(_ => affiliated).ToArray();
                if (!affiliated)
                {
                    living = alive.Where(item => !IsAffiliated(item, false)).ToArray();
                }
                var dead = _state.Npcs.Values.Where(item => !item.IsAlive && item.DeathAgeDays.HasValue &&
                    IsAffiliated(item, true) == affiliated).ToArray();
                var groupActionEvents = _events.Where(item =>
                    IsActionSelectionEvent(item) && item.ActorSettlementId.HasValue == affiliated).ToArray();
                var groupActions = groupActionEvents.LongLength;
                var restActions = groupActionEvents.LongCount(item => item.Type == SimulationEventType.Rest);
                return new AffiliationGroupStatistics(
                    name,
                    living.Length,
                    living.Length == 0 ? 0 : living.Average(item => (double)item.AgeDays / _config.World.DaysPerYear),
                    dead.Length == 0 ? 0 : dead.Average(item => (double)item.DeathAgeDays!.Value / _config.World.DaysPerYear),
                    living.Length == 0 ? 0 : living.Average(item => item.CurrentHp),
                    dead.LongCount(item => item.DeathCause?.StartsWith("combat:", StringComparison.Ordinal) == true),
                    dead.LongCount(item => item.DeathCause == "vitality"),
                    restActions,
                    groupActions == 0 ? 0 : (double)restActions / groupActions,
                    _events.LongCount(item => item.Type == SimulationEventType.ReproductionAttempt &&
                                              item.ActorSettlementId.HasValue == affiliated),
                    _events.LongCount(item => item.Type == SimulationEventType.ReproductionSuccess &&
                                              item.ActorSettlementId.HasValue == affiliated),
                    _events.LongCount(item => item.Type == SimulationEventType.Birth &&
                                              item.TargetSettlementId.HasValue == affiliated),
                    _events.LongCount(item => item.Type == SimulationEventType.ConceptMarkAcquired &&
                                              item.ActorSettlementId.HasValue == affiliated));
            }

            static PhaseWindowStatistics AggregateWindow(
                string name,
                IEnumerable<DailyPopulationRecord> source)
            {
                var values = source.OrderBy(item => item.Tick).ToArray();
                return new PhaseWindowStatistics(
                    name,
                    values.Length,
                    values.Length == 0 ? 0 : values.Average(item => item.Population),
                    values.Sum(item => (long)item.Births),
                    values.Sum(item => (long)item.Deaths),
                    values.Sum(item => (long)item.CombatDeaths),
                    values.Sum(item => (long)item.CollisionAttacks),
                    values.Sum(item => (long)item.ReproductionAttempts),
                    values.Sum(item => (long)item.ReproductionSuccesses),
                    values.Length == 0 ? 0 : values.Average(item => item.AverageAgeYears),
                    values.Length == 0 ? 0 : values.Average(item => item.AffiliationRate));
            }
        }
    }

    private static bool IsActionSelectionEvent(SimulationEvent item) => item.Type switch
    {
        SimulationEventType.Idle or
        SimulationEventType.Rest or
        SimulationEventType.Communication or
        SimulationEventType.Attack or
        SimulationEventType.AttackSuppressed or
        SimulationEventType.ReproductionAttempt or
        SimulationEventType.MoveFailed or
        SimulationEventType.CollisionAttack or
        SimulationEventType.CollisionSuppressed => true,
        SimulationEventType.Move or SimulationEventType.Flee =>
            !item.Detail.StartsWith("second-step", StringComparison.Ordinal),
        _ => false
    };

    private static string DetailReason(string detail)
    {
        var separator = detail.IndexOf(';');
        return separator < 0 ? detail : detail[..separator];
    }

    private static double? ParseDamage(string detail)
    {
        const string prefix = "damage=";
        return detail.StartsWith(prefix, StringComparison.Ordinal) &&
               double.TryParse(detail[prefix.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
