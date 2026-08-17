using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Decision;
using Simulation.Core.Domain;
using Simulation.Core.Needs;
using Simulation.Core.Perception;
using Simulation.Core.Randomness;
using Simulation.Core.Reproduction;
using Simulation.Core.Social;

namespace Simulation.Core.Resolution;

public sealed record EventDraft(
    int MicroRound,
    SimulationEventType Type,
    long? ActorId,
    long? TargetId,
    Position? Position,
    bool Success,
    string Detail);

internal sealed record ReproductionResolution(bool Accepted, long? TargetId);

public sealed class ActionResolutionSystem
{
    private const int AttackPhase = 0;
    private const int ReproductionPhase = 1;
    private const int CommunicationPhase = 2;
    private const int MovementPhase = 3;
    private const int RestPhase = 4;
    private const int IdlePhase = 5;

    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;
    private readonly PerceptionSystem _perception;
    private readonly UtilityDecisionSystem _decision;
    private readonly CommunicationSystem _communication;
    private readonly ReproductionSystem _reproduction;
    private readonly NeedsSystem _needs;
    private readonly InvasionSystem _invasion;
    private readonly ConceptAuraSystem _aura;

    public ActionResolutionSystem(
        SimulationConfig config,
        RandomStreamFactory random,
        PerceptionSystem perception,
        UtilityDecisionSystem decision,
        CommunicationSystem communication,
        ReproductionSystem reproduction,
        NeedsSystem needs)
        : this(config, random, perception, decision, communication, reproduction, needs,
            new InvasionSystem(config, random), new ConceptAuraSystem(config, random))
    {
    }

    public ActionResolutionSystem(
        SimulationConfig config,
        RandomStreamFactory random,
        PerceptionSystem perception,
        UtilityDecisionSystem decision,
        CommunicationSystem communication,
        ReproductionSystem reproduction,
        NeedsSystem needs,
        InvasionSystem invasion,
        ConceptAuraSystem aura)
    {
        _config = config;
        _random = random;
        _perception = perception;
        _decision = decision;
        _communication = communication;
        _reproduction = reproduction;
        _needs = needs;
        _invasion = invasion;
        _aura = aura;
    }

