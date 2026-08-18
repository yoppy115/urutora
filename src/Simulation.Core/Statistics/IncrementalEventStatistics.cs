using Simulation.Core.Domain;

namespace Simulation.Core.Statistics;

/// <summary>
/// Lifetime event counters maintained as events are emitted. This observer state is
/// deliberately excluded from simulation authority and avoids retaining raw events
/// merely to answer terminal/UI count queries.
/// </summary>
internal sealed class IncrementalEventStatistics
{
    private readonly Dictionary<SimulationEventType, long> _eventCounts = new();

    public long UpdateCount { get; private set; }

    public void Observe(SimulationEvent simulationEvent)
    {
        UpdateCount++;
        _eventCounts[simulationEvent.Type] = Count(simulationEvent.Type) + 1;
    }

    public long Count(SimulationEventType type) => _eventCounts.GetValueOrDefault(type);
}
