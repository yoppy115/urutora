namespace Simulation.App;

internal sealed class WorldBatchRun
{
    public bool IsActive { get; private set; }
    public int TargetTick { get; private set; }
    public int TotalWorlds { get; private set; }
    public int CompletedWorlds { get; private set; }

    public int RemainingWorlds => Math.Max(0, TotalWorlds - CompletedWorlds);

    public void Start(int targetYears, int totalWorlds, int daysPerYear)
    {
        if (targetYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetYears));
        }

        if (totalWorlds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalWorlds));
        }

        if (daysPerYear <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysPerYear));
        }

        TargetTick = checked(targetYears * daysPerYear);
        TotalWorlds = totalWorlds;
        CompletedWorlds = 0;
        IsActive = true;
    }

    public bool HasReachedTarget(int currentTick) => IsActive && currentTick >= TargetTick;

    public bool RecordWorldCompleted()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("No batch run is active.");
        }

        CompletedWorlds++;
        if (CompletedWorlds >= TotalWorlds)
        {
            IsActive = false;
            return false;
        }

        return true;
    }

    public void Cancel()
    {
        IsActive = false;
    }
}