    public void ResolveRound(
        WorldState world,
        IReadOnlyList<ActionIntent> intents,
        int microRound,
        Action<EventDraft> emit,
        Action<DecisionTrace>? recordInterruptDecision = null)
    {
        var pending = intents.ToDictionary(intent => intent.ActorId);
        var executed = new HashSet<long>();
        var attackInterruptUsed = new HashSet<long>();
        var reproductionInterruptUsed = new HashSet<long>();
        var restInterruptUsed = new HashSet<long>();

        ResolveTargetedPhase(AttackPhase);
        ResolveTargetedPhase(ReproductionPhase);
        ResolveTargetedPhase(CommunicationPhase);
        ResolveMovementPhase();
        ResolveSimplePhase(RestPhase);
        ResolveSimplePhase(IdlePhase);
        _invasion.ResolveVictories(world, EmitDomain, microRound);
        return;

        void EmitDomain(
            int round,
            SimulationEventType type,
            long? actorId,
            long? targetId,
            Position? position,
            bool success,
            string detail) => Emit(emit, round, type, actorId, targetId, position, success, detail);

        void ResolveTargetedPhase(int phase)
        {
            foreach (var scheduled in OrderForPhase(world, pending.Values, phase, microRound))
            {
                if (!pending.TryGetValue(scheduled.ActorId, out var intent) || PhaseOf(intent.Kind) != phase ||
                    !world.Npcs.TryGetValue(intent.ActorId, out var actor) || !actor.IsAlive)
                {
                    continue;
                }

                executed.Add(actor.Id);
                pending.Remove(actor.Id);
                switch (phase)
                {
                    case AttackPhase:
                    {
                        var attacked = ResolveExplicitAttack(world, actor, intent, microRound, emit);
                        foreach (var victimId in attacked.OrderBy(item => item))
                        {
                            ReplaceUnexecutedIntent(
                                victimId, phase, "attack", attackInterruptUsed, world, pending, executed,
                                microRound, emit, recordInterruptDecision);
                        }

                        break;
                    }
                    case ReproductionPhase:
                    {
                        var outcome = ResolveReproduction(world, actor, intent, microRound, emit);
                        if (outcome.Accepted && outcome.TargetId.HasValue)
                        {
                            ReplaceUnexecutedIntent(
                                outcome.TargetId.Value, phase, "reproduction-accepted", reproductionInterruptUsed,
                                world, pending, executed, microRound, emit, recordInterruptDecision);
                        }

                        break;
                    }
                    case CommunicationPhase:
                        ResolveCommunication(world, actor, intent, microRound, emit);
                        break;
                }

                _needs.ApplyActiveActionCost(actor, intent.Kind);
            }
        }

        void ResolveMovementPhase()
        {
            var roundStartOccupancy = world.Npcs.Values
                .Where(item => item.IsAlive)
                .ToDictionary(item => item.Position, item => item.Id);
            var claimedEmptyDestinations = new HashSet<Position>();
            foreach (var scheduled in OrderForPhase(world, pending.Values, MovementPhase, microRound))
            {
                if (!pending.TryGetValue(scheduled.ActorId, out var intent) || PhaseOf(intent.Kind) != MovementPhase ||
                    !world.Npcs.TryGetValue(intent.ActorId, out var actor) || !actor.IsAlive)
                {
                    continue;
                }

                executed.Add(actor.Id);
                pending.Remove(actor.Id);
                if (intent.Destination.HasValue)
                {
                    var destination = intent.Destination.Value;
                    if (!roundStartOccupancy.ContainsKey(destination) && !claimedEmptyDestinations.Add(destination))
                    {
                        var alternative = SelectAlternativeDestination(world, actor, intent, destination, microRound);
                        intent = intent with { Destination = alternative };
                        if (alternative.HasValue && !roundStartOccupancy.ContainsKey(alternative.Value))
                        {
                            claimedEmptyDestinations.Add(alternative.Value);
                        }
                    }
                }

                var collisionTarget = intent.Destination.HasValue
                    ? FindAliveAt(world, intent.Destination.Value)
                    : null;
                var collisionPolicy = collisionTarget is null
                    ? CollisionPolicy.Combat
                    : SettlementQueries.Collision(world, actor, collisionTarget, _config);
                var attacked = ResolveMovement(
                    world, actor, intent, microRound, intent.Kind == ActionKind.Flee, emit);
                _needs.ApplyActiveActionCost(actor, intent.Kind);
                foreach (var victimId in attacked.OrderBy(item => item))
                {
                    ReplaceUnexecutedIntent(
                        victimId, MovementPhase, "attack", attackInterruptUsed, world, pending, executed,
                        microRound, emit, recordInterruptDecision);
                }

                if (collisionTarget is { IsAlive: true } && collisionPolicy != CollisionPolicy.Combat &&
                    SettlementQueries.IsInsideAnyRestCollisionRegion(world, collisionTarget.Position, _config) &&
                    pending.TryGetValue(collisionTarget.Id, out var restIntent) && restIntent.Kind == ActionKind.Rest)
                {
                    ReplaceUnexecutedIntent(
                        collisionTarget.Id, MovementPhase, "rest-collision", restInterruptUsed, world, pending, executed,
                        microRound, emit, recordInterruptDecision);
                }
            }
        }

        void ResolveSimplePhase(int phase)
        {
            foreach (var scheduled in OrderForPhase(world, pending.Values, phase, microRound))
            {
                if (!pending.TryGetValue(scheduled.ActorId, out var intent) || PhaseOf(intent.Kind) != phase ||
                    !world.Npcs.TryGetValue(intent.ActorId, out var actor) || !actor.IsAlive)
                {
                    continue;
                }

                executed.Add(actor.Id);
                pending.Remove(actor.Id);
                if (intent.Kind == ActionKind.Rest)
                {
                    var orderCore = world.Phase == WorldPhase.Order &&
                                    SettlementQueries.FindActiveCore(world, actor.Position, _config) is not null;
                    _needs.ApplyRest(actor, orderCore ? _config.Settlement.OrderRestMultiplier : 1);
                    Emit(emit, microRound, SimulationEventType.Rest, actor.Id, null, actor.Position, true,
                        orderCore ? "order-core" : "standard");
                    _invasion.WithdrawForRest(world, actor, EmitDomain, microRound);
                }
                else
                {
                    Emit(emit, microRound, SimulationEventType.Idle, actor.Id, null, actor.Position, true,
                        "No perceived action candidate.");
                }

                _needs.ApplyActiveActionCost(actor, intent.Kind);
            }
        }
    }

