using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Social;

public sealed record FissionEvaluationResult(
    IReadOnlySet<int> FormedFromSettlementIds,
    IReadOnlySet<int> InvasionFallbackSettlementIds);

public sealed class SettlementFissionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public SettlementFissionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public FissionEvaluationResult Evaluate(WorldState world, DomainEventEmitter emit)
    {
        RecordResidentDay(world);
        var formed = new HashSet<int>();
        var fallback = new HashSet<int>();

        foreach (var parent in SettlementQueries.ActiveSettlements(world))
        {
            var eligible = world.Phase == WorldPhase.Order &&
                           parent.Support >= _config.Settlement.SupportRecoveryThreshold &&
                           !IsSettlementEngaged(world, parent.Id) &&
                           !HasActiveMigration(world, parent.Id);
            parent.FissionPressureDays = eligible &&
                                         parent.CrowdingPressure >= _config.Settlement.FissionPressureThreshold
                ? parent.FissionPressureDays + 1
                : 0;
            if (parent.FissionPressureDays < _config.Settlement.FissionPressureConsecutiveDays)
            {
                continue;
            }

            var migrants = EligibleMigrants(world, parent);
            if (migrants.Length < _config.Settlement.FissionMinimumMigrants)
            {
                fallback.Add(parent.Id);
                continue;
            }

            var hotspot = SelectHotspot(world, parent);
            if (hotspot is null)
            {
                fallback.Add(parent.Id);
                continue;
            }

            FormChild(world, parent, hotspot.Value, migrants, emit);
            formed.Add(parent.Id);
            parent.FissionPressureDays = 0;
            parent.CrowdingConsecutiveDays = 0;
        }

        CompleteMigrations(world, _config, emit, 0, "tick-end");
        return new FissionEvaluationResult(formed, fallback);
    }

    public static Position? MigrationTarget(WorldState world, NpcState npc)
    {
        if (!npc.MigrationTargetSettlementId.HasValue || npc.InvasionParticipation is
                InvasionParticipationState.Advancing or InvasionParticipationState.Defending or
                InvasionParticipationState.FieldRest)
        {
            return null;
        }

        return world.Settlements.TryGetValue(npc.MigrationTargetSettlementId.Value, out var child) &&
               child.DissolvedTick is null
            ? child.Center
            : null;
    }

    public static void CompleteMigrations(
        WorldState world,
        SimulationConfig config,
        DomainEventEmitter emit,
        int microRound,
        string reason,
        NpcState? onlyNpc = null)
    {
        IEnumerable<NpcState> candidates = onlyNpc is null
            ? world.Npcs.Values.OrderBy(item => item.Id)
            : new[] { onlyNpc };
        foreach (var npc in candidates.Where(item => item.MigrationTargetSettlementId.HasValue).ToArray())
        {
            var childId = npc.MigrationTargetSettlementId!.Value;
            if (!npc.IsAlive)
            {
                npc.MigrationTargetSettlementId = null;
                npc.MigrationStartedTick = null;
                continue;
            }

            if (!world.Settlements.TryGetValue(childId, out var child) || child.DissolvedTick.HasValue)
            {
                npc.MigrationTargetSettlementId = null;
                npc.MigrationStartedTick = null;
                if (npc.IsAlive)
                {
                    emit(microRound, SimulationEventType.MigrationInterrupted, npc.Id, null, npc.Position, true,
                        $"child={childId};reason=child-inactive");
                }
                continue;
            }

            if (npc.Position.ChebyshevDistance(child.Center) > config.Settlement.InfluenceRadius)
            {
                continue;
            }

            var started = npc.MigrationStartedTick ?? world.Tick;
            npc.MigrationTargetSettlementId = null;
            npc.MigrationStartedTick = null;
            emit(microRound, SimulationEventType.MigrationCompleted, npc.Id, null, npc.Position, true,
                $"child={childId};started={started};duration={Math.Max(0, world.Tick - started)};reason={reason}");
        }
    }

    private void RecordResidentDay(WorldState world)
    {
        var counts = world.Npcs.Values
            .Where(item => item.IsAlive)
            .GroupBy(item => item.Position)
            .OrderBy(item => item.Key)
            .ToDictionary(item => item.Key, item => item.Count());
        world.FissionResidentHistory.Add(new DailyHotspotResidents(world.Tick, counts));
        var oldest = world.Tick - _config.Settlement.FissionResidentWindowDays + 1;
        world.FissionResidentHistory.RemoveAll(item => item.Tick < oldest);
    }

    private Position? SelectHotspot(WorldState world, SettlementState parent)
    {
        var aggregate = new Dictionary<Position, int>();
        foreach (var day in world.FissionResidentHistory.OrderBy(item => item.Tick))
        {
            foreach (var pair in day.ResidentCounts.OrderBy(item => item.Key))
            {
                aggregate[pair.Key] = aggregate.GetValueOrDefault(pair.Key) + pair.Value;
            }
        }

        var current = world.Npcs.Values
            .Where(item => item.IsAlive)
            .GroupBy(item => item.Position)
            .ToDictionary(item => item.Key, item => item.Count());
        var size = _config.Settlement.HotspotWindowSize;
        var candidates = new List<(Position Origin, Position Center, int ResidentDays, int ParentDistance, double Tie)>();
        for (var y = 0; y <= _config.World.Height - size; y++)
        {
            for (var x = 0; x <= _config.World.Width - size; x++)
            {
                var cells = WindowCells(x, y, size).ToArray();
                var residentDays = cells.Sum(cell => aggregate.GetValueOrDefault(cell));
                if (residentDays < _config.Settlement.FissionResidentDaysThreshold ||
                    cells.Sum(cell => current.GetValueOrDefault(cell)) < _config.Settlement.FissionCurrentResidentsMinimum)
                {
                    continue;
                }

                var geometric = new Position(x + size / 2, y + size / 2);
                var validCenters = cells
                    .Where(cell => IsValidCenter(world, parent, cell))
                    .Select(cell => new
                    {
                        Cell = cell,
                        Days = aggregate.GetValueOrDefault(cell),
                        Occupied = current.ContainsKey(cell),
                        GeometricDistance = cell.ChebyshevDistance(geometric),
                        Tie = _random.StablePriority("fission", world.Tick, parent.Id, "center-tie", cell.ToString())
                    })
                    .OrderByDescending(item => item.Days)
                    .ThenByDescending(item => item.Occupied)
                    .ThenBy(item => item.GeometricDistance)
                    .ThenBy(item => item.Tie)
                    .ThenBy(item => item.Cell)
                    .FirstOrDefault();
                if (validCenters is null)
                {
                    continue;
                }

                candidates.Add((
                    new Position(x, y),
                    validCenters.Cell,
                    residentDays,
                    parent.Center.ChebyshevDistance(validCenters.Cell),
                    _random.StablePriority("fission", world.Tick, parent.Id, "hotspot-tie", $"{x},{y}")));
            }
        }

        return candidates
            .OrderByDescending(item => item.ResidentDays)
            .ThenBy(item => item.ParentDistance)
            .ThenBy(item => item.Tie)
            .ThenBy(item => item.Origin)
            .Select(item => (Position?)item.Center)
            .FirstOrDefault();
    }

    private bool IsValidCenter(WorldState world, SettlementState parent, Position center)
    {
        if (center.X < 0 || center.X >= _config.World.Width || center.Y < 0 || center.Y >= _config.World.Height ||
            world.Landmarks.Any(item => item.Position == center))
        {
            return false;
        }

        var parentDistance = parent.Center.ChebyshevDistance(center);
        if (parentDistance < _config.Settlement.FissionMinimumDistance ||
            parentDistance > _config.Settlement.FissionMaximumDistance)
        {
            return false;
        }

        foreach (var other in SettlementQueries.ActiveSettlements(world))
        {
            var distance = other.Center.ChebyshevDistance(center);
            if (other.Id == parent.Id)
            {
                if (distance <= _config.Settlement.InfluenceRadius ||
                    distance <= _config.Settlement.CoreRadius * 2)
                {
                    return false;
                }
                continue;
            }

            if (distance <= _config.Settlement.InfluenceRadius * 2 ||
                distance <= _config.Settlement.CoreRadius * 2)
            {
                return false;
            }
        }

        return true;
    }

    private void FormChild(
        WorldState world,
        SettlementState parent,
        Position center,
        IReadOnlyList<NpcState> eligible,
        DomainEventEmitter emit)
    {
        var targetCount = Math.Max(
            _config.Settlement.FissionMinimumMigrants,
            (int)Math.Round(eligible.Count * _config.Settlement.FissionMigrantRatio, MidpointRounding.AwayFromZero));
        var migrants = eligible
            .OrderBy(item => _random.StablePriority("fission", world.Tick, parent.Id, "migrant", item.Id.ToString()))
            .ThenBy(item => item.Id)
            .Take(Math.Min(targetCount, eligible.Count))
            .OrderBy(item => item.Id)
            .ToArray();
        if (migrants.Length < _config.Settlement.FissionMinimumMigrants)
        {
            return;
        }

        var child = new SettlementState
        {
            Id = world.NextSettlementId++,
            Center = center,
            FormedTick = world.Tick,
            EffectiveTick = world.Tick + 1,
            FounderIds = migrants.Select(item => item.Id).ToArray(),
            ParentSettlementId = parent.Id,
            FoundingResidentBaseline = migrants.Length,
            Support = 50
        };
        world.Settlements.Add(child.Id, child);
        parent.ChildSettlementIds.Add(child.Id);

        var migrantIds = migrants.Select(item => item.Id).ToHashSet();
        foreach (var npc in migrants)
        {
            var previous = npc.SettlementId;
            npc.SettlementId = child.Id;
            npc.SettlementAffinity[child.Id] = Math.Max(
                npc.SettlementAffinity.GetValueOrDefault(child.Id),
                _config.Settlement.FissionFounderAffinity);
            npc.FissionFounder = true;
            npc.MigrationTargetSettlementId = child.Id;
            npc.MigrationStartedTick = world.Tick;
            emit(0, SimulationEventType.AffiliationChanged, npc.Id, null, npc.Position, true,
                $"from={previous};to={child.Id};reason=fission");
            emit(0, SimulationEventType.MigrationStarted, npc.Id, null, npc.Position, true,
                $"parent={parent.Id};child={child.Id}");
        }

        foreach (var npc in world.Npcs.Values
                     .Where(item => item.IsAlive && !migrantIds.Contains(item.Id) &&
                                    SettlementQueries.ActiveSettlement(world, item.SettlementId) is null &&
                                    item.Position.ChebyshevDistance(center) <= _config.Settlement.CoreRadius)
                     .OrderBy(item => item.Id))
        {
            npc.SettlementAffinity[child.Id] = npc.SettlementAffinity.GetValueOrDefault(child.Id) +
                                               _config.Settlement.FissionCoreResidentAffinity;
            emit(0, SimulationEventType.AffinityChanged, npc.Id, null, npc.Position, true,
                $"settlement={child.Id};reason=fission-core;delta={_config.Settlement.FissionCoreResidentAffinity:R};" +
                $"current={npc.SettlementAffinity[child.Id]:R}");
        }

        emit(0, SimulationEventType.SettlementFission, null, null, center, true,
            $"parent={parent.Id};child={child.Id};effectiveTick={child.EffectiveTick};" +
            $"migrants={string.Join(',', migrants.Select(item => item.Id))}");
        CompleteMigrations(world, _config, emit, 0, "formation");
    }

    private NpcState[] EligibleMigrants(WorldState world, SettlementState parent) => world.Npcs.Values
        .Where(item => item.IsAlive && item.SettlementId == parent.Id && !item.InvasionId.HasValue &&
                       !item.MigrationTargetSettlementId.HasValue)
        .OrderBy(item => item.Id)
        .ToArray();

    private static bool IsSettlementEngaged(WorldState world, int settlementId) => world.Invasions.Values.Any(item =>
        item.EndTick is null && (item.AttackSettlementId == settlementId || item.DefenseSettlementId == settlementId));

    private static bool HasActiveMigration(WorldState world, int parentId) => world.Npcs.Values.Any(item =>
        item.IsAlive && item.MigrationTargetSettlementId.HasValue &&
        world.Settlements.TryGetValue(item.MigrationTargetSettlementId.Value, out var child) &&
        child.ParentSettlementId == parentId && child.DissolvedTick is null);

    private static IEnumerable<Position> WindowCells(int x, int y, int size)
    {
        for (var cellY = y; cellY < y + size; cellY++)
        {
            for (var cellX = x; cellX < x + size; cellX++)
            {
                yield return new Position(cellX, cellY);
            }
        }
    }
}
