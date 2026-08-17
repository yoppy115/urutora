using Simulation.Core.Configuration;
using Simulation.Core.Decision;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Reproduction;

public sealed class ReproductionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public ReproductionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public bool CanParticipate(NpcState npc)
    {
        if (!npc.IsAlive || !npc.IsMature(_config) || npc.ReproductionCooldownDays > 0)
        {
            return false;
        }

        var effective = npc.EffectiveStats(_config);
        return npc.CurrentHp >= effective.MaxHp * _config.Reproduction.MinimumHpRatio;
    }

    public bool Accepts(NpcState target, int tick, int microRound, long initiatorId, double utilityPenalty = 0)
    {
        var acceptUtility = AcceptanceUtility(target, utilityPenalty);
        var candidates = new[]
        {
            new ActionCandidate(ActionKind.Reproduction, initiatorId, null, acceptUtility, "accept", new Dictionary<string, double>()),
            new ActionCandidate(ActionKind.Reproduction, initiatorId, null, 0, "reject", new Dictionary<string, double>())
        };
        var trace = UtilityDecisionSystem.SelectWeighted(
            target.Id,
            tick,
            microRound,
            candidates,
            2,
            _config.Utility.Temperature,
            _random.Create("reproduction", tick, target.Id, "acceptance-reaction", $"{microRound}:{initiatorId}"),
            "ReproductionAcceptance");
        return trace.Selected.StableKey == "accept";
    }

    public static double AcceptanceUtility(NpcState target, double utilityPenalty = 0) =>
        target.Needs.Reproduction - 0.40 * target.Needs.Survival - 0.20 * target.Needs.Rest - utilityPenalty;

    public BirthRequest CreateRequest(NpcState first, NpcState second, int tick, int microRound)
    {
        var minimum = Math.Min(first.Id, second.Id);
        var maximum = Math.Max(first.Id, second.Id);
        return new BirthRequest(
            StableHash.StableId("birth-request", tick, microRound, minimum, maximum),
            first.Id,
            second.Id,
            first.Position,
            second.Position,
            new GeneticSnapshot(first.BaseStats.Copy(), first.RiskPreference),
            new GeneticSnapshot(second.BaseStats.Copy(), second.RiskPreference),
            tick);
    }

    public IReadOnlyList<BirthResolution> ResolveBirths(WorldState world)
    {
        var requests = world.BirthRequests
            .OrderBy(item => item.RequestId, StringComparer.Ordinal)
            .ToArray();
        if (requests.Length == 0)
        {
            return Array.Empty<BirthResolution>();
        }

        var occupied = world.Npcs.Values.Where(item => item.IsAlive).Select(item => item.Position).ToHashSet();
        var landmarks = world.Landmarks.Select(item => item.Position).ToHashSet();
        var preferences = requests.ToDictionary(
            request => request.RequestId,
            request => CreatePreferences(request, occupied, landmarks),
            StringComparer.Ordinal);
        var reserved = new HashSet<Position>();
        var assignments = new Dictionary<string, Position>(StringComparer.Ordinal);
        var indexes = requests.ToDictionary(item => item.RequestId, _ => 0, StringComparer.Ordinal);
        var unresolved = requests.Select(item => item.RequestId).ToHashSet(StringComparer.Ordinal);

        while (unresolved.Count > 0)
        {
            var proposals = new Dictionary<Position, List<string>>();
            var exhausted = new List<string>();
            foreach (var requestId in unresolved.OrderBy(item => item, StringComparer.Ordinal))
            {
                var choices = preferences[requestId];
                var index = indexes[requestId];
                while (index < choices.Count && reserved.Contains(choices[index]))
                {
                    index++;
                }

                indexes[requestId] = index;
                if (index >= choices.Count)
                {
                    exhausted.Add(requestId);
                    continue;
                }

                if (!proposals.TryGetValue(choices[index], out var contenders))
                {
                    contenders = new List<string>();
                    proposals.Add(choices[index], contenders);
                }

                contenders.Add(requestId);
            }

            foreach (var requestId in exhausted)
            {
                unresolved.Remove(requestId);
            }

            if (proposals.Count == 0)
            {
                break;
            }

            foreach (var proposal in proposals.OrderBy(item => item.Key))
            {
                var winner = proposal.Value
                    .OrderBy(requestId => _random.StablePriority(
                        "birth", world.Tick, 0, "cell-conflict", $"{proposal.Key.X}:{proposal.Key.Y}:{requestId}"))
                    .ThenBy(requestId => requestId, StringComparer.Ordinal)
                    .First();
                assignments[winner] = proposal.Key;
                reserved.Add(proposal.Key);
                unresolved.Remove(winner);

                foreach (var loser in proposal.Value.Where(item => item != winner))
                {
                    indexes[loser]++;
                }
            }
        }

        var outcomes = new List<BirthResolution>();
        foreach (var request in requests)
        {
            if (!assignments.TryGetValue(request.RequestId, out var position))
            {
                outcomes.Add(new BirthResolution(request, null, null));
                continue;
            }

            var childId = world.NextNpcId++;
            var childStats = CreateChildGenetics(request, world.Tick);
            var child = new NpcState
            {
                Id = childId,
                Position = position,
                BaseStats = childStats.BaseStats,
                RiskPreference = childStats.RiskPreference,
                CurrentHp = childStats.BaseStats.MaxHp,
                AgeDays = 0,
                ParentAId = request.ParentAId,
                ParentBId = request.ParentBId
            };
            child.Needs.Activity = _config.Reproduction.NewbornInitialNeed;
            child.Needs.Rest = _config.Reproduction.NewbornInitialNeed;
            child.Needs.Communication = _config.Reproduction.NewbornInitialNeed;
            child.Needs.Reproduction = 0;
            world.Npcs.Add(child.Id, child);
            outcomes.Add(new BirthResolution(request, child, position));
        }

        world.BirthRequests.Clear();
        return outcomes;
    }

    public GeneticSnapshot CreateChildGenetics(BirthRequest request, int tick)
    {
        var maxHp = Inherit(
            request, tick, "max-hp", request.ParentAGenetics.BaseStats.MaxHp,
            request.ParentBGenetics.BaseStats.MaxHp,
            _config.Reproduction.MutationStandardDeviation * _config.Reproduction.MaxHpMutationScale,
            _config.InitialPopulation.MinimumMaxHp,
            _config.InitialPopulation.MaximumMaxHp);
        var action = Inherit(request, tick, "action", request.ParentAGenetics.BaseStats.Action,
            request.ParentBGenetics.BaseStats.Action, _config.Reproduction.MutationStandardDeviation, 0, 10);
        var combat = Inherit(request, tick, "combat", request.ParentAGenetics.BaseStats.Combat,
            request.ParentBGenetics.BaseStats.Combat, _config.Reproduction.MutationStandardDeviation, 0, 10);
        var communication = Inherit(request, tick, "communication", request.ParentAGenetics.BaseStats.Communication,
            request.ParentBGenetics.BaseStats.Communication, _config.Reproduction.MutationStandardDeviation, 0, 10);
        var riskPreference = Inherit(request, tick, "risk-preference", request.ParentAGenetics.RiskPreference,
            request.ParentBGenetics.RiskPreference, _config.Reproduction.MutationStandardDeviation, 0, 1);

        return new GeneticSnapshot(new BaseStats
        {
            MaxHp = maxHp,
            Action = action,
            Combat = combat,
            Communication = communication
        }, riskPreference);
    }

    private List<Position> CreatePreferences(
        BirthRequest request,
        IReadOnlySet<Position> occupied,
        IReadOnlySet<Position> landmarks)
    {
        var candidates = request.ParentAPositionAtConception.Neighbors()
            .Concat(request.ParentBPositionAtConception.Neighbors())
            .Distinct()
            .Where(position => position.X >= 0 && position.X < _config.World.Width &&
                               position.Y >= 0 && position.Y < _config.World.Height)
            .Where(position => !occupied.Contains(position) && !landmarks.Contains(position))
            .OrderBy(position => _random.StablePriority(
                "birth", request.ConceptionTick, 0, "location-preference", $"{request.RequestId}:{position.X}:{position.Y}"))
            .ThenBy(position => position)
            .ToList();
        return candidates;
    }

    private double Inherit(
        BirthRequest request,
        int tick,
        string trait,
        double first,
        double second,
        double mutationStandardDeviation,
        double minimum,
        double maximum)
    {
        var blend = _random.Create("genetics", tick, 0, "blend", $"{request.RequestId}:{trait}").NextDouble();
        var value = first + (second - first) * blend;
        var mutation = _random.Create("genetics", tick, 0, "mutation-chance", $"{request.RequestId}:{trait}");
        if (mutation.NextDouble() < _config.Reproduction.MutationChance)
        {
            value += _random.Create("genetics", tick, 0, "mutation-value", $"{request.RequestId}:{trait}")
                .NextGaussian(0, mutationStandardDeviation);
        }

        return Math.Clamp(value, minimum, maximum);
    }
}

public sealed record BirthResolution(BirthRequest Request, NpcState? Child, Position? Position)
{
    public bool Success => Child is not null;
}
