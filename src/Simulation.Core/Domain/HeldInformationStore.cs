using System.Collections;

namespace Simulation.Core.Domain;

public sealed class HeldInformationStore : IReadOnlyList<InformationRecord>
{
    private readonly LinkedList<InformationRecord> _items = new();
    private readonly Dictionary<(long SubjectId, InformationProperty Property), Queue<LinkedListNode<InformationRecord>>> _byKey = new();
    private readonly Dictionary<long, int> _subjectCounts = new();
    private readonly Dictionary<(long SubjectId, InformationProperty Property), InformationRecord> _representatives = new();
    private readonly Dictionary<long, Dictionary<InformationProperty, InformationRecord>> _representativesBySubject = new();
    private readonly Dictionary<long, Position> _representativePositions = new();
    private readonly Dictionary<Position, HashSet<long>> _subjectsByRepresentativePosition = new();
    private long[]? _orderedSubjectIds;
    private readonly List<InformationRecord> _samplingRecords = new();
    private readonly Dictionary<string, int> _samplingIndexes = new(StringComparer.Ordinal);

    public int Count => _items.Count;
    public long Version { get; private set; }
    public long RepresentativeVersion { get; private set; }

    public InformationRecord this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var node = index <= Count / 2 ? _items.First : _items.Last;
            if (index <= Count / 2)
            {
                for (var current = 0; current < index; current++)
                {
                    node = node!.Next;
                }
            }
            else
            {
                for (var current = Count - 1; current > index; current--)
                {
                    node = node!.Previous;
                }
            }

