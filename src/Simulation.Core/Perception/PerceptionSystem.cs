using System.Collections.Concurrent;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;
using Simulation.Core.Social;

namespace Simulation.Core.Perception;

public sealed class PerceptionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;
    private readonly ConcurrentDictionary<long, CachedPerceptionView> _viewCache = new();
    private long _evictionCount;
    private long _subjectPurgeCount;
    private long _positionInvalidationCount;

    public PerceptionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    public long EvictionCount => Interlocked.Read(ref _evictionCount);
    public long SubjectPurgeCount => Interlocked.Read(ref _subjectPurgeCount);
    public long PositionInvalidationCount => Interlocked.Read(ref _positionInvalidationCount);

    public void Observe(WorldState realitySnapshot)
    {
        var alive = realitySnapshot.Npcs.Values.Where(item => item.IsAlive).OrderBy(item => item.Id).ToArray();
        var spatialIndex = alive.GroupBy(item => item.Position)
            .ToDictionary(item => item.Key, item => item.OrderBy(value => value.Id).ToArray());
        var degree = ParallelExecutionPolicy.ResolveDegree(_config.Performance, alive.Length);
        if (degree == 1)
        {
            foreach (var observer in alive)
            {
                ObserveOne(realitySnapshot, observer, spatialIndex);
            }
            return;
        }

        Parallel.ForEach(alive, new ParallelOptions { MaxDegreeOfParallelism = degree },
            observer => ObserveOne(realitySnapshot, observer, spatialIndex));
    }

    private void ObserveOne(
        WorldState world,
        NpcState observer,
        IReadOnlyDictionary<Position, NpcState[]> spatialIndex)
    {
        Maintain(observer, world.Tick);
        ObserveOwnSettlement(world, observer);
        foreach (var settlement in SettlementQueries.ActiveSettlements(world)
                     .Where(item => item.Center.ChebyshevDistance(observer.Position) <= _config.Observation.MaximumDistance))
        {
            AddSettlementField(observer, settlement.Id, SettlementBeliefField.ActiveStatus, 1, null, null,
                1, observer.Id, world.Tick, KnowledgeSourceType.DirectObservation, "center-active");
            AddSettlementField(observer, settlement.Id, SettlementBeliefField.Center, null, settlement.Center, null,
                1, observer.Id, world.Tick, KnowledgeSourceType.DirectObservation, "center-position");
        }

        var maximumDistance = _config.Observation.MaximumDistance;
        var nearby = new List<NpcState>();
        for (var y = observer.Position.Y - maximumDistance; y <= observer.Position.Y + maximumDistance; y++)
        {
            for (var x = observer.Position.X - maximumDistance; x <= observer.Position.X + maximumDistance; x++)
            {
                if (spatialIndex.TryGetValue(new Position(x, y), out var occupants))
                {
                    nearby.AddRange(occupants);
                }
            }
        }

        foreach (var subject in nearby.Where(item => item.Id != observer.Id).OrderBy(item => item.Id))
        {
            var distance = observer.Position.ChebyshevDistance(subject.Position);
            if (distance is < 1 || distance > maximumDistance)
            {
                continue;
            }

            var confidence = _config.Observation.ConfidenceByDistance[distance];
            AddPersonField(observer, subject.Id, PersonBeliefField.Position, null, subject.Position, null,
                confidence, observer.Id, world.Tick, KnowledgeSourceType.DirectObservation, "position");
            AddPersonField(observer, subject.Id, PersonBeliefField.AliveStatus, 1, null, null,
                confidence, observer.Id, world.Tick, KnowledgeSourceType.DirectObservation, "alive");
            var maxError = _config.Observation.ErrorFactor * (distance + 1);
            AddNoisy(observer, subject.Id, PersonBeliefField.EstimatedHp, subject.CurrentHp,
                confidence, maxError, world.Tick);
            AddNoisy(observer, subject.Id, PersonBeliefField.EstimatedCombat,
                subject.EffectiveStats(_config).Combat, confidence, maxError, world.Tick);
            AddPersonField(observer, subject.Id, PersonBeliefField.LifeStage,
                (double)(subject.IsMature(_config) ? PerceivedLifeStage.Mature : PerceivedLifeStage.Child),
                null, null, confidence, observer.Id, world.Tick, KnowledgeSourceType.DirectObservation, "life-stage");
            AddPersonField(observer, subject.Id, PersonBeliefField.SettlementAffiliation,
                subject.SettlementId ?? -1, null, null, confidence, observer.Id, world.Tick,
                KnowledgeSourceType.DirectObservation, "settlement");
            AddPersonField(observer, subject.Id, PersonBeliefField.ConceptMarks, null, null,
                subject.ConceptMarks.ToHashSet(), confidence, observer.Id, world.Tick,
                KnowledgeSourceType.DirectObservation, "marks");
            if (subject.SettlementId.HasValue)
            {
                AddSettlementField(observer, subject.SettlementId.Value, SettlementBeliefField.ActiveStatus,
                    1, null, null, confidence, observer.Id, world.Tick,
                    KnowledgeSourceType.DirectObservation, "observed-affiliation");
            }
            if (SettlementQueries.AreInvasionOpponents(world, observer, subject))
            {
                observer.ThreatMemory[subject.Id] = new ThreatMemory(subject.Id, world.Tick);
            }
        }
        Maintain(observer, world.Tick);
    }

    public void RecordThreat(NpcState victim, NpcState attacker, int tick)
    {
        victim.ThreatMemory[attacker.Id] = new ThreatMemory(attacker.Id, tick);
        AddPersonField(victim, attacker.Id, PersonBeliefField.Position, null, attacker.Position, null,
            1, victim.Id, tick, KnowledgeSourceType.Threat, "position");
        AddPersonField(victim, attacker.Id, PersonBeliefField.AliveStatus, attacker.IsAlive ? 1 : 0, null, null,
            1, victim.Id, tick, KnowledgeSourceType.Threat, "alive");
        AddPersonField(victim, attacker.Id, PersonBeliefField.EstimatedHp, Math.Max(attacker.CurrentHp, 0), null, null,
            1, victim.Id, tick, KnowledgeSourceType.Threat, "hp");
    }

    public void RecordCombatOutcome(NpcState observer, NpcState subject, int tick)
    {
        if (!subject.IsAlive)
        {
            if (observer.Knowledge.RemovePerson(subject.Id, true))
            {
                Interlocked.Increment(ref _subjectPurgeCount);
            }
            return;
        }
        AddPersonField(observer, subject.Id, PersonBeliefField.Position, null, subject.Position, null,
            1, observer.Id, tick, KnowledgeSourceType.DirectOutcome, "position");
        AddPersonField(observer, subject.Id, PersonBeliefField.AliveStatus, 1, null, null,
            1, observer.Id, tick, KnowledgeSourceType.DirectOutcome, "alive");
        AddPersonField(observer, subject.Id, PersonBeliefField.EstimatedHp, Math.Max(subject.CurrentHp, 0), null, null,
            1, observer.Id, tick, KnowledgeSourceType.DirectOutcome, "hp");
    }

    public bool InvalidatePosition(NpcState observer, long subjectId)
    {
        if (!observer.Knowledge.InvalidatePersonPosition(subjectId))
        {
            return false;
        }
        Interlocked.Increment(ref _positionInvalidationCount);
        return true;
    }

    public bool PurgeSubject(NpcState observer, long subjectId)
    {
        observer.ThreatMemory.Remove(subjectId);
        if (!observer.Knowledge.RemovePerson(subjectId))
        {
            return false;
        }
        Interlocked.Increment(ref _subjectPurgeCount);
        return true;
    }

    public bool AddPersonField(
        NpcState observer,
        long subjectId,
        PersonBeliefField field,
        double? number,
        Position? position,
        IReadOnlySet<ConceptKind>? concepts,
        double confidence,
        long sourceId,
        int tick,
        KnowledgeSourceType sourceType,
        string stableScope)
    {
        var sequence = observer.NextInformationSequence++;
        var value = new BeliefValue(
            StableHash.StableId("person-belief", observer.Id, subjectId, field, tick, sourceType, sourceId,
                sequence, stableScope),
            sourceType,
            sourceId,
            Math.Clamp(confidence, 0, 1),
            tick,
            number,
            position,
            null,
            concepts);
        if (field == PersonBeliefField.AliveStatus && number < 0.5 &&
            (!observer.Knowledge.Persons.TryGetValue(subjectId, out var belief) ||
             !belief.Fields.TryGetValue(field, out var existing) || KnowledgeStore.ShouldReplace(existing, value)))
        {
            return observer.Knowledge.RemovePerson(subjectId, true);
        }
        return observer.Knowledge.UpsertPerson(
            subjectId, field, value, sourceType == KnowledgeSourceType.DirectObservation);
    }

    public bool AddSettlementField(
        NpcState observer,
        int settlementId,
        SettlementBeliefField field,
        double? number,
        Position? position,
        string? text,
        double confidence,
        long sourceId,
        int tick,
        KnowledgeSourceType sourceType,
        string stableScope)
    {
        var sequence = observer.NextInformationSequence++;
        return observer.Knowledge.UpsertSettlement(settlementId, field, new BeliefValue(
            StableHash.StableId("settlement-belief", observer.Id, settlementId, field, tick, sourceType,
                sourceId, sequence, stableScope),
            sourceType, sourceId, Math.Clamp(confidence, 0, 1), tick, number, position, text));
    }

    public PerceptionView CreateView(NpcState owner, int tick)
    {
        var version = owner.Knowledge.Version;
        var activeThreats = owner.ThreatMemory.Values
            .Where(item => tick - item.LastThreatTick <= _config.Observation.ThreatMemoryDays)
            .Select(item => item.SubjectId)
            .ToHashSet();
        if (_viewCache.TryGetValue(owner.Id, out var cached) && cached.Tick == tick &&
            cached.InformationVersion == version && cached.OwnerPosition == owner.Position &&
            cached.ActiveThreatIds.SetEquals(activeThreats))
        {
            return cached.View;
        }

        var view = new PerceptionView(owner.Knowledge.Persons.Values, activeThreats,
            owner.Position, _config.Observation.MaximumDistance);
        _viewCache[owner.Id] = new CachedPerceptionView(tick, version, owner.Position, activeThreats, view);
        return view;
    }

    public static double TransmissionConfidence(
        double sourceConfidence,
        double receiverEffectiveCommunication,
        CommunicationConfig config)
    {
        var quality = Math.Clamp(receiverEffectiveCommunication, 0, 10);
        return Math.Clamp(sourceConfidence * (config.ConfidenceBase + config.ConfidencePerAbility * quality), 0, 1);
    }

    public int PersonCapacity(NpcState owner)
    {
        var stableCommunication = owner.BaseStats.Communication *
                                  (owner.ConceptMarks.Contains(ConceptKind.Communication)
                                      ? _config.Concept.EffectiveMultiplier
                                      : 1);
        return Math.Max(1, (int)Math.Round(
            _config.Observation.PersonBeliefBaseCapacity +
            _config.Observation.PersonBeliefCapacityPerStableCommunication * stableCommunication,
            MidpointRounding.AwayFromZero));
    }

    private void Maintain(NpcState observer, int tick)
    {
        var before = observer.Knowledge.CapacityRemovalCount;
        observer.Knowledge.MaintainPersons(
            observer, tick, _config.Observation.PersonBeliefTtlDays, PersonCapacity(observer),
            _config.Observation.ThreatMemoryDays);
        var removed = observer.Knowledge.CapacityRemovalCount - before;
        if (removed > 0)
        {
            Interlocked.Add(ref _evictionCount, removed);
        }
    }

    private void ObserveOwnSettlement(WorldState world, NpcState observer)
    {
        if (SettlementQueries.ActiveSettlement(world, observer.SettlementId) is not { } own)
        {
            return;
        }
        AddSettlementField(observer, own.Id, SettlementBeliefField.ActiveStatus, 1, null, null,
            1, observer.Id, world.Tick, KnowledgeSourceType.Self, "active");
        AddSettlementField(observer, own.Id, SettlementBeliefField.Center, null, own.Center, null,
            1, observer.Id, world.Tick, KnowledgeSourceType.Self, "center");
        var relation = own.ParentSettlementId.HasValue || own.ChildSettlementIds.Count > 0
            ? $"parent={own.ParentSettlementId?.ToString() ?? "-"};children={string.Join(',', own.ChildSettlementIds.Order())}"
            : "parent=-;children=";
        AddSettlementField(observer, own.Id, SettlementBeliefField.ParentChild, null, null, relation,
            1, observer.Id, world.Tick, KnowledgeSourceType.Self, "relations");
    }

    private void AddNoisy(
        NpcState observer,
        long subjectId,
        PersonBeliefField field,
        double realityValue,
        double confidence,
        double maximumError,
        int tick)
    {
        var stream = _random.Create("observation", tick, observer.Id, "numeric-error", $"{subjectId}:{field}");
        AddPersonField(observer, subjectId, field, realityValue * (1 + stream.NextDouble(-maximumError, maximumError)),
            null, null, confidence, observer.Id, tick, KnowledgeSourceType.DirectObservation, "numeric");
    }

    private sealed record CachedPerceptionView(
        int Tick,
        long InformationVersion,
        Position OwnerPosition,
        IReadOnlySet<long> ActiveThreatIds,
        PerceptionView View);
}

