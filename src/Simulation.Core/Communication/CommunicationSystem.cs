using Simulation.Core.Configuration;
using Simulation.Core.Domain;
using Simulation.Core.Perception;
using Simulation.Core.Randomness;

namespace Simulation.Core.Communication;

public sealed class CommunicationSystem
{
    private readonly SimulationConfig _config;
    private readonly RandomStreamFactory _random;
    private readonly PerceptionSystem _perception;

    public CommunicationSystem(
        SimulationConfig config,
        RandomStreamFactory random,
        PerceptionSystem? perception = null)
    {
        _config = config;
        _random = random;
        _perception = perception ?? new PerceptionSystem(config, random);
    }

    public CommunicationResult Exchange(NpcState initiator, NpcState target, int tick, int microRound)
    {
        var sentByInitiator = Transmit(initiator, target, tick, microRound, "forward");
        var sentByTarget = Transmit(target, initiator, tick, microRound, "return");
        return new CommunicationResult(sentByInitiator, sentByTarget);
    }

    public double ErrorMaximum(double receiverEffectiveCommunication)
    {
        var quality = Math.Clamp(receiverEffectiveCommunication, 0, 10);
        return _config.Communication.ErrorMaximumBase * (1 - quality / 10);
    }

    public double SubjectSwapChance(double receiverEffectiveCommunication)
    {
        var quality = Math.Clamp(receiverEffectiveCommunication, 0, 10);
        return _config.Communication.SubjectSwapChanceBase * (1 - quality / 10);
    }

    private int Transmit(NpcState sender, NpcState receiver, int tick, int microRound, string direction)
    {
        var senderCommunication = sender.EffectiveStats(_config).Communication;
        var sendCount = 1 + (int)Math.Floor(senderCommunication / _config.Communication.SendCountAbilityDivisor);
        var candidates = CreateDifferential(sender, receiver, tick, microRound, direction)
            .Take(sendCount)
            .ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }

