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
            var eventsByType = _events
                .GroupBy(item => item.Type)
                .ToDictionary(item => item.Key, item => (IReadOnlyList<SimulationEvent>)item.ToArray());
            var actionSelectionEvents = eventsByType.Values
                .SelectMany(item => item)
                .Where(IsActionSelectionEvent)
                .ToArray();
            var deathCauses = Events(SimulationEventType.Death)
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
            var reproductionOutcomes = Events(SimulationEventType.ReproductionSuccess)
                .Concat(Events(SimulationEventType.ReproductionFailure))
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
                    var results = Events(type).Where(item =>
                        item.Detail == "miss" || item.Detail.StartsWith("damage=", StringComparison.Ordinal)).ToArray();
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
                        Events(SimulationEventType.ConceptMarkAcquired)
                            .LongCount(item => item.Detail == concept.ToString()),
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
                    var members = alive.Where(npc => npc.SettlementId == item.Id).ToArray();
                    var population = members.Length;
                    var corePopulation = members.Count(npc =>
                        npc.Position.ChebyshevDistance(item.Center) <= _config.Settlement.CoreRadius);
                    var influenceOnlyPopulation = members.Count(npc =>
                    {
                        var distance = npc.Position.ChebyshevDistance(item.Center);
                        return distance > _config.Settlement.CoreRadius &&
                               distance <= _config.Settlement.InfluenceRadius;
                    });
                    var movementBias = Events(SimulationEventType.MovementBiasApplied)
                        .Where(simulationEvent => simulationEvent.ActorSettlementId == item.Id)
                        .ToArray();
                    var averageResidents = item.SupportHistory.Count == 0
                        ? 0
                        : item.SupportHistory.Average(day => day.AffiliatedResidentsInInfluence);
                    var reproductionSuccesses = item.SupportHistory.Sum(day => day.ReproductionSuccessesInInfluence);
                    var socialActions = item.SupportHistory.Sum(day => day.SocialActionsByMembersInInfluence);
                    var memberDays = item.SupportHistory.Sum(day => day.MemberDaysInInfluence);
                    return new SettlementStatistics(
                        item.Id,
                        item.Center,
                        item.FormedTick,
                        item.FounderIds.Count,
                        item.IsActive(_state.Tick),
                        population,
                        corePopulation,
                        influenceOnlyPopulation,
                        population - corePopulation - influenceOnlyPopulation,
                        alive.Length == 0 ? 0 : (double)population / alive.Length,
                        item.CoreOccupancy,
                        item.CrowdingPressure,
                        item.CrowdingConsecutiveDays,
                        item.CrowdingInvasionArmed,
                        item.CrowdingRearmConsecutiveDays,
                        item.CrowdingRearmCount,
                        item.Support,
                        item.SupportPopulationComponent,
                        item.SupportReproductionComponent,
                        item.SupportSocialComponent,
                        item.LowSupportDays,
                        item.SupportHistory.Count,
                        Math.Max(item.FoundingResidentBaseline, _config.Settlement.SupportFoundingResidentFloor),
                        averageResidents,
                        reproductionSuccesses,
                        socialActions,
                        memberDays,
                        memberDays * _config.Settlement.SupportSocialActionsPerMemberDay,
                        movementBias.LongLength,
                        movementBias.LongCount(simulationEvent =>
                            DetailValue(simulationEvent.Detail, "strong") == "1"),
                        movementBias.LongCount(simulationEvent =>
                            DetailValue(simulationEvent.Detail, "strongRest") == "1"),
                        movementBias.LongCount(simulationEvent =>
                            DetailValue(simulationEvent.Detail, "strongHp") == "1"),
                        movementBias.LongCount(simulationEvent =>
                            DetailDouble(simulationEvent.Detail, "homeDelta") > 0),
                        movementBias.LongCount(simulationEvent =>
                            DetailValue(simulationEvent.Detail, "enteredCore") == "1"),
                        movementBias.LongCount(simulationEvent =>
                            DetailDouble(simulationEvent.Detail, "foreignDirection") < 0),
                        movementBias.LongCount(simulationEvent =>
                            DetailDouble(simulationEvent.Detail, "foreignDirection") > 0),
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
            var restEvents = Events(SimulationEventType.Rest);
            var selectedRestNeeds = restEvents
                .Select(item => DetailDouble(item.Detail, "selectedRestNeed"))
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
            var selectedRestPressures = restEvents
                .Select(item => DetailDouble(item.Detail, "restPressure"))
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
            var fatigueContributions = Events(SimulationEventType.FatigueApplied)
                .GroupBy(item => DetailValue(item.Detail, "cause") ?? "unknown", StringComparer.Ordinal)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(group => new FatigueContributionStatistics(
                    group.Key,
                    group.LongCount(),
                    group.Sum(item => DetailDouble(item.Detail, "requested") ?? 0),
                    group.Sum(item => DetailDouble(item.Detail, "applied") ?? 0)))
                .ToArray();
            var restDiagnostics = new RestDiagnosticsStatistics(
                restEvents.Count,
                actionSelectionEvents.Length == 0 ? 0 : (double)restEvents.Count / actionSelectionEvents.Length,
                alive.Length == 0 ? 0 : alive.Average(item => item.Needs.Rest),
                selectedRestNeeds.Length == 0 ? 0 : selectedRestNeeds.Average(),
                selectedRestPressures.Length == 0 ? 0 : selectedRestPressures.Average(),
                restEvents.LongCount(item => DetailValue(item.Detail, "invasion") is { } value && value != "-"),
                restEvents.LongCount(item => DetailValue(item.Detail, "invasionRole") == InvasionRole.Attacker.ToString()),
                restEvents.LongCount(item => DetailValue(item.Detail, "invasionRole") == InvasionRole.Defender.ToString()),
                Events(SimulationEventType.InvasionParticipantWithdrew)
                    .LongCount(item => item.Detail.Contains("reason=rest", StringComparison.Ordinal)),
                fatigueContributions);
            var violence = new ViolenceStatistics(
                Events(SimulationEventType.CollisionAttack).LongCount() +
                Events(SimulationEventType.CollisionSuppressed).LongCount(),
                Events(SimulationEventType.CollisionAttack).LongCount(),
                Events(SimulationEventType.CollisionSuppressed)
                    .LongCount(item => item.Detail.Contains("same-settlement", StringComparison.Ordinal)),
                Events(SimulationEventType.CollisionSuppressed)
                    .LongCount(item => item.Detail.Contains("unaffiliated-protected", StringComparison.Ordinal)),
                Events(SimulationEventType.CollisionSuppressed)
                    .LongCount(item => item.Detail.Contains("other-settlement-friction", StringComparison.Ordinal)),
                Events(SimulationEventType.SettlementFrictionChanged)
                    .LongCount(item => !item.Detail.Contains("reason=decay", StringComparison.Ordinal)),
                Events(SimulationEventType.Attack).LongCount(),
                Events(SimulationEventType.Counterattack).LongCount(),
                Events(SimulationEventType.Pursuit).LongCount(),
                _state.AttackCandidateSuppressionCount,
                Events(SimulationEventType.AttackSuppressed).LongCount(),
                _state.UnaffiliatedThreatExceptionAttackCount);
            var reproductionScopes = new[] { "same-core", "outside-penalty", "unknown" }
                .Select(scope => new ReproductionScopeStatistics(
                    scope,
                    Events(SimulationEventType.ReproductionAttempt)
                        .LongCount(item => item.Detail.Contains($"scope={scope}", StringComparison.Ordinal)),
                    Events(SimulationEventType.ReproductionSuccess)
                        .LongCount(item => item.Detail.Contains($"scope={scope}", StringComparison.Ordinal)),
                    Events(SimulationEventType.ReproductionFailure)
                        .LongCount(item => item.Detail.Contains($"scope={scope}", StringComparison.Ordinal))))
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
                    var fleeing = Events(SimulationEventType.Flee)
                        .Where(simulationEvent => simulationEvent.Tick == _state.Tick - 1 &&
                            simulationEvent.ActorId.HasValue)
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
                Events(SimulationEventType.AuraApplied).LongCount(),
                Events(SimulationEventType.AuraExpired).LongCount(),
                _state.AuraSelfMarkSuppressionCount,
                Events(SimulationEventType.AuraApplied)
                    .LongCount(item => item.Detail.Contains("concept=Survival", StringComparison.Ordinal)),
                Events(SimulationEventType.AuraExpired)
                    .LongCount(item => item.Detail.Contains("concept=Survival", StringComparison.Ordinal)),
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
            var phaseEcology = _state.PopulationHistory
                .GroupBy(item => _state.OrderStartTick.HasValue && item.Tick >= _state.OrderStartTick.Value
                    ? WorldPhase.Order
                    : WorldPhase.Generation)
                .OrderBy(item => item.Key)
                .Select(group =>
                {
                    var values = group.ToArray();
                    return new PhaseEcologyStatistics(
                        group.Key,
                        values.Length,
                        values.Average(item => item.Population),
                        values.Average(item => item.AverageAgeYears),
                        values.Average(item => item.AverageHp),
                        values.Sum(item => (long)item.Births),
                        values.Sum(item => (long)item.ReproductionAttempts),
                        values.Sum(item => (long)item.ReproductionSuccesses),
                        values.Sum(item => (long)item.CombatDeaths),
                        values.Sum(item => (long)item.VitalityDeaths),
                        values.Sum(item => item.CollisionDamage),
                        values.Sum(item => item.ExplicitAttackDamage));
                })
                .ToArray();
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
                restDiagnostics,
                violence,
                reproductionScopes,
                invasionStatistics,
                auras,
                transitionWindows,
                phaseEcology,
                _state.SettlementCandidateCount,
                _state.SettlementCandidateConflictCount,
                _state.SettlementCandidateRejectionCount,
                _state.InvasionStartPreventedCount);

            TargetedActionStatistics TargetedStatistics(ActionKind action, SimulationEventType eventType)
            {
                var attempts = Events(eventType);
                var absent = action == ActionKind.Reproduction
                    ? Events(SimulationEventType.ReproductionFailure).LongCount(item => item.Detail == "target-absent")
                    : attempts.LongCount(item => item.Detail == "target-absent");
                return new TargetedActionStatistics(action, attempts.Count, absent);
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
                var groupActionEvents = actionSelectionEvents
                    .Where(item => item.ActorSettlementId.HasValue == affiliated).ToArray();
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
                    Events(SimulationEventType.ReproductionAttempt)
                        .LongCount(item => item.ActorSettlementId.HasValue == affiliated),
                    Events(SimulationEventType.ReproductionSuccess)
                        .LongCount(item => item.ActorSettlementId.HasValue == affiliated),
                    Events(SimulationEventType.Birth)
                        .LongCount(item => item.TargetSettlementId.HasValue == affiliated),
                    Events(SimulationEventType.ConceptMarkAcquired)
                        .LongCount(item => item.ActorSettlementId.HasValue == affiliated));
            }

            IReadOnlyList<SimulationEvent> Events(SimulationEventType type) =>
                eventsByType.TryGetValue(type, out var values) ? values : Array.Empty<SimulationEvent>();

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

    private static string? DetailValue(string detail, string key)
    {
        var prefix = key + "=";
        return detail.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    private static double? DetailDouble(string detail, string key) =>
        double.TryParse(DetailValue(detail, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
