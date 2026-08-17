using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Social;

public enum CollisionPolicy
{
    Combat,
    SameSettlementSuppressed,
    UnaffiliatedProtected,
    OtherSettlementFriction
}

public static class SettlementQueries
{
    public static IReadOnlyList<SettlementState> ActiveSettlements(WorldState world) => world.Settlements.Values
        .Where(item => item.IsActive(world.Tick))
        .OrderBy(item => item.Id)
        .ToArray();

    public static SettlementState? ActiveSettlement(WorldState world, int? settlementId)
    {
        if (!settlementId.HasValue || !world.Settlements.TryGetValue(settlementId.Value, out var settlement) ||
            !settlement.IsActive(world.Tick))
        {
            return null;
        }

        return settlement;
    }

    public static SettlementState? FindActiveCore(WorldState world, Position position, SimulationConfig config) =>
        ActiveSettlements(world)
            .Where(item => item.Center.ChebyshevDistance(position) <= config.Settlement.CoreRadius)
            .OrderBy(item => item.Center.ChebyshevDistance(position))
            .ThenBy(item => item.Id)
            .FirstOrDefault();

    public static bool IsInsideInfluence(
        WorldState world,
        int settlementId,
        Position position,
        SimulationConfig config) =>
        ActiveSettlement(world, settlementId) is { } settlement &&
        settlement.Center.ChebyshevDistance(position) <= config.Settlement.InfluenceRadius;

    public static bool IsInsideAnyRestCollisionRegion(
        WorldState world,
        Position position,
        SimulationConfig config) =>
        ActiveSettlements(world).Any(item =>
            item.Center.ChebyshevDistance(position) <= config.Settlement.RestCollisionRadius);

    public static bool HasActiveThreat(NpcState actor, long targetId, int tick, SimulationConfig config) =>
        actor.ThreatMemory.TryGetValue(targetId, out var memory) &&
        tick - memory.LastThreatTick <= config.Observation.ThreatMemoryDays;

    public static bool AreInvasionOpponents(WorldState world, NpcState first, NpcState second)
    {
        if (!first.SettlementId.HasValue || !second.SettlementId.HasValue)
        {
            return false;
        }

        return world.Invasions.Values.Any(item => item.IsActive(world.Tick) &&
            ((item.AttackSettlementId == first.SettlementId && item.DefenseSettlementId == second.SettlementId) ||
             (item.AttackSettlementId == second.SettlementId && item.DefenseSettlementId == first.SettlementId)));
    }

    public static CollisionPolicy Collision(
        WorldState world,
        NpcState mover,
        NpcState occupant,
        SimulationConfig config)
    {
        if (world.Phase != WorldPhase.Order || AreInvasionOpponents(world, mover, occupant))
        {
            return CollisionPolicy.Combat;
        }

        if (mover.SettlementId.HasValue && mover.SettlementId == occupant.SettlementId &&
            ActiveSettlement(world, mover.SettlementId) is not null)
        {
            return CollisionPolicy.SameSettlementSuppressed;
        }

        if (mover.SettlementId.HasValue && !occupant.SettlementId.HasValue &&
            IsInsideInfluence(world, mover.SettlementId.Value, occupant.Position, config) &&
            !HasActiveThreat(mover, occupant.Id, world.Tick, config))
        {
            return CollisionPolicy.UnaffiliatedProtected;
        }

        if (mover.SettlementId.HasValue && occupant.SettlementId.HasValue &&
            mover.SettlementId != occupant.SettlementId &&
            ActiveSettlement(world, mover.SettlementId) is not null &&
            ActiveSettlement(world, occupant.SettlementId) is not null)
        {
            return CollisionPolicy.OtherSettlementFriction;
        }

        return CollisionPolicy.Combat;
    }

    public static string? ExplicitAttackProtection(
        WorldState world,
        NpcState attacker,
        NpcState target,
        SimulationConfig config)
    {
        if (world.Phase != WorldPhase.Order || AreInvasionOpponents(world, attacker, target))
        {
            return null;
        }

        if (attacker.SettlementId.HasValue && attacker.SettlementId == target.SettlementId &&
            ActiveSettlement(world, attacker.SettlementId) is not null)
        {
            return "same-settlement";
        }

        if (attacker.SettlementId.HasValue && !target.SettlementId.HasValue &&
            IsInsideInfluence(world, attacker.SettlementId.Value, target.Position, config) &&
            !HasActiveThreat(attacker, target.Id, world.Tick, config))
        {
            return "unaffiliated-protected";
        }

        return null;
    }

    public static bool SameActiveCore(
        WorldState world,
        Position first,
        Position second,
        SimulationConfig config)
    {
        var firstCore = FindActiveCore(world, first, config);
        var secondCore = FindActiveCore(world, second, config);
        return firstCore is not null && firstCore.Id == secondCore?.Id;
    }

    public static IReadOnlyList<Position> UsableCoreCells(
        WorldState world,
        SettlementState settlement,
        SimulationConfig config)
    {
        var landmarks = world.Landmarks.Select(item => item.Position).ToHashSet();
        var result = new List<Position>();
        for (var y = settlement.Center.Y - config.Settlement.CoreRadius;
             y <= settlement.Center.Y + config.Settlement.CoreRadius;
             y++)
        {
            for (var x = settlement.Center.X - config.Settlement.CoreRadius;
                 x <= settlement.Center.X + config.Settlement.CoreRadius;
                 x++)
            {
                var position = new Position(x, y);
                if (x >= 0 && x < config.World.Width && y >= 0 && y < config.World.Height &&
                    !landmarks.Contains(position))
                {
                    result.Add(position);
                }
            }
        }

        result.Sort();
        return result;
    }

    public static void AddFriction(
        WorldState world,
        int firstSettlementId,
        int secondSettlementId,
        double amount,
        string reason,
        DomainEventEmitter emit,
        int microRound)
    {
        if (firstSettlementId == secondSettlementId || amount <= 0)
        {
            return;
        }

        var pair = SettlementPair.Create(firstSettlementId, secondSettlementId);
        if (!world.Frictions.TryGetValue(pair, out var friction))
        {
            friction = new SettlementFriction
            {
                Pair = pair,
                LastFrictionEventTick = world.Tick
            };
            world.Frictions.Add(pair, friction);
        }

        friction.CurrentFriction += amount;
        friction.LastFrictionEventTick = world.Tick;
        friction.LifetimeFrictionEvents++;
        if (reason == "collision")
        {
            friction.CollisionEvents++;
        }
        else if (reason == "explicit-threat")
        {
            friction.ExplicitThreatEvents++;
        }

        emit(microRound, SimulationEventType.SettlementFrictionChanged, null, null, null, true,
            $"pair={pair.FirstId},{pair.SecondId};reason={reason};delta={amount:R};current={friction.CurrentFriction:R}");
    }
}
