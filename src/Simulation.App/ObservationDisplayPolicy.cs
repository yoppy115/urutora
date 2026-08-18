using Simulation.Core;
using Simulation.Core.Domain;

namespace Simulation.App;

internal static class ObservationDisplayPolicy
{
    public static IEnumerable<SimulationEvent> VisibleRecentEvents(IEnumerable<SimulationEvent> events) => events
        .Where(item => item.Type != SimulationEventType.SettlementFrictionChanged);
}
