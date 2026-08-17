using Simulation.Core;
using Simulation.Core.Domain;

namespace Simulation.App;

internal static class ObservationDisplayPolicy
{
    public static IReadOnlyList<SettlementStatistics> VisibleSocialSettlements(
        IEnumerable<SettlementStatistics> settlements) => settlements
        .Where(item => !item.DissolvedTick.HasValue)
        .OrderBy(item => item.Id)
        .ToArray();

    public static IReadOnlyList<FrictionStatistics> FrictionsForSettlement(
        IEnumerable<FrictionStatistics> frictions,
        int settlementId) => frictions
        .Where(item => item.FirstSettlementId == settlementId || item.SecondSettlementId == settlementId)
        .OrderByDescending(item => item.CurrentFriction)
        .ThenBy(item => item.FirstSettlementId == settlementId ? item.SecondSettlementId : item.FirstSettlementId)
        .ToArray();

    public static IEnumerable<SimulationEvent> VisibleRecentEvents(IEnumerable<SimulationEvent> events) => events
        .Where(item => item.Type != SimulationEventType.SettlementFrictionChanged);
}
