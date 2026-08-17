using System.Collections.Concurrent;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Perception;

public sealed class PerceptionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;
    private readonly ConcurrentDictionary<long, CachedPerceptionView> _viewCache = new();

    public PerceptionSystem(SimulationConfig config, RandomStreamFactory random)
    {
        _config = config;
        _random = random;
    }

    private long _evictionCount;
    private long _subjectPurgeCount;
    private long _positionInvalidationCount;

    public long EvictionCount => Interlocked.Read(ref _evictionCount);
    public long SubjectPurgeCount => Interlocked.Read(ref _subjectPurgeCount);
    public long PositionInvalidationCount => Interlocked.Read(ref _positionInvalidationCount);

    public void Observe(WorldState realitySnapshot)
    {
        var alive = realitySnapshot.Npcs.Values
            .Where(npc => npc.IsAlive)
            .OrderBy(npc => npc.Id)
            .ToArray();
        var spatialIndex = alive
            .GroupBy(npc => npc.Position)
            .ToDictionary(group => group.Key, group => group.OrderBy(npc => npc.Id).ToArray());
        var deadSpatialIndex = realitySnapshot.Npcs.Values
            .Where(npc => !npc.IsAlive)
            .GroupBy(npc => npc.Position)
            .ToDictionary(group => group.Key, group => group.OrderBy(npc => npc.Id).ToArray());
        var degree = ParallelExecutionPolicy.ResolveDegree(_config.Performance, alive.Length);

        if (degree == 1)
        {
            foreach (var observer in alive)
            {
                ObserveOne(realitySnapshot, observer, spatialIndex, deadSpatialIndex);
            }
            return;
        }

        Parallel.ForEach(
            alive,
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            observer => ObserveOne(realitySnapshot, observer, spatialIndex, deadSpatialIndex));
    }

    private void ObserveOne(
        WorldState realitySnapshot,
        NpcState observer,
        IReadOnlyDictionary<Position, NpcState[]> spatialIndex,
        IReadOnlyDictionary<Position, NpcState[]> deadSpatialIndex)
    {
        var maximumDistance = _config.Observation.MaximumDistance;
        var directlyConfirmedGone = new List<long>();
        for (var y = observer.Position.Y - maximumDistance; y <= observer.Position.Y + maximumDistance; y++)
        {
            for (var x = observer.Position.X - maximumDistance; x <= observer.Position.X + maximumDistance; x++)
            {
                if (!deadSpatialIndex.TryGetValue(new Position(x, y), out var subjects))
                {
                    continue;
                }
                directlyConfirmedGone.AddRange(subjects
                    .Where(subject => observer.HeldInformation.ContainsSubject(subject.Id))
                    .Select(subject => subject.Id));
            }
        }
        directlyConfirmedGone.Sort();
        foreach (var subjectId in directlyConfirmedGone)
        {
            PurgeSubject(observer, subjectId);
        }

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

        foreach (var subject in nearby.OrderBy(item => item.Id))
        {
            if (observer.Id == subject.Id)
            {
                continue;
            }

            var distance = observer.Position.ChebyshevDistance(subject.Position);
            if (distance is < 1 || distance > maximumDistance)
            {
                continue;
            }

            var confidence = _config.Observation.ConfidenceByDistance[distance];
            Add(observer, subject.Id, InformationProperty.PositionX, subject.Position.X, confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
            Add(observer, subject.Id, InformationProperty.PositionY, subject.Position.Y, confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
            Add(observer, subject.Id, InformationProperty.Alive, 1, confidence, realitySnapshot.Tick, InformationAcquisition.Observation);

            var maxError = _config.Observation.ErrorFactor * (distance + 1);
            AddNoisy(observer, subject.Id, InformationProperty.CurrentHp, subject.CurrentHp, confidence, maxError, realitySnapshot.Tick);
            AddNoisy(observer, subject.Id, InformationProperty.Combat, subject.EffectiveStats(_config).Combat, confidence, maxError, realitySnapshot.Tick);
            Add(observer, subject.Id, InformationProperty.LifeStage,
                subject.IsMature(_config) ? (double)PerceivedLifeStage.Mature : (double)PerceivedLifeStage.Child,
                confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
            Add(observer, subject.Id, InformationProperty.MarkStruggle, subject.ConceptMarks.Contains(ConceptKind.Struggle) ? 1 : 0,
                confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
            Add(observer, subject.Id, InformationProperty.MarkSurvival, subject.ConceptMarks.Contains(ConceptKind.Survival) ? 1 : 0,
                confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
            Add(observer, subject.Id, InformationProperty.MarkCommunication, subject.ConceptMarks.Contains(ConceptKind.Communication) ? 1 : 0,
                confidence, realitySnapshot.Tick, InformationAcquisition.Observation);
        }
    }

    public void RecordThreat(NpcState victim, NpcState attacker, int tick)
    {
        victim.ThreatMemory[attacker.Id] = new ThreatMemory(attacker.Id, tick);
        Add(victim, attacker.Id, InformationProperty.PositionX, attacker.Position.X, 1, tick, InformationAcquisition.DirectOutcome);
        Add(victim, attacker.Id, InformationProperty.PositionY, attacker.Position.Y, 1, tick, InformationAcquisition.DirectOutcome);
        Add(victim, attacker.Id, InformationProperty.Alive, attacker.IsAlive ? 1 : 0, 1, tick, InformationAcquisition.DirectOutcome);
        Add(victim, attacker.Id, InformationProperty.CurrentHp, Math.Max(attacker.CurrentHp, 0), 1, tick, InformationAcquisition.DirectOutcome);
    }

    public void RecordCombatOutcome(NpcState observer, NpcState subject, int tick)
    {
        if (!subject.IsAlive)
        {
            PurgeSubject(observer, subject.Id);
            return;
        }

        Add(observer, subject.Id, InformationProperty.PositionX, subject.Position.X, 1, tick, InformationAcquisition.DirectOutcome);
        Add(observer, subject.Id, InformationProperty.PositionY, subject.Position.Y, 1, tick, InformationAcquisition.DirectOutcome);
        Add(observer, subject.Id, InformationProperty.Alive, subject.IsAlive ? 1 : 0, 1, tick, InformationAcquisition.DirectOutcome);
        Add(observer, subject.Id, InformationProperty.CurrentHp, Math.Max(subject.CurrentHp, 0), 1, tick, InformationAcquisition.DirectOutcome);
    }

    public bool InvalidatePosition(NpcState observer, long subjectId)
    {
        var removed = observer.HeldInformation.RemoveAll(item =>
            item.SubjectId == subjectId && item.Property is InformationProperty.PositionX or InformationProperty.PositionY);
        if (removed > 0)
        {
            Interlocked.Increment(ref _positionInvalidationCount);
            return true;
        }

        return false;
    }

    public bool PurgeSubject(NpcState observer, long subjectId)
    {
        var removed = observer.HeldInformation.RemoveAll(item => item.SubjectId == subjectId);
        observer.ThreatMemory.Remove(subjectId);
        if (removed > 0)
        {
            Interlocked.Increment(ref _subjectPurgeCount);
            return true;
        }

        return false;
    }

    public void AddInformation(
        NpcState observer,
        long subjectId,
        InformationProperty property,
        double value,
        double confidence,
        long sourceId,
        int tick,
        InformationAcquisition acquisition,
        string? stableScope = null)
    {
        var sequence = observer.NextInformationSequence++;
        var id = StableHash.StableId(
            "info", observer.Id, subjectId, property, tick, acquisition, sourceId, sequence, stableScope ?? string.Empty);
        var evicted = observer.HeldInformation.AddBounded(new InformationRecord(
            id,
            subjectId,
            property,
            value,
            confidence,
            sourceId,
            acquisition,
            tick), _config.Observation.HeldInformationCapacityPerSubjectProperty);
        if (evicted > 0)
        {
            Interlocked.Add(ref _evictionCount, evicted);
        }
    }

    public PerceptionView CreateView(NpcState owner, int tick)
    {
        var informationVersion = owner.HeldInformation.RepresentativeVersion;
        var activeThreats = owner.ThreatMemory.Values
            .Where(item => tick - item.LastThreatTick <= _config.Observation.ThreatMemoryDays)
            .Select(item => item.SubjectId)
            .ToHashSet();
        if (_viewCache.TryGetValue(owner.Id, out var cached) &&
            cached.Tick == tick && cached.InformationVersion == informationVersion &&
            cached.OwnerPosition == owner.Position && cached.ActiveThreatIds.SetEquals(activeThreats))
        {
            return cached.View;
        }

        var view = new PerceptionView(
            owner.HeldInformation.RepresentativeSubjectsNear(owner.Position, _config.Observation.MaximumDistance),
            activeThreats);
        _viewCache[owner.Id] = new CachedPerceptionView(tick, informationVersion, owner.Position, activeThreats, view);
        return view;
    }

    public static double TransmissionConfidence(double sourceConfidence, double receiverEffectiveCommunication, CommunicationConfig config)
    {
        var quality = Math.Clamp(receiverEffectiveCommunication, 0, 10);
        var factor = config.ConfidenceBase + config.ConfidencePerAbility * quality;
        return Math.Clamp(sourceConfidence * factor, 0, 1);
    }

    private void AddNoisy(
        NpcState observer,
        long subjectId,
        InformationProperty property,
        double realityValue,
        double confidence,
        double maximumError,
        int tick)
    {
        var stream = _random.Create("observation", tick, observer.Id, "numeric-error", $"{subjectId}:{property}");
        var error = stream.NextDouble(-maximumError, maximumError);
        Add(observer, subjectId, property, realityValue * (1 + error), confidence, tick, InformationAcquisition.Observation);
    }

    private void Add(
        NpcState observer,
        long subjectId,
        InformationProperty property,
        double value,
        double confidence,
        int tick,
        InformationAcquisition acquisition)
    {
        AddInformation(observer, subjectId, property, value, confidence, observer.Id, tick, acquisition);
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
        IEnumerable<KeyValuePair<long, IReadOnlyDictionary<InformationProperty, InformationRecord>>> records,
        IReadOnlySet<long> threatIds)
    {
        _entities = records
            .OrderBy(item => item.Key)
            .Select(item => BuildEntity(item.Key, item.Value, threatIds.Contains(item.Key)))
            .ToArray();
        _entitiesById = _entities.ToDictionary(item => item.EntityId);
        _threats = _entities.Where(item => item.IsThreat).ToArray();
    }

    public IReadOnlyList<PerceivedEntity> Entities => _entities;

    public IReadOnlyList<PerceivedEntity> Threats => _threats;

    public PerceivedEntity? Find(long subjectId)
    {
        return _entitiesById.GetValueOrDefault(subjectId);
    }

    private PerceivedEntity BuildEntity(
        long subjectId,
        IReadOnlyDictionary<InformationProperty, InformationRecord> properties,
        bool isThreat)
    {
        static double? Read(IReadOnlyDictionary<InformationProperty, InformationRecord> source, InformationProperty property) =>
            source.TryGetValue(property, out var record) ? record.EstimatedValue : null;

        Position? position = null;
        var x = Read(properties, InformationProperty.PositionX);
        var y = Read(properties, InformationProperty.PositionY);
        if (x.HasValue && y.HasValue)
        {
            position = new Position((int)Math.Round(x.Value), (int)Math.Round(y.Value));
        }

        var alive = Read(properties, InformationProperty.Alive);
        var lifeStage = Read(properties, InformationProperty.LifeStage);
        return new PerceivedEntity(
            subjectId,
            position,
            alive.HasValue ? alive.Value >= 0.5 : null,
            Read(properties, InformationProperty.CurrentHp),
            Read(properties, InformationProperty.Combat),
            lifeStage.HasValue ? (PerceivedLifeStage)(int)Math.Round(lifeStage.Value) : null,
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
