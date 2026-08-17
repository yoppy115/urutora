using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Needs;

public enum FatigueCause
{
    Communication,
    Move,
    ReproductionAttempt,
    Attack,
    CollisionAttack,
    Flee,
    Counterattack,
    Pursuit
}

public sealed record FatigueApplication(FatigueCause Cause, double RequestedDelta, double AppliedDelta);

public sealed class NeedsSystem
{
    private readonly SimulationConfig _config;

    public NeedsSystem(SimulationConfig config)
    {
        _config = config;
    }

    public void UpdateDaily(WorldState state)
    {
        foreach (var npc in state.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id))
        {
            npc.Needs.Activity += _config.Needs.DailyActivityIncrease;
            npc.Needs.Rest += _config.Needs.DailyRestIncrease;
            if (npc.ActiveAuras.Count > 0)
            {
                npc.Needs.Rest -= _config.Aura.RestNeedDailyReduction;
            }
            npc.Needs.Communication += _config.Needs.DailyCommunicationIncrease;
            if (npc.IsMature(_config))
            {
                npc.Needs.Reproduction += _config.Needs.DailyReproductionIncrease;
            }
            else
            {
                npc.Needs.Reproduction = 0;
            }

            RefreshSurvival(npc);
            npc.Needs.ClampAll();
        }
    }

    public void RefreshSurvival(NpcState npc)
    {
        var maximum = npc.EffectiveStats(_config).MaxHp;
        npc.Needs.Survival = maximum <= 0 ? 10 : Math.Clamp(10 * (1 - npc.CurrentHp / maximum), 0, 10);
    }

    public void ApplyRest(NpcState npc, double effectMultiplier = 1)
    {
        npc.Needs.Rest += _config.Action.RestRestChange * effectMultiplier;
        npc.Needs.Activity += _config.Action.RestActivityChange;
        npc.Needs.ClampAll();
    }

    public double RestPressure(double restNeed)
    {
        var value = Math.Clamp(restNeed, 0, 10);
        var threshold = _config.Action.RestPressure.Threshold;
        if (value <= threshold)
        {
            return 0;
        }

        var denominator = Math.Log(1 + 10 - threshold);
        return _config.Action.RestPressure.Scale * Math.Log(1 + value - threshold) / denominator;
    }

    public double RestUtility(NeedsSnapshot needs) =>
        RestPressure(needs.Rest) - _config.Action.RestPressure.ActivityPenalty * needs.Activity;

    public FatigueApplication? ApplyActiveActionCost(
        NpcState npc,
        ActionKind kind,
        double fatigueMultiplier = 1,
        FatigueCause? causeOverride = null)
    {
        if (kind is ActionKind.Idle or ActionKind.Rest)
        {
            return null;
        }

        npc.Needs.Activity += _config.Action.ActiveActivityChange;
        var cause = causeOverride ?? kind switch
        {
            ActionKind.Communication => FatigueCause.Communication,
            ActionKind.Move => FatigueCause.Move,
            ActionKind.Reproduction => FatigueCause.ReproductionAttempt,
            ActionKind.Attack => FatigueCause.Attack,
            ActionKind.Flee => FatigueCause.Flee,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var fatigue = ApplyFatigue(npc, cause, fatigueMultiplier);
        npc.Needs.ClampAll();
        return fatigue;
    }

    public FatigueApplication ApplyReactionFatigue(NpcState npc, FatigueCause cause) =>
        ApplyFatigue(npc, cause, 1);

    private FatigueApplication ApplyFatigue(NpcState npc, FatigueCause cause, double multiplier)
    {
        var requested = cause switch
        {
            FatigueCause.Communication => _config.Action.Fatigue.Communication,
            FatigueCause.Move => _config.Action.Fatigue.Move,
            FatigueCause.ReproductionAttempt => _config.Action.Fatigue.ReproductionAttempt,
            FatigueCause.Attack => _config.Action.Fatigue.Attack,
            FatigueCause.CollisionAttack => _config.Action.Fatigue.CollisionAttack,
            FatigueCause.Flee => _config.Action.Fatigue.Flee,
            FatigueCause.Counterattack => _config.Action.Fatigue.Counterattack,
            FatigueCause.Pursuit => _config.Action.Fatigue.Pursuit,
            _ => throw new ArgumentOutOfRangeException(nameof(cause))
        } * multiplier;
        var before = npc.Needs.Rest;
        npc.Needs.Rest = Math.Clamp(before + requested, 0, 10);
        return new FatigueApplication(cause, requested, npc.Needs.Rest - before);
    }
}
