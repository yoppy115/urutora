using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simulation.Core.Configuration;

public sealed class SimulationConfig
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = string.Empty;
    public WorldConfig World { get; set; } = new();
    public InitialPopulationConfig InitialPopulation { get; set; } = new();
    public ActionConfig Action { get; set; } = new();
    public NeedsConfig Needs { get; set; } = new();
    public ObservationConfig Observation { get; set; } = new();
    public UtilityConfig Utility { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public CommunicationConfig Communication { get; set; } = new();
    public ReproductionConfig Reproduction { get; set; } = new();
    public VitalityConfig Vitality { get; set; } = new();
    public ConceptConfig Concept { get; set; } = new();

    public void Validate()
    {
        var errors = new List<string>();
        Require(SchemaVersion == 1, "schemaVersion must be 1.", errors);
        Require(!string.IsNullOrWhiteSpace(Id), "id is required.", errors);
        Require(World.Width > 2 && World.Height > 2, "world dimensions must be greater than 2.", errors);
        Require(World.DaysPerYear > 0, "world.daysPerYear must be positive.", errors);
        Require(World.InitialPopulation > 0, "world.initialPopulation must be positive.", errors);
        Require(World.Landmarks.Count == 3, "world.landmarks must contain the three v0 concepts.", errors);
        Require(World.Landmarks.Select(item => item.Concept).Distinct().Count() == 3,
            "world.landmarks must use distinct concepts.", errors);
        Require(World.Landmarks.Select(item => (item.X, item.Y)).Distinct().Count() == 3,
            "world.landmarks must occupy distinct cells.", errors);
        Require(World.InitialPopulation <= World.Width * World.Height - World.Landmarks.Count,
            "world.initialPopulation exceeds non-Landmark cell capacity.", errors);
        foreach (var landmark in World.Landmarks)
        {
            Require(landmark.X >= 0 && landmark.X < World.Width && landmark.Y >= 0 && landmark.Y < World.Height,
                $"landmark {landmark.Concept} is outside the world.", errors);
        }

        Require(InitialPopulation.MaxHpStandardDeviation >= 0, "initialPopulation.maxHpStandardDeviation cannot be negative.", errors);
        Require(InitialPopulation.AbilityStandardDeviation >= 0, "initialPopulation.abilityStandardDeviation cannot be negative.", errors);
        Require(InitialPopulation.MinimumMaxHp > 0 && InitialPopulation.MaximumMaxHp >= InitialPopulation.MinimumMaxHp,
            "initialPopulation MaxHP bounds are invalid.", errors);
        Require(InitialPopulation.MinimumAgeDays >= 0 && InitialPopulation.MaximumAgeDays >= InitialPopulation.MinimumAgeDays,
            "initialPopulation age bounds are invalid.", errors);
        Require(InitialPopulation.InitialNeedMinimum >= 0 && InitialPopulation.InitialNeedMaximum <= 10 &&
                InitialPopulation.InitialNeedMaximum >= InitialPopulation.InitialNeedMinimum,
            "initialPopulation need bounds must be within 0..10.", errors);
        Require(new[]
            {
                InitialPopulation.MaxHpMean, InitialPopulation.MaxHpStandardDeviation,
                InitialPopulation.MinimumMaxHp, InitialPopulation.MaximumMaxHp,
                InitialPopulation.AbilityMean, InitialPopulation.AbilityStandardDeviation,
                InitialPopulation.RiskPreferenceMean, InitialPopulation.RiskPreferenceStandardDeviation,
                InitialPopulation.InitialNeedMinimum, InitialPopulation.InitialNeedMaximum
            }.All(double.IsFinite), "initialPopulation values must be finite.", errors);

        Require(Action.MaximumActionsPerDay >= 1, "action.maximumActionsPerDay must be positive.", errors);
        Require(Action.RepeatDenominator > 0, "action.repeatDenominator must be positive.", errors);
        Require(Action.SecondStepFactor >= 0, "action.secondStepFactor cannot be negative.", errors);

        Require(Observation.MaximumDistance == 3, "v0 observation.maximumDistance must be 3.", errors);
        Require(Observation.ErrorFactor >= 0, "observation.errorFactor cannot be negative.", errors);
        Require(Observation.ConfidenceByDistance.Count == Observation.MaximumDistance + 1,
            "observation.confidenceByDistance must include indexes 0..maximumDistance.", errors);
        Require(Observation.ConfidenceByDistance.All(value => value is >= 0 and <= 1),
            "observation confidence must be within 0..1.", errors);
        Require(Observation.ThreatMemoryDays > 0, "observation.threatMemoryDays must be positive.", errors);
        Require(Observation.HeldInformationCapacityPerSubjectProperty == 3,
            "v0.15 observation.heldInformationCapacityPerSubjectProperty must be 3.", errors);

        Require(double.IsFinite(Utility.Temperature) && Utility.Temperature > 0,
            "utility.temperature must be finite and greater than zero.", errors);
        Require(Utility.TopCandidates == 3, "v0 utility.topCandidates must be 3.", errors);
        Require(AllUtilityEffects().All(double.IsFinite), "utility coefficients must be finite.", errors);

        Require(Combat.HitChanceMinimum is >= 0 and <= 1 && Combat.HitChanceMaximum is >= 0 and <= 1 &&
                Combat.HitChanceMaximum >= Combat.HitChanceMinimum, "combat hit chance bounds are invalid.", errors);
        Require(Combat.DamageRandomMinimum > 0 && Combat.DamageRandomMaximum >= Combat.DamageRandomMinimum,
            "combat damage random bounds are invalid.", errors);

        Require(Communication.Range == 2, "v0 communication.range must be 2.", errors);
        Require(Communication.SendCountAbilityDivisor > 0, "communication.sendCountAbilityDivisor must be positive.", errors);
        Require(Communication.ConfidenceBase is >= 0 and <= 1, "communication.confidenceBase must be within 0..1.", errors);
        Require(Communication.ConfidencePerAbility >= 0, "communication.confidencePerAbility cannot be negative.", errors);

        Require(Reproduction.Range == 1, "v0 reproduction.range must be 1.", errors);
        Require(Reproduction.MatureAgeDays >= 0, "reproduction.matureAgeDays cannot be negative.", errors);
        Require(Reproduction.CooldownDays >= 0, "reproduction.cooldownDays cannot be negative.", errors);
        Require(Reproduction.MinimumHpRatio is >= 0 and <= 1, "reproduction.minimumHpRatio must be within 0..1.", errors);
        Require(Reproduction.NeedThreshold is >= 0 and <= 10, "reproduction.needThreshold must be within 0..10.", errors);
        Require(Reproduction.MutationChance is >= 0 and <= 1, "reproduction.mutationChance must be within 0..1.", errors);
        Require(Reproduction.MutationStandardDeviation >= 0, "reproduction.mutationStandardDeviation cannot be negative.", errors);
        Require(Reproduction.MaxHpMutationScale > 0, "reproduction.maxHpMutationScale must be positive.", errors);
        Require(Reproduction.NewbornInitialNeed is >= 0 and <= 10, "reproduction.newbornInitialNeed must be within 0..10.", errors);

        ValidateVitalityCurve(errors);

        Require(Concept.ExposureThreshold > 0, "concept.exposureThreshold must be positive.", errors);
        Require(double.IsFinite(Concept.EffectiveMultiplier) && Concept.EffectiveMultiplier >= 1,
            "concept.effectiveMultiplier must be finite and at least 1.", errors);
        Require(Concept.ExposureByDistance.Count == 4, "concept.exposureByDistance must include exactly indexes 0..3.", errors);
        Require(Concept.ExposureByDistance.All(value => value >= 0), "concept exposure cannot be negative.", errors);

        if (errors.Count > 0)
        {
            throw new ConfigurationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void Require(bool condition, string message, ICollection<string> errors)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }

    private IEnumerable<double> AllUtilityEffects()
    {
        foreach (var effect in new[]
                 {
                     Utility.Move, Utility.Rest, Utility.Communication,
                     Utility.Reproduction, Utility.Attack, Utility.Flee
                 })
        {
            yield return effect.Survival;
            yield return effect.Rest;
            yield return effect.Activity;
            yield return effect.Communication;
            yield return effect.Reproduction;
        }
    }

    private void ValidateVitalityCurve(ICollection<string> errors)
    {
        var points = Vitality.ControlPoints;
        Require(points.Count >= 7, "vitality.controlPoints must contain at least seven points.", errors);
        Require(points.Count == points.Select(item => item.AgeDays).Distinct().Count(),
            "vitality.controlPoints ageDays must be unique.", errors);
        Require(points.SequenceEqual(points.OrderBy(item => item.AgeDays)),
            "vitality.controlPoints must be ordered by ageDays.", errors);
        Require(points.Count > 0 && points[0].AgeDays == 0,
            "vitality.controlPoints must start at ageDays 0.", errors);
        Require(points.All(item => item.AgeDays >= 0 && double.IsFinite(item.DailyVitalChange)),
            "vitality.controlPoints values must be non-negative ages and finite changes.", errors);

        if (points.Count == 0)
        {
            return;
        }

        double Sample(int ageDays)
        {
            if (ageDays <= points[0].AgeDays)
            {
                return points[0].DailyVitalChange;
            }

            for (var index = 0; index < points.Count - 1; index++)
            {
                var first = points[index];
                var second = points[index + 1];
                if (ageDays > second.AgeDays)
                {
                    continue;
                }

                var t = (double)(ageDays - first.AgeDays) / (second.AgeDays - first.AgeDays);
                var smooth = t * t * (3 - 2 * t);
                return first.DailyVitalChange + (second.DailyVitalChange - first.DailyVitalChange) * smooth;
            }

            return points[^1].DailyVitalChange;
        }

        var halfYear = World.DaysPerYear / 2;
        var oneYear = World.DaysPerYear;
        var oneAndHalfYears = (World.DaysPerYear * 3 + 1) / 2;
        var twoAndHalfYears = (World.DaysPerYear * 5 + 1) / 2;
        var threeYears = World.DaysPerYear * 3;
        Require(Sample(0) >= 0 && Sample(halfYear) > Sample(0),
            "vitality curve must increase recovery strength from birth to 0.5 years.", errors);
        Require(Sample(halfYear) > 0 && Sample(oneYear) > 0,
            "vitality curve must provide strong recovery from 0.5 to 1 year.", errors);
        Require(Sample(oneAndHalfYears) <= 1e-9,
            "vitality curve must not recover at or after 1.5 years.", errors);
        Require(Sample(twoAndHalfYears) < 0,
            "vitality curve must decay by 2.5 years.", errors);
        Require(Sample(threeYears) < Sample(twoAndHalfYears),
            "vitality curve must accelerate decay by 3 years.", errors);
    }
}

public sealed class WorldConfig
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int InitialPopulation { get; set; }
    public int DaysPerYear { get; set; }
    public List<LandmarkConfig> Landmarks { get; set; } = new();
}

