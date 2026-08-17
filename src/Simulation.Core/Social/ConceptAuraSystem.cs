using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Social;

public sealed class ConceptAuraSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public ConceptAuraSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public void Refresh(WorldState world, DomainEventEmitter emit, int microRound)
    {
        var alive = world.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id).ToArray();
        var holderIndex = BuildHolderIndex(world, alive);
        foreach (var npc in alive)
        {
            var desired = new HashSet<ConceptKind>();
            if (SettlementQueries.ActiveSettlement(world, npc.SettlementId) is not null)
            {
                foreach (var concept in Enum.GetValues<ConceptKind>())
                {
                    var holderExists = HasOtherHolder(holderIndex, npc, concept);
                    if (!holderExists)
                    {
                        continue;
                    }

                    if (npc.ConceptMarks.Contains(concept))
                    {
                        world.AuraSelfMarkSuppressionCount++;
                    }
                    else
                    {
                        desired.Add(concept);
                    }
                }
            }

            var previous = npc.ActiveAuras.ToHashSet();
            foreach (var concept in previous.Except(desired).OrderBy(item => item))
            {
                npc.ActiveAuras.Remove(concept);
                emit(microRound, SimulationEventType.AuraExpired, npc.Id, null, npc.Position, true,
                    $"concept={concept};settlement={npc.SettlementId?.ToString() ?? "-"}");
            }

            foreach (var concept in desired.Except(previous).OrderBy(item => item))
            {
                npc.ActiveAuras.Add(concept);
                emit(microRound, SimulationEventType.AuraApplied, npc.Id, null, npc.Position, true,
                    $"concept={concept};settlement={npc.SettlementId?.ToString() ?? "-"}");
            }

            if (previous.Contains(ConceptKind.Survival) && !npc.ActiveAuras.Contains(ConceptKind.Survival))
            {
                var maximum = npc.EffectiveStats(_config).MaxHp;
                if (npc.CurrentHp > maximum)
                {
                    var previousHp = npc.CurrentHp;
                    npc.CurrentHp = maximum;
                    emit(microRound, SimulationEventType.TemporaryMaxHpNormalized, npc.Id, null, npc.Position, true,
                        $"from={previousHp:R};to={maximum:R};reason=survival-aura-expired");
                }
            }
        }

        foreach (var dead in world.Npcs.Values.Where(item => !item.IsAlive && item.ActiveAuras.Count > 0))
        {
            dead.ActiveAuras.Clear();
        }
    }

    private static Dictionary<(int SettlementId, ConceptKind Concept, Position Position), List<long>> BuildHolderIndex(
        WorldState world,
        IReadOnlyList<NpcState> alive)
    {
        var result = new Dictionary<(int SettlementId, ConceptKind Concept, Position Position), List<long>>();
        foreach (var holder in alive)
        {
            if (SettlementQueries.ActiveSettlement(world, holder.SettlementId) is null)
            {
                continue;
            }

            foreach (var concept in holder.ConceptMarks.OrderBy(item => item))
            {
                var key = (holder.SettlementId!.Value, concept, holder.Position);
                if (!result.TryGetValue(key, out var ids))
                {
                    ids = new List<long>();
                    result.Add(key, ids);
                }

                ids.Add(holder.Id);
            }
        }

        return result;
    }

    private bool HasOtherHolder(
        IReadOnlyDictionary<(int SettlementId, ConceptKind Concept, Position Position), List<long>> holderIndex,
        NpcState npc,
        ConceptKind concept)
    {
        if (!npc.SettlementId.HasValue)
        {
            return false;
        }

        for (var y = npc.Position.Y - _config.Aura.Radius; y <= npc.Position.Y + _config.Aura.Radius; y++)
        {
            for (var x = npc.Position.X - _config.Aura.Radius; x <= npc.Position.X + _config.Aura.Radius; x++)
            {
                if (holderIndex.TryGetValue(
                        (npc.SettlementId.Value, concept, new Position(x, y)), out var ids) &&
                    ids.Any(id => id != npc.Id))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public Position? FindCohesionTarget(WorldState world, NpcState npc)
    {
        if (!npc.InvasionId.HasValue || !npc.HasAdvanceBias)
        {
            return null;
        }

        return world.Npcs.Values
            .Where(holder => holder.IsAlive && holder.Id != npc.Id &&
                             holder.InvasionId == npc.InvasionId && holder.InvasionRole == npc.InvasionRole &&
                             holder.ConceptMarks.Count > 0 &&
                             holder.Position.ChebyshevDistance(npc.Position) <= _config.Aura.Radius)
            .OrderBy(holder => holder.Position.ChebyshevDistance(npc.Position))
            .ThenBy(holder => _random.StablePriority(
                "aura", world.Tick, npc.Id, "cohesion-holder", $"{npc.InvasionId}:{holder.Id}"))
            .ThenBy(holder => holder.Id)
            .Select(holder => (Position?)holder.Position)
            .FirstOrDefault();
    }
}
