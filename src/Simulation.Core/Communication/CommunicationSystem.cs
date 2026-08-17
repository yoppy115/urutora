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
        var initiatorSelection = SelectForTransmission(initiator, tick, microRound, "forward");
        var targetSelection = SelectForTransmission(target, tick, microRound, "return");
        var sentByInitiator = Transmit(initiator, target, initiatorSelection, tick, microRound, "forward");
        var sentByTarget = Transmit(target, initiator, targetSelection, tick, microRound, "return");
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

    private int Transmit(
        NpcState sender,
        NpcState receiver,
        IReadOnlyList<InformationRecord> selected,
        int tick,
        int microRound,
        string direction)
    {
        if (selected.Count == 0)
        {
            return 0;
        }

        var receiverCommunication = receiver.EffectiveStats(_config).Communication;
        var errorMaximum = ErrorMaximum(receiverCommunication);
        var swapChance = SubjectSwapChance(receiverCommunication);
        var knownSubjects = receiver.HeldInformation.OrderedSubjectIds();

        foreach (var information in selected)
        {
            var subjectId = information.SubjectId;
            var swapStream = _random.Create(
                "communication", tick, sender.Id, "subject-swap", $"{direction}:{microRound}:{information.InformationId}:{receiver.Id}");
            if (knownSubjects.Length > 0 && swapStream.NextDouble() < swapChance)
            {
                var existingIndex = Array.BinarySearch(knownSubjects, subjectId);
                var replacementCount = knownSubjects.Length - (existingIndex >= 0 ? 1 : 0);
                if (replacementCount > 0)
                {
                    var replacementIndex = swapStream.NextInt(replacementCount);
                    if (existingIndex >= 0 && replacementIndex >= existingIndex)
                    {
                        replacementIndex++;
                    }
                    subjectId = knownSubjects[replacementIndex];
                }
            }

            var value = information.EstimatedValue;
            if (information.Property is InformationProperty.CurrentHp or InformationProperty.Combat)
            {
                var distortion = _random.Create(
                    "communication", tick, sender.Id, "numeric-distortion", $"{direction}:{microRound}:{information.InformationId}:{receiver.Id}")
                    .NextDouble(-errorMaximum, errorMaximum);
                value *= 1 + distortion;
            }

            var confidence = PerceptionSystem.TransmissionConfidence(
                information.Confidence,
                receiverCommunication,
                _config.Communication);
            _perception.AddInformation(
                receiver,
                subjectId,
                information.Property,
                value,
                confidence,
                sender.Id,
                tick,
                InformationAcquisition.Communication,
                $"{information.InformationId}:{microRound}:{direction}");
        }

        return selected.Count;
    }

    private InformationRecord[] SelectForTransmission(
        NpcState sender,
        int tick,
        int microRound,
        string direction)
    {
        if (sender.HeldInformation.Count == 0)
        {
            return Array.Empty<InformationRecord>();
        }

        var senderCommunication = sender.EffectiveStats(_config).Communication;
        var sendCount = 1 + (int)Math.Floor(senderCommunication / _config.Communication.SendCountAbilityDivisor);
        sendCount = Math.Min(sendCount, sender.HeldInformation.Count);
        var random = _random.Create(
            "communication",
            tick,
            sender.Id,
            "held-information-selection",
            $"{direction}:{microRound}");
        var selectedRanks = new HashSet<int>();
        var selected = new List<InformationRecord>(sendCount);
        while (selected.Count < sendCount)
        {
            var rank = random.NextInt(sender.HeldInformation.Count);
            if (selectedRanks.Add(rank))
            {
                selected.Add(sender.HeldInformation.RecordAtSamplingIndex(rank));
            }
        }

        return selected.ToArray();
    }
}

public sealed record CommunicationResult(int SentByInitiator, int SentByTarget);
