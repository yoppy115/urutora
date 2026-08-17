using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Social;

public sealed class SettlementMaintenanceCoordinator
{
    private readonly SimulationConfig _config;
    private readonly SettlementFormationSystem _formation;
    private readonly InvasionSystem _invasion;

    public SettlementMaintenanceCoordinator(
        SimulationConfig config,
        RandomStreamFactory random,
        InvasionSystem invasion)
    {
        _config = config;
        _formation = new SettlementFormationSystem(config, random);
        _invasion = invasion;
    }

    public void ActivatePending(WorldState world, DomainEventEmitter emit)
    {
        if (world.PendingPhase == WorldPhase.Order && world.OrderStartTick == world.Tick)
        {
            world.Phase = WorldPhase.Order;
            world.PendingPhase = null;
            emit(0, SimulationEventType.WorldPhaseChanged, null, null, null, true,
                $"from={WorldPhase.Generation};to={WorldPhase.Order};effectiveTick={world.Tick}");
        }

        _invasion.ActivatePending(world, emit);
    }

    public void RunEndOfDay(
        WorldState world,
        IReadOnlyList<SimulationEvent> dayEvents,
        DomainEventEmitter emit)
    {
        RecordDailyPopulation(world, dayEvents, emit);
        ApplyAffinity(world, dayEvents, emit);
        ResolveMembership(world, emit);
        DecayFriction(world, emit);
        UpdateDemographics(world, emit);

        if ((world.Tick + 1) % _config.Settlement.EvaluationIntervalDays == 0)
        {
            emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=6;hotspot=snapshot");
            _formation.EvaluateAndForm(world, emit);
            emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=7;hotspot=committed");
        }

        EvaluateNaturalDissolution(world, emit);
        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=8;dissolution=evaluated");
        EvaluateOrderTransition(world, emit);
        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true,
            $"phase=9;order=evaluated;stabilityDays={world.StabilityConsecutiveDays}");
        UpdateCrowding(world, dayEvents, emit);
        var resting = dayEvents.Where(item => item.Type == SimulationEventType.Rest && item.Success && item.ActorId.HasValue)
            .Select(item => item.ActorId!.Value)
            .ToHashSet();
        _invasion.StartEligibleInvasions(world, resting, emit);
        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=11;invasion=evaluated");
        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true,
            $"phase=12;worldPhase={world.Phase};pendingPhase={world.PendingPhase?.ToString() ?? "-"};" +
            $"settlements={SettlementQueries.ActiveSettlements(world).Count};invasions=" +
            world.Invasions.Values.Count(item => item.EndTick is null));
    }

    private void RecordDailyPopulation(
        WorldState world,
        IReadOnlyList<SimulationEvent> dayEvents,
        DomainEventEmitter emit)
    {
        var population = world.Npcs.Values.Count(item => item.IsAlive);
        var births = dayEvents.Count(item => item.Type == SimulationEventType.Birth && item.Success);
        var deaths = dayEvents.Count(item => item.Type == SimulationEventType.Death && item.Success);
        var combatDeaths = dayEvents.Count(item => item.Type == SimulationEventType.Death &&
            item.Detail.StartsWith("combat:", StringComparison.Ordinal));
        var collisionAttacks = dayEvents.Count(item => item.Type == SimulationEventType.CollisionAttack);
        var reproductionAttempts = dayEvents.Count(item => item.Type == SimulationEventType.ReproductionAttempt);
        var reproductionSuccesses = dayEvents.Count(item => item.Type == SimulationEventType.ReproductionSuccess);
        var alive = world.Npcs.Values.Where(item => item.IsAlive).ToArray();
        var averageAgeYears = alive.Length == 0
            ? 0
            : alive.Average(item => (double)item.AgeDays / _config.World.DaysPerYear);
        var affiliated = alive.Count(item => SettlementQueries.ActiveSettlement(world, item.SettlementId) is not null);
        var affiliationRate = alive.Length == 0 ? 0 : (double)affiliated / alive.Length;
        world.PopulationHistory.Add(new DailyPopulationRecord(
            world.Tick,
            population,
            births,
            deaths,
            combatDeaths,
            collisionAttacks,
            reproductionAttempts,
            reproductionSuccesses,
            averageAgeYears,
            affiliationRate));
        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true,
            $"phase=1;population={population};births={births};deaths={deaths}");
    }

    private void ApplyAffinity(
        WorldState world,
        IReadOnlyList<SimulationEvent> dayEvents,
        DomainEventEmitter emit)
    {
        foreach (var settlement in SettlementQueries.ActiveSettlements(world))
        {
            foreach (var npc in world.Npcs.Values
                         .Where(item => item.IsAlive &&
                                        item.Position.ChebyshevDistance(settlement.Center) <= _config.Settlement.CoreRadius)
                         .OrderBy(item => item.Id))
            {
                AddAffinity(npc, settlement.Id, _config.Settlement.StayAffinityDaily, "stay", emit);
            }
        }

        foreach (var item in dayEvents.OrderBy(item => item.EventId, StringComparer.Ordinal))
        {
            var gain = item.Type switch
            {
                SimulationEventType.Rest when item.Success => _config.Settlement.RestAffinity,
                SimulationEventType.Communication when item.Success => _config.Settlement.CommunicationAffinity,
                SimulationEventType.ReproductionSuccess when item.Success => _config.Settlement.ReproductionSuccessAffinity,
                _ => 0
            };
            if (gain <= 0 || !item.ActorId.HasValue || !item.Position.HasValue)
            {
                continue;
            }

            var settlement = SettlementQueries.FindActiveCore(world, item.Position.Value, _config);
            if (settlement is null || !world.Npcs.TryGetValue(item.ActorId.Value, out var actor))
            {
                continue;
            }

            AddAffinity(actor, settlement.Id, gain, item.Type.ToString(), emit);
            if (item.Type == SimulationEventType.ReproductionSuccess && item.TargetId.HasValue &&
                world.Npcs.TryGetValue(item.TargetId.Value, out var target))
            {
                AddAffinity(target, settlement.Id, gain, item.Type.ToString(), emit);
            }
        }

        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=2;affinity=committed");
    }

    private static void AddAffinity(
        NpcState npc,
        int settlementId,
        double gain,
        string reason,
        DomainEventEmitter emit)
    {
        npc.SettlementAffinity[settlementId] = npc.SettlementAffinity.GetValueOrDefault(settlementId) + gain;
        emit(0, SimulationEventType.AffinityChanged, npc.Id, null, npc.Position, true,
            $"settlement={settlementId};reason={reason};delta={gain:R};current={npc.SettlementAffinity[settlementId]:R}");
    }

    private void ResolveMembership(WorldState world, DomainEventEmitter emit)
    {
        var activeIds = SettlementQueries.ActiveSettlements(world).Select(item => item.Id).ToHashSet();
        foreach (var npc in world.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id))
        {
            if (npc.InvasionId.HasValue)
            {
                continue;
            }

            if (npc.SettlementId.HasValue && !activeIds.Contains(npc.SettlementId.Value))
            {
                var old = npc.SettlementId;
                npc.SettlementId = null;
                emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from={old};to=unaffiliated;reason=inactive-settlement");
            }

            var eligible = npc.SettlementAffinity
                .Where(item => activeIds.Contains(item.Key) && item.Value >= _config.Settlement.MembershipThreshold)
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key)
                .ToArray();
            if (eligible.Length == 0)
            {
                continue;
            }

            var best = eligible[0];
            if (!npc.SettlementId.HasValue)
            {
                npc.SettlementId = best.Key;
                emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from=unaffiliated;to={best.Key};reason=threshold");
                continue;
            }

            if (best.Key == npc.SettlementId.Value)
            {
                continue;
            }

            var current = npc.SettlementAffinity.GetValueOrDefault(npc.SettlementId.Value);
            if (best.Value >= current + _config.Settlement.MembershipSwitchMargin)
            {
                var old = npc.SettlementId;
                npc.SettlementId = best.Key;
                emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from={old};to={best.Key};reason=switch-margin");
            }
        }

        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=3;membership=resolved");
    }

    private void DecayFriction(WorldState world, DomainEventEmitter emit)
    {
        foreach (var friction in world.Frictions.Values.OrderBy(item => item.Pair.FirstId).ThenBy(item => item.Pair.SecondId))
        {
            var elapsed = world.Tick - friction.LastFrictionEventTick;
            if (elapsed < _config.Settlement.FrictionDecayIntervalDays ||
                elapsed % _config.Settlement.FrictionDecayIntervalDays != 0 || friction.CurrentFriction <= 0)
            {
                continue;
            }

            var previous = friction.CurrentFriction;
            friction.CurrentFriction = Math.Max(0, previous - _config.Settlement.FrictionDecayAmount);
            var decay = previous - friction.CurrentFriction;
            friction.LifetimeDecay += decay;
            emit(0, SimulationEventType.SettlementFrictionChanged, null, null, null, true,
                $"pair={friction.Pair.FirstId},{friction.Pair.SecondId};reason=decay;delta={-decay:R};" +
                $"current={friction.CurrentFriction:R}");
        }

        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=4;friction=decayed");
    }

    private void UpdateDemographics(WorldState world, DomainEventEmitter emit)
    {
        var oldestReproductionTick = world.Tick - _config.Settlement.HotspotWindowDays + 1;
        world.ReproductionSuccesses.RemoveAll(item => item.Tick < oldestReproductionTick);
        var window = world.PopulationHistory.TakeLast(_config.Settlement.StabilityWindowDays).ToArray();
        if (window.Length == 0)
        {
            world.PopulationCv = 0;
            world.DemographicImbalance = 0;
        }
        else
        {
            var mean = window.Average(item => item.Population);
            var variance = window.Average(item => Math.Pow(item.Population - mean, 2));
            world.PopulationCv = mean <= 0 ? double.PositiveInfinity : Math.Sqrt(variance) / mean;
            var births = window.Sum(item => item.Births);
            var deaths = window.Sum(item => item.Deaths);
            world.DemographicImbalance = (double)Math.Abs(births - deaths) / Math.Max(births + deaths, 1);
        }

        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true,
            $"phase=5;populationCv={world.PopulationCv:R};demographicImbalance={world.DemographicImbalance:R};" +
            $"window={window.Length}");
    }

    private void EvaluateOrderTransition(WorldState world, DomainEventEmitter emit)
    {
        if (world.Phase == WorldPhase.Order || world.PendingPhase == WorldPhase.Order)
        {
            return;
        }

        var fullWindow = world.PopulationHistory.Count >= _config.Settlement.StabilityWindowDays;
        var stable = fullWindow && world.PopulationCv <= _config.Settlement.PopulationCvMaximum &&
                     world.DemographicImbalance <= _config.Settlement.DemographicImbalanceMaximum;
        world.StabilityConsecutiveDays = stable ? world.StabilityConsecutiveDays + 1 : 0;
        if (world.StabilityConsecutiveDays >= _config.Settlement.StabilityConsecutiveDays)
        {
            world.PendingPhase = WorldPhase.Order;
            world.OrderStartTick = world.Tick + 1;
            emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true,
                $"phase=9;transition=pending;effectiveTick={world.OrderStartTick}");
        }
    }

    private void EvaluateNaturalDissolution(WorldState world, DomainEventEmitter emit)
    {
        var population = Math.Max(1, world.Npcs.Values.Count(item => item.IsAlive));
        foreach (var settlement in SettlementQueries.ActiveSettlements(world).ToArray())
        {
            var members = world.Npcs.Values.Count(item => item.IsAlive && item.SettlementId == settlement.Id);
            var ratio = (double)members / population;
            settlement.LowPopulationConsecutiveDays = ratio <= _config.Settlement.NaturalDissolutionPopulationRatio
                ? settlement.LowPopulationConsecutiveDays + 1
                : 0;
            if (settlement.LowPopulationConsecutiveDays < _config.Settlement.NaturalDissolutionConsecutiveDays)
            {
                continue;
            }

            settlement.DissolvedTick = world.Tick;
            settlement.DissolutionReason = "low-population";
            foreach (var npc in world.Npcs.Values.Where(item => item.SettlementId == settlement.Id).OrderBy(item => item.Id))
            {
                npc.SettlementId = null;
                npc.InvasionId = null;
                npc.InvasionRole = null;
                npc.HasAdvanceBias = false;
                npc.HasDefenseBias = false;
                emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from={settlement.Id};to=unaffiliated;reason=dissolution");
            }

            emit(0, SimulationEventType.SettlementDissolved, null, null, settlement.Center, true,
                $"settlement={settlement.Id};reason=low-population;days={settlement.LowPopulationConsecutiveDays}");
        }
    }

    private void UpdateCrowding(
        WorldState world,
        IReadOnlyList<SimulationEvent> dayEvents,
        DomainEventEmitter emit)
    {
        foreach (var settlement in SettlementQueries.ActiveSettlements(world))
        {
            var usable = SettlementQueries.UsableCoreCells(world, settlement, _config);
            var occupied = world.Npcs.Values
                .Where(item => item.IsAlive && usable.Contains(item.Position))
                .Select(item => item.Position)
                .Distinct()
                .Count();
            var memberIds = world.Npcs.Values.Where(item => item.IsAlive && item.SettlementId == settlement.Id)
                .Select(item => item.Id)
                .ToHashSet();
            var moveEvents = dayEvents.Where(item => item.ActorId.HasValue && memberIds.Contains(item.ActorId.Value) &&
                item.Type is SimulationEventType.Move or SimulationEventType.Flee or SimulationEventType.MoveFailed or
                    SimulationEventType.CollisionAttack or SimulationEventType.CollisionSuppressed).ToArray();
            var blocked = moveEvents.Count(item => item.Type is SimulationEventType.MoveFailed or
                SimulationEventType.CollisionAttack or SimulationEventType.CollisionSuppressed);
            settlement.CoreOccupancy = usable.Count == 0 ? 0 : (double)occupied / usable.Count;
            settlement.BlockedMovementRate = moveEvents.Length == 0 ? 0 : (double)blocked / moveEvents.Length;
            settlement.CrowdingPressure = Math.Clamp(
                _config.Settlement.CrowdingOccupancyWeight * settlement.CoreOccupancy +
                _config.Settlement.CrowdingBlockedMovementWeight * settlement.BlockedMovementRate, 0, 1);
            settlement.CrowdingHistory.Add(settlement.CrowdingPressure);
            if (settlement.CrowdingHistory.Count > _config.Settlement.CrowdingWindowDays)
            {
                settlement.CrowdingHistory.RemoveRange(
                    0, settlement.CrowdingHistory.Count - _config.Settlement.CrowdingWindowDays);
            }

            var rollingEligible = settlement.CrowdingHistory.Count == _config.Settlement.CrowdingWindowDays &&
                                  settlement.CrowdingHistory.Average() >= _config.Settlement.CrowdingThreshold;
            settlement.CrowdingConsecutiveDays = rollingEligible ? settlement.CrowdingConsecutiveDays + 1 : 0;
        }

        emit(0, SimulationEventType.SettlementMaintenance, null, null, null, true, "phase=10;crowding=updated");
    }
}
