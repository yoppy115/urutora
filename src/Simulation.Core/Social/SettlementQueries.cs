using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Social;

public enum CollisionPolicy
{
    Combat,
    SameSettlementSuppressed,
    ParentChildSuppressed,
    UnaffiliatedProtected,
    OtherSettlementFriction
}

public sealed record BirthSettlementAssignment(int SettlementId, SettlementBirthPlacement Placement);

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
        if (!first.InvasionId.HasValue || first.InvasionId != second.InvasionId ||
            first.InvasionRole == second.InvasionRole ||
            first.InvasionParticipation is InvasionParticipationState.Retreating or InvasionParticipationState.Dead ||
            second.InvasionParticipation is InvasionParticipationState.Retreating or InvasionParticipationState.Dead)
        {
            return false;
        }

        return world.Invasions.TryGetValue(first.InvasionId.Value, out var invasion) && invasion.IsActive(world.Tick);
    }

    public static bool AreDirectParentChild(WorldState world, int firstId, int secondId) =>
        world.Settlements.TryGetValue(firstId, out var first) &&
        world.Settlements.TryGetValue(secondId, out var second) &&
        first.DissolvedTick is null && second.DissolvedTick is null &&
        (first.ParentSettlementId == secondId || second.ParentSettlementId == firstId);

    public static CollisionPolicy Collision(
        WorldState world,
        NpcState mover,
        NpcState occupant,
        SimulationConfig config)
    {
        if (mover.SettlementId.HasValue && mover.SettlementId == occupant.SettlementId &&
            ActiveSettlement(world, mover.SettlementId) is not null)
        {
            return CollisionPolicy.SameSettlementSuppressed;
        }

        if (world.Phase != WorldPhase.Order || AreInvasionOpponents(world, mover, occupant))
        {
            return CollisionPolicy.Combat;
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
            if (AreDirectParentChild(world, mover.SettlementId.Value, occupant.SettlementId.Value))
            {
                return CollisionPolicy.ParentChildSuppressed;
            }
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

        if (attacker.SettlementId.HasValue && target.SettlementId.HasValue &&
            AreDirectParentChild(world, attacker.SettlementId.Value, target.SettlementId.Value) &&
            !HasActiveThreat(attacker, target.Id, world.Tick, config))
        {
            return "parent-child-nonaggression";
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

    public static BirthSettlementAssignment? BirthSettlement(
        WorldState world,
        NpcState first,
        NpcState second,
        SimulationConfig config)
    {
        if (first.SettlementId.HasValue && first.SettlementId == second.SettlementId &&
            ActiveSettlement(world, first.SettlementId) is not null)
        {
            return new BirthSettlementAssignment(
                first.SettlementId.Value,
                SettlementBirthPlacement.ParentNeighborhood);
        }

        if (first.SettlementId.HasValue ^ second.SettlementId.HasValue)
        {
            var settlementId = first.SettlementId ?? second.SettlementId!.Value;
            var settlement = ActiveSettlement(world, settlementId);
            if (settlement is not null &&
                settlement.Center.ChebyshevDistance(first.Position) <= config.Settlement.InfluenceRadius &&
                settlement.Center.ChebyshevDistance(second.Position) <= config.Settlement.InfluenceRadius)
            {
                return new BirthSettlementAssignment(settlementId, SettlementBirthPlacement.Influence);
            }

            return null;
        }

        var candidates = new[] { first.SettlementId, second.SettlementId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .Where(settlementId =>
            {
                var settlement = ActiveSettlement(world, settlementId);
                return settlement is not null &&
                       settlement.Center.ChebyshevDistance(first.Position) <= config.Settlement.CoreRadius &&
                       settlement.Center.ChebyshevDistance(second.Position) <= config.Settlement.CoreRadius;
            })
            .OrderBy(item => item)
            .ToArray();

        return candidates.Length == 1
            ? new BirthSettlementAssignment(candidates[0], SettlementBirthPlacement.Core)
            : null;
    }

    public static string BirthPlacementLabel(SettlementBirthPlacement placement) => placement switch
    {
        SettlementBirthPlacement.Core => "core",
        SettlementBirthPlacement.Influence => "influence",
        _ => "parent-neighborhood"
    };

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

    public static IReadOnlyList<Position> UsableInfluenceCells(
        WorldState world,
        SettlementState settlement,
        SimulationConfig config)
    {
        var landmarks = world.Landmarks.Select(item => item.Position).ToHashSet();
        var result = new List<Position>();
        for (var y = settlement.Center.Y - config.Settlement.InfluenceRadius;
             y <= settlement.Center.Y + config.Settlement.InfluenceRadius;
             y++)
        {
            for (var x = settlement.Center.X - config.Settlement.InfluenceRadius;
                 x <= settlement.Center.X + config.Settlement.InfluenceRadius;
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
        double maximum,
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

        var previous = friction.CurrentFriction;
        friction.CurrentFriction = Math.Clamp(previous + amount, 0, maximum);
        var applied = friction.CurrentFriction - previous;
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
            $"pair={pair.FirstId},{pair.SecondId};reason={reason};delta={applied:R};requested={amount:R};" +
            $"current={friction.CurrentFriction:R}");
    }
}
