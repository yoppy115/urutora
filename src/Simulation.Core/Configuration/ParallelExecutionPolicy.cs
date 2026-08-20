namespace Simulation.Core.Configuration;

internal static class ParallelExecutionPolicy
{
    public static int ResolveDegree(PerformanceConfig config, int population)
    {
        if (population < config.MinimumPopulationForParallelism)
        {
            return 1;
        }

        var requested = config.MaximumDegreeOfParallelism == 0
            ? Environment.ProcessorCount
            : config.MaximumDegreeOfParallelism;
        return Math.Clamp(requested, 1, Math.Max(1, population));
    }
}