        var receiverCommunication = receiver.EffectiveStats(_config).Communication;
        var errorMaximum = ErrorMaximum(receiverCommunication);
        var knownSubjects = receiver.Knowledge.Persons.Keys.OrderBy(item => item).ToArray();
        foreach (var item in candidates)
        {
            switch (item.Category)
            {
                case KnowledgeCategory.Event:
                    var eventBelief = item.Event!;
                    receiver.Knowledge.UpsertEvent(eventBelief with
                    {
                        SourceType = KnowledgeSourceType.Communication,
                        SourceId = sender.Id,
                        Confidence = PerceptionSystem.TransmissionConfidence(
                            eventBelief.Confidence, receiverCommunication, _config.Communication),
                        UpdatedTick = tick
                    });
                    break;
                case KnowledgeCategory.Settlement:
                    var settlement = item.Settlement!.Value;
                    var settlementValue = Distort(settlement.Value, errorMaximum, sender, receiver, tick,
                        microRound, direction, $"settlement:{settlement.SettlementId}:{settlement.Field}");
                    _perception.AddSettlementField(
                        receiver, settlement.SettlementId, settlement.Field,
                        settlementValue.Number, settlementValue.Position, settlementValue.Text,
                        PerceptionSystem.TransmissionConfidence(
                            settlement.Value.Confidence, receiverCommunication, _config.Communication),
                        sender.Id, tick, KnowledgeSourceType.Communication,
                        $"{settlement.Value.InformationId}:{microRound}:{direction}");
                    break;
                case KnowledgeCategory.Person:
                    var person = item.Person!.Value;
                    var subjectId = MaybeSwapSubject(person.SubjectId, knownSubjects, sender, receiver, tick,
                        microRound, direction, person.Value.InformationId, receiverCommunication);
                    var personValue = Distort(person.Value, errorMaximum, sender, receiver, tick,
                        microRound, direction, $"person:{person.SubjectId}:{person.Field}");
                    _perception.AddPersonField(
                        receiver, subjectId, person.Field,
                        personValue.Number, personValue.Position, personValue.Concepts,
                        PerceptionSystem.TransmissionConfidence(
                            person.Value.Confidence, receiverCommunication, _config.Communication),
                        sender.Id, tick, KnowledgeSourceType.Communication,
                        $"{person.Value.InformationId}:{microRound}:{direction}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        receiver.Knowledge.MaintainPersons(receiver, tick, _config.Observation.PersonBeliefTtlDays,
            _perception.PersonCapacity(receiver), _config.Observation.ThreatMemoryDays);
        return candidates.Length;
    }

    private IEnumerable<Transmission> CreateDifferential(
        NpcState sender,
        NpcState receiver,
        int tick,
        int microRound,
        string direction)
    {
        double Priority(string category, string id) => _random.StablePriority(
            "communication", tick, sender.Id, $"{category}-selection", $"{direction}:{microRound}:{receiver.Id}:{id}");

        foreach (var belief in sender.Knowledge.Events.Values
                     .Where(item => !receiver.Knowledge.Events.ContainsKey(item.EventId))
                     .OrderBy(item => Priority("event", item.EventId))
                     .ThenBy(item => item.EventId, StringComparer.Ordinal))
        {
            yield return new Transmission(KnowledgeCategory.Event, belief, null, null);
        }

        foreach (var item in sender.Knowledge.Settlements.Values
                     .SelectMany(belief => belief.Fields.Select(field =>
                         (SettlementId: belief.SettlementId, Field: field.Key, Value: field.Value)))
                     .Where(item => IsDifferentialSettlement(receiver, item.SettlementId, item.Field, item.Value, tick))
                     .OrderBy(item => Priority("settlement", $"{item.SettlementId}:{item.Field}"))
                     .ThenBy(item => item.SettlementId)
                     .ThenBy(item => item.Field))
        {
            yield return new Transmission(KnowledgeCategory.Settlement, null, item, null);
        }

        foreach (var item in sender.Knowledge.Persons.Values
                     .SelectMany(belief => belief.Fields.Select(field =>
                         (SubjectId: belief.SubjectId, Field: field.Key, Value: field.Value)))
                     .Where(item => IsDifferentialPerson(receiver, item.SubjectId, item.Field, item.Value, tick))
                     .OrderBy(item => Priority("person", $"{item.SubjectId}:{item.Field}"))
                     .ThenBy(item => item.SubjectId)
                     .ThenBy(item => item.Field))
        {
            yield return new Transmission(KnowledgeCategory.Person, null, null, item);
        }
    }

    private static bool IsDifferentialPerson(
        NpcState receiver,
        long subjectId,
        PersonBeliefField field,
        BeliefValue source,
        int tick)
    {
        if (!receiver.Knowledge.Persons.TryGetValue(subjectId, out var belief) ||
            !belief.Fields.TryGetValue(field, out var existing))
        {
            return true;
        }
        return KnowledgeStore.ShouldReplace(existing, source with
        {
            InformationId = $"communication:{source.InformationId}",
            SourceType = KnowledgeSourceType.Communication,
            UpdatedTick = tick
        });
    }

    private static bool IsDifferentialSettlement(
        NpcState receiver,
        int settlementId,
        SettlementBeliefField field,
        BeliefValue source,
        int tick)
    {
        if (!receiver.Knowledge.Settlements.TryGetValue(settlementId, out var belief) ||
            !belief.Fields.TryGetValue(field, out var existing))
        {
            return true;
        }
        return KnowledgeStore.ShouldReplace(existing, source with
        {
            InformationId = $"communication:{source.InformationId}",
            SourceType = KnowledgeSourceType.Communication,
            UpdatedTick = tick
        });
    }

    private BeliefValue Distort(
        BeliefValue value,
        double errorMaximum,
        NpcState sender,
        NpcState receiver,
        int tick,
        int microRound,
        string direction,
        string scope)
    {
        var distortible = scope.Contains(PersonBeliefField.EstimatedHp.ToString(), StringComparison.Ordinal) ||
                          scope.Contains(PersonBeliefField.EstimatedCombat.ToString(), StringComparison.Ordinal) ||
                          scope.Contains(SettlementBeliefField.PopulationEstimate.ToString(), StringComparison.Ordinal);
        if (!value.Number.HasValue || !distortible)
        {
            return value;
        }
        var distortion = _random.Create("communication", tick, sender.Id, "numeric-distortion",
                $"{direction}:{microRound}:{value.InformationId}:{receiver.Id}:{scope}")
            .NextDouble(-errorMaximum, errorMaximum);
        return value with { Number = value.Number.Value * (1 + distortion) };
    }

    private long MaybeSwapSubject(
        long subjectId,
        IReadOnlyList<long> knownSubjects,
        NpcState sender,
        NpcState receiver,
        int tick,
        int microRound,
        string direction,
        string informationId,
        double receiverCommunication)
    {
        var alternatives = knownSubjects.Where(item => item != subjectId).ToArray();
        if (alternatives.Length == 0)
        {
            return subjectId;
        }
        var stream = _random.Create("communication", tick, sender.Id, "subject-swap",
            $"{direction}:{microRound}:{informationId}:{receiver.Id}");
        return stream.NextDouble() < SubjectSwapChance(receiverCommunication)
            ? alternatives[stream.NextInt(alternatives.Length)]
            : subjectId;
    }

    private enum KnowledgeCategory
    {
        Event,
        Settlement,
        Person
    }

    private sealed record Transmission(
        KnowledgeCategory Category,
        EventBelief? Event,
        (int SettlementId, SettlementBeliefField Field, BeliefValue Value)? Settlement,
        (long SubjectId, PersonBeliefField Field, BeliefValue Value)? Person);
}

public sealed record CommunicationResult(int SentByInitiator, int SentByTarget);