            return node!.Value;
        }
    }

    public void Add(InformationRecord record)
    {
        var records = AddCore(record);
        UpdateRepresentative((record.SubjectId, record.Property), records);
    }

    private Queue<LinkedListNode<InformationRecord>> AddCore(InformationRecord record)
    {
        var node = _items.AddLast(record);
        AddSamplingRecord(record);
        var key = (record.SubjectId, record.Property);
        if (!_byKey.TryGetValue(key, out var records))
        {
            records = new Queue<LinkedListNode<InformationRecord>>();
            _byKey.Add(key, records);
        }

        records.Enqueue(node);
        var subjectCount = _subjectCounts.GetValueOrDefault(record.SubjectId);
        _subjectCounts[record.SubjectId] = subjectCount + 1;
        if (subjectCount == 0)
        {
            _orderedSubjectIds = null;
        }
        Version++;
        return records;
    }

    public void AddRange(IEnumerable<InformationRecord> records)
    {
        foreach (var record in records)
        {
            Add(record);
        }
    }

    public int AddBounded(InformationRecord record, int capacityPerSubjectProperty)
    {
        if (capacityPerSubjectProperty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityPerSubjectProperty));
        }

        var records = AddCore(record);
        var evicted = 0;
        while (records.Count > capacityPerSubjectProperty)
        {
            var removed = records.Dequeue();
            _items.Remove(removed);
            RemoveSamplingRecord(removed.Value);
            DecrementSubject(removed.Value.SubjectId);
            evicted++;
            Version++;
        }
        UpdateRepresentative((record.SubjectId, record.Property), records);

        return evicted;
    }

    public int RemoveAll(Predicate<InformationRecord> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var removed = 0;
        var node = _items.First;
        while (node is not null)
        {
            var next = node.Next;
            if (match(node.Value))
            {
                _items.Remove(node);
                removed++;
            }
            node = next;
        }

        if (removed > 0)
        {
            RebuildIndex();
            Version++;
        }

        return removed;
    }

    public IEnumerator<InformationRecord> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public long[] OrderedSubjectIds() => _orderedSubjectIds ??= _subjectCounts.Keys.OrderBy(item => item).ToArray();

    public bool ContainsSubject(long subjectId) => _subjectCounts.ContainsKey(subjectId);

    public InformationRecord RecordAtSamplingIndex(int index)
    {
        return index >= 0 && index < _samplingRecords.Count
            ? _samplingRecords[index]
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    public IEnumerable<KeyValuePair<long, IReadOnlyDictionary<InformationProperty, InformationRecord>>>
        RepresentativeSubjectsNear(Position center, int maximumDistance)
    {
        for (var y = center.Y - maximumDistance; y <= center.Y + maximumDistance; y++)
        {
            for (var x = center.X - maximumDistance; x <= center.X + maximumDistance; x++)
            {
                if (!_subjectsByRepresentativePosition.TryGetValue(new Position(x, y), out var subjects))
                {
                    continue;
                }

                foreach (var subjectId in subjects)
                {
                    yield return new KeyValuePair<long, IReadOnlyDictionary<InformationProperty, InformationRecord>>(
                        subjectId,
                        _representativesBySubject[subjectId]);
                }
            }
        }
    }

    private void RebuildIndex()
    {
        _byKey.Clear();
        _subjectCounts.Clear();
        _representatives.Clear();
        _representativesBySubject.Clear();
        _representativePositions.Clear();
        _subjectsByRepresentativePosition.Clear();
        _orderedSubjectIds = null;
        _samplingRecords.Clear();
        _samplingIndexes.Clear();
        for (var node = _items.First; node is not null; node = node.Next)
        {
            var key = (node.Value.SubjectId, node.Value.Property);
            if (!_byKey.TryGetValue(key, out var records))
            {
                records = new Queue<LinkedListNode<InformationRecord>>();
                _byKey.Add(key, records);
            }
            records.Enqueue(node);
            _subjectCounts[node.Value.SubjectId] = _subjectCounts.GetValueOrDefault(node.Value.SubjectId) + 1;
            AddSamplingRecord(node.Value);
        }
        foreach (var (key, records) in _byKey)
        {
            var representative = Best(records);
            _representatives.Add(key, representative);
            SetSubjectRepresentative(key, representative);
        }
        RepresentativeVersion++;
    }

    private void DecrementSubject(long subjectId)
    {
        var count = _subjectCounts[subjectId] - 1;
        if (count == 0)
        {
            _subjectCounts.Remove(subjectId);
            _orderedSubjectIds = null;
        }
        else
        {
            _subjectCounts[subjectId] = count;
        }
    }

    private void UpdateRepresentative(
        (long SubjectId, InformationProperty Property) key,
        Queue<LinkedListNode<InformationRecord>> records)
    {
        var best = Best(records);
        if (!_representatives.TryGetValue(key, out var current) || current != best)
        {
            _representatives[key] = best;
            SetSubjectRepresentative(key, best);
            RepresentativeVersion++;
        }
    }

    private void SetSubjectRepresentative(
        (long SubjectId, InformationProperty Property) key,
        InformationRecord representative)
    {
        if (!_representativesBySubject.TryGetValue(key.SubjectId, out var properties))
        {
            properties = new Dictionary<InformationProperty, InformationRecord>();
            _representativesBySubject.Add(key.SubjectId, properties);
        }
        properties[key.Property] = representative;
        if (key.Property is InformationProperty.PositionX or InformationProperty.PositionY &&
            properties.TryGetValue(InformationProperty.PositionX, out var x) &&
            properties.TryGetValue(InformationProperty.PositionY, out var y))
        {
            UpdateRepresentativePosition(
                key.SubjectId,
                new Position((int)Math.Round(x.EstimatedValue), (int)Math.Round(y.EstimatedValue)));
        }
    }

    private void UpdateRepresentativePosition(long subjectId, Position position)
    {
        if (_representativePositions.TryGetValue(subjectId, out var previous))
        {
            if (previous == position)
            {
                return;
            }
            if (_subjectsByRepresentativePosition.TryGetValue(previous, out var previousSubjects))
            {
                previousSubjects.Remove(subjectId);
                if (previousSubjects.Count == 0)
                {
                    _subjectsByRepresentativePosition.Remove(previous);
                }
            }
        }

        _representativePositions[subjectId] = position;
        if (!_subjectsByRepresentativePosition.TryGetValue(position, out var subjects))
        {
            subjects = new HashSet<long>();
            _subjectsByRepresentativePosition.Add(position, subjects);
        }
        subjects.Add(subjectId);
    }

    private static InformationRecord Best(IEnumerable<LinkedListNode<InformationRecord>> records)
    {
        InformationRecord? best = null;
        foreach (var node in records)
        {
            var candidate = node.Value;
            if (best is null || candidate.Confidence > best.Confidence ||
                (candidate.Confidence.Equals(best.Confidence) && candidate.AcquiredTick > best.AcquiredTick) ||
                (candidate.Confidence.Equals(best.Confidence) && candidate.AcquiredTick == best.AcquiredTick &&
                 string.CompareOrdinal(candidate.InformationId, best.InformationId) < 0))
            {
                best = candidate;
            }
        }

        return best ?? throw new InvalidOperationException("Held Information index contains an empty key.");
    }

    private void AddSamplingRecord(InformationRecord record)
    {
        if (!_samplingIndexes.TryAdd(record.InformationId, _samplingRecords.Count))
        {
            throw new InvalidOperationException($"Duplicate InformationId: {record.InformationId}");
        }
        _samplingRecords.Add(record);
    }

    private void RemoveSamplingRecord(InformationRecord record)
    {
        if (!_samplingIndexes.Remove(record.InformationId, out var index))
        {
            throw new InvalidOperationException($"Information sampling index lost {record.InformationId}.");
        }

        var lastIndex = _samplingRecords.Count - 1;
        if (index != lastIndex)
        {
            var replacement = _samplingRecords[lastIndex];
            _samplingRecords[index] = replacement;
            _samplingIndexes[replacement.InformationId] = index;
        }
        _samplingRecords.RemoveAt(lastIndex);
    }
}