public sealed class PerceptionView
{
    private readonly IReadOnlyDictionary<long, PerceivedEntity> _entitiesById;
    private readonly IReadOnlyList<PerceivedEntity> _entities;
    private readonly IReadOnlyList<PerceivedEntity> _threats;

    public PerceptionView(
        IEnumerable<PersonBelief> beliefs,
        IReadOnlySet<long> threatIds,
        Position ownerPosition,
        int maximumDistance)
    {
        _entities = beliefs.OrderBy(item => item.SubjectId)
            .Select(item => BuildEntity(item, threatIds.Contains(item.SubjectId)))
            .Where(item => item.Position.HasValue &&
                           ownerPosition.ChebyshevDistance(item.Position.Value) <= maximumDistance)
            .ToArray();
        _entitiesById = _entities.ToDictionary(item => item.EntityId);
        _threats = _entities.Where(item => item.IsThreat).ToArray();
    }

    public IReadOnlyList<PerceivedEntity> Entities => _entities;
    public IReadOnlyList<PerceivedEntity> Threats => _threats;
    public PerceivedEntity? Find(long subjectId) => _entitiesById.GetValueOrDefault(subjectId);

    private static PerceivedEntity BuildEntity(PersonBelief belief, bool isThreat)
    {
        double? Read(PersonBeliefField field) => belief.Fields.TryGetValue(field, out var value) ? value.Number : null;
        Position? position = belief.Fields.TryGetValue(PersonBeliefField.Position, out var knownPosition)
            ? knownPosition.Position
            : null;
        var alive = Read(PersonBeliefField.AliveStatus);
        var stage = Read(PersonBeliefField.LifeStage);
        return new PerceivedEntity(
            belief.SubjectId,
            position,
            alive.HasValue ? alive.Value >= 0.5 : null,
            Read(PersonBeliefField.EstimatedHp),
            Read(PersonBeliefField.EstimatedCombat),
            stage.HasValue ? (PerceivedLifeStage)(int)Math.Round(stage.Value) : null,
            isThreat);
    }
}

public sealed record PerceivedEntity(
    long EntityId,
    Position? Position,
    bool? IsAlive,
    double? CurrentHp,
    double? Combat,
    PerceivedLifeStage? LifeStage,
    bool IsThreat);
