using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Perception;
using Simulation.Core.Randomness;

namespace Simulation.Core.Decision;

public sealed record WorldDecisionRules(
    int Width,
    int Height,
    IReadOnlySet<Position> LandmarkPositions)
{
    public bool IsInside(Position position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    public bool IsTraversableForIntent(Position position) => IsInside(position) && !LandmarkPositions.Contains(position);
}

public sealed record DecisionContext(
    long EntityId,
    Position Position,
    double CurrentHp,
    EffectiveStats EffectiveStats,
    double RiskPreference,
    int AgeDays,
    int ReproductionCooldownDays,
    NeedsSnapshot Needs,
    PerceptionView Perception,
    WorldDecisionRules WorldRules);

public sealed record ActionCandidate(
    ActionKind Kind,
    long? TargetId,
    Position? Destination,
    double Utility,
    string StableKey,
    IReadOnlyDictionary<string, double> Breakdown,
    Position? PerceivedTargetPosition = null);

public sealed record CandidateWeight(string StableKey, ActionKind Kind, double Utility, double Weight);

public sealed record DecisionTrace(
    long EntityId,
    int Tick,
    int MicroRound,
    ActionCandidate Selected,
    IReadOnlyList<CandidateWeight> WeightedCandidates,
    double Draw,
    string RandomPurpose,
    string DecisionReason = "initial");

public sealed record ActionIntent(
    string IntentId,
    long ActorId,
    ActionKind Kind,
    long? TargetId,
    Position? Destination,
    DecisionTrace Decision,
    Position? PerceivedTargetPosition = null);

public sealed class UtilityDecisionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

    public UtilityDecisionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public DecisionTrace Decide(DecisionContext context, int tick, int microRound)
    {
        var candidates = BuildCandidates(context, tick, microRound);
        return SelectWeighted(
            context.EntityId,
            tick,
            microRound,
            candidates,
            _config.Utility.TopCandidates,
            _config.Utility.Temperature,
            _random.Create("decision", tick, context.EntityId, "utility-choice", microRound.ToString()),
            "UtilityChoice");
    }

    public ActionIntent CreateIntent(DecisionTrace trace, string intentScope = "initial")
    {
        return new ActionIntent(
            StableHash.StableId("intent", trace.Tick, trace.MicroRound, trace.EntityId, trace.Selected.StableKey, intentScope),
            trace.EntityId,
            trace.Selected.Kind,
            trace.Selected.TargetId,
            trace.Selected.Destination,
            trace,
            trace.Selected.PerceivedTargetPosition);
    }

    public IReadOnlyList<ActionCandidate> BuildCandidates(DecisionContext context, int tick, int microRound)
    {
        var result = new List<ActionCandidate>();
        AddMove(context, tick, microRound, result);
        AddRest(context, result);
        AddCommunication(context, tick, microRound, result);
        AddAttack(context, result);
        AddFlee(context, tick, microRound, result);
        AddReproduction(context, tick, microRound, result);
        return result;
    }

    public static DecisionTrace SelectWeighted(
        long entityId,
        int tick,
        int microRound,
        IReadOnlyList<ActionCandidate> input,
        int topCandidateCount,
        double temperature,
        DeterministicRandom random,
        string randomPurpose)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(random);
        if (topCandidateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topCandidateCount));
        }

        if (!double.IsFinite(temperature) || temperature <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be finite and greater than zero.");
        }

        if (input.Any(candidate => !double.IsFinite(candidate.Utility)))
        {
            throw new InvalidOperationException("Candidate utility must be finite.");
        }

        var stableKeys = input.Select(item => item.StableKey).ToArray();
        if (stableKeys.Distinct(StringComparer.Ordinal).Count() != stableKeys.Length)
        {
            throw new InvalidOperationException("Candidate stable keys must be unique.");
        }

        if (input.Count == 0)
        {
            var idle = new ActionCandidate(ActionKind.Idle, null, null, 0, "idle", new Dictionary<string, double>());
            return new DecisionTrace(entityId, tick, microRound, idle, Array.Empty<CandidateWeight>(), 0, randomPurpose);
        }

        var ranked = input
            .OrderByDescending(candidate => candidate.Utility)
            .ThenBy(candidate => candidate.StableKey, StringComparer.Ordinal)
            .Take(Math.Min(topCandidateCount, input.Count))
            .ToArray();

        if (ranked.Length == 1)
        {
            var only = ranked[0];
            return new DecisionTrace(
                entityId,
                tick,
                microRound,
                only,
                new[] { new CandidateWeight(only.StableKey, only.Kind, only.Utility, 1) },
                0,
                randomPurpose);
        }

        var maximum = ranked[0].Utility;
        var rawWeights = ranked.Select(candidate => Math.Exp((candidate.Utility - maximum) / temperature)).ToArray();
        var total = rawWeights.Sum();
        if (!double.IsFinite(total) || total <= 0)
        {
            throw new InvalidOperationException("Softmax produced invalid weights.");
        }

        var weights = ranked
            .Select((candidate, index) => new CandidateWeight(candidate.StableKey, candidate.Kind, candidate.Utility, rawWeights[index] / total))
            .ToArray();
        var draw = random.NextDouble();
        var cumulative = 0.0;
        var selectedIndex = ranked.Length - 1;
        for (var index = 0; index < weights.Length; index++)
        {
            cumulative += weights[index].Weight;
            if (draw < cumulative)
            {
                selectedIndex = index;
                break;
            }
        }

        return new DecisionTrace(entityId, tick, microRound, ranked[selectedIndex], weights, draw, randomPurpose);
    }

    public double ThreatRisk(DecisionContext context, PerceivedEntity target)
    {
        var perceivedCombat = target.Combat ?? context.EffectiveStats.Combat;
        var selfHpRatio = context.EffectiveStats.MaxHp <= 0 ? 0 : context.CurrentHp / context.EffectiveStats.MaxHp;
        return Math.Clamp(5 + 0.5 * (perceivedCombat - context.EffectiveStats.Combat) + 5 * (1 - selfHpRatio), 0, 10);
    }

    public AttackUtilityResult AttackUtility(DecisionContext context, PerceivedEntity target)
    {
        var perceivedCombat = target.Combat ?? context.EffectiveStats.Combat;
        var perceivedHp = Math.Max(target.CurrentHp ?? context.EffectiveStats.MaxHp, 1);
        var hit = Math.Clamp(
            _config.Combat.HitChanceBase + _config.Combat.HitChancePerCombatDifference *
            (context.EffectiveStats.Combat - perceivedCombat),
            _config.Combat.HitChanceMinimum,
            _config.Combat.HitChanceMaximum);
        var expectedDamage = Math.Max(
            _config.Combat.DamageMinimum,
            _config.Combat.DamageBase +
            _config.Combat.DamageAttackerFactor * context.EffectiveStats.Combat -
            _config.Combat.DamageDefenderFactor * perceivedCombat);
        var neutralization = Math.Clamp(hit * expectedDamage / perceivedHp, 0, 1);
        var threatRisk = ThreatRisk(context, target);
        var survivalPressure = Math.Max(context.Needs.Survival, threatRisk);
        var utility = _config.Utility.Attack.Survival * survivalPressure * neutralization +
                      _config.Utility.Attack.Activity * context.Needs.Activity +
                      _config.Utility.Attack.Rest * context.Needs.Rest -
                      (1 - context.RiskPreference) * threatRisk;
        return new AttackUtilityResult(utility, threatRisk, hit, expectedDamage, neutralization, survivalPressure);
    }

    public FleeUtilityResult FleeUtility(DecisionContext context, PerceivedEntity target)
    {
        var threatRisk = ThreatRisk(context, target);
        var secondStep = Math.Clamp(_config.Action.SecondStepFactor * context.EffectiveStats.Action, 0, 1);
        var safetyEffect = Math.Clamp((1 + secondStep) / 2, 0, 1);
        var survivalPressure = Math.Max(context.Needs.Survival, threatRisk);
        var utility = _config.Utility.Flee.Survival * survivalPressure * safetyEffect +
                      _config.Utility.Flee.Activity * context.Needs.Activity +
                      _config.Utility.Flee.Rest * context.Needs.Rest -
                      (1 - context.RiskPreference) * threatRisk;
        return new FleeUtilityResult(utility, threatRisk, safetyEffect, survivalPressure);
    }

    private void AddMove(DecisionContext context, int tick, int microRound, ICollection<ActionCandidate> output)
    {
        var destinations = context.Position.Neighbors()
            .Where(context.WorldRules.IsTraversableForIntent)
            .OrderBy(position => position)
            .ToArray();
        if (destinations.Length == 0)
        {
            return;
        }

        var stream = _random.Create("decision", tick, context.EntityId, "move-direction", microRound.ToString());
        var destination = destinations[stream.NextInt(destinations.Length)];
        var utility = NeedUtility(context.Needs, _config.Utility.Move);
        output.Add(Candidate(ActionKind.Move, null, destination, utility,
            new Dictionary<string, double> { ["needUtility"] = utility }));
    }

    private void AddRest(DecisionContext context, ICollection<ActionCandidate> output)
    {
        var utility = NeedUtility(context.Needs, _config.Utility.Rest);
        output.Add(Candidate(ActionKind.Rest, null, null, utility,
            new Dictionary<string, double> { ["needUtility"] = utility }));
    }

    private void AddCommunication(DecisionContext context, int tick, int microRound, ICollection<ActionCandidate> output)
    {
        var targets = context.Perception.Entities
            .Where(target => target.IsAlive != false && target.Position.HasValue &&
                             context.Position.ChebyshevDistance(target.Position.Value) <= _config.Communication.Range)
            .OrderBy(target => target.EntityId)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var target = SelectRandomTarget(targets, context.EntityId, tick, microRound, "communication-target");
        var utility = NeedUtility(context.Needs, _config.Utility.Communication);
        output.Add(Candidate(ActionKind.Communication, target.EntityId, null, utility,
            new Dictionary<string, double> { ["needUtility"] = utility }, target.Position));
    }

    private void AddAttack(DecisionContext context, ICollection<ActionCandidate> output)
    {
        foreach (var target in context.Perception.Threats
                     .Where(target => target.IsAlive != false && target.Position.HasValue &&
                                      context.Position.ChebyshevDistance(target.Position.Value) <= 1)
                     .OrderBy(target => target.EntityId))
        {
            var result = AttackUtility(context, target);
            output.Add(Candidate(ActionKind.Attack, target.EntityId, null, result.Utility,
                new Dictionary<string, double>
                {
                    ["threatRisk"] = result.ThreatRisk,
                    ["subjectiveHit"] = result.SubjectiveHitChance,
                    ["expectedDamage"] = result.ExpectedDamage,
                    ["neutralization"] = result.ThreatNeutralization
                }, target.Position));
        }
    }

    private void AddFlee(DecisionContext context, int tick, int microRound, ICollection<ActionCandidate> output)
    {
        var targets = context.Perception.Threats
            .Where(target => target.IsAlive != false && target.Position.HasValue &&
                             context.Position.ChebyshevDistance(target.Position.Value) <= 3)
            .Select(target => (Target: target, Risk: ThreatRisk(context, target)))
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var maximumRisk = targets.Max(item => item.Risk);
        var tied = targets.Where(item => Math.Abs(item.Risk - maximumRisk) < 1e-12)
            .OrderBy(item => item.Target.EntityId)
            .ToArray();
        var stream = _random.Create("decision", tick, context.EntityId, "primary-threat", microRound.ToString());
        var target = tied[stream.NextInt(tied.Length)].Target;
        var destinations = context.Position.Neighbors()
            .Where(context.WorldRules.IsTraversableForIntent)
            .Select(position => (Position: position, Distance: position.ChebyshevDistance(target.Position!.Value)))
            .ToArray();
        if (destinations.Length == 0)
        {
            return;
        }

        var maximumDistance = destinations.Max(item => item.Distance);
        var best = destinations.Where(item => item.Distance == maximumDistance)
            .Select(item => item.Position)
            .OrderBy(position => position)
            .ToArray();
        var directionStream = _random.Create("decision", tick, context.EntityId, "flee-direction", microRound.ToString());
        var destination = best[directionStream.NextInt(best.Length)];
        var utilityResult = FleeUtility(context, target);
        output.Add(Candidate(ActionKind.Flee, target.EntityId, destination, utilityResult.Utility,
            new Dictionary<string, double>
            {
                ["threatRisk"] = utilityResult.ThreatRisk,
                ["safetyEffect"] = utilityResult.SafetyEffect
            }));
    }

    private void AddReproduction(DecisionContext context, int tick, int microRound, ICollection<ActionCandidate> output)
    {
        if (context.AgeDays < _config.Reproduction.MatureAgeDays || context.ReproductionCooldownDays > 0 ||
            context.Needs.Reproduction < _config.Reproduction.NeedThreshold ||
            context.CurrentHp < context.EffectiveStats.MaxHp * _config.Reproduction.MinimumHpRatio)
        {
            return;
        }

        var targets = context.Perception.Entities
            .Where(target => target.IsAlive != false && target.Position.HasValue &&
                             target.LifeStage == PerceivedLifeStage.Mature &&
                             context.Position.ChebyshevDistance(target.Position.Value) <= _config.Reproduction.Range)
            .OrderBy(target => target.EntityId)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var target = SelectRandomTarget(targets, context.EntityId, tick, microRound, "reproduction-target");
        var utility = NeedUtility(context.Needs, _config.Utility.Reproduction);
        output.Add(Candidate(ActionKind.Reproduction, target.EntityId, null, utility,
            new Dictionary<string, double> { ["needUtility"] = utility }, target.Position));
    }

    private PerceivedEntity SelectRandomTarget(
        IReadOnlyList<PerceivedEntity> targets,
        long entityId,
        int tick,
        int microRound,
        string purpose)
    {
        var stream = _random.Create("decision", tick, entityId, purpose, microRound.ToString());
        return targets[stream.NextInt(targets.Count)];
    }

    private static ActionCandidate Candidate(
        ActionKind kind,
        long? targetId,
        Position? destination,
        double utility,
        IReadOnlyDictionary<string, double> breakdown,
        Position? perceivedTargetPosition = null)
    {
        var key = $"{kind}:{targetId?.ToString() ?? "-"}:{destination?.X.ToString() ?? "-"}:{destination?.Y.ToString() ?? "-"}";
        return new ActionCandidate(kind, targetId, destination, utility, key, breakdown, perceivedTargetPosition);
    }

    private static double NeedUtility(NeedsSnapshot needs, UtilityEffectConfig effect) =>
        needs.Survival * effect.Survival +
        needs.Rest * effect.Rest +
        needs.Activity * effect.Activity +
        needs.Communication * effect.Communication +
        needs.Reproduction * effect.Reproduction;
}

public sealed record AttackUtilityResult(
    double Utility,
    double ThreatRisk,
    double SubjectiveHitChance,
    double ExpectedDamage,
    double ThreatNeutralization,
    double SurvivalPressure);

public sealed record FleeUtilityResult(
    double Utility,
    double ThreatRisk,
    double SafetyEffect,
    double SurvivalPressure);
