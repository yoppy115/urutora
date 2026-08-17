using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Social;

public sealed record SettlementCandidate(
    string CandidateId,
    Position WindowOrigin,
    int ReproductionSuccessCount,
    IReadOnlyList<string> ReproductionSuccessEventIds,
    IReadOnlyList<Position> CenterCandidates,
    IReadOnlyList<long> FounderCandidates);

public sealed record SettlementFormationResult(
    int CandidateCount,
    int ConflictCount,
    int RejectedCount,
    IReadOnlyList<int> FormedSettlementIds);

public sealed class SettlementFormationSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public SettlementFormationSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public IReadOnlyList<SettlementCandidate> CreateCandidateSnapshot(WorldState world)
    {
        var startTick = world.Tick - _config.Settlement.HotspotWindowDays + 1;
        var successes = world.ReproductionSuccesses
            .Where(item => item.Tick >= startTick && item.Tick <= world.Tick)
            .OrderBy(item => item.EventId, StringComparer.Ordinal)
            .ToArray();
        var landmarkCells = world.Landmarks.Select(item => item.Position).ToHashSet();
        var size = _config.Settlement.HotspotWindowSize;
        var candidates = new List<SettlementCandidate>();

        for (var y = 0; y <= _config.World.Height - size; y++)
        {
            for (var x = 0; x <= _config.World.Width - size; x++)
            {
                var origin = new Position(x, y);
                var included = successes.Where(item =>
                        item.Position.X >= x && item.Position.X < x + size &&
                        item.Position.Y >= y && item.Position.Y < y + size)
                    .ToArray();
                if (included.Length < _config.Settlement.HotspotSuccessThreshold)
                {
                    continue;
                }

                var cells = new List<Position>();
                for (var cellY = y; cellY < y + size; cellY++)
                {
                    for (var cellX = x; cellX < x + size; cellX++)
                    {
                        var cell = new Position(cellX, cellY);
                        if (!landmarkCells.Contains(cell))
                        {
                            cells.Add(cell);
                        }
                    }
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                var eventIds = included.Select(item => item.EventId)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray();
                var founders = included.SelectMany(item => new[] { item.ParentAId, item.ParentBId })
                    .Distinct()
                    .OrderBy(item => item)
                    .ToArray();
                candidates.Add(new SettlementCandidate(
                    StableHash.StableId("settlement-candidate", world.Tick, x, y, string.Join(',', eventIds)),
                    origin,
                    included.Length,
                    eventIds,
                    cells.OrderBy(item => item).ToArray(),
                    founders));
            }
        }

        return candidates.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
    }

    public SettlementFormationResult EvaluateAndForm(WorldState world, DomainEventEmitter emit)
    {
        var candidates = CreateCandidateSnapshot(world);
        world.SettlementCandidateCount += candidates.Count;
        var ranked = candidates
            .OrderByDescending(item => item.ReproductionSuccessCount)
            .ThenBy(item => _random.StablePriority(
                "settlement", world.Tick, 0, "hotspot-tie", item.CandidateId))
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var acceptedCenters = new List<Position>();
        var formed = new List<int>();
        var conflicts = 0;
        var rejected = 0;

        foreach (var candidate in ranked)
        {
            emit(0, SimulationEventType.SettlementCandidateEvaluated, null, null, candidate.WindowOrigin, true,
                $"candidate={candidate.CandidateId};successes={candidate.ReproductionSuccessCount};events=" +
                string.Join(',', candidate.ReproductionSuccessEventIds));
            var centerStream = _random.Create(
                "settlement", world.Tick, 0, "hotspot-center", candidate.CandidateId);
            var center = candidate.CenterCandidates[centerStream.NextInt(candidate.CenterCandidates.Count)];
            var conflictsWithExisting = world.Settlements.Values
                .Where(item => item.DissolvedTick is null)
                .Any(item => item.Center.ChebyshevDistance(center) <= _config.Settlement.MinimumCenterDistance);
            var conflictsWithAccepted = acceptedCenters.Any(item =>
                item.ChebyshevDistance(center) <= _config.Settlement.MinimumCenterDistance);
            if (conflictsWithExisting || conflictsWithAccepted)
            {
                conflicts++;
                rejected++;
                world.SettlementCandidateConflictCount++;
                world.SettlementCandidateRejectionCount++;
                emit(0, SimulationEventType.SettlementCandidateRejected, null, null, center, false,
                    $"candidate={candidate.CandidateId};reason=spacing;successes={candidate.ReproductionSuccessCount}");
                continue;
            }

            var settlement = FormSettlement(world, candidate, center, emit);
            acceptedCenters.Add(center);
            formed.Add(settlement.Id);
        }

        return new SettlementFormationResult(candidates.Count, conflicts, rejected, formed);
    }

    private SettlementState FormSettlement(
        WorldState world,
        SettlementCandidate candidate,
        Position center,
        DomainEventEmitter emit)
    {
        var founders = candidate.FounderCandidates
            .Where(id => world.Npcs.TryGetValue(id, out var npc) && npc.IsAlive)
            .OrderBy(id => id)
            .ToArray();
        var settlement = new SettlementState
        {
            Id = world.NextSettlementId++,
            Center = center,
            FormedTick = world.Tick,
            EffectiveTick = world.Tick + 1,
            FounderIds = founders
        };
        world.Settlements.Add(settlement.Id, settlement);

        var founderSet = founders.ToHashSet();
        var coreResidents = world.Npcs.Values
            .Where(item => item.IsAlive && item.Position.ChebyshevDistance(center) <= _config.Settlement.CoreRadius)
            .OrderBy(item => item.Id)
            .ToArray();
        foreach (var npc in coreResidents)
        {
            var gain = founderSet.Contains(npc.Id)
                ? _config.Settlement.FounderAffinity
                : _config.Settlement.CoreResidentAffinity;
            npc.SettlementAffinity[settlement.Id] = npc.SettlementAffinity.GetValueOrDefault(settlement.Id) + gain;
            emit(0, SimulationEventType.AffinityChanged, npc.Id, null, npc.Position, true,
                $"settlement={settlement.Id};reason=formation;delta={gain:R};current={npc.SettlementAffinity[settlement.Id]:R}");
        }

        foreach (var npc in coreResidents.Where(item =>
                     item.SettlementAffinity.GetValueOrDefault(settlement.Id) >= _config.Settlement.MembershipThreshold))
        {
            if (npc.InvasionId.HasValue)
            {
                continue;
            }

            var currentAffinity = npc.SettlementId.HasValue
                ? npc.SettlementAffinity.GetValueOrDefault(npc.SettlementId.Value)
                : double.NegativeInfinity;
            if (!npc.SettlementId.HasValue ||
                npc.SettlementAffinity[settlement.Id] >= currentAffinity + _config.Settlement.MembershipSwitchMargin)
            {
                var previous = npc.SettlementId;
                npc.SettlementId = settlement.Id;
                emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                    $"from={previous?.ToString() ?? "unaffiliated"};to={settlement.Id};reason=formation");
            }
        }

        EstablishInitialHostility(world, settlement, coreResidents, emit);
        emit(0, SimulationEventType.SettlementFormed, null, null, center, true,
            $"settlement={settlement.Id};effectiveTick={settlement.EffectiveTick};founders={string.Join(',', founders)};" +
            $"candidate={candidate.CandidateId};successes={candidate.ReproductionSuccessCount}");
        return settlement;
    }

    private void EstablishInitialHostility(
        WorldState world,
        SettlementState settlement,
        IReadOnlyList<NpcState> formationResidents,
        DomainEventEmitter emit)
    {
        var cohort = formationResidents
            .Where(item => settlement.FounderIds.Contains(item.Id) || item.SettlementId == settlement.Id)
            .OrderBy(item => item.Id)
            .ToArray();
        if (cohort.Length == 0)
        {
            return;
        }

        foreach (var other in SettlementQueries.ActiveSettlements(world).Where(item => item.Id != settlement.Id))
        {
            var otherMembers = world.Npcs.Values
                .Where(item => item.IsAlive && item.SettlementId == other.Id)
                .Select(item => item.Id)
                .ToHashSet();
            if (otherMembers.Count == 0)
            {
                continue;
            }

            var threatened = cohort.Count(npc => npc.ThreatMemory.Any(memory =>
                otherMembers.Contains(memory.Key) &&
                world.Tick - memory.Value.LastThreatTick <= _config.Observation.ThreatMemoryDays));
            var ratio = (double)threatened / cohort.Length;
            var edge = new HostilityEdge(settlement.Id, other.Id);
            if (ratio >= _config.Settlement.InitialHostilityThreshold && world.Hostilities.Add(edge))
            {
                emit(0, SimulationEventType.InitialHostilityEstablished, null, null, settlement.Center, true,
                    $"source={settlement.Id};target={other.Id};ratio={ratio:R};cohort={cohort.Length}");
            }
        }
    }
}
