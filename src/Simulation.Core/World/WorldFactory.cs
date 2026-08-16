using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.World;

public static class WorldFactory
{
    public static WorldState Create(SimulationConfig config, RandomStreamFactory random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(random);
        config.Validate();

        var world = new WorldState();
        foreach (var landmarkConfig in config.World.Landmarks)
        {
            world.Landmarks.Add(new Landmark(
                ConceptKindParser.Parse(landmarkConfig.Concept),
                new Position(landmarkConfig.X, landmarkConfig.Y)));
        }

        var occupied = world.Landmarks.Select(item => item.Position).ToHashSet();
        var positions = CreateStratifiedPositions(config, random, occupied);
        for (var index = 0; index < config.World.InitialPopulation; index++)
        {
            var id = index + 1L;
            var baseStats = new BaseStats
            {
                MaxHp = Math.Clamp(
                    random.Create("initialization", -1, id, "max-hp").NextGaussian(
                        config.InitialPopulation.MaxHpMean,
                        config.InitialPopulation.MaxHpStandardDeviation),
                    config.InitialPopulation.MinimumMaxHp,
                    config.InitialPopulation.MaximumMaxHp),
                Action = Math.Clamp(
                    random.Create("initialization", -1, id, "action").NextGaussian(
                        config.InitialPopulation.AbilityMean,
                        config.InitialPopulation.AbilityStandardDeviation), 0, 10),
                Combat = Math.Clamp(
                    random.Create("initialization", -1, id, "combat").NextGaussian(
                        config.InitialPopulation.AbilityMean,
                        config.InitialPopulation.AbilityStandardDeviation), 0, 10),
                Communication = Math.Clamp(
                    random.Create("initialization", -1, id, "communication").NextGaussian(
                        config.InitialPopulation.AbilityMean,
                        config.InitialPopulation.AbilityStandardDeviation), 0, 10)
            };

            var ageRange = config.InitialPopulation.MaximumAgeDays - config.InitialPopulation.MinimumAgeDays + 1;
            var ageDays = config.InitialPopulation.MinimumAgeDays +
                          random.Create("initialization", -1, id, "age-days").NextInt(ageRange);
            var npc = new NpcState
            {
                Id = id,
                Position = positions[index],
                BaseStats = baseStats,
                RiskPreference = Math.Clamp(
                    random.Create("initialization", -1, id, "risk-preference").NextGaussian(
                        config.InitialPopulation.RiskPreferenceMean,
                        config.InitialPopulation.RiskPreferenceStandardDeviation), 0, 1),
                CurrentHp = baseStats.MaxHp,
                AgeDays = ageDays
            };

            npc.Needs.Activity = InitialNeed(config, random, id, "activity-need");
            npc.Needs.Rest = InitialNeed(config, random, id, "rest-need");
            npc.Needs.Communication = InitialNeed(config, random, id, "communication-need");
            npc.Needs.Reproduction = InitialNeed(config, random, id, "reproduction-need");
            npc.Needs.Survival = 0;
            world.Npcs.Add(id, npc);
        }

        world.NextNpcId = config.World.InitialPopulation + 1L;
        return world;
    }

    private static IReadOnlyList<Position> CreateStratifiedPositions(
        SimulationConfig config,
        RandomStreamFactory random,
        ISet<Position> occupied)
    {
        var population = config.World.InitialPopulation;
        var aspect = (double)config.World.Width / config.World.Height;
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(population * aspect)));
        var rows = Math.Max(1, (int)Math.Ceiling((double)population / columns));
        var result = new List<Position>(population);

        for (var index = 0; index < population; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var minimumX = column * config.World.Width / columns;
            var maximumXExclusive = Math.Max(minimumX + 1, (column + 1) * config.World.Width / columns);
            var minimumY = row * config.World.Height / rows;
            var maximumYExclusive = Math.Max(minimumY + 1, (row + 1) * config.World.Height / rows);
            var stream = random.Create("initialization", -1, index + 1L, "stratified-position");
            var candidates = new List<Position>();
            for (var y = minimumY; y < Math.Min(maximumYExclusive, config.World.Height); y++)
            {
                for (var x = minimumX; x < Math.Min(maximumXExclusive, config.World.Width); x++)
                {
                    var candidate = new Position(x, y);
                    if (!occupied.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            Position selected;
            if (candidates.Count > 0)
            {
                candidates.Sort();
                selected = candidates[stream.NextInt(candidates.Count)];
            }
            else
            {
                selected = FindFallback(config, random, occupied, index + 1L);
            }

            occupied.Add(selected);
            result.Add(selected);
        }

        return result;
    }

    private static Position FindFallback(
        SimulationConfig config,
        RandomStreamFactory random,
        ISet<Position> occupied,
        long entityId)
    {
        var available = new List<(Position Position, double Priority)>();
        for (var y = 0; y < config.World.Height; y++)
        {
            for (var x = 0; x < config.World.Width; x++)
            {
                var position = new Position(x, y);
                if (!occupied.Contains(position))
                {
                    available.Add((position, random.StablePriority(
                        "initialization", -1, entityId, "fallback-position", $"{x}:{y}")));
                }
            }
        }

        if (available.Count == 0)
        {
            throw new InvalidOperationException("The configured world has no room for the initial population.");
        }

        return available.OrderBy(item => item.Priority).ThenBy(item => item.Position).First().Position;
    }

    private static double InitialNeed(
        SimulationConfig config,
        RandomStreamFactory random,
        long entityId,
        string purpose)
    {
        return random.Create("initialization", -1, entityId, purpose).NextDouble(
            config.InitialPopulation.InitialNeedMinimum,
            config.InitialPopulation.InitialNeedMaximum);
    }
}
