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
        foreach (var npc in alive)
        {
            var desired = new HashSet<ConceptKind>();
            if (SettlementQueries.ActiveSettlement(world, npc.SettlementId) is not null)
            {
                foreach (var concept in Enum.GetValues<ConceptKind>())
                {
                    var holderExists = alive.Any(holder =>
                        holder.Id != npc.Id &&
                        holder.SettlementId == npc.SettlementId &&
                        holder.ConceptMarks.Contains(concept) &&
                        holder.Position.ChebyshevDistance(npc.Position) <= _config.Aura.Radius);
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