    private IReadOnlyList<ActionIntent> OrderForPhase(
        WorldState world,
        IEnumerable<ActionIntent> intents,
        int phase,
        int microRound) => intents
        .Where(intent => PhaseOf(intent.Kind) == phase)
        .OrderByDescending(intent => world.Npcs.TryGetValue(intent.ActorId, out var npc) && npc.IsAlive
            ? npc.EffectiveStats(_config).Action
            : double.NegativeInfinity)
        .ThenBy(intent => _random.StablePriority(
            "scheduling", world.Tick, intent.ActorId, "intent-conflict", $"{microRound}:{phase}"))
        .ThenBy(intent => intent.ActorId)
        .ToArray();

    private void ReplaceUnexecutedIntent(
        long actorId,
        int currentPhase,
        string reason,
        ISet<long> used,
        WorldState world,
        IDictionary<long, ActionIntent> pending,
        IReadOnlySet<long> executed,
        int microRound,
        Action<EventDraft> emit,
        Action<DecisionTrace>? recordInterruptDecision)
    {
        if (executed.Contains(actorId) || !pending.TryGetValue(actorId, out var oldIntent) || !used.Add(actorId))
        {
            return;
        }

        if (!world.Npcs.TryGetValue(actorId, out var actor) || !actor.IsAlive)
        {
            pending.Remove(actorId);
            Emit(emit, microRound, SimulationEventType.IntentReplaced, actorId, null, actor?.Position, false,
                $"reason={reason};old={oldIntent.Kind};new=-;status=dead");
            return;
        }

        _needs.RefreshSurvival(actor);
        var trace = _decision.Decide(CreateDecisionContext(world, actor), world.Tick, microRound) with
        {
            DecisionReason = reason
        };
        recordInterruptDecision?.Invoke(trace);
        var replacement = _decision.CreateIntent(trace, $"interrupt:{reason}");
        var replacementPhase = PhaseOf(replacement.Kind);
        var status = replacementPhase > currentPhase ? "pending" : "expired";
        if (replacementPhase > currentPhase)
        {
            pending[actorId] = replacement;
        }
        else
        {
            pending.Remove(actorId);
        }

        Emit(emit, microRound, SimulationEventType.IntentReplaced, actorId, replacement.TargetId, actor.Position, true,
            $"reason={reason};old={oldIntent.Kind};new={replacement.Kind};status={status}");
    }

