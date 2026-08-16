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
        var initiatorSource = initiator.HeldInformation.ToArray();
        var targetSource = target.HeldInformation.ToArray();
        var sentByInitiator = Transmit(initiator, target, initiatorSource, tick, microRound, "forward");
        var sentByTarget = Transmit(target, initiator, targetSource, tick, microRound, "return");
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
        IReadOnlyList<InformationRecord> source,
        int tick,
        int microRound,
        string direction)
    {
        if (source.Count == 0)
        {
            return 0;
        }

        var senderCommunication = sender.EffectiveStats(_config).Communication;
        var sendCount = 1 + (int)Math.Floor(senderCommunication / _config.Communication.SendCountAbilityDivisor);
        sendCount = Math.Min(sendCount, source.Count);
        var selected = source
            .OrderBy(item => _random.StablePriority(
                "communication", tick, sender.Id, "held-information-selection", $"{direction}:{microRound}:{item.InformationId}"))
            .ThenBy(item => item.InformationId, StringComparer.Ordinal)
            .Take(sendCount)
            .ToArray();
        var receiverCommunication = receiver.EffectiveStats(_config).Communication;
        var errorMaximum = ErrorMaximum(receiverCommunication);
        var swapChance = SubjectSwapChance(receiverCommunication);
        var knownSubjects = receiver.HeldInformation
            .Select(item => item.SubjectId)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        foreach (var information in selected)
        {
            var subjectId = information.SubjectId;
            var swapStream = _random.Create(
                "communication", tick, sender.Id, "subject-swap", $"{direction}:{microRound}:{information.InformationId}:{receiver.Id}");
            if (knownSubjects.Length > 0 && swapStream.NextDouble() < swapChance)
            {
                var replacements = knownSubjects.Where(id => id != subjectId).ToArray();
                if (replacements.Length > 0)
                {
                    subjectId = replacements[swapStream.NextInt(replacements.Length)];
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

        return selected.Length;
    }
}

public sealed record CommunicationResult(int SentByInitiator, int SentByTarget);
