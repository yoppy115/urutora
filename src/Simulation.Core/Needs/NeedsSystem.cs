using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Needs;

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

    public void ApplyRest(NpcState npc)
    {
        npc.Needs.Rest += _config.Action.RestRestChange;
        npc.Needs.Activity += _config.Action.RestActivityChange;
        npc.Needs.ClampAll();
    }

    public void ApplyActiveActionCost(NpcState npc, ActionKind kind)
    {
        if (kind is ActionKind.Idle or ActionKind.Rest)
        {
            return;
        }

        npc.Needs.Activity += _config.Action.ActiveActivityChange;
        npc.Needs.Rest += _config.Action.ActiveRestChange;
        npc.Needs.ClampAll();
    }
}