    private static int PhaseOf(ActionKind kind) => kind switch
    {
        ActionKind.Attack => AttackPhase,
        ActionKind.Reproduction => ReproductionPhase,
        ActionKind.Communication => CommunicationPhase,
        ActionKind.Move or ActionKind.Flee => MovementPhase,
        ActionKind.Rest => RestPhase,
        ActionKind.Idle => IdlePhase,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private Position? SelectAlternativeDestination(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        Position attempted,
        int microRound)
    {
        var landmarks = world.Landmarks.Select(item => item.Position).ToHashSet();
        var alternatives = actor.Position.Neighbors()
            .Where(position => IsInside(position))
            .Where(position => !landmarks.Contains(position) && position != attempted)
            .OrderBy(position => position)
            .ToArray();
        if (alternatives.Length == 0)
        {
            return null;
        }

        var stream = _random.Create("spatial", world.Tick, actor.Id, "move-conflict-retry", $"{microRound}:{intent.IntentId}");
        return alternatives[stream.NextInt(alternatives.Length)];
    }

    private IReadOnlySet<long> ResolveMovement(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        int microRound,
        bool fleeing,
        Action<EventDraft> emit)
    {
        var attacked = new HashSet<long>();
        if (!intent.Destination.HasValue || !IsInside(intent.Destination.Value) || IsLandmark(world, intent.Destination.Value))
        {
            Emit(emit, microRound, SimulationEventType.MoveFailed, actor.Id, intent.TargetId, actor.Position, false,
                "Destination is invalid.");
            return attacked;
        }

        var original = actor.Position;
        var destination = intent.Destination.Value;
        var occupant = FindAliveAt(world, destination);
        if (occupant is not null)
        {
            var policy = SettlementQueries.Collision(world, actor, occupant, _config);
            if (policy == CollisionPolicy.Combat)
            {
                ResolveAttack(world, actor, occupant, microRound, SimulationEventType.CollisionAttack, true, 1, emit, attacked);
            }
            else
            {
                var reason = policy switch
                {
                    CollisionPolicy.SameSettlementSuppressed => "same-settlement",
                    CollisionPolicy.UnaffiliatedProtected => "unaffiliated-protected",
                    CollisionPolicy.OtherSettlementFriction => "other-settlement-friction",
                    _ => throw new ArgumentOutOfRangeException()
                };
                Emit(emit, microRound, SimulationEventType.CollisionSuppressed, actor.Id, occupant.Id, actor.Position, true,
                    $"reason={reason}");
                if (policy == CollisionPolicy.OtherSettlementFriction && actor.SettlementId.HasValue &&
                    occupant.SettlementId.HasValue)
                {
                    SettlementQueries.AddFriction(
                        world, actor.SettlementId.Value, occupant.SettlementId.Value,
                        _config.Settlement.FrictionCollisionIncrease, "collision",
                        (round, type, actorId, targetId, position, success, detail) =>
                            Emit(emit, round, type, actorId, targetId, position, success, detail),
                        microRound);
                }
            }
            return attacked;
        }

        actor.Position = destination;
        var eventType = fleeing ? SimulationEventType.Flee : SimulationEventType.Move;
        Emit(emit, microRound, eventType, actor.Id, intent.TargetId, destination, true, $"{original}->{destination}");

        var probability = Math.Clamp(_config.Action.SecondStepFactor * actor.EffectiveStats(_config).Action, 0, 1);
        var secondStepStream = _random.Create("spatial", world.Tick, actor.Id, "second-step", $"{microRound}:{intent.IntentId}");
        if (secondStepStream.NextDouble() < probability)
        {
            var deltaX = Math.Sign(destination.X - original.X);
            var deltaY = Math.Sign(destination.Y - original.Y);
            var second = new Position(destination.X + deltaX, destination.Y + deltaY);
            if (IsInside(second) && !IsLandmark(world, second) && FindAliveAt(world, second) is null)
            {
                actor.Position = second;
                Emit(emit, microRound, eventType, actor.Id, intent.TargetId, second, true,
                    $"second-step {destination}->{second}");
            }
        }

        if (fleeing && intent.TargetId.HasValue)
        {
            foreach (var victimId in ResolvePursuit(world, actor, intent.TargetId.Value, microRound, emit))
            {
                attacked.Add(victimId);
            }
        }

        return attacked;
    }

    private void ResolveCommunication(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        int microRound,
        Action<EventDraft> emit)
    {
        actor.Needs.Communication += _config.Needs.InitiatedCommunicationChange;
        actor.Needs.ClampAll();
        if (!TryResolveTarget(world, actor, intent, _config.Communication.Range, microRound, emit, out var target))
        {
            Emit(emit, microRound, SimulationEventType.Communication, actor.Id, intent.TargetId, actor.Position, false,
                "target-absent");
            return;
        }

        var result = _communication.Exchange(actor, target, world.Tick, microRound);
        Emit(emit, microRound, SimulationEventType.Communication, actor.Id, target.Id, actor.Position, true,
            $"sent={result.SentByInitiator},received={result.SentByTarget}");
    }

    private IReadOnlySet<long> ResolveExplicitAttack(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        int microRound,
        Action<EventDraft> emit)
    {
        var attacked = new HashSet<long>();
        if (!TryResolveTarget(world, actor, intent, 1, microRound, emit, out var target))
        {
            Emit(emit, microRound, SimulationEventType.Attack, actor.Id, intent.TargetId, actor.Position, false,
                "target-absent");
            return attacked;
        }

        var protection = SettlementQueries.ExplicitAttackProtection(world, actor, target, _config);
        if (protection is not null)
        {
            Emit(emit, microRound, SimulationEventType.AttackSuppressed, actor.Id, target.Id, actor.Position, false,
                $"reason={protection}");
            return attacked;
        }

        if (world.Phase == WorldPhase.Order && actor.SettlementId.HasValue && target.SettlementId.HasValue &&
            actor.SettlementId != target.SettlementId && !SettlementQueries.AreInvasionOpponents(world, actor, target))
        {
            SettlementQueries.AddFriction(
                world, actor.SettlementId.Value, target.SettlementId.Value,
                _config.Settlement.FrictionExplicitThreatIncrease, "explicit-threat",
                (round, type, actorId, targetId, position, success, detail) =>
                    Emit(emit, round, type, actorId, targetId, position, success, detail),
                microRound);
        }
        else if (world.Phase == WorldPhase.Order && actor.SettlementId.HasValue && !target.SettlementId.HasValue &&
                 SettlementQueries.IsInsideInfluence(world, actor.SettlementId.Value, target.Position, _config) &&
                 SettlementQueries.HasActiveThreat(actor, target.Id, world.Tick, _config))
        {
            world.UnaffiliatedThreatExceptionAttackCount++;
        }

        ResolveAttack(world, actor, target, microRound, SimulationEventType.Attack, true, 1, emit, attacked);
        return attacked;
    }

    private bool TryResolveTarget(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        int range,
        int microRound,
        Action<EventDraft> emit,
        out NpcState target)
    {
        if (intent.TargetId.HasValue && intent.PerceivedTargetPosition.HasValue &&
            world.Npcs.TryGetValue(intent.TargetId.Value, out var found) && found.IsAlive &&
            found.Position == intent.PerceivedTargetPosition.Value &&
            actor.Position.ChebyshevDistance(found.Position) <= range)
        {
            target = found;
            return true;
        }

        if (intent.TargetId.HasValue && _perception.InvalidatePosition(actor, intent.TargetId.Value))
        {
            Emit(emit, microRound, SimulationEventType.TargetPositionInvalidated, actor.Id, intent.TargetId,
                actor.Position, true, "target-absent;properties=PositionX,PositionY");
        }

        target = null!;
        return false;
    }

    private void ResolveAttack(
        WorldState world,
        NpcState attacker,
        NpcState defender,
        int microRound,
        SimulationEventType eventType,
        bool allowCounterattack,
        double attackerCombatFactor,
        Action<EventDraft> emit,
        ISet<long> attacked)
    {
        if (!attacker.IsAlive || !defender.IsAlive)
        {
            return;
        }

        attacked.Add(defender.Id);
        _perception.RecordThreat(defender, attacker, world.Tick);
        var attackerCombat = attacker.EffectiveStats(_config).Combat * attackerCombatFactor;
        var defenderCombat = defender.EffectiveStats(_config).Combat;
        var hitChance = Math.Clamp(
            _config.Combat.HitChanceBase + _config.Combat.HitChancePerCombatDifference * (attackerCombat - defenderCombat),
            _config.Combat.HitChanceMinimum,
            _config.Combat.HitChanceMaximum);
        var scope = $"{microRound}:{eventType}:{defender.Id}";
        var hit = _random.Create("combat", world.Tick, attacker.Id, "hit", scope).NextDouble() < hitChance;
        if (!hit)
        {
            Emit(emit, microRound, eventType, attacker.Id, defender.Id, attacker.Position, false, "miss");
        }
        else
        {
            var randomFactor = _random.Create("combat", world.Tick, attacker.Id, "damage", scope)
                .NextDouble(_config.Combat.DamageRandomMinimum, _config.Combat.DamageRandomMaximum);
            var damage = Math.Max(
                _config.Combat.DamageMinimum,
                _config.Combat.DamageBase +
                _config.Combat.DamageAttackerFactor * attackerCombat -
                _config.Combat.DamageDefenderFactor * defenderCombat) * randomFactor;
            defender.CurrentHp -= damage;
            var died = defender.CurrentHp <= 0;
            if (died)
            {
                defender.CurrentHp = 0;
                defender.IsAlive = false;
            }

            _needs.RefreshSurvival(defender);
            Emit(emit, microRound, eventType, attacker.Id, defender.Id, attacker.Position, true,
                FormattableString.Invariant($"damage={damage:F4}"));
            if (died)
            {
                defender.SettlementAtDeathId = defender.SettlementId;
                defender.DeathAgeDays = defender.AgeDays;
                defender.DeathCause = $"combat:{eventType}";
                Emit(emit, microRound, SimulationEventType.Death, defender.Id, attacker.Id, defender.Position, true,
                    $"combat:{eventType}");
                _invasion.NotifyDeath(
                    world,
                    defender,
                    (round, type, actorId, targetId, position, success, detail) =>
                        Emit(emit, round, type, actorId, targetId, position, success, detail),
                    microRound);
            }
        }

        _perception.RecordCombatOutcome(attacker, defender, world.Tick);
        _perception.RecordCombatOutcome(defender, attacker, world.Tick);

        if (allowCounterattack && defender.IsAlive && attacker.IsAlive &&
            defender.Position.ChebyshevDistance(attacker.Position) <= 1)
        {
            ResolveAttack(world, defender, attacker, microRound, SimulationEventType.Counterattack, false,
                _config.Combat.CounterattackCombatFactor, emit, attacked);
        }
    }

    private IReadOnlySet<long> ResolvePursuit(
        WorldState world,
        NpcState fleeing,
        long pursuerId,
        int microRound,
        Action<EventDraft> emit)
    {
        var attacked = new HashSet<long>();
        if (!world.Npcs.TryGetValue(pursuerId, out var pursuer) || !pursuer.IsAlive || !fleeing.IsAlive)
        {
            return attacked;
        }

        _perception.RecordCombatOutcome(pursuer, fleeing, world.Tick);
        _needs.RefreshSurvival(pursuer);
        var context = CreateDecisionContext(world, pursuer);
        var perceived = context.Perception.Find(fleeing.Id);
        if (perceived is null)
        {
            return attacked;
        }

        var attack = _decision.AttackUtility(context, perceived);
        var selfHpRatio = context.EffectiveStats.MaxHp <= 0 ? 0 : context.CurrentHp / context.EffectiveStats.MaxHp;
        var pursuitRisk = Math.Clamp(
            5 + 0.5 * ((perceived.Combat ?? context.EffectiveStats.Combat) - context.EffectiveStats.Combat) + 5 * (1 - selfHpRatio),
            0,
            10);
        var pursueUtility = 0.5 * attack.Utility + 0.20 * context.Needs.Activity - 0.25 * context.Needs.Rest -
                             (1 - context.RiskPreference) * pursuitRisk;
        var disengageUtility = 0.25 * context.Needs.Rest + (1 - context.RiskPreference) * pursuitRisk;
        var choices = new[]
        {
            new ActionCandidate(ActionKind.Attack, fleeing.Id, null, pursueUtility, "pursue", new Dictionary<string, double>()),
            new ActionCandidate(ActionKind.Idle, fleeing.Id, null, disengageUtility, "disengage", new Dictionary<string, double>())
        };
        var trace = UtilityDecisionSystem.SelectWeighted(
            pursuer.Id,
            world.Tick,
            microRound,
            choices,
            2,
            _config.Utility.Temperature,
            _random.Create("combat", world.Tick, pursuer.Id, "pursuit-choice", $"{microRound}:{fleeing.Id}"),
            "PursuitChoice");
        if (trace.Selected.StableKey != "pursue")
        {
            return attacked;
        }

        var chance = Math.Clamp(
            0.50 + 0.05 * (pursuer.EffectiveStats(_config).Action - fleeing.EffectiveStats(_config).Action),
            0.20,
            0.80);
        if (_random.Create("combat", world.Tick, pursuer.Id, "pursuit-catch", $"{microRound}:{fleeing.Id}").NextDouble() >= chance)
        {
            Emit(emit, microRound, SimulationEventType.Pursuit, pursuer.Id, fleeing.Id, pursuer.Position, false, "catch failed");
            return attacked;
        }

        Emit(emit, microRound, SimulationEventType.Pursuit, pursuer.Id, fleeing.Id, pursuer.Position, true, "catch succeeded");
        ResolveAttack(world, pursuer, fleeing, microRound, SimulationEventType.Pursuit, false, 1, emit, attacked);
        return attacked;
    }

    private ReproductionResolution ResolveReproduction(
        WorldState world,
        NpcState actor,
        ActionIntent intent,
        int microRound,
        Action<EventDraft> emit)
    {
        if (!TryResolveTarget(world, actor, intent, _config.Reproduction.Range, microRound, emit, out var target))
        {
            Emit(emit, microRound, SimulationEventType.ReproductionAttempt, actor.Id, intent.TargetId, actor.Position, true,
                "attempt;scope=unknown");
            Emit(emit, microRound, SimulationEventType.ReproductionFailure, actor.Id, intent.TargetId, actor.Position, false,
                "target-absent");
            return new ReproductionResolution(false, intent.TargetId);
        }

        var sameCore = world.Phase != WorldPhase.Order ||
                       SettlementQueries.SameActiveCore(world, actor.Position, target.Position, _config);
        var scope = sameCore ? "same-core" : "outside-penalty";
        Emit(emit, microRound, SimulationEventType.ReproductionAttempt, actor.Id, target.Id, actor.Position, true,
            $"attempt;scope={scope}");

        var failure = ReproductionFailureReason(actor, target);
        if (failure is not null)
        {
            Emit(emit, microRound, SimulationEventType.ReproductionFailure, actor.Id, target.Id, actor.Position, false,
                $"{failure};scope={scope}");
            return new ReproductionResolution(false, target.Id);
        }

        var acceptancePenalty = sameCore ? 0 : _config.Settlement.OutsideReproductionUtilityPenalty;
        if (!_reproduction.Accepts(target, world.Tick, microRound, actor.Id, acceptancePenalty))
        {
            Emit(emit, microRound, SimulationEventType.ReproductionFailure, actor.Id, target.Id, actor.Position, false,
                $"rejected;scope={scope}");
            return new ReproductionResolution(false, target.Id);
        }

        actor.Needs.Reproduction += _config.Needs.SuccessfulReproductionChange;
        target.Needs.Reproduction += _config.Needs.SuccessfulReproductionChange;
        actor.Needs.ClampAll();
        target.Needs.ClampAll();
        actor.ReproductionCooldownDays = _config.Reproduction.CooldownDays;
        target.ReproductionCooldownDays = _config.Reproduction.CooldownDays;
        var birthSettlementId = SettlementQueries.BirthSettlement(world, actor, target, _config);
        world.BirthRequests.Add(_reproduction.CreateRequest(
            actor, target, world.Tick, microRound, birthSettlementId));
        Emit(emit, microRound, SimulationEventType.ReproductionSuccess, actor.Id, target.Id, actor.Position, true,
            $"birth request queued;scope={scope};birth-settlement={birthSettlementId?.ToString() ?? "-"}");
        return new ReproductionResolution(true, target.Id);
    }

    private string? ReproductionFailureReason(NpcState actor, NpcState target)
    {
        if (!actor.IsAlive || !target.IsAlive)
        {
            return "target-absent";
        }

        if (!actor.IsMature(_config) || !target.IsMature(_config))
        {
            return "maturity";
        }

        if (actor.ReproductionCooldownDays > 0 || target.ReproductionCooldownDays > 0)
        {
            return "cooldown";
        }

        if (actor.CurrentHp < actor.EffectiveStats(_config).MaxHp * _config.Reproduction.MinimumHpRatio ||
            target.CurrentHp < target.EffectiveStats(_config).MaxHp * _config.Reproduction.MinimumHpRatio)
        {
            return "hp";
        }

        if (actor.Needs.Reproduction < _config.Reproduction.NeedThreshold)
        {
            return "other-reality-failure";
        }

        return null;
    }

    private DecisionContext CreateDecisionContext(WorldState world, NpcState npc)
    {
        var activeCores = SettlementQueries.ActiveSettlements(world)
            .Select(item => new SettlementCoreRule(item.Id, item.Center, _config.Settlement.CoreRadius))
            .ToArray();
        var suppressedTargets = world.Npcs.Values
            .Where(item => item.IsAlive && item.Id != npc.Id &&
                           SettlementQueries.ExplicitAttackProtection(world, npc, item, _config) is not null)
            .Select(item => item.Id)
            .ToHashSet();
        var movementTarget = _invasion.MovementTarget(world, npc);
        var rules = new WorldDecisionRules(
            _config.World.Width,
            _config.World.Height,
            world.Landmarks.Select(item => item.Position).ToHashSet(),
            activeCores,
            suppressedTargets,
            movementTarget,
            npc.HasAdvanceBias ? _config.Invasion.AdvanceBiasWeight :
                npc.HasDefenseBias ? _config.Invasion.DefenseBiasWeight : 0,
            _aura.FindCohesionTarget(world, npc),
            _config.Invasion.AuraCohesionWeight,
            world.Phase == WorldPhase.Order);
        var perception = _perception.CreateView(npc, world.Tick);
        world.AttackCandidateSuppressionCount += perception.Threats.LongCount(item =>
            rules.IsAttackSuppressed(item.EntityId));
        return new DecisionContext(
            npc.Id,
            npc.Position,
            npc.CurrentHp,
            npc.EffectiveStats(_config),
            npc.RiskPreference,
            npc.AgeDays,
            npc.ReproductionCooldownDays,
            npc.Needs.Snapshot(),
            perception,
            rules);
    }

    private bool IsInside(Position position) =>
        position.X >= 0 && position.X < _config.World.Width && position.Y >= 0 && position.Y < _config.World.Height;

    private static bool IsLandmark(WorldState world, Position position) =>
        world.Landmarks.Any(item => item.Position == position);

    private static NpcState? FindAliveAt(WorldState world, Position position) =>
        world.Npcs.Values.FirstOrDefault(item => item.IsAlive && item.Position == position);

    private static void Emit(
        Action<EventDraft> emit,
        int microRound,
        SimulationEventType type,
        long? actorId,
        long? targetId,
        Position? position,
        bool success,
        string detail) => emit(new EventDraft(microRound, type, actorId, targetId, position, success, detail));
}