public sealed class LandmarkConfig
{
    public string Concept { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class InitialPopulationConfig
{
    public double MaxHpMean { get; set; }
    public double MaxHpStandardDeviation { get; set; }
    public double MinimumMaxHp { get; set; }
    public double MaximumMaxHp { get; set; }
    public double AbilityMean { get; set; }
    public double AbilityStandardDeviation { get; set; }
    public double RiskPreferenceMean { get; set; }
    public double RiskPreferenceStandardDeviation { get; set; }
    public int MinimumAgeDays { get; set; }
    public int MaximumAgeDays { get; set; }
    public double InitialNeedMinimum { get; set; }
    public double InitialNeedMaximum { get; set; }
}

public sealed class ActionConfig
{
    public int MaximumActionsPerDay { get; set; }
    public double RepeatDenominator { get; set; }
    public double SecondStepFactor { get; set; }
    public double ActiveActivityChange { get; set; }
    public double ActiveRestChange { get; set; }
    public double RestRestChange { get; set; }
    public double RestActivityChange { get; set; }
}

public sealed class NeedsConfig
{
    public double DailyActivityIncrease { get; set; }
    public double DailyRestIncrease { get; set; }
    public double DailyCommunicationIncrease { get; set; }
    public double DailyReproductionIncrease { get; set; }
    public double InitiatedCommunicationChange { get; set; }
    public double SuccessfulReproductionChange { get; set; }
}

public sealed class ObservationConfig
{
    public int MaximumDistance { get; set; }
    public double ErrorFactor { get; set; }
    public List<double> ConfidenceByDistance { get; set; } = new();
    public int ThreatMemoryDays { get; set; }
    public int HeldInformationCapacityPerSubjectProperty { get; set; }
}

public sealed class UtilityConfig
{
    public int TopCandidates { get; set; }
    public double Temperature { get; set; }
    public UtilityEffectConfig Move { get; set; } = new();
    public UtilityEffectConfig Rest { get; set; } = new();
    public UtilityEffectConfig Communication { get; set; } = new();
    public UtilityEffectConfig Reproduction { get; set; } = new();
    public UtilityEffectConfig Attack { get; set; } = new();
    public UtilityEffectConfig Flee { get; set; } = new();
}

public sealed class UtilityEffectConfig
{
    public double Survival { get; set; }
    public double Rest { get; set; }
    public double Activity { get; set; }
    public double Communication { get; set; }
    public double Reproduction { get; set; }
}

public sealed class CombatConfig
{
    public double HitChanceBase { get; set; }
    public double HitChancePerCombatDifference { get; set; }
    public double HitChanceMinimum { get; set; }
    public double HitChanceMaximum { get; set; }
    public double DamageBase { get; set; }
    public double DamageAttackerFactor { get; set; }
    public double DamageDefenderFactor { get; set; }
    public double DamageMinimum { get; set; }
    public double DamageRandomMinimum { get; set; }
    public double DamageRandomMaximum { get; set; }
    public double CounterattackCombatFactor { get; set; }
}

public sealed class CommunicationConfig
{
    public int Range { get; set; }
    public double SendCountAbilityDivisor { get; set; }
    public double ErrorMaximumBase { get; set; }
    public double SubjectSwapChanceBase { get; set; }
    public double ConfidenceBase { get; set; }
    public double ConfidencePerAbility { get; set; }
}

public sealed class ReproductionConfig
{
    public int Range { get; set; }
    public int MatureAgeDays { get; set; }
    public int CooldownDays { get; set; }
    public double NeedThreshold { get; set; }
    public double MinimumHpRatio { get; set; }
    public double MutationChance { get; set; }
    public double MutationStandardDeviation { get; set; }
    public double MaxHpMutationScale { get; set; }
    public double NewbornInitialNeed { get; set; }
}

public sealed class VitalityConfig
{
    public List<VitalityControlPointConfig> ControlPoints { get; set; } = new();
}

public sealed class VitalityControlPointConfig
{
    public int AgeDays { get; set; }
    public double DailyVitalChange { get; set; }
}

public sealed class ConceptConfig
{
    public List<double> ExposureByDistance { get; set; } = new();
    public double ExposureThreshold { get; set; }
    public double EffectiveMultiplier { get; set; }
}

public static class SimulationConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static SimulationConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadJson(File.ReadAllText(path), path);
    }

    public static SimulationConfig LoadJson(string json, string sourceDescription = "embedded replay configuration")
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);
        SimulationConfig config;
        try
        {
            config = JsonSerializer.Deserialize<SimulationConfig>(json, Options)
                ?? throw new ConfigurationException("Configuration root cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException(
                $"Invalid configuration JSON at {sourceDescription}: {exception.Message}", exception);
        }

        config.Validate();
        return config;
    }
}

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
