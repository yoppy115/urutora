using System.Reflection;
using FsCheck;
using FsCheck.Fluent;
using Simulation.Core;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Decision;
using Simulation.Core.Domain;
using Simulation.Core.Lifecycle;
using Simulation.Core.Needs;
using Simulation.Core.Perception;
using Simulation.Core.Randomness;
using Simulation.Core.Reproduction;
using Simulation.Core.Resolution;

namespace Simulation.Core.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        ("configuration schema is strict", ConfigurationSchemaIsStrict),
        ("v0.15 defaults and initial ages are applied", V015DefaultsAndInitialAges),
        ("partitioned RNG is deterministic and local", PartitionedRandomIsDeterministicAndLocal),
        ("utility candidate count and edge rules", UtilityCandidateRules),
        ("decision public API cannot receive Reality", DecisionApiCannotReceiveReality),
        ("observation error and unobserved Reality boundary", ObservationAndSubjectiveBoundary),
        ("communication stays within held information", CommunicationUsesHeldInformationOnly),
        ("held information is FIFO bounded and TargetAbsent only clears position", HeldInformationAndTargetAbsent),
        ("reproduction candidates stay subjective and Reality rejects later", ReproductionCandidateBoundary),
        ("targeted phases precede movement and attack interrupt is bounded", TargetedPhaseAndInterrupt),
        ("Move conflict is input-order independent", MoveConflictIsInputOrderIndependent),
        ("combat reactions cannot recurse", CombatReactionCannotRecurse),
        ("collision attack causes immediate death without movement", CollisionAttackAndImmediateDeath),
        ("failed active action still pays Need cost", FailedActiveActionPaysNeedCost),
        ("vitality curve is continuous and eventually lethal", VitalityCurve),
        ("ConceptMark preserves Base stats", ConceptMarkPreservesBaseStats),
        ("genetics allowlist excludes acquired state", GeneticsAllowlist),
        ("birth batch arbitration is queue-order independent", BirthArbitrationIsOrderIndependent),
        ("NPC detail projection exposes stable lineage", NpcDetailProjectionExposesStableLineage),
        ("NPC action history excludes movement events", NpcActionHistoryExcludesMovementEvents),
        ("world statistics count selected action commands", WorldStatisticsCountSelectedActions),
        ("whole run and render frequency are deterministic", WholeRunAndRenderDeterminism),
        ("FsCheck generated runs remain deterministic", FsCheckGeneratedRunsRemainDeterministic),
        ("daily Micro Rounds respect maximum actions", MaximumActionsPerDay)
    };

    private static int Main()
    {
        var failures = new List<string>();
        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures.Add(name);
                Console.WriteLine($"FAIL {name}");
                Console.WriteLine(exception);
            }
        }

        Console.WriteLine($"TEST_SUMMARY total={Tests.Length} passed={Tests.Length - failures.Count} failed={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void ConfigurationSchemaIsStrict()
    {
        var config = LoadConfig();
        Equal(64, config.World.Width);
        var original = File.ReadAllText(ConfigPath());
        var invalidPath = Path.Combine(Path.GetTempPath(), $"world-sim-invalid-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(invalidPath, original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"unknown\": true", StringComparison.Ordinal));
            Throws<ConfigurationException>(() => SimulationConfigLoader.Load(invalidPath));
        }
        finally
        {
            if (File.Exists(invalidPath))
            {
                File.Delete(invalidPath);
            }
        }
    }

    private static void V015DefaultsAndInitialAges()
    {
        var config = LoadConfig();
        Equal("v0.15-default-1", config.Id);
        Equal(180, config.Reproduction.MatureAgeDays);
        Equal(90, config.Reproduction.CooldownDays);
        Equal(90, config.Observation.ThreatMemoryDays);
        Equal(3, config.Observation.HeldInformationCapacityPerSubjectProperty);
        Equal(0.04, config.Needs.DailyReproductionIncrease, 0);
        Equal(50, config.InitialPopulation.MaxHpMean, 0);
        Equal(4, config.Combat.DamageBase, 0);
        Equal(0.9, config.Combat.DamageAttackerFactor, 0);
        Equal(0.4, config.Combat.DamageDefenderFactor, 0);

        var world = Simulation.Core.World.WorldFactory.Create(config, new RandomStreamFactory(915));
        True(world.Npcs.Values.All(item => item.AgeDays is >= 180 and <= 700),
            "Initial age escaped the v0.15 day range.");
    }

    private static void PartitionedRandomIsDeterministicAndLocal()
    {
        var firstFactory = new RandomStreamFactory(8147291);
        var secondFactory = new RandomStreamFactory(8147291);
        var first = firstFactory.Create("utility", 12, 42, "choice", "0");
        var second = secondFactory.Create("utility", 12, 42, "choice", "0");
        for (var index = 0; index < 10; index++)
        {
            Equal(first.NextUInt64(), second.NextUInt64());
        }

        var baseline = firstFactory.Create("combat", 9, 7, "damage", "target:8").NextUInt64();
        _ = firstFactory.Create("unrelated", 9, 7, "diagnostic").NextUInt64();
        var afterUnrelated = firstFactory.Create("combat", 9, 7, "damage", "target:8").NextUInt64();
        Equal(baseline, afterUnrelated);
        NotEqual(
            firstFactory.Create("combat", 9, 7, "hit", "target:8").NextUInt64(),
            firstFactory.Create("combat", 9, 7, "damage", "target:8").NextUInt64());
    }

    private static void UtilityCandidateRules()
    {
        var candidates = new[]
        {
            Candidate("a", 4),
            Candidate("b", 3),
            Candidate("c", -2),
            Candidate("d", -3)
        };
        var selected = UtilityDecisionSystem.SelectWeighted(1, 0, 1, candidates, 3, 1,
            new DeterministicRandom(1), "test");
        Equal(3, selected.WeightedCandidates.Count);
        True(selected.WeightedCandidates.All(item => item.StableKey != "d"), "Top 3 included the fourth candidate.");

        var idle = UtilityDecisionSystem.SelectWeighted(1, 0, 1, Array.Empty<ActionCandidate>(), 3, 1,
            new DeterministicRandom(1), "test");
        Equal(ActionKind.Idle, idle.Selected.Kind);

        var one = UtilityDecisionSystem.SelectWeighted(1, 0, 1, new[] { Candidate("only", -10) }, 3, 1,
            new DeterministicRandom(1), "test");
        Equal("only", one.Selected.StableKey);

        var two = UtilityDecisionSystem.SelectWeighted(1, 0, 1, new[] { Candidate("x", -1), Candidate("y", -1) }, 3, 1,
            new DeterministicRandom(1), "test");
        Equal(2, two.WeightedCandidates.Count);

        var tiedForward = UtilityDecisionSystem.SelectWeighted(1, 0, 1,
            new[] { Candidate("b", 1), Candidate("a", 1), Candidate("c", 1), Candidate("d", 1) }, 3, 1,
            new DeterministicRandom(99), "test");
        var tiedReverse = UtilityDecisionSystem.SelectWeighted(1, 0, 1,
            new[] { Candidate("d", 1), Candidate("c", 1), Candidate("a", 1), Candidate("b", 1) }, 3, 1,
            new DeterministicRandom(99), "test");
        Equal(tiedForward.Selected.StableKey, tiedReverse.Selected.StableKey);
        SequenceEqual(
            tiedForward.WeightedCandidates.Select(item => item.StableKey),
            tiedReverse.WeightedCandidates.Select(item => item.StableKey));
        Throws<ArgumentOutOfRangeException>(() => UtilityDecisionSystem.SelectWeighted(
            1, 0, 1, candidates, 3, 0, new DeterministicRandom(1), "test"));
    }

    private static void DecisionApiCannotReceiveReality()
    {
        var forbidden = new[] { typeof(WorldState), typeof(NpcState) };
        var methods = typeof(UtilityDecisionSystem).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            foreach (var parameter in method.GetParameters())
            {
                True(!forbidden.Contains(parameter.ParameterType),
                    $"{method.Name} exposes Reality type {parameter.ParameterType.Name}.");
            }
        }
    }

    private static void ObservationAndSubjectiveBoundary()
    {
        var config = LoadConfig();
        var world = EmptyWorld(config);
        var observer = Npc(1, new Position(0, 0), action: 5, combat: 5, communication: 5, hp: 100);
        var subject = Npc(2, new Position(3, 3), action: 5, combat: 8, communication: 5, hp: 80);
        world.Npcs.Add(observer.Id, observer);
        world.Npcs.Add(subject.Id, subject);
        world.NextNpcId = 3;
        var random = new RandomStreamFactory(1234);
        var perceptionSystem = new PerceptionSystem(config, random);
        perceptionSystem.Observe(world);
        var view = perceptionSystem.CreateView(observer, 0);
        var perceived = view.Find(subject.Id)!;
        True(Math.Abs(perceived.CurrentHp!.Value / subject.CurrentHp - 1) <= 0.10 + 1e-12, "HP observation exceeded distance error.");
        True(Math.Abs(perceived.Combat!.Value / 8 - 1) <= 0.10 + 1e-12, "Combat observation exceeded distance error.");

        var rules = new WorldDecisionRules(config.World.Width, config.World.Height, world.Landmarks.Select(item => item.Position).ToHashSet());
        observer.ThreatMemory[subject.Id] = new ThreatMemory(subject.Id, 0);
        var contextBefore = DecisionContextFor(observer, config, perceptionSystem.CreateView(observer, 0), rules);
        var decision = new UtilityDecisionSystem(config, random);
        var riskBefore = decision.ThreatRisk(contextBefore, contextBefore.Perception.Find(subject.Id)!);
        subject.CurrentHp = 1;
        var contextAfter = DecisionContextFor(observer, config, perceptionSystem.CreateView(observer, 0), rules);
        var riskAfter = decision.ThreatRisk(contextAfter, contextAfter.Perception.Find(subject.Id)!);
        Equal(riskBefore, riskAfter, 1e-12);

        observer.HeldInformation.Add(new InformationRecord(
            "new-perceived-combat", subject.Id, InformationProperty.Combat, 0, 1, observer.Id,
            InformationAcquisition.DirectOutcome, 1));
        var changedContext = DecisionContextFor(observer, config, perceptionSystem.CreateView(observer, 1), rules);
        var changedTarget = changedContext.Perception.Find(subject.Id)!;
        True(Math.Abs(decision.AttackUtility(contextBefore, perceived).Utility -
                      decision.AttackUtility(changedContext, changedTarget).Utility) > 1e-9,
            "PerceivedCombat did not change Attack Utility.");
        True(Math.Abs(decision.FleeUtility(contextBefore, perceived).Utility -
                      decision.FleeUtility(changedContext, changedTarget).Utility) > 1e-9,
            "PerceivedCombat did not change Flee Utility.");
    }

    private static void CommunicationUsesHeldInformationOnly()
    {
        var config = LoadConfig();
        var random = new RandomStreamFactory(77);
        var sender = Npc(1, new Position(0, 0), action: 5, combat: 5, communication: 5, hp: 100);
        var receiver = Npc(2, new Position(1, 0), action: 5, combat: 5, communication: 10, hp: 100);
        sender.HeldInformation.Add(new InformationRecord(
            "known", 99, InformationProperty.Combat, 7, 0.8, sender.Id,
            InformationAcquisition.Observation, 0));
        var system = new CommunicationSystem(config, random);
        var result = system.Exchange(sender, receiver, 1, 1);
        Equal(1, result.SentByInitiator);
        Equal(1, receiver.HeldInformation.Count);
        Equal(99L, receiver.HeldInformation[0].SubjectId);
        True(receiver.HeldInformation[0].Confidence <= 0.8, "Transmission increased confidence.");
        True(system.ErrorMaximum(12) >= 0, "Communication error became negative above ability 10.");
        True(system.SubjectSwapChance(12) >= 0, "Subject swap chance became negative above ability 10.");
    }

    private static void HeldInformationAndTargetAbsent()
    {
        var config = LoadConfig();
        var random = new RandomStreamFactory(415);
        var perception = new PerceptionSystem(config, random);
        var observer = Npc(1, new Position(1, 1), 5, 5, 5, 100);
        for (var index = 1; index <= 4; index++)
        {
            perception.AddInformation(
                observer, 2, InformationProperty.Combat, index, index == 4 ? 0.01 : 1,
                observer.Id, index, InformationAcquisition.Observation);
        }

        SequenceEqual(new[] { 2d, 3d, 4d }, observer.HeldInformation
            .Where(item => item.SubjectId == 2 && item.Property == InformationProperty.Combat)
            .Select(item => item.EstimatedValue));
        Equal(1L, perception.EvictionCount);

        observer.HeldInformation.AddRange(PerceivedPositionRecords(observer.Id, 2, new Position(2, 1), 5));
        var target = Npc(2, new Position(3, 1), 5, 5, 5, 100);
        var world = EmptyWorld(config);
        world.Npcs.Add(observer.Id, observer);
        world.Npcs.Add(target.Id, target);
        var attack = TargetedIntent(observer.Id, ActionKind.Attack, target.Id, new Position(2, 1));
        var events = ResolveRound(config, world, new[] { attack });
        True(events.Any(item => item.Type == SimulationEventType.Attack && item.Detail == "target-absent"));
        True(events.Any(item => item.Type == SimulationEventType.TargetPositionInvalidated));
        True(observer.HeldInformation.Any(item => item.SubjectId == target.Id &&
                                                  item.Property == InformationProperty.Alive),
            "TargetAbsent purged non-position information.");
        True(observer.HeldInformation.All(item => item.SubjectId != target.Id ||
                                                item.Property is not InformationProperty.PositionX and
                                                    not InformationProperty.PositionY),
            "TargetAbsent retained stale position information.");

        target.IsAlive = false;
        perception.RecordCombatOutcome(observer, target, 6);
        True(observer.HeldInformation.All(item => item.SubjectId != target.Id),
            "Directly confirmed disappearance did not purge the subject.");
    }

    private static void MoveConflictIsInputOrderIndependent()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.InitialPopulation = 3;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 7, Y = 7 },
            new() { Concept = "survival", X = 6, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 6 }
        };
        var firstWorld = ConflictWorld(config);
        var secondWorld = ConflictWorld(config);
        var intents = new[]
        {
            MoveIntent(1, new Position(2, 2)),
            MoveIntent(2, new Position(2, 2))
        };
        var firstEvents = ResolveRound(config, firstWorld, intents);
        var secondEvents = ResolveRound(config, secondWorld, intents.Reverse().ToArray());
        SequenceEqual(
            firstWorld.Npcs.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value.Position}"),
            secondWorld.Npcs.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value.Position}"));
        SequenceEqual(
            firstEvents.Select(item => $"{item.Type}:{item.ActorId}:{item.TargetId}:{item.Position}:{item.Success}"),
            secondEvents.Select(item => $"{item.Type}:{item.ActorId}:{item.TargetId}:{item.Position}:{item.Success}"));
    }

    private static void ReproductionCandidateBoundary()
    {
        var config = LoadConfig();
        var random = new RandomStreamFactory(522);
        var perception = new PerceptionSystem(config, random);
        var actor = Npc(1, new Position(1, 1), 5, 5, 5, 100);
        actor.AgeDays = config.Reproduction.MatureAgeDays;
        actor.Needs.Reproduction = 10;
        actor.HeldInformation.AddRange(PerceivedPositionRecords(actor.Id, 2, new Position(2, 1), 0));
        actor.HeldInformation.Add(new InformationRecord(
            "mature", 2, InformationProperty.LifeStage, (double)PerceivedLifeStage.Mature, 1, actor.Id,
            InformationAcquisition.Observation, 0));
        var rules = new WorldDecisionRules(8, 8, new HashSet<Position>());
        var decision = new UtilityDecisionSystem(config, random);
        var candidate = decision.BuildCandidates(
                DecisionContextFor(actor, config, perception.CreateView(actor, 0), rules), 0, 1)
            .Single(item => item.Kind == ActionKind.Reproduction);
        Equal(2L, candidate.TargetId!.Value);

        var target = Npc(2, new Position(2, 1), 5, 5, 5, 1);
        target.AgeDays = config.Reproduction.MatureAgeDays;
        target.ReproductionCooldownDays = 1;
        var world = EmptyWorld(config);
        world.Npcs.Add(actor.Id, actor);
        world.Npcs.Add(target.Id, target);
        var trace = new DecisionTrace(actor.Id, 0, 1, candidate, Array.Empty<CandidateWeight>(), 0, "test");
        var intent = decision.CreateIntent(trace);
        var events = ResolveRound(config, world, new[] { intent });
        True(events.Any(item => item.Type == SimulationEventType.ReproductionFailure &&
                                item.Detail == "reality-precondition"),
            "Reality did not reject the subjectively valid candidate.");
    }

    private static void TargetedPhaseAndInterrupt()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 7, Y = 7 },
            new() { Concept = "survival", X = 6, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 6 }
        };
        config.Combat.HitChanceMinimum = 0;
        config.Combat.HitChanceMaximum = 0;
        config.Combat.HitChanceBase = 0;
        config.Combat.HitChancePerCombatDifference = 0;
        config.Utility.Temperature = 0.0001;
        foreach (var effect in new[]
                 {
                     config.Utility.Move, config.Utility.Rest, config.Utility.Communication,
                     config.Utility.Reproduction, config.Utility.Attack, config.Utility.Flee
                 })
        {
            effect.Survival = 0;
            effect.Rest = 0;
            effect.Activity = 0;
            effect.Communication = 0;
            effect.Reproduction = 0;
        }

        config.Utility.Rest.Rest = 1;
        var world = EmptyWorld(config);
        var first = Npc(1, new Position(1, 1), 9, 0, 0, 100);
        var second = Npc(2, new Position(1, 2), 8, 0, 0, 100);
        var victim = Npc(3, new Position(2, 1), 1, 0, 0, 100);
        victim.Needs.Rest = 10;
        world.Npcs.Add(first.Id, first);
        world.Npcs.Add(second.Id, second);
        world.Npcs.Add(victim.Id, victim);
        var intents = new[]
        {
            TargetedIntent(first.Id, ActionKind.Attack, victim.Id, victim.Position),
            TargetedIntent(second.Id, ActionKind.Attack, victim.Id, victim.Position),
            MoveIntent(victim.Id, new Position(3, 1))
        };
        var events = ResolveRound(config, world, intents);
        var firstMovement = events.ToList().FindIndex(item => item.Type is SimulationEventType.Move or SimulationEventType.Flee);
        var lastAttack = events.ToList().FindLastIndex(item => item.Type == SimulationEventType.Attack);
        True(firstMovement < 0 || lastAttack < firstMovement, "Movement executed before the Attack phase completed.");
        Equal(1, events.Count(item => item.Type == SimulationEventType.IntentReplaced && item.ActorId == victim.Id));
        True(events.Any(item => item.Type == SimulationEventType.IntentReplaced && item.ActorId == victim.Id &&
                                item.Detail.Contains("new=Rest;status=pending", StringComparison.Ordinal)),
            "Attack interrupt did not replace the same action slot from latest state.");
        True(events.All(item => item.Type != SimulationEventType.Move || item.ActorId != victim.Id),
            "The victim executed its discarded Move intent.");
    }

    private static void CombatReactionCannotRecurse()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 7, Y = 7 },
            new() { Concept = "survival", X = 6, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 6 }
        };
        var world = EmptyWorld(config);
        var attacker = Npc(1, new Position(1, 1), action: 5, combat: 0, communication: 0, hp: 100);
        var defender = Npc(2, new Position(2, 1), action: 5, combat: 0, communication: 0, hp: 100);
        world.Npcs.Add(attacker.Id, attacker);
        world.Npcs.Add(defender.Id, defender);
        var trace = new DecisionTrace(attacker.Id, 0, 1, Candidate("attack", 1), Array.Empty<CandidateWeight>(), 0, "test");
        var intent = new ActionIntent(
            "attack-intent", attacker.Id, ActionKind.Attack, defender.Id, null, trace, defender.Position);
        var events = ResolveRound(config, world, new[] { intent });
        True(events.Count(item => item.Type == SimulationEventType.Counterattack) <= 1,
            "Counterattack recursively produced another Counterattack.");
    }

    private static void CollisionAttackAndImmediateDeath()
    {
        var config = LoadConfig();
        ConfigureTinyWorld(config);
        config.Utility.Temperature = 0.0001;
        config.Utility.Communication.Activity = 0;
        config.Utility.Communication.Communication = 0;
        var world = EmptyWorld(config);
        var actor = Npc(1, new Position(1, 1), action: 10, combat: 10, communication: 0, hp: 100);
        actor.Needs.Activity = 10;
        actor.Needs.Rest = 0;
        world.Npcs.Add(actor.Id, actor);

        var id = 2L;
        foreach (var position in actor.Position.Neighbors().Where(position => !world.Landmarks.Any(item => item.Position == position)))
        {
            var defender = Npc(id++, position, action: 0, combat: 0, communication: 0, hp: 0.01);
            defender.Needs.Rest = 10;
            world.Npcs.Add(defender.Id, defender);
        }
        world.NextNpcId = id;

        var engine = SimulationEngine.CreateForTesting(config, 444, world);
        var result = engine.AdvanceOneDay();
        var collision = result.Events.FirstOrDefault(item => item.Type == SimulationEventType.CollisionAttack && item.ActorId == actor.Id);
        True(collision is not null,
            $"No collision attack occurred. traces={string.Join(",", engine.LastDecisionTraces.Where(item => item.EntityId == actor.Id).Select(item => item.Selected.StableKey))}; events={string.Join(",", result.Events.Select(item => item.Type + ":" + item.ActorId + ":" + item.TargetId))}");
        True(!result.Events.Any(item => item.ActorId == actor.Id && item.MicroRound == collision!.MicroRound && item.Type == SimulationEventType.Move),
            "Collision attack moved the attacker during the same action.");
        var death = result.Events.First(item => item.Type == SimulationEventType.Death);
        True(result.Events.Where(item => item.ActorId == death.ActorId &&
                                        item.Type is not SimulationEventType.Death and not SimulationEventType.IntentReplaced)
            .All(item => item.Type == SimulationEventType.Counterattack), "Dead NPC acted after death.");
    }

    private static void FailedActiveActionPaysNeedCost()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.InitialPopulation = 1;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 7, Y = 7 },
            new() { Concept = "survival", X = 6, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 6 }
        };
        config.Utility.Temperature = 0.0001;
        var world = EmptyWorld(config);
        var actor = Npc(1, new Position(0, 0), action: 0, combat: 0, communication: 5, hp: 100);
        actor.Needs.Activity = 5;
        actor.Needs.Communication = 10;
        actor.HeldInformation.AddRange(PerceivedPositionRecords(actor.Id, 2, new Position(1, 0), 0));
        world.Npcs.Add(actor.Id, actor);
        world.NextNpcId = 2;
        var engine = SimulationEngine.CreateForTesting(config, 111, world);
        var result = engine.AdvanceOneDay();
        True(result.Events.Any(item => item.Type == SimulationEventType.Communication && !item.Success),
            "Expected stale Communication to fail.");
        Equal(3.1, actor.Needs.Activity, 1e-9);
        Equal(7, actor.Needs.Communication, 1e-9);
    }

    private static void VitalityCurve()
    {
        var config = LoadConfig();
        var system = new VitalitySystem(config);
        var start = (config.World.DaysPerYear * 3 + 1) / 2;
        True(system.DailyVitalChange(start - 1) > 0, "Vitality was not positive before aging start.");
        Equal(0, system.DailyVitalChange(start), 1e-15);
        True(system.DailyVitalChange(start + 1) < 0, "Vitality was not negative after aging start.");

        var old = Npc(1, new Position(0, 0), action: 0, combat: 0, communication: 0, hp: 1);
        old.AgeDays = start + 20_000;
        for (var day = 0; day < 1_000 && old.IsAlive; day++)
        {
            system.ApplyDailyChange(old);
        }
        True(!old.IsAlive, "Aging alone could not eventually cause death.");
    }

    private static void ConceptMarkPreservesBaseStats()
    {
        var config = LoadConfig();
        config.Concept.ExposureThreshold = 1;
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.InitialPopulation = 1;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 1, Y = 1 },
            new() { Concept = "survival", X = 7, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 0 }
        };
        var world = EmptyWorld(config);
        var npc = Npc(1, new Position(2, 1), action: 5, combat: 5, communication: 5, hp: 100);
        npc.Needs.Rest = 10;
        var original = npc.BaseStats.Copy();
        world.Npcs.Add(npc.Id, npc);
        world.NextNpcId = 2;
        var engine = SimulationEngine.CreateForTesting(config, 5, world);
        engine.AdvanceOneDay();
        True(npc.ConceptMarks.Contains(ConceptKind.Struggle), "Struggle mark was not acquired.");
        Equal(original.Action, npc.BaseStats.Action, 0);
        Equal(original.Combat, npc.BaseStats.Combat, 0);
        Equal(original.Action * config.Concept.EffectiveMultiplier, npc.EffectiveStats(config).Action, 1e-12);
    }

    private static void GeneticsAllowlist()
    {
        var config = LoadConfig();
        config.Reproduction.MutationChance = 0;
        config.World.Width = 8;
        config.World.Height = 8;
        var first = Npc(1, new Position(2, 2), action: 2, combat: 4, communication: 6, hp: 90);
        var second = Npc(2, new Position(3, 2), action: 8, combat: 6, communication: 4, hp: 110);
        first.ConceptMarks.Add(ConceptKind.Struggle);
        first.ConceptExposure[ConceptKind.Struggle] = 500;
        first.HeldInformation.Add(new InformationRecord("private", 99, InformationProperty.Combat, 9, 1, 1,
            InformationAcquisition.Observation, 0));
        var system = new ReproductionSystem(config, new RandomStreamFactory(88));
        var request = system.CreateRequest(first, second, 0, 1);
        var childGenes = system.CreateChildGenetics(request, 0);
        True(childGenes.BaseStats.Action is >= 2 and <= 8, "Child action was outside parental blend without mutation.");

        var world = EmptyWorld(config);
        world.Npcs.Add(first.Id, first);
        world.Npcs.Add(second.Id, second);
        world.NextNpcId = 3;
        world.BirthRequests.Add(request);
        var resolution = system.ResolveBirths(world).Single();
        True(resolution.Success, "Expected an available birth cell.");
        var child = resolution.Child!;
        Equal(0, child.ConceptMarks.Count);
        Equal(0, child.ConceptExposure.Count);
        Equal(0, child.HeldInformation.Count);
        Equal(0, child.ThreatMemory.Count);
        Equal(0, child.AgeDays);
    }

    private static void BirthArbitrationIsOrderIndependent()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        var requests = new[]
        {
            BirthRequest("request-a", 1, 2, new Position(2, 2), new Position(3, 2)),
            BirthRequest("request-b", 3, 4, new Position(2, 3), new Position(3, 3)),
            BirthRequest("request-c", 5, 6, new Position(2, 4), new Position(3, 4))
        };
        var firstWorld = BirthWorld(config, requests);
        var secondWorld = BirthWorld(config, requests.Reverse());
        var first = new ReproductionSystem(config, new RandomStreamFactory(909)).ResolveBirths(firstWorld)
            .ToDictionary(item => item.Request.RequestId, item => item.Position);
        var second = new ReproductionSystem(config, new RandomStreamFactory(909)).ResolveBirths(secondWorld)
            .ToDictionary(item => item.Request.RequestId, item => item.Position);
        SequenceEqual(
            first.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"),
            second.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"));
    }

    private static void WholeRunAndRenderDeterminism()
    {
        const long seed = 8147291;
        var first = new SimulationEngine(LoadConfig(), seed);
        var second = new SimulationEngine(LoadConfig(), seed);
        for (var day = 0; day < 8; day++)
        {
            first.AdvanceOneDay();
            _ = first.GetSnapshot();
            _ = first.GetSnapshot(5);
            _ = first.GetWorldStatistics();
            _ = first.GetCurrentAgeDistribution(183);
            _ = first.GetNpcDetails(first.GetSnapshot(1).Npcs.First().Id);
            second.AdvanceOneDay();
        }

        SequenceEqual(first.EventFingerprints(), second.EventFingerprints());
        Equal(first.GetSnapshot().Npcs.Count, second.GetSnapshot().Npcs.Count);
    }

    private static void NpcDetailProjectionExposesStableLineage()
    {
        var config = LoadConfig();
        config.World.Width = 8;
        config.World.Height = 8;
        config.World.InitialPopulation = 3;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 7, Y = 7 },
            new() { Concept = "survival", X = 6, Y = 7 },
            new() { Concept = "communication", X = 7, Y = 6 }
        };
        var world = EmptyWorld(config);
        var first = Npc(1, new Position(1, 1), action: 2, combat: 3, communication: 4, hp: 100);
        var second = Npc(2, new Position(2, 1), action: 5, combat: 6, communication: 7, hp: 100);
        var child = new NpcState
        {
            Id = 3,
            Position = new Position(1, 2),
            BaseStats = new BaseStats { MaxHp = 100, Action = 3, Combat = 4, Communication = 5 },
            RiskPreference = 0.5,
            CurrentHp = 100,
            AgeDays = 0,
            ParentAId = first.Id,
            ParentBId = second.Id
        };
        world.Npcs.Add(first.Id, first);
        world.Npcs.Add(second.Id, second);
        world.Npcs.Add(child.Id, child);
        world.NextNpcId = 4;
        var engine = SimulationEngine.CreateForTesting(config, 321, world);

        var parentDetails = engine.GetNpcDetails(first.Id)!;
        Equal(first.Id, parentDetails.Id);
        SequenceEqual(new[] { child.Id }, parentDetails.ChildIds);
        Equal(0, parentDetails.ActionHistory.Count);
        var childDetails = engine.GetNpcDetails(child.Id)!;
        Equal(first.Id, childDetails.ParentAId);
        Equal(second.Id, childDetails.ParentBId);
    }

    private static void NpcActionHistoryExcludesMovementEvents()
    {
        var engine = new SimulationEngine(LoadConfig(), 321);
        engine.AdvanceOneDay();
        var details = engine.GetSnapshot().Npcs
            .Select(item => engine.GetNpcDetails(item.Id, 20))
            .FirstOrDefault(item => item?.ActionHistory.Count > 0)
            ?? throw new InvalidOperationException("No NPC action history was projected.");

        True(details.ActionHistory.Count <= 20, "Action history limit was not applied.");
        True(details.ActionHistory.All(item =>
                item.Type is not SimulationEventType.Move and not SimulationEventType.MoveFailed),
            "Movement event leaked into NPC action history.");
        True(details.ActionHistory.All(item => item.OtherNpcId != details.Id),
            "NPC action history reported itself as the other participant.");
        Throws<ArgumentOutOfRangeException>(() => engine.GetNpcDetails(details.Id, 0));
    }

    private static void WorldStatisticsCountSelectedActions()
    {
        var engine = new SimulationEngine(LoadConfig(), 123456);
        var initial = engine.GetWorldStatistics();
        Equal(200, initial.Population);
        Equal(0L, initial.ActionSelections.Sum(item => item.Count));

        engine.AdvanceOneDay();
        var statistics = engine.GetWorldStatistics();
        Equal(engine.GetSnapshot().Npcs.Count, statistics.Population);
        Equal((long)engine.LastDecisionTraces.Count(item => item.DecisionReason == "initial"),
            statistics.ActionSelections.Sum(item => item.Count));
        True(statistics.AverageAgeYears > 0, "Average age was not calculated.");

        var distribution = engine.GetCurrentAgeDistribution(183);
        Equal(statistics.Population, distribution.Population);
        Equal(statistics.Population, distribution.Buckets.Sum(item => item.Count));
        Equal(183, distribution.BucketSizeDays);
        True(distribution.Buckets.Select((item, index) =>
                item.MinimumAgeDays == index * 183 && item.MaximumAgeDaysExclusive == (index + 1) * 183)
            .All(item => item), "Age distribution buckets were not contiguous.");
        Throws<ArgumentOutOfRangeException>(() => engine.GetCurrentAgeDistribution(0));
    }

    private static void MaximumActionsPerDay()
    {
        var config = LoadConfig();
        var engine = new SimulationEngine(config, 55);
        engine.AdvanceOneDay();
        var maximum = engine.LastDecisionTraces.Where(item => item.DecisionReason == "initial")
            .GroupBy(item => item.EntityId).Max(group => group.Count());
        True(maximum <= config.Action.MaximumActionsPerDay,
            $"Observed {maximum} actions with maximum {config.Action.MaximumActionsPerDay}.");
    }

    private static void FsCheckGeneratedRunsRemainDeterministic()
    {
        var config = LoadConfig();
        var property = Prop.ForAll<long, int, int>((runSeed, daySample, splitSample) =>
        {
            var days = Math.Abs(daySample % 8) + 1;
            var split = Math.Abs(splitSample % (days + 1));

            var uninterrupted = new SimulationEngine(config, runSeed);
            uninterrupted.AdvanceDays(days);

            var observed = new SimulationEngine(config, runSeed);
            observed.AdvanceDays(split);
            _ = observed.GetSnapshot();
            _ = observed.GetWorldStatistics();
            observed.AdvanceDays(days - split);

            return uninterrupted.EventFingerprints().SequenceEqual(observed.EventFingerprints()) &&
                   uninterrupted.DeterministicStateFingerprint() == observed.DeterministicStateFingerprint();
        });

        Check.One(
            "generated-seed-render-independence",
            Config.QuickThrowOnFailure
                .WithMaxTest(32)
                .WithReplay(1145655947UL, 296144285UL),
            property);
    }

    private static SimulationConfig LoadConfig() => SimulationConfigLoader.Load(ConfigPath());

    private static string ConfigPath() => Path.Combine(AppContext.BaseDirectory, "simulation", "configs", "v0-default.json");

    private static WorldState EmptyWorld(SimulationConfig config)
    {
        var world = new WorldState();
        foreach (var item in config.World.Landmarks)
        {
            world.Landmarks.Add(new Landmark(ConceptKindParser.Parse(item.Concept), new Position(item.X, item.Y)));
        }
        return world;
    }

    private static NpcState Npc(long id, Position position, double action, double combat, double communication, double hp)
    {
        return new NpcState
        {
            Id = id,
            Position = position,
            BaseStats = new BaseStats { MaxHp = Math.Max(hp, 100), Action = action, Combat = combat, Communication = communication },
            RiskPreference = 0.5,
            CurrentHp = hp,
            AgeDays = 20 * 365
        };
    }

    private static DecisionContext DecisionContextFor(
        NpcState npc,
        SimulationConfig config,
        PerceptionView view,
        WorldDecisionRules rules) => new(
            npc.Id,
            npc.Position,
            npc.CurrentHp,
            npc.EffectiveStats(config),
            npc.RiskPreference,
            npc.AgeDays,
            npc.ReproductionCooldownDays,
            npc.Needs.Snapshot(),
            view,
            rules);

    private static IEnumerable<InformationRecord> PerceivedPositionRecords(long observerId, long subjectId, Position position, int tick)
    {
        yield return new InformationRecord("px", subjectId, InformationProperty.PositionX, position.X, 1, observerId, InformationAcquisition.Observation, tick);
        yield return new InformationRecord("py", subjectId, InformationProperty.PositionY, position.Y, 1, observerId, InformationAcquisition.Observation, tick);
        yield return new InformationRecord("alive", subjectId, InformationProperty.Alive, 1, 1, observerId, InformationAcquisition.Observation, tick);
    }

    private static void ConfigureTinyWorld(SimulationConfig config)
    {
        config.World.Width = 3;
        config.World.Height = 3;
        config.World.InitialPopulation = 6;
        config.World.Landmarks = new List<LandmarkConfig>
        {
            new() { Concept = "struggle", X = 0, Y = 0 },
            new() { Concept = "survival", X = 2, Y = 0 },
            new() { Concept = "communication", X = 0, Y = 2 }
        };
    }

    private static BirthRequest BirthRequest(string id, long first, long second, Position firstPosition, Position secondPosition)
    {
        var genes = new GeneticSnapshot(new BaseStats { MaxHp = 100, Action = 5, Combat = 5, Communication = 5 }, 0.5);
        return new BirthRequest(id, first, second, firstPosition, secondPosition, genes, genes, 0);
    }

    private static WorldState BirthWorld(SimulationConfig config, IEnumerable<BirthRequest> requests)
    {
        var world = EmptyWorld(config);
        world.NextNpcId = 100;
        world.BirthRequests.AddRange(requests);
        return world;
    }

    private static WorldState ConflictWorld(SimulationConfig config)
    {
        var world = EmptyWorld(config);
        world.Npcs.Add(1, Npc(1, new Position(1, 2), action: 5, combat: 0, communication: 0, hp: 100));
        world.Npcs.Add(2, Npc(2, new Position(3, 2), action: 5, combat: 0, communication: 0, hp: 100));
        world.NextNpcId = 3;
        return world;
    }

    private static ActionIntent MoveIntent(long actorId, Position destination)
    {
        var selected = new ActionCandidate(
            ActionKind.Move,
            null,
            destination,
            1,
            $"move-{actorId}",
            new Dictionary<string, double>());
        var trace = new DecisionTrace(actorId, 0, 1, selected, Array.Empty<CandidateWeight>(), 0, "test");
        return new ActionIntent($"intent-{actorId}", actorId, ActionKind.Move, null, destination, trace);
    }

    private static ActionIntent TargetedIntent(
        long actorId,
        ActionKind kind,
        long targetId,
        Position perceivedTargetPosition)
    {
        var selected = new ActionCandidate(
            kind,
            targetId,
            null,
            1,
            $"{kind}-{actorId}-{targetId}",
            new Dictionary<string, double>(),
            perceivedTargetPosition);
        var trace = new DecisionTrace(actorId, 0, 1, selected, Array.Empty<CandidateWeight>(), 0, "test");
        return new ActionIntent(
            $"intent-{actorId}-{kind}", actorId, kind, targetId, null, trace, perceivedTargetPosition);
    }

    private static IReadOnlyList<EventDraft> ResolveRound(
        SimulationConfig config,
        WorldState world,
        IReadOnlyList<ActionIntent> intents)
    {
        var random = new RandomStreamFactory(2026);
        var perception = new PerceptionSystem(config, random);
        var decision = new UtilityDecisionSystem(config, random);
        var communication = new CommunicationSystem(config, random);
        var reproduction = new ReproductionSystem(config, random);
        var needs = new NeedsSystem(config);
        var resolver = new ActionResolutionSystem(
            config,
            random,
            perception,
            decision,
            communication,
            reproduction,
            needs);
        var events = new List<EventDraft>();
        resolver.ResolveRound(world, intents, 1, events.Add);
        return events;
    }

    private static ActionCandidate Candidate(string key, double utility) =>
        new(ActionKind.Move, null, null, utility, key, new Dictionary<string, double>());

    private static void True(bool condition, string message = "Assertion failed.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void Equal(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}, tolerance {tolerance:R}.");
        }
    }

    private static void NotEqual<T>(T first, T second)
    {
        if (EqualityComparer<T>.Default.Equals(first, second))
        {
            throw new InvalidOperationException($"Values were unexpectedly equal: {first}.");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        var first = expected.ToArray();
        var second = actual.ToArray();
        if (!first.SequenceEqual(second))
        {
            throw new InvalidOperationException($"Sequences differ.{Environment.NewLine}Expected: {string.Join(",", first)}{Environment.NewLine}Actual: {string.Join(",", second)}");
        }
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
    }
}
