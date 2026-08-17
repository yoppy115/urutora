using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Social;

public sealed class InvasionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public InvasionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public void ActivatePending(WorldState world, DomainEventEmitter emit)
    {
        foreach (var invasion in world.Invasions.Values
                     .Where(item => item.EffectiveTick == world.Tick && item.EndTick is null)
                     .OrderBy(item => item.Id))
        {
            if (SettlementQueries.ActiveSettlement(world, invasion.AttackSettlementId) is null ||
                SettlementQueries.ActiveSettlement(world, invasion.DefenseSettlementId) is null)
            {
                End(world, invasion, InvasionOutcome.DefenseVictory, emit, 0, "settlement-inactive-before-start");
                continue;
            }

            foreach (var participantId in invasion.AttackParticipantIds.OrderBy(item => item))
            {
                if (!world.Npcs.TryGetValue(participantId, out var npc) || !npc.IsAlive ||
                    npc.SettlementId != invasion.AttackSettlementId || npc.WithdrawnInvasionIds.Contains(invasion.Id))
                {
                    continue;
                }

                npc.InvasionId = invasion.Id;
                npc.InvasionRole = InvasionRole.Attacker;
                npc.HasAdvanceBias = true;
                emit(0, SimulationEventType.InvasionParticipantJoined, npc.Id, null, npc.Position, true,
                    $"invasion={invasion.Id};role=attacker");
            }

            var defense = world.Npcs.Values
                .Where(item => item.IsAlive && item.SettlementId == invasion.DefenseSettlementId &&
                               SettlementQueries.IsInsideInfluence(
                                   world, invasion.DefenseSettlementId, item.Position, _config))
                .OrderBy(item => item.Id)
                .ToArray();
            foreach (var npc in defense)
            {
                npc.InvasionId = invasion.Id;
                npc.InvasionRole = InvasionRole.Defender;
                npc.HasDefenseBias = true;
                invasion.DefenseParticipantIds.Add(npc.Id);
                emit(0, SimulationEventType.InvasionParticipantJoined, npc.Id, null, npc.Position, true,
                    $"invasion={invasion.Id};role=defender");
            }

            emit(0, SimulationEventType.InvasionStarted, null, null, null, true,
                $"invasion={invasion.Id};attack={invasion.AttackSettlementId};defense={invasion.DefenseSettlementId};" +
                $"force={invasion.AttackParticipantIds.Count};reason={invasion.TargetReason};" +
                $"crowding={invasion.TriggerCrowdingPressure:R}");
        }
    }

    public void StartEligibleInvasions(WorldState world, IReadOnlySet<long> restingNpcIds, DomainEventEmitter emit)
    {
        foreach (var source in SettlementQueries.ActiveSettlements(world)
                     .Where(item => item.CrowdingConsecutiveDays >= _config.Settlement.CrowdingConsecutiveDays)
                     .OrderBy(item => item.Id))
        {
            if (IsSettlementEngaged(world, source.Id))
            {
                continue;
            }

            var target = SelectTarget(world, source);
            if (target is null || IsSettlementEngaged(world, target.Value.Settlement.Id))
            {
                continue;
            }

            var population = world.Npcs.Values.Count(item => item.IsAlive && item.SettlementId == source.Id);
            var rate = Math.Clamp(
                _config.Invasion.MobilizationBase + _config.Invasion.MobilizationCrowdingFactor * source.CrowdingPressure,
                _config.Invasion.MobilizationMinimum,
                _config.Invasion.MobilizationMaximum);
            var forceSize = (int)Math.Round(population * rate, MidpointRounding.AwayFromZero);
            if (forceSize <= 0)
            {
                continue;
            }

            var candidates = world.Npcs.Values
                .Where(item => item.IsAlive && item.SettlementId == source.Id && !restingNpcIds.Contains(item.Id) &&
                               !item.InvasionId.HasValue)
                .OrderBy(item => item.Id)
                .ToArray();
            var desiredCore = (int)Math.Round(forceSize * _config.Invasion.CoreCohortRatio,
                MidpointRounding.AwayFromZero);
            var coreCandidates = candidates
                .Where(item => item.Position.ChebyshevDistance(source.Center) <= _config.Settlement.CoreRadius)
                .OrderByDescending(item => item.SettlementAffinity.GetValueOrDefault(source.Id))
                .ThenBy(item => _random.StablePriority(
                    "invasion", world.Tick, source.Id, "core-cohort", item.Id.ToString()))
                .ThenBy(item => item.Id)
                .ToList();
            var frontierCandidates = candidates
                .Where(item => item.Position.ChebyshevDistance(source.Center) > _config.Settlement.CoreRadius)
                .OrderBy(item => _random.StablePriority(
                    "invasion", world.Tick, source.Id, "frontier-cohort", item.Id.ToString()))
                .ThenBy(item => item.Id)
                .ToList();
            var core = coreCandidates.Take(Math.Min(desiredCore, forceSize)).ToList();
            var frontier = frontierCandidates.Take(forceSize - core.Count).ToList();
            if (core.Count + frontier.Count < forceSize)
            {
                core.AddRange(coreCandidates.Skip(core.Count).Take(forceSize - core.Count - frontier.Count));
            }
            if (core.Count + frontier.Count < forceSize)
            {
                frontier.AddRange(frontierCandidates.Skip(frontier.Count)
                    .Take(forceSize - core.Count - frontier.Count));
            }

            var participants = core.Concat(frontier).Select(item => item.Id).Distinct().OrderBy(item => item).ToArray();
            if (participants.Length == 0)
            {
                continue;
            }

            var invasion = new InvasionState
            {
                Id = world.NextInvasionId++,
                AttackSettlementId = source.Id,
                DefenseSettlementId = target.Value.Settlement.Id,
                CreatedTick = world.Tick,
                EffectiveTick = world.Tick + 1,
                TriggerCrowdingPressure = source.CrowdingPressure,
                TargetReason = target.Value.Reason,
                AttackParticipantIds = participants,
                CoreCohortIds = core.Select(item => item.Id).OrderBy(item => item).ToArray(),
                FrontierCohortIds = frontier.Select(item => item.Id).OrderBy(item => item).ToArray()
            };
            world.Invasions.Add(invasion.Id, invasion);
            emit(0, SimulationEventType.SettlementMaintenance, null, null, source.Center, true,
                $"phase=11;invasion={invasion.Id};status=pending;effectiveTick={invasion.EffectiveTick}");
        }
    }

    public Position? MovementTarget(WorldState world, NpcState npc)
    {
        if (!npc.InvasionId.HasValue || !world.Invasions.TryGetValue(npc.InvasionId.Value, out var invasion) ||
            !invasion.IsActive(world.Tick))
        {
            return null;
        }

        if (npc.HasAdvanceBias && world.Settlements.TryGetValue(invasion.DefenseSettlementId, out var defense))
        {
            return defense.Center;
        }

        if (npc.HasDefenseBias && world.Settlements.TryGetValue(invasion.DefenseSettlementId, out var defended) &&
            world.Settlements.TryGetValue(invasion.AttackSettlementId, out var attacker))
        {
            return new Position(
                defended.Center.X + Math.Sign(attacker.Center.X - defended.Center.X),
                defended.Center.Y + Math.Sign(attacker.Center.Y - defended.Center.Y));
        }

        return null;
    }

    public void WithdrawForRest(WorldState world, NpcState npc, DomainEventEmitter emit, int microRound)
    {
        if (!npc.InvasionId.HasValue || !world.Invasions.TryGetValue(npc.InvasionId.Value, out var invasion) ||
            !invasion.IsActive(world.Tick))
        {
            return;
        }

        var role = npc.InvasionRole;
        npc.WithdrawnInvasionIds.Add(invasion.Id);
        npc.InvasionId = null;
        npc.InvasionRole = null;
        npc.HasAdvanceBias = false;
        npc.HasDefenseBias = false;
        if (role == InvasionRole.Attacker)
        {
            invasion.RestWithdrawals++;
        }

        emit(microRound, SimulationEventType.InvasionParticipantWithdrew, npc.Id, null, npc.Position, true,
            $"invasion={invasion.Id};role={role};reason=rest");
    }

    public void NotifyDeath(WorldState world, NpcState npc, DomainEventEmitter emit, int microRound)
    {
        if (!npc.InvasionId.HasValue || !world.Invasions.TryGetValue(npc.InvasionId.Value, out var invasion))
        {
            return;
        }

        invasion.DeathWithdrawals++;
        var role = npc.InvasionRole;
        npc.InvasionId = null;
        npc.InvasionRole = null;
        npc.HasAdvanceBias = false;
        npc.HasDefenseBias = false;
        emit(microRound, SimulationEventType.InvasionParticipantWithdrew, npc.Id, null, npc.Position, true,
            $"invasion={invasion.Id};role={role};reason=death");
    }

    public void ResolveVictories(WorldState world, DomainEventEmitter emit, int microRound)
    {
        foreach (var invasion in world.Invasions.Values.Where(item => item.IsActive(world.Tick)).OrderBy(item => item.Id).ToArray())
        {
            var aliveAdvance = world.Npcs.Values.Count(item => item.IsAlive && item.InvasionId == invasion.Id &&
                item.InvasionRole == InvasionRole.Attacker && item.HasAdvanceBias);
            if (aliveAdvance == 0)
            {
                End(world, invasion, InvasionOutcome.DefenseVictory, emit, microRound, "no-alive-advance-participants");
                continue;
            }

            if (!world.Settlements.TryGetValue(invasion.DefenseSettlementId, out var defense))
            {
                End(world, invasion, InvasionOutcome.AttackVictory, emit, microRound, "defense-settlement-missing");
                continue;
            }

            var usable = SettlementQueries.UsableCoreCells(world, defense, _config);
            var attackerCells = world.Npcs.Values
                .Where(item => item.IsAlive && item.SettlementId == invasion.AttackSettlementId && usable.Contains(item.Position))
                .Select(item => item.Position)
                .Distinct()
                .Count();
            var rate = usable.Count == 0 ? 0 : (double)attackerCells / usable.Count;
            var centerOccupied = world.Npcs.Values.Any(item => item.IsAlive &&
                item.SettlementId == invasion.AttackSettlementId && item.Position == defense.Center);
            invasion.MaximumCoreOccupationRate = Math.Max(invasion.MaximumCoreOccupationRate, rate);
            invasion.CenterOccupied |= centerOccupied;
            if (centerOccupied || rate >= _config.Invasion.AttackOccupationThreshold)
            {
                End(world, invasion, InvasionOutcome.AttackVictory, emit, microRound,
                    centerOccupied ? "center-occupied" : "core-occupation");
            }
        }
    }

    private (SettlementState Settlement, string Reason)? SelectTarget(WorldState world, SettlementState source)
    {
        var candidates = SettlementQueries.ActiveSettlements(world).Where(item => item.Id != source.Id)
            .Select(item =>
            {
                var hostile = world.Hostilities.Contains(new HostilityEdge(source.Id, item.Id));
                var pair = SettlementPair.Create(source.Id, item.Id);
                var friction = world.Frictions.TryGetValue(pair, out var state) ? state.CurrentFriction : 0;
                var distance = source.Center.ChebyshevDistance(item.Center);
                return new
                {
                    Settlement = item,
                    Hostile = hostile,
                    Friction = friction,
                    Distance = distance,
                    Priority = _random.StablePriority(
                        "invasion", world.Tick, source.Id, "target-tie", item.Id.ToString())
                };
            })
            .OrderByDescending(item => item.Hostile)
            .ThenByDescending(item => item.Friction)
            .ThenBy(item => item.Distance)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Settlement.Id)
            .FirstOrDefault();
        if (candidates is null)
        {
            return null;
        }

        var reason = candidates.Hostile
            ? "hostility"
            : candidates.Friction > 0 ? "friction" : "distance";
        return (candidates.Settlement, reason);
    }

    private static bool IsSettlementEngaged(WorldState world, int settlementId) => world.Invasions.Values.Any(item =>
        item.EndTick is null && (item.AttackSettlementId == settlementId || item.DefenseSettlementId == settlementId));

    private static void End(
        WorldState world,
        InvasionState invasion,
        InvasionOutcome outcome,
        DomainEventEmitter emit,
        int microRound,
        string reason)
    {
        if (invasion.EndTick.HasValue)
        {
            return;
        }

        invasion.EndTick = world.Tick;
        invasion.Outcome = outcome;
        foreach (var npc in world.Npcs.Values.Where(item => item.InvasionId == invasion.Id).OrderBy(item => item.Id))
        {
            npc.InvasionId = null;
            npc.InvasionRole = null;
            npc.HasAdvanceBias = false;
            npc.HasDefenseBias = false;
        }

        if (outcome == InvasionOutcome.AttackVictory &&
            world.Settlements.TryGetValue(invasion.DefenseSettlementId, out var defeated))
        {
            defeated.DissolvedTick = world.Tick;
            defeated.DissolutionReason = "integrated";
            defeated.IntegratedIntoSettlementId = invasion.AttackSettlementId;
            foreach (var npc in world.Npcs.Values.Where(item => item.SettlementId == defeated.Id).OrderBy(item => item.Id))
            {
                var previous = npc.SettlementId;
                npc.SettlementId = invasion.AttackSettlementId;
                emit(microRound, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from={previous};to={invasion.AttackSettlementId};reason=conquest");
            }

            emit(microRound, SimulationEventType.SettlementIntegrated, null, null, defeated.Center, true,
                $"from={defeated.Id};to={invasion.AttackSettlementId};invasion={invasion.Id}");
        }

        emit(microRound, SimulationEventType.InvasionEnded, null, null, null, true,
            $"invasion={invasion.Id};outcome={outcome};reason={reason};" +
            $"occupation={invasion.MaximumCoreOccupationRate:R};center={invasion.CenterOccupied}");
    }
}
