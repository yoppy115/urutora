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
using Simulation.Core.Social;

namespace Simulation.Core.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        ("configuration schema is strict", ConfigurationSchemaIsStrict),
        ("v0.2.3 defaults preserve v0.15 ecology", V023DefaultsAndInitialAges),
        ("v0.2 logged seeds form Settlements with the v0.2.1 hotspot", LoggedV02SeedsFormSettlements),
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
        ("Settlement birth affiliation applies parent, Influence, and Core scopes", SettlementBirthAffiliationRules),
        ("birth batch arbitration is queue-order independent", BirthArbitrationIsOrderIndependent),
        ("NPC detail projection exposes stable lineage", NpcDetailProjectionExposesStableLineage),
        ("NPC action history excludes movement events", NpcActionHistoryExcludesMovementEvents),
        ("NPC kill count includes combat deaths only", NpcKillCountIncludesCombatDeathsOnly),
        ("world statistics count selected action commands", WorldStatisticsCountSelectedActions),
        ("world statistics projection is cached until the next advance", WorldStatisticsCacheInvalidatesOnAdvance),
        ("Generation hotspot forms a deterministic Settlement", GenerationHotspotFormsSettlement),
        ("Settlement areas are excluded from new Hotspots and Cores", SettlementAreasAreExcludedFromFormation),
        ("Order transition commits on the following tick", OrderTransitionCommitsNextTick),
        ("Order collision policies suppress and convert combat", OrderCollisionPolicies),
        ("Order protects unaffiliated NPC until an active Threat exists", UnaffiliatedProtectionRules),
        ("Generation keeps Order Rest bonus disabled", GenerationKeepsOrderRestBonusDisabled),
        ("outside-Core reproduction applies both utility penalties", OutsideCoreReproductionPenalty),
        ("Concept Aura is non-stacking and normalizes temporary MaxHP", ConceptAuraRules),
        ("Friction decay is symmetric and bounded", FrictionDecayRules),
        ("Invasion Rest withdraws while Flee state remains", InvasionWithdrawalRules),
        ("Core occupation denominator excludes unusable cells", CoreOccupationDenominator),
        ("whole run and render frequency are deterministic", WholeRunAndRenderDeterminism),
        ("serial and parallel read phases are deterministic", SerialAndParallelReadPhasesAreDeterministic),
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
            File.WriteAllText(invalidPath, original.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2, \"unknown\": true", StringComparison.Ordinal));
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

    private static void V023DefaultsAndInitialAges()
    {
        var config = LoadConfig();
        Equal("v0.2.3-default-1", config.Id);
        Equal(5, config.Settlement.HotspotWindowSize);
        Equal(3, config.Settlement.HotspotSuccessThreshold);
        Equal(0.125, config.Concept.ExposureByDistance[4], 0);
        Equal(180, config.Reproduction.MatureAgeDays);
        Equal(90, config.Reproduction.CooldownDays);
        Equal(90, config.Observation.ThreatMemoryDays);
        Equal(3, config.Observation.HeldInformationCapacityPerSubjectProperty);
        Equal(0.04, config.Needs.DailyReproductionIncrease, 0);
        Equal(50, config.InitialPopulation.MaxHpMean, 0);
        Equal(4, config.Combat.DamageBase, 0);
        Equal(0.9, config.Combat.DamageAttackerFactor, 0);
        Equal(0.4, config.Combat.DamageDefenderFactor, 0);
        Equal(2, config.Settlement.CoreRadius);
        Equal(0, config.Performance.MaximumDegreeOfParallelism);
        Equal(128, config.Performance.MinimumPopulationForParallelism);

        var world = Simulation.Core.World.WorldFactory.Create(config, new RandomStreamFactory(915));
        True(world.Npcs.Values.All(item => item.AgeDays is >= 180 and <= 700),
            "Initial age escaped the v0.15 day range.");
    }

    private static void LoggedV02SeedsFormSettlements()
    {
        var cases = new[]
        {
            (Seed: 8147291L, Days: 110),
            (Seed: 8147292L, Days: 50)
        };

        foreach (var (seed, days) in cases)
        {
            var engine = new SimulationEngine(LoadConfig(), seed);
            engine.AdvanceDays(days);
            var snapshot = engine.GetSnapshot();
            var statistics = engine.GetWorldStatistics();
            True(snapshot.Settlements.Any(item => item.IsActive),
                $"No Settlement formed for logged seed {seed} within {days} days.");
            True(statistics.HotspotCandidates > 0,
                $"No Hotspot Candidate was evaluated for logged seed {seed}.");
        }
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
                                item.Detail.StartsWith("cooldown;", StringComparison.Ordinal)),
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

    private static void SettlementBirthAffiliationRules()
    {
        var config = LoadConfig();
        config.World.Width = 14;
        config.World.Height = 14;
        var world = SocialWorld(config);
        var resident = Npc(1, new Position(6, 2), 5, 5, 5, 100);
        resident.SettlementId = 1;
        resident.SettlementAffinity[1] = config.Settlement.MembershipThreshold;
        var partner = Npc(2, new Position(7, 2), 5, 5, 5, 100);
        world.Npcs.Add(resident.Id, resident);
        world.Npcs.Add(partner.Id, partner);
        world.NextNpcId = 3;

        var birthSettlement = SettlementQueries.BirthSettlement(world, resident, partner, config);
        Equal(1, birthSettlement!.SettlementId);
        Equal(SettlementBirthPlacement.Influence, birthSettlement.Placement);
        var reproduction = new ReproductionSystem(config, new RandomStreamFactory(922));
        world.BirthRequests.Add(reproduction.CreateRequest(
            resident,
            partner,
            world.Tick,
            1,
            birthSettlement.SettlementId,
            birthSettlement.Placement));
        var result = reproduction.ResolveBirths(world).Single();

        True(result.Success, "Qualifying one-parent Settlement birth had no Influence birth cell.");
        Equal(SettlementBirthPlacement.Influence, result.AppliedPlacement);
        Equal(1, result.Child!.SettlementId!.Value);
        Equal(config.Settlement.MembershipThreshold, result.Child.SettlementAffinity[1], 0);
        True(world.Settlements[1].Center.ChebyshevDistance(result.Child.Position) <= config.Settlement.InfluenceRadius,
            "One-parent affiliated child was born outside the Settlement Influence.");
        True(world.Settlements[1].Center.ChebyshevDistance(result.Child.Position) > config.Settlement.CoreRadius,
            "One-parent affiliation remained restricted to the Settlement Core.");

        resident.Position = new Position(11, 11);
        partner.Position = new Position(12, 11);
        partner.SettlementId = 1;
        partner.SettlementAffinity[1] = config.Settlement.MembershipThreshold;
        var sharedSettlement = SettlementQueries.BirthSettlement(world, resident, partner, config);
        Equal(1, sharedSettlement!.SettlementId);
        Equal(SettlementBirthPlacement.ParentNeighborhood, sharedSettlement.Placement);
        world.BirthRequests.Add(reproduction.CreateRequest(
            resident,
            partner,
            world.Tick,
            2,
            sharedSettlement.SettlementId,
            sharedSettlement.Placement));
        var sharedResult = reproduction.ResolveBirths(world).Single();
        True(sharedResult.Success, "Shared-Settlement parents could not give birth outside the Core.");
        Equal(SettlementBirthPlacement.ParentNeighborhood, sharedResult.AppliedPlacement);
        Equal(1, sharedResult.Child!.SettlementId!.Value);
        Equal(config.Settlement.MembershipThreshold, sharedResult.Child.SettlementAffinity[1], 0);
        True(world.Settlements[1].Center.ChebyshevDistance(sharedResult.Child.Position) > config.Settlement.InfluenceRadius,
            "Shared-Settlement child was moved into the Settlement Influence.");

        partner.SettlementId = null;
        True(SettlementQueries.BirthSettlement(world, resident, partner, config) is null,
            "A one-parent reproduction outside the Settlement Influence inherited affiliation.");

        resident.Position = new Position(2, 2);
        partner.Position = new Position(3, 2);
        partner.SettlementId = 2;
        var differentSettlements = SettlementQueries.BirthSettlement(world, resident, partner, config);
        Equal(1, differentSettlements!.SettlementId);
        Equal(SettlementBirthPlacement.Core, differentSettlements.Placement);

        config.Settlement.CoreRadius = 7;
        True(SettlementQueries.BirthSettlement(world, resident, partner, config) is null,
            "Overlapping parental Settlement candidates were resolved by arbitrary order.");
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

    private static void NpcKillCountIncludesCombatDeathsOnly()
    {
        var events = new[]
        {
            new SimulationEvent("combat", 1, 1, SimulationEventType.Death, 2, 1, new Position(1, 1), true, "combat:Attack"),
            new SimulationEvent("vitality", 2, 0, SimulationEventType.Death, 3, null, new Position(2, 2), true, "vitality"),
            new SimulationEvent("attack", 3, 1, SimulationEventType.Attack, 1, 4, new Position(3, 3), true, "damage=1")
        };

        Equal(1L, SimulationEngine.CountKills(events, 1));
        Equal(0L, SimulationEngine.CountKills(events, 2));
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

    private static void WorldStatisticsCacheInvalidatesOnAdvance()
    {
        var engine = new SimulationEngine(LoadConfig(), 9221);
        var first = engine.GetWorldStatistics();
        var repeated = engine.GetWorldStatistics();
        True(ReferenceEquals(first, repeated), "Unchanged World statistics were recomputed.");
        engine.AdvanceOneDay();
        var advanced = engine.GetWorldStatistics();
        True(!ReferenceEquals(first, advanced), "World statistics cache survived an authoritative advance.");
        Equal(1, advanced.Tick);
    }

    private static void GenerationHotspotFormsSettlement()
    {
        var config = LoadConfig();
        config.Settlement.HotspotSuccessThreshold = 4;
        var first = HotspotWorld(config, reverse: false);
        var second = HotspotWorld(config, reverse: true);
        var firstEvents = new List<EventDraft>();
        var secondEvents = new List<EventDraft>();
        var firstSystem = new SettlementFormationSystem(config, new RandomStreamFactory(9441));
        var secondSystem = new SettlementFormationSystem(config, new RandomStreamFactory(9441));
        var firstResult = firstSystem.EvaluateAndForm(first, Capture(firstEvents));
        var secondResult = secondSystem.EvaluateAndForm(second, Capture(secondEvents));

        Equal(1, firstResult.FormedSettlementIds.Count);
        Equal(first.Settlements.Single().Value.Center, second.Settlements.Single().Value.Center);
        SequenceEqual(
            first.Settlements.Single().Value.FounderIds,
            second.Settlements.Single().Value.FounderIds);
        Equal(WorldPhase.Generation, first.Phase);
        True(first.Settlements.Single().Value.EffectiveTick == first.Tick + 1,
            "Settlement did not use next-tick activation.");
        var formed = first.Settlements.Single().Value;
        True(first.Npcs.Values
                .Where(item => item.Position.ChebyshevDistance(formed.Center) <= config.Settlement.CoreRadius)
                .All(item => item.SettlementAffinity.Values.Any(value => value >= config.Settlement.CoreResidentAffinity)),
            "Core residents did not receive formation Affinity.");
        True(firstEvents.Any(item => item.Type == SimulationEventType.SettlementFormed),
            "Settlement formation was not logged.");
    }

    private static void SettlementAreasAreExcludedFromFormation()
    {
        var config = LoadConfig();
        config.Settlement.HotspotSuccessThreshold = 3;
        var world = EmptyWorld(config);
        world.Tick = 20;
        world.Settlements.Add(1, new SettlementState
        {
            Id = 1,
            Center = new Position(10, 10),
            FormedTick = 0,
            EffectiveTick = 0,
            FounderIds = Array.Empty<long>()
        });
        for (var index = 0; index < 3; index++)
        {
            world.ReproductionSuccesses.Add(new ReproductionSuccessRecord(
                $"inside-{index}", 18 + index, new Position(12, 10), 1, 2));
            world.ReproductionSuccesses.Add(new ReproductionSuccessRecord(
                $"outside-{index}", 18 + index, new Position(30, 30), 3, 4));
        }

        var candidates = new SettlementFormationSystem(config, new RandomStreamFactory(823)).CreateCandidateSnapshot(world);
        True(candidates.Count > 0, "The unrelated external Hotspot was lost.");
        True(candidates.All(candidate => candidate.ReproductionSuccessEventIds.All(id => id.StartsWith("outside-", StringComparison.Ordinal))),
            "A reproduction success inside existing Influence contributed to a Hotspot.");
        var protectedDistance = config.Settlement.InfluenceRadius + config.Settlement.CoreRadius;
        True(candidates.SelectMany(candidate => candidate.CenterCandidates)
                .All(center => center.ChebyshevDistance(new Position(10, 10)) > protectedDistance),
            "A new 5x5 Core could overlap existing Settlement Influence.");
    }

    private static void OrderTransitionCommitsNextTick()
    {
        var config = LoadConfig();
        config.Settlement.StabilityWindowDays = 2;
        config.Settlement.StabilityConsecutiveDays = 2;
        config.Settlement.EvaluationIntervalDays = 1000;
        var world = EmptyWorld(config);
        world.Npcs.Add(1, Npc(1, new Position(10, 10), 5, 5, 5, 100));
        var random = new RandomStreamFactory(77);
        var invasion = new InvasionSystem(config, random);
        var maintenance = new SettlementMaintenanceCoordinator(config, random, invasion);
        var events = new List<EventDraft>();
        var emit = Capture(events);

        for (var tick = 0; tick < 3; tick++)
        {
            world.Tick = tick;
            maintenance.RunEndOfDay(world, Array.Empty<SimulationEvent>(), emit);
        }

        Equal(WorldPhase.Generation, world.Phase);
        Equal(WorldPhase.Order, world.PendingPhase!.Value);
        Equal(3, world.OrderStartTick!.Value);
        world.Tick = 3;
        maintenance.ActivatePending(world, emit);
        Equal(WorldPhase.Order, world.Phase);
        True(events.Any(item => item.Type == SimulationEventType.WorldPhaseChanged),
            "Order activation event was not emitted.");
    }

    private static void OrderCollisionPolicies()
    {
        var config = LoadConfig();
        var same = SocialWorld(config);
        same.Npcs.Add(1, Npc(1, new Position(1, 1), 5, 5, 5, 100));
        same.Npcs.Add(2, Npc(2, new Position(2, 1), 5, 5, 5, 100));
        same.Npcs[1].SettlementId = 1;
        same.Npcs[2].SettlementId = 1;
        var suppressed = ResolveRound(config, same, new[] { MoveIntent(1, new Position(2, 1)) });
        True(suppressed.Any(item => item.Type == SimulationEventType.CollisionSuppressed &&
                                    item.Detail.Contains("same-settlement", StringComparison.Ordinal)),
            "Same-Settlement collision was not suppressed.");
        True(suppressed.All(item => item.Type != SimulationEventType.CollisionAttack),
            "Same-Settlement collision leaked into Combat.");

        var different = SocialWorld(config);
        different.Npcs.Add(1, Npc(1, new Position(1, 1), 5, 5, 5, 100));
        different.Npcs.Add(2, Npc(2, new Position(2, 1), 5, 5, 5, 100));
        different.Npcs[1].SettlementId = 1;
        different.Npcs[2].SettlementId = 2;
        var friction = ResolveRound(config, different, new[] { MoveIntent(1, new Position(2, 1)) });
        Equal(1d, different.Frictions[SettlementPair.Create(1, 2)].CurrentFriction, 0);
        True(friction.Any(item => item.Type == SimulationEventType.SettlementFrictionChanged),
            "Other-Settlement collision did not create Friction.");
    }

    private static void OutsideCoreReproductionPenalty()
    {
        var config = LoadConfig();
        var random = new RandomStreamFactory(188);
        var perception = new PerceptionSystem(config, random);
        var actor = Npc(1, new Position(3, 0), 5, 5, 5, 100);
        actor.AgeDays = config.Reproduction.MatureAgeDays;
        actor.Needs.Reproduction = 10;
        actor.HeldInformation.AddRange(PerceivedPositionRecords(actor.Id, 2, new Position(4, 0), 0));
        actor.HeldInformation.Add(new InformationRecord(
            "mature-outside", 2, InformationProperty.LifeStage, (double)PerceivedLifeStage.Mature, 1, actor.Id,
            InformationAcquisition.Observation, 0));
        var core = new[] { new SettlementCoreRule(1, new Position(0, 0), config.Settlement.CoreRadius) };
        var noPenaltyRules = new WorldDecisionRules(8, 8, new HashSet<Position>(), core,
            OutsideReproductionPenaltyEnabled: false);
        var penaltyRules = noPenaltyRules with { OutsideReproductionPenaltyEnabled = true };
        var decision = new UtilityDecisionSystem(config, random);
        var view = perception.CreateView(actor, 0);
        var baseline = decision.BuildCandidates(DecisionContextFor(actor, config, view, noPenaltyRules), 0, 1)
            .Single(item => item.Kind == ActionKind.Reproduction);
        var penalized = decision.BuildCandidates(DecisionContextFor(actor, config, view, penaltyRules), 0, 1)
            .Single(item => item.Kind == ActionKind.Reproduction);
        Equal(config.Settlement.OutsideReproductionUtilityPenalty, baseline.Utility - penalized.Utility, 1e-12);

        var target = Npc(2, new Position(4, 0), 5, 5, 5, 100);
        target.Needs.Reproduction = 8;
        Equal(config.Settlement.OutsideReproductionUtilityPenalty,
            ReproductionSystem.AcceptanceUtility(target) -
            ReproductionSystem.AcceptanceUtility(target, config.Settlement.OutsideReproductionUtilityPenalty), 1e-12);
    }

    private static void UnaffiliatedProtectionRules()
    {
        var config = LoadConfig();
        config.Combat.HitChanceBase = 0;
        config.Combat.HitChanceMinimum = 0;
        config.Combat.HitChanceMaximum = 0;
        var world = SocialWorld(config);
        var resident = Npc(1, new Position(1, 1), 5, 5, 5, 100);
        resident.SettlementId = 1;
        var unaffiliated = Npc(2, new Position(2, 1), 5, 5, 5, 100);
        world.Npcs.Add(1, resident);
        world.Npcs.Add(2, unaffiliated);
        var protectedEvents = ResolveRound(config, world, new[]
        {
            TargetedIntent(1, ActionKind.Attack, 2, unaffiliated.Position)
        });
        True(protectedEvents.Any(item => item.Type == SimulationEventType.AttackSuppressed &&
                                         item.Detail.Contains("unaffiliated-protected", StringComparison.Ordinal)),
            "Unaffiliated protection was not revalidated at Resolution.");

        resident.ThreatMemory[2] = new ThreatMemory(2, world.Tick);
        var threatEvents = ResolveRound(config, world, new[]
        {
            TargetedIntent(1, ActionKind.Attack, 2, unaffiliated.Position)
        });
        True(threatEvents.Any(item => item.Type == SimulationEventType.Attack),
            "Active Threat did not lift unaffiliated protection.");
        Equal(1L, world.UnaffiliatedThreatExceptionAttackCount);
    }

    private static void GenerationKeepsOrderRestBonusDisabled()
    {
        var config = LoadConfig();
        var generation = SocialWorld(config);
        generation.Phase = WorldPhase.Generation;
        var generationNpc = Npc(1, new Position(2, 2), 5, 5, 5, 100);
        generationNpc.Needs.Rest = 7;
        generation.Npcs.Add(1, generationNpc);
        ResolveRound(config, generation, new[] { SimpleIntent(1, ActionKind.Rest) });
        Equal(3d, generationNpc.Needs.Rest, 1e-12);

        var order = SocialWorld(config);
        var orderNpc = Npc(1, new Position(2, 2), 5, 5, 5, 100);
        orderNpc.Needs.Rest = 7;
        order.Npcs.Add(1, orderNpc);
        ResolveRound(config, order, new[] { SimpleIntent(1, ActionKind.Rest) });
        Equal(1d, orderNpc.Needs.Rest, 1e-12);
    }

    private static void ConceptAuraRules()
    {
        var config = LoadConfig();
        var world = SocialWorld(config);
        var holder = Npc(1, new Position(1, 1), 5, 5, 5, 100);
        holder.SettlementId = 1;
        holder.ConceptMarks.Add(ConceptKind.Survival);
        var secondHolder = Npc(2, new Position(1, 2), 5, 5, 5, 100);
        secondHolder.SettlementId = 1;
        secondHolder.ConceptMarks.Add(ConceptKind.Survival);
        var target = Npc(3, new Position(2, 1), 5, 5, 5, 90);
        target.SettlementId = 1;
        world.Npcs.Add(holder.Id, holder);
        world.Npcs.Add(secondHolder.Id, secondHolder);
        world.Npcs.Add(target.Id, target);
        var events = new List<EventDraft>();
        var aura = new ConceptAuraSystem(config, new RandomStreamFactory(90));
        aura.Refresh(world, Capture(events), 1);
        Equal(90d, target.CurrentHp, 0);
        Equal(110d, target.EffectiveStats(config).MaxHp, 1e-9);

        target.ConceptMarks.Add(ConceptKind.Survival);
        aura.Refresh(world, Capture(events), 1);
        True(!target.ActiveAuras.Contains(ConceptKind.Survival), "Self Mark incorrectly stacked with the same Aura.");
        Equal(120d, target.EffectiveStats(config).MaxHp, 1e-9);

        target.ConceptMarks.Remove(ConceptKind.Survival);
        aura.Refresh(world, Capture(events), 1);
        target.CurrentHp = 105;
        target.Position = new Position(20, 20);
        aura.Refresh(world, Capture(events), 1);
        Equal(100d, target.CurrentHp, 1e-9);
        True(events.Any(item => item.Type == SimulationEventType.TemporaryMaxHpNormalized),
            "Aura expiry did not log non-damage HP normalization.");
        True(events.All(item => item.Type != SimulationEventType.Attack && item.Type != SimulationEventType.Death),
            "Aura normalization produced a Combat reaction.");
    }

    private static void FrictionDecayRules()
    {
        var config = LoadConfig();
        var world = EmptyWorld(config);
        var pair = SettlementPair.Create(2, 1);
        world.Frictions[pair] = new SettlementFriction
        {
            Pair = pair,
            CurrentFriction = 2,
            LastFrictionEventTick = 0
        };
        var random = new RandomStreamFactory(71);
        var maintenance = new SettlementMaintenanceCoordinator(config, random, new InvasionSystem(config, random));
        var events = new List<EventDraft>();
        world.Tick = 30;
        maintenance.RunEndOfDay(world, Array.Empty<SimulationEvent>(), Capture(events));
        Equal(1d, world.Frictions[SettlementPair.Create(1, 2)].CurrentFriction, 0);
        world.Tick = 60;
        maintenance.RunEndOfDay(world, Array.Empty<SimulationEvent>(), Capture(events));
        Equal(0d, world.Frictions[pair].CurrentFriction, 0);
        world.Tick = 90;
        maintenance.RunEndOfDay(world, Array.Empty<SimulationEvent>(), Capture(events));
        Equal(0d, world.Frictions[pair].CurrentFriction, 0);
    }

    private static void InvasionWithdrawalRules()
    {
        var config = LoadConfig();
        var world = SocialWorld(config);
        var participant = Npc(1, new Position(1, 1), 5, 5, 5, 100);
        participant.SettlementId = 1;
        participant.InvasionId = 1;
        participant.InvasionRole = InvasionRole.Attacker;
        participant.HasAdvanceBias = true;
        world.Npcs.Add(participant.Id, participant);
        world.Invasions.Add(1, new InvasionState
        {
            Id = 1,
            AttackSettlementId = 1,
            DefenseSettlementId = 2,
            CreatedTick = 0,
            EffectiveTick = 0,
            TriggerCrowdingPressure = 0.8,
            TargetReason = "test",
            AttackParticipantIds = new[] { 1L },
            CoreCohortIds = new[] { 1L },
            FrontierCohortIds = Array.Empty<long>()
        });
        var selected = new ActionCandidate(ActionKind.Flee, null, new Position(0, 1), 1, "flee-test",
            new Dictionary<string, double>());
        var trace = new DecisionTrace(1, 0, 1, selected, Array.Empty<CandidateWeight>(), 0, "test");
        ResolveRound(config, world, new[]
        {
            new ActionIntent("flee-intent", 1, ActionKind.Flee, null, new Position(0, 1), trace)
        });
        Equal(1, participant.InvasionId!.Value);
        True(participant.HasAdvanceBias, "Flee incorrectly removed Advance Bias.");

        var events = new List<EventDraft>();
        var invasion = new InvasionSystem(config, new RandomStreamFactory(73));
        invasion.WithdrawForRest(world, participant, Capture(events), 2);
        True(!participant.InvasionId.HasValue && !participant.HasAdvanceBias,
            "Rest did not withdraw the invasion participant.");
        True(participant.WithdrawnInvasionIds.Contains(1), "Rest withdrawal did not prevent same-event rejoin.");
    }

    private static void CoreOccupationDenominator()
    {
        var config = LoadConfig();
        var world = EmptyWorld(config);
        var settlement = new SettlementState
        {
            Id = 1,
            Center = new Position(16, 16),
            FormedTick = 0,
            EffectiveTick = 0,
            FounderIds = Array.Empty<long>()
        };
        world.Settlements.Add(1, settlement);
        var cells = SettlementQueries.UsableCoreCells(world, settlement, config);
        Equal(24, cells.Count);
        True(!cells.Contains(new Position(16, 16)), "Landmark was included in usable Core denominator.");
        True(cells.Contains(new Position(15, 16)), "Ordinary occupiable Core cell was excluded.");
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

    private static void SerialAndParallelReadPhasesAreDeterministic()
    {
        var serialConfig = LoadConfig();
        serialConfig.Performance.MaximumDegreeOfParallelism = 1;
        serialConfig.Performance.MinimumPopulationForParallelism = 1;
        var parallelConfig = LoadConfig();
        parallelConfig.Performance.MaximumDegreeOfParallelism = 4;
        parallelConfig.Performance.MinimumPopulationForParallelism = 1;

        var serial = new SimulationEngine(serialConfig, 8147291);
        var parallel = new SimulationEngine(parallelConfig, 8147291);
        serial.AdvanceDays(20);
        parallel.AdvanceDays(20);

        SequenceEqual(serial.EventFingerprints(), parallel.EventFingerprints());
        Equal(serial.DeterministicStateFingerprint(), parallel.DeterministicStateFingerprint());
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

    private static WorldState HotspotWorld(SimulationConfig config, bool reverse)
    {
        var world = EmptyWorld(config);
        world.Tick = 14;
        world.Npcs.Add(1, Npc(1, new Position(5, 5), 5, 5, 5, 100));
        world.Npcs.Add(2, Npc(2, new Position(6, 5), 5, 5, 5, 100));
        var records = Enumerable.Range(0, 4).Select(index => new ReproductionSuccessRecord(
            $"success-{index}", 10 + index, new Position(5, 5), 1, 2)).ToArray();
        world.ReproductionSuccesses.AddRange(reverse ? records.Reverse() : records);
        return world;
    }

    private static WorldState SocialWorld(SimulationConfig config)
    {
        var world = EmptyWorld(config);
        world.Phase = WorldPhase.Order;
        world.Settlements.Add(1, new SettlementState
        {
            Id = 1,
            Center = new Position(2, 2),
            FormedTick = -1,
            EffectiveTick = 0,
            FounderIds = Array.Empty<long>()
        });
        world.Settlements.Add(2, new SettlementState
        {
            Id = 2,
            Center = new Position(8, 8),
            FormedTick = -1,
            EffectiveTick = 0,
            FounderIds = Array.Empty<long>()
        });
        return world;
    }

    private static DomainEventEmitter Capture(ICollection<EventDraft> events) =>
        (microRound, type, actorId, targetId, position, success, detail) =>
            events.Add(new EventDraft(microRound, type, actorId, targetId, position, success, detail));

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
        config.Settlement.HotspotWindowSize = 3;
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

    private static ActionIntent SimpleIntent(long actorId, ActionKind kind)
    {
        var selected = new ActionCandidate(kind, null, null, 1, $"{kind}-{actorId}",
            new Dictionary<string, double>());
        var trace = new DecisionTrace(actorId, 0, 1, selected, Array.Empty<CandidateWeight>(), 0, "test");
        return new ActionIntent($"intent-{actorId}-{kind}", actorId, kind, null, null, trace);
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
