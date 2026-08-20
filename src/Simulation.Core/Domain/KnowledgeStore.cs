namespace Simulation.Core.Domain;

public enum KnowledgeSourceType
{
    Communication,
    ParticipantEvent,
    DirectOutcome,
    Threat,
    DirectObservation,
    Self
}

public enum PersonBeliefField
{
    AliveStatus,
    Position,
    EstimatedHp,
    EstimatedCombat,
    LifeStage,
    SettlementAffiliation,
    ConceptMarks
}

public enum SettlementBeliefField
{
    ActiveStatus,
    Center,
    PopulationEstimate,
    Relation,
    ParentChild,
    KnownConcepts
}

public sealed record BeliefValue(
    string InformationId,
    KnowledgeSourceType SourceType,
    long? SourceId,
    double Confidence,
    int UpdatedTick,
    double? Number = null,
    Position? Position = null,
    string? Text = null,
    IReadOnlySet<ConceptKind>? Concepts = null);

public sealed class PersonBelief
{
    public required long SubjectId { get; init; }
    public int LastRecognizedTick { get; set; }
    public bool EverDirectlyObserved { get; set; }
    public Dictionary<PersonBeliefField, BeliefValue> Fields { get; } = new();

    public bool HearsayOnly => !EverDirectlyObserved &&
                               Fields.Values.All(item => item.SourceType == KnowledgeSourceType.Communication);

    public double AggregateConfidence => Enum.GetValues<PersonBeliefField>()
        .Average(field => Fields.TryGetValue(field, out var value) ? Math.Clamp(value.Confidence, 0, 1) : 0);
}

public sealed record EventBelief(
    string EventId,
    SimulationEventType EventType,
    int EventTick,
    int? PinImportance,
    KnowledgeSourceType SourceType,
    long? SourceId,
    double Confidence,
    int UpdatedTick,
    string Detail);

public sealed class SettlementBelief
{
    public required int SettlementId { get; init; }
    public Dictionary<SettlementBeliefField, BeliefValue> Fields { get; } = new();
}

public sealed class KnowledgeStore
{
    private readonly Dictionary<long, PersonBelief> _persons = new();
    private readonly Dictionary<string, EventBelief> _events = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SettlementBelief> _settlements = new();

    public IReadOnlyDictionary<long, PersonBelief> Persons => _persons;
    public IReadOnlyDictionary<string, EventBelief> Events => _events;
    public IReadOnlyDictionary<int, SettlementBelief> Settlements => _settlements;
    public long Version { get; private set; }
    public long TtlRemovalCount { get; private set; }
    public long CapacityRemovalCount { get; private set; }
    public long DeathRecognitionRemovalCount { get; private set; }

    public bool UpsertPerson(
        long subjectId,
        PersonBeliefField field,
        BeliefValue value,
        bool directlyObserved)
    {
        if (!_persons.TryGetValue(subjectId, out var belief))
        {
            belief = new PersonBelief { SubjectId = subjectId };
            _persons.Add(subjectId, belief);
        }

        belief.LastRecognizedTick = Math.Max(belief.LastRecognizedTick, value.UpdatedTick);
        belief.EverDirectlyObserved |= directlyObserved;
        if (belief.Fields.TryGetValue(field, out var existing) && !ShouldReplace(existing, value))
        {
            return false;
        }

        belief.Fields[field] = value;
        Version++;
        return true;
    }

    public bool InvalidatePersonPosition(long subjectId)
    {
        if (!_persons.TryGetValue(subjectId, out var belief) ||
            !belief.Fields.Remove(PersonBeliefField.Position))
        {
            return false;
        }

        Version++;
        return true;
    }

    public bool RemovePerson(long subjectId, bool deathRecognition = false)
    {
        if (!_persons.Remove(subjectId))
        {
            return false;
        }

        if (deathRecognition)
        {
            DeathRecognitionRemovalCount++;
        }
        Version++;
        return true;
    }

