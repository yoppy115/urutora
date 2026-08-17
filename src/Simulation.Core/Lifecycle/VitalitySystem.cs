using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.Core.Lifecycle;

public sealed class VitalitySystem
{
    private readonly SimulationConfig _config;

    public VitalitySystem(SimulationConfig config)
    {
        _config = config;
    }

    public double DailyVitalChange(int ageDays)
    {
        if (ageDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ageDays));
        }

        var points = _config.Vitality.ControlPoints;
        if (ageDays <= points[0].AgeDays)
        {
            return points[0].DailyVitalChange;
        }

        for (var index = 0; index < points.Count - 1; index++)
        {
            var first = points[index];
            var second = points[index + 1];
            if (ageDays > second.AgeDays)
            {
                continue;
            }

            var t = (double)(ageDays - first.AgeDays) / (second.AgeDays - first.AgeDays);
            var smooth = t * t * (3 - 2 * t);
            return first.DailyVitalChange + (second.DailyVitalChange - first.DailyVitalChange) * smooth;
        }

        return points[^1].DailyVitalChange;
    }

    public bool ApplyDailyChange(NpcState npc, double changeMultiplier = 1)
    {
        if (!npc.IsAlive)
        {
            return false;
        }

        var maximumHp = npc.EffectiveStats(_config).MaxHp;
        npc.CurrentHp = Math.Min(maximumHp, npc.CurrentHp + DailyVitalChange(npc.AgeDays) * changeMultiplier);
        npc.AgeDays++;
        if (npc.ReproductionCooldownDays > 0)
        {
            npc.ReproductionCooldownDays--;
        }

        if (npc.CurrentHp <= 0)
        {
            npc.CurrentHp = 0;
            npc.IsAlive = false;
            return true;
        }

        return false;
    }
}
