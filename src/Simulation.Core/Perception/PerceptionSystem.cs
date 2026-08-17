using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Randomness;

namespace Simulation.Core.Perception;

public sealed class PerceptionSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;

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
        var degree = ParallelExecutionPolicy.ResolveDegree(_config.Performance, alive.Length);

        if (degree == 1)
        {
            foreach (var observer in alive)
            {
                ObserveOne(realitySnapshot, observer, spatialIndex);
            }
            return;
        }

        Parallel.ForEach(
            alive,
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            observer => ObserveOne(realitySnapshot, observer, spatialIndex));
    }

    private void ObserveOne(
        WorldState realitySnapshot,
        NpcState observer,
        IReadOnlyDictionary<Position, NpcState[]> spatialIndex)
    {
        var maximumDistance = _config.Observation.MaximumDistance;
        var directlyConfirmedGone = observer.HeldInformation
            .Select(item => item.SubjectId)
            .Distinct()
            .Where(subjectId => realitySnapshot.Npcs.TryGetValue(subjectId, out var subject) &&
                                !subject.IsAlive &&
                                observer.Position.ChebyshevDistance(subject.Position) <= maximumDistance)
            .OrderBy(item => item)
            .ToArray();
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
        observer.HeldInformation.Add(new InformationRecord(
            id,
            subjectId,
            property,
            value,
            confidence,
            sourceId,
            acquisition,
            tick));

        var matching = observer.HeldInformation
            .Select((item, index) => (Item: item, Index: index))
            .Where(item => item.Item.SubjectId == subjectId && item.Item.Property == property)
            .ToArray();
        while (matching.Length > _config.Observation.HeldInformationCapacityPerSubjectProperty)
        {
            observer.HeldInformation.RemoveAt(matching[0].Index);
            Interlocked.Increment(ref _evictionCount);
            matching = observer.HeldInformation
                .Select((item, index) => (Item: item, Index: index))
                .Where(item => item.Item.SubjectId == subjectId && item.Item.Property == property)
                .ToArray();
        }
    }

    public PerceptionView CreateView(NpcState owner, int tick)
    {
        var representative = owner.HeldInformation
            .GroupBy(item => (item.SubjectId, item.Property))
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .ThenByDescending(item => item.AcquiredTick)
                .ThenBy(item => item.InformationId, StringComparer.Ordinal)
                .First())
            .GroupBy(item => item.SubjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<InformationProperty, InformationRecord>)group.ToDictionary(item => item.Property));

        var activeThreats = owner.ThreatMemory.Values
            .Where(item => tick - item.LastThreatTick <= _config.Observation.ThreatMemoryDays)
            .Select(item => item.SubjectId)
            .ToHashSet();

        return new PerceptionView(representative, activeThreats);
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
}

public sealed class PerceptionView
{
    private readonly IReadOnlyDictionary<long, IReadOnlyDictionary<InformationProperty, InformationRecord>> _records;
    private readonly IReadOnlySet<long> _threatIds;
    private readonly IReadOnlyList<PerceivedEntity> _entities;
    private readonly IReadOnlyList<PerceivedEntity> _threats;

    public PerceptionView(
        IReadOnlyDictionary<long, IReadOnlyDictionary<InformationProperty, InformationRecord>> records,
        IReadOnlySet<long> threatIds)
    {
        _records = records;
        _threatIds = threatIds;
        _entities = _records
            .OrderBy(item => item.Key)
            .Select(item => BuildEntity(item.Key, item.Value))
            .ToArray();
        _threats = _entities.Where(item => item.IsThreat).ToArray();
    }

    public IReadOnlyList<PerceivedEntity> Entities => _entities;

    public IReadOnlyList<PerceivedEntity> Threats => _threats;

    public PerceivedEntity? Find(long subjectId)
    {
        return _records.TryGetValue(subjectId, out var properties) ? BuildEntity(subjectId, properties) : null;
    }

    private PerceivedEntity BuildEntity(
        long subjectId,
        IReadOnlyDictionary<InformationProperty, InformationRecord> properties)
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
            _threatIds.Contains(subjectId));
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
