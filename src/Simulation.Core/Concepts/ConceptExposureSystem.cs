using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Concepts;

public sealed class ConceptExposureSystem
{
    private readonly SimulationConfig _config;

    public ConceptExposureSystem(SimulationConfig config)
    {
        _config = config;
    }

    public IReadOnlyList<ConceptMarkAcquisition> Apply(WorldState state)
    {
        var acquisitions = new List<ConceptMarkAcquisition>();
        foreach (var npc in state.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id))
        {
            foreach (var landmark in state.Landmarks.OrderBy(item => item.Concept))
            {
                var distance = npc.Position.ChebyshevDistance(landmark.Position);
                if (distance < 1 || distance >= _config.Concept.ExposureByDistance.Count)
                {
                    continue;
                }

                var exposure = _config.Concept.ExposureByDistance[distance];
                if (exposure <= 0)
                {
                    continue;
                }

                npc.ConceptExposure.TryGetValue(landmark.Concept, out var current);
                current += exposure;
                npc.ConceptExposure[landmark.Concept] = current;
                if (current >= _config.Concept.ExposureThreshold && npc.ConceptMarks.Add(landmark.Concept))
                {
                    acquisitions.Add(new ConceptMarkAcquisition(npc.Id, npc.Position, landmark.Concept));
                }
            }
        }

        return acquisitions;
    }
}

public sealed record ConceptMarkAcquisition(long EntityId, Position Position, ConceptKind Concept);