    public void MaintainPersons(NpcState owner, int tick, int ttlDays, int capacity, int threatMemoryDays = int.MaxValue)
    {
        foreach (var subjectId in _persons.Values
                     .Where(item => tick - item.LastRecognizedTick >= ttlDays)
                     .Select(item => item.SubjectId)
                     .OrderBy(item => item)
                     .ToArray())
        {
            _persons.Remove(subjectId);
            TtlRemovalCount++;
            Version++;
        }

        while (_persons.Count > capacity)
        {
            var remove = _persons.Values
                .Select(item => new
                {
                    Belief = item,
                    Protected = IsProtected(owner, item, tick, threatMemoryDays),
                    Distance = item.Fields.TryGetValue(PersonBeliefField.Position, out var position) &&
                               position.Position.HasValue
                        ? owner.Position.ChebyshevDistance(position.Position.Value)
                        : int.MaxValue
                })
                .OrderBy(item => item.Protected)
                .ThenByDescending(item => item.Belief.HearsayOnly)
                .ThenBy(item => item.Belief.LastRecognizedTick)
                .ThenBy(item => item.Belief.AggregateConfidence)
                .ThenByDescending(item => item.Distance)
                .ThenBy(item => item.Belief.SubjectId)
                .First();
            _persons.Remove(remove.Belief.SubjectId);
            CapacityRemovalCount++;
            Version++;
        }
    }

    public void UpsertEvent(EventBelief belief)
    {
        if (_events.TryGetValue(belief.EventId, out var existing) &&
            existing.UpdatedTick > belief.UpdatedTick)
        {
            return;
        }
        _events[belief.EventId] = belief;
        Version++;
    }

    public bool UpsertSettlement(int settlementId, SettlementBeliefField field, BeliefValue value)
    {
        if (!_settlements.TryGetValue(settlementId, out var belief))
        {
            belief = new SettlementBelief { SettlementId = settlementId };
            _settlements.Add(settlementId, belief);
        }

        if (belief.Fields.TryGetValue(field, out var existing) && !ShouldReplace(existing, value))
        {
            return false;
        }
        belief.Fields[field] = value;
        Version++;
        return true;
    }

    public static bool ShouldReplace(BeliefValue existing, BeliefValue candidate)
    {
        var source = SourceRank(candidate.SourceType).CompareTo(SourceRank(existing.SourceType));
        if (source != 0)
        {
            return source > 0;
        }
        var tick = candidate.UpdatedTick.CompareTo(existing.UpdatedTick);
        if (tick != 0)
        {
            return tick > 0;
        }
        var confidence = candidate.Confidence.CompareTo(existing.Confidence);
        if (confidence != 0)
        {
            return confidence > 0;
        }
        return string.Compare(candidate.InformationId, existing.InformationId, StringComparison.Ordinal) < 0;
    }

    private static int SourceRank(KnowledgeSourceType source) => source switch
    {
        KnowledgeSourceType.Self => 5,
        KnowledgeSourceType.DirectObservation => 4,
        KnowledgeSourceType.DirectOutcome => 4,
        KnowledgeSourceType.Threat => 4,
        KnowledgeSourceType.ParticipantEvent => 3,
        _ => 1
    };

    private static bool IsProtected(NpcState owner, PersonBelief belief, int tick, int threatMemoryDays)
    {
        if (owner.ThreatMemory.TryGetValue(belief.SubjectId, out var threat) &&
            tick - threat.LastThreatTick >= 0 &&
            tick - threat.LastThreatTick <= threatMemoryDays)
        {
            return true;
        }
        if (owner.SettlementId.HasValue &&
            belief.Fields.TryGetValue(PersonBeliefField.SettlementAffiliation, out var settlement) &&
            settlement.Number == owner.SettlementId.Value)
        {
            return true;
        }
        return belief.EverDirectlyObserved;
    }
}
