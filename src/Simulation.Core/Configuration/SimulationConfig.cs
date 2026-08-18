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
    public PerformanceConfig Performance { get; set; } = new();
    public UtilityConfig Utility { get; set; } = new();
    public CombatConfig Combat { get; set; } = new();
    public CommunicationConfig Communication { get; set; } = new();
    public ReproductionConfig Reproduction { get; set; } = new();
    public VitalityConfig Vitality { get; set; } = new();
    public ConceptConfig Concept { get; set; } = new();
    public SettlementConfig Settlement { get; set; } = new();
    public InvasionConfig Invasion { get; set; } = new();
    public AuraConfig Aura { get; set; } = new();
    public EventHistoryConfig EventHistory { get; set; } = new();

    public void Validate()
    {
        var errors = new List<string>();
        Require(SchemaVersion == 4, "schemaVersion must be 4.", errors);
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
        Require(new[]
        {
            Action.Fatigue.Communication,
            Action.Fatigue.Move,
            Action.Fatigue.ReproductionAttempt,
            Action.Fatigue.Attack,
            Action.Fatigue.CollisionAttack,
            Action.Fatigue.Flee,
            Action.Fatigue.Counterattack,
            Action.Fatigue.Pursuit
        }.All(value => double.IsFinite(value) && value >= 0),
            "action fatigue values must be finite and non-negative.", errors);
        Require(double.IsFinite(Action.RestPressure.Threshold) && Action.RestPressure.Threshold is >= 0 and < 10 &&
                double.IsFinite(Action.RestPressure.Scale) && Action.RestPressure.Scale > 0 &&
                double.IsFinite(Action.RestPressure.ActivityPenalty) && Action.RestPressure.ActivityPenalty >= 0,
            "action.restPressure values are invalid.", errors);

        Require(Observation.MaximumDistance == 3, "v0 observation.maximumDistance must be 3.", errors);
        Require(Observation.ErrorFactor >= 0, "observation.errorFactor cannot be negative.", errors);
        Require(Observation.ConfidenceByDistance.Count == Observation.MaximumDistance + 1,
            "observation.confidenceByDistance must include indexes 0..maximumDistance.", errors);
        Require(Observation.ConfidenceByDistance.All(value => value is >= 0 and <= 1),
            "observation confidence must be within 0..1.", errors);
        Require(Observation.ThreatMemoryDays > 0, "observation.threatMemoryDays must be positive.", errors);
        Require(Observation.PersonBeliefBaseCapacity > 0 &&
                Observation.PersonBeliefCapacityPerStableCommunication >= 0 &&
                Observation.PersonBeliefTtlDays > 0,
            "v0.2.5 PersonBelief capacity or TTL values are invalid.", errors);
        Require(Performance.MaximumDegreeOfParallelism is >= 0 and <= 128,
            "performance.maximumDegreeOfParallelism must be within 0..128.", errors);
        Require(Performance.MinimumPopulationForParallelism > 0,
            "performance.minimumPopulationForParallelism must be positive.", errors);

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
        Require(Concept.ExposureByDistance.Count == 5, "v0.2 concept.exposureByDistance must include exactly indexes 0..4.", errors);
        Require(Concept.ExposureByDistance.All(value => value >= 0), "concept exposure cannot be negative.", errors);

        ValidateSettlement(errors);
        ValidateInvasion(errors);
        Require(Aura.Radius == 2, "v0.2 aura.radius must be 2.", errors);
        Require(double.IsFinite(Aura.RestNeedDailyReduction) && Aura.RestNeedDailyReduction >= 0,
            "aura.restNeedDailyReduction must be finite and non-negative.", errors);
        Require(double.IsFinite(Aura.EffectiveMultiplier) && Aura.EffectiveMultiplier >= 1,
            "aura.effectiveMultiplier must be finite and at least 1.", errors);
        Require(EventHistory.RecentEventCapacity > 0,
            "eventHistory.recentEventCapacity must be positive.", errors);

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
                     Utility.Move, Utility.Communication,
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

    private void ValidateSettlement(ICollection<string> errors)
    {
        Require(Settlement.HotspotWindowDays > 0, "settlement.hotspotWindowDays must be positive.", errors);
        Require(Settlement.HotspotWindowSize > 0 &&
                Settlement.HotspotWindowSize <= Math.Min(World.Width, World.Height),
            "settlement.hotspotWindowSize must fit the World.", errors);
        Require(Settlement.HotspotSuccessThreshold > 0, "settlement.hotspotSuccessThreshold must be positive.", errors);
        Require(Settlement.EvaluationIntervalDays > 0, "settlement.evaluationIntervalDays must be positive.", errors);
        Require(Settlement.MinimumCenterDistance >= 0, "settlement.minimumCenterDistance cannot be negative.", errors);
        Require(Settlement.CoreRadius >= 0 && Settlement.InfluenceRadius >= Settlement.CoreRadius,
            "settlement radii are invalid.", errors);
        Require(Settlement.FounderAffinity >= Settlement.MembershipThreshold,
            "settlement.founderAffinity must reach membershipThreshold.", errors);
        Require(Settlement.CoreResidentAffinity >= 0 && Settlement.MembershipThreshold > 0 &&
                Settlement.MembershipSwitchMargin >= 0,
            "settlement affinity thresholds are invalid.", errors);
        Require(new[]
        {
            Settlement.StayAffinityDaily,
            Settlement.RestAffinity,
            Settlement.CommunicationAffinity,
            Settlement.ReproductionSuccessAffinity,
            Settlement.PopulationCvMaximum,
            Settlement.DemographicImbalanceMaximum,
            Settlement.OrderRestMultiplier,
            Settlement.PositiveVitalityMultiplier,
            Settlement.NegativeVitalityMultiplier,
            Settlement.OutsideReproductionUtilityPenalty,
            Settlement.InitialHostilityThreshold,
            Settlement.FrictionCollisionIncrease,
            Settlement.FrictionExplicitThreatIncrease,
            Settlement.FrictionDecayAmount,
            Settlement.PressureResidentLoadWeight,
            Settlement.PressureMovementCongestionWeight,
            Settlement.PressureReturnFailureWeight,
            Settlement.CrowdingThreshold,
            Settlement.FrictionMaximum,
            Settlement.MoveFatigueInfluenceMultiplier,
            Settlement.MoveFatigueCoreMultiplier,
            Settlement.HomeBiasTowardWeight,
            Settlement.HomeBiasAwayWeight,
            Settlement.StrongHomeBiasTowardWeight,
            Settlement.StrongHomeBiasAwayWeight,
            Settlement.StrongHomeBiasRestThreshold,
            Settlement.StrongHomeBiasHpRatioThreshold,
            Settlement.ForeignInfluenceEntryWeight,
            Settlement.ForeignCoreEntryWeight,
            Settlement.ForeignExitWeight,
            Settlement.ForeignDeeperWeight,
            Settlement.GenerationPositiveVitalityMultiplier,
            Settlement.GenerationAffinityMultiplier,
            Settlement.SupportPopulationWeight,
            Settlement.SupportReproductionWeight,
            Settlement.SupportSocialWeight,
            Settlement.SupportSocialActionsPerMemberDay,
            Settlement.SupportLowThreshold,
            Settlement.SupportRecoveryThreshold,
            Settlement.SupportRenewalPotentialThreshold,
            Settlement.FissionPressureThreshold,
            Settlement.FissionMigrantRatio,
            Settlement.FissionFounderAffinity,
            Settlement.FissionCoreResidentAffinity,
            Settlement.MigrationBiasWeight
        }.All(double.IsFinite), "settlement numeric values must be finite.", errors);
        Require(Settlement.StayAffinityDaily >= 0 && Settlement.RestAffinity >= 0 &&
                Settlement.CommunicationAffinity >= 0 && Settlement.ReproductionSuccessAffinity >= 0,
            "settlement affinity gains cannot be negative.", errors);
        Require(Settlement.StabilityWindowDays > 1 && Settlement.StabilityConsecutiveDays > 0,
            "settlement stability windows are invalid.", errors);
        Require(Settlement.PopulationCvMaximum >= 0 && Settlement.DemographicImbalanceMaximum is >= 0 and <= 1,
            "settlement Order thresholds are invalid.", errors);
        Require(Settlement.OrderRestMultiplier >= 1 && Settlement.PositiveVitalityMultiplier >= 1 &&
                Settlement.NegativeVitalityMultiplier is >= 0 and <= 1,
            "settlement Order multipliers are invalid.", errors);
        Require(Settlement.OutsideReproductionUtilityPenalty >= 0 && Settlement.RestCollisionRadius >= 0,
            "settlement Order penalties are invalid.", errors);
        Require(Settlement.InitialHostilityThreshold is >= 0 and <= 1,
            "settlement.initialHostilityThreshold must be within 0..1.", errors);
        Require(Settlement.FrictionCollisionIncrease >= 0 && Settlement.FrictionExplicitThreatIncrease >= 0 &&
                Settlement.FrictionDecayIntervalDays > 0 && Settlement.FrictionDecayAmount >= 0 &&
                Settlement.FrictionMaximum > 0,
            "settlement friction values are invalid.", errors);
        Require(Settlement.PressureResidentLoadWeight >= 0 &&
                Settlement.PressureMovementCongestionWeight >= 0 &&
                Settlement.PressureReturnFailureWeight >= 0 &&
                Math.Abs(Settlement.PressureResidentLoadWeight +
                         Settlement.PressureMovementCongestionWeight +
                         Settlement.PressureReturnFailureWeight - 1) < 1e-9,
            "settlement pressure weights must sum to 1.", errors);
        Require(Settlement.CrowdingThreshold is >= 0 and <= 1 && Settlement.CrowdingWindowDays > 0 &&
                Settlement.CrowdingConsecutiveDays > 0,
            "settlement crowding thresholds are invalid.", errors);
        Require(Settlement.MoveFatigueInfluenceMultiplier is > 0 and <= 1 &&
                Settlement.MoveFatigueCoreMultiplier > 0 &&
                Settlement.MoveFatigueCoreMultiplier <= Settlement.MoveFatigueInfluenceMultiplier,
            "settlement Move fatigue multipliers are invalid.", errors);
        Require(Settlement.HomeBiasTowardWeight > 0 && Settlement.HomeBiasAwayWeight > 0 &&
                Settlement.StrongHomeBiasTowardWeight > 0 && Settlement.StrongHomeBiasAwayWeight > 0 &&
                Settlement.StrongHomeBiasRestThreshold is >= 0 and <= 10 &&
                Settlement.StrongHomeBiasHpRatioThreshold is >= 0 and <= 1,
            "settlement Home Bias values are invalid.", errors);
        Require(Settlement.ForeignInfluenceEntryWeight > 0 && Settlement.ForeignCoreEntryWeight > 0 &&
                Settlement.ForeignExitWeight > 0 && Settlement.ForeignDeeperWeight > 0,
            "settlement Foreign avoidance values are invalid.", errors);
        Require(Settlement.GenerationPositiveVitalityMultiplier >= 1 && Settlement.GenerationAffinityMultiplier >= 1,
            "settlement Generation Proto-Order multipliers are invalid.", errors);
        Require(Settlement.SupportWindowDays > 0 && Settlement.SupportFoundingResidentFloor > 0 &&
                Settlement.SupportPopulationWeight >= 0 && Settlement.SupportReproductionWeight >= 0 &&
                Settlement.SupportSocialWeight >= 0 &&
                Math.Abs(Settlement.SupportPopulationWeight + Settlement.SupportReproductionWeight +
                         Settlement.SupportSocialWeight - 100) < 1e-9 &&
                Settlement.SupportSocialActionsPerMemberDay > 0 &&
                Settlement.SupportLowThreshold >= 0 &&
                Settlement.SupportRecoveryThreshold > Settlement.SupportLowThreshold &&
                Settlement.SupportLowDaysForDissolution > 0 &&
                Settlement.SupportRenewalPotentialThreshold is >= 0 and <= 100 &&
                Settlement.SupportRenewalConsecutiveDays > 0,
            "settlement Support or hysteresis values are invalid.", errors);
        Require(Settlement.FissionPressureThreshold is >= 0 and <= 1 &&
                Settlement.FissionPressureConsecutiveDays > 0 &&
                Settlement.FissionResidentWindowDays > 0 &&
                Settlement.FissionResidentDaysThreshold > 0 &&
                Settlement.FissionCurrentResidentsMinimum > 0 &&
                Settlement.FissionMinimumDistance > Settlement.InfluenceRadius &&
                Settlement.FissionMaximumDistance >= Settlement.FissionMinimumDistance &&
                Settlement.FissionMigrantRatio is > 0 and <= 1 &&
                Settlement.FissionMinimumMigrants > 0 &&
                Settlement.FissionFounderAffinity >= Settlement.MembershipThreshold &&
                Settlement.FissionCoreResidentAffinity >= 0 &&
                Settlement.MigrationBiasWeight > 0,
            "settlement Fission values are invalid.", errors);
    }

    private void ValidateInvasion(ICollection<string> errors)
    {
        Require(new[]
        {
            Invasion.MobilizationBase,
            Invasion.MobilizationCrowdingFactor,
            Invasion.MobilizationMinimum,
            Invasion.MobilizationMaximum,
            Invasion.CoreCohortRatio,
            Invasion.AttackOccupationThreshold,
            Invasion.AdvanceBiasWeight,
            Invasion.DefenseBiasWeight,
            Invasion.AuraCohesionWeight,
            Invasion.CrowdingRearmPressureThreshold,
            Invasion.SevereInjuryHpRatio,
            Invasion.AttackCollapseRatio
        }.All(double.IsFinite), "invasion numeric values must be finite.", errors);
        Require(Invasion.MobilizationMinimum is >= 0 and <= 1 &&
                Invasion.MobilizationMaximum is >= 0 and <= 1 &&
                Invasion.MobilizationMaximum >= Invasion.MobilizationMinimum,
            "invasion mobilization bounds are invalid.", errors);
        Require(Invasion.CoreCohortRatio is >= 0 and <= 1 && Invasion.AttackOccupationThreshold is > 0 and <= 1,
            "invasion cohort or occupation ratio is invalid.", errors);
        Require(Invasion.AdvanceBiasWeight > Invasion.AuraCohesionWeight &&
                Invasion.DefenseBiasWeight >= 0 && Invasion.AuraCohesionWeight >= 0,
            "invasion bias weights must keep Advance primary and Cohesion secondary.", errors);
        Require(Invasion.CrowdingRearmPressureThreshold is >= 0 and <= 1 &&
                Invasion.CrowdingRearmConsecutiveDays > 0,
            "invasion crowding re-arm values are invalid.", errors);
        Require(Invasion.SevereInjuryHpRatio is > 0 and < 1 &&
                Invasion.AttackOccupationConsecutiveDays > 0 &&
                Invasion.AttackCollapseRatio is > 0 and < 1 &&
                Invasion.AttackCollapseConsecutiveDays > 0 &&
                Invasion.InfluenceClearConsecutiveDays > 0 &&
                Invasion.StalemateDays > 0 &&
                Invasion.MinimumForceSize > 0,
            "v0.2.5 invasion state or victory values are invalid.", errors);
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
    public double RestRestChange { get; set; }
    public double RestActivityChange { get; set; }
    public ActionFatigueConfig Fatigue { get; set; } = new();
    public RestPressureConfig RestPressure { get; set; } = new();
}

public sealed class ActionFatigueConfig
{
    public double Communication { get; set; }
    public double Move { get; set; }
    public double ReproductionAttempt { get; set; }
    public double Attack { get; set; }
    public double CollisionAttack { get; set; }
    public double Flee { get; set; }
    public double Counterattack { get; set; }
    public double Pursuit { get; set; }
}

public sealed class RestPressureConfig
{
    public double Threshold { get; set; }
    public double Scale { get; set; }
    public double ActivityPenalty { get; set; }
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
    public int PersonBeliefBaseCapacity { get; set; }
    public double PersonBeliefCapacityPerStableCommunication { get; set; }
    public int PersonBeliefTtlDays { get; set; }
}

public sealed class PerformanceConfig
{
    public int MaximumDegreeOfParallelism { get; set; }
    public int MinimumPopulationForParallelism { get; set; } = 128;
}

public sealed class UtilityConfig
{
    public int TopCandidates { get; set; }
    public double Temperature { get; set; }
    public UtilityEffectConfig Move { get; set; } = new();
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

public sealed class SettlementConfig
{
    public int HotspotWindowDays { get; set; }
    public int HotspotWindowSize { get; set; }
    public int HotspotSuccessThreshold { get; set; }
    public int EvaluationIntervalDays { get; set; }
    public int MinimumCenterDistance { get; set; }
    public int CoreRadius { get; set; }
    public int InfluenceRadius { get; set; }
    public double FounderAffinity { get; set; }
    public double CoreResidentAffinity { get; set; }
    public double MembershipThreshold { get; set; }
    public double MembershipSwitchMargin { get; set; }
    public double StayAffinityDaily { get; set; }
    public double RestAffinity { get; set; }
    public double CommunicationAffinity { get; set; }
    public double ReproductionSuccessAffinity { get; set; }
    public int StabilityWindowDays { get; set; }
    public double PopulationCvMaximum { get; set; }
    public double DemographicImbalanceMaximum { get; set; }
    public int StabilityConsecutiveDays { get; set; }
    public double OrderRestMultiplier { get; set; }
    public double PositiveVitalityMultiplier { get; set; }
    public double NegativeVitalityMultiplier { get; set; }
    public double OutsideReproductionUtilityPenalty { get; set; }
    public int RestCollisionRadius { get; set; }
    public double InitialHostilityThreshold { get; set; }
    public double FrictionCollisionIncrease { get; set; }
    public double FrictionExplicitThreatIncrease { get; set; }
    public int FrictionDecayIntervalDays { get; set; }
    public double FrictionDecayAmount { get; set; }
    public double FrictionMaximum { get; set; }
    public double PressureResidentLoadWeight { get; set; }
    public double PressureMovementCongestionWeight { get; set; }
    public double PressureReturnFailureWeight { get; set; }
    public double CrowdingThreshold { get; set; }
    public int CrowdingWindowDays { get; set; }
    public int CrowdingConsecutiveDays { get; set; }
    public double MoveFatigueInfluenceMultiplier { get; set; }
    public double MoveFatigueCoreMultiplier { get; set; }
    public double HomeBiasTowardWeight { get; set; }
    public double HomeBiasAwayWeight { get; set; }
    public double StrongHomeBiasTowardWeight { get; set; }
    public double StrongHomeBiasAwayWeight { get; set; }
    public double StrongHomeBiasRestThreshold { get; set; }
    public double StrongHomeBiasHpRatioThreshold { get; set; }
    public double ForeignInfluenceEntryWeight { get; set; }
    public double ForeignCoreEntryWeight { get; set; }
    public double ForeignExitWeight { get; set; }
    public double ForeignDeeperWeight { get; set; }
    public double GenerationPositiveVitalityMultiplier { get; set; }
    public double GenerationAffinityMultiplier { get; set; }
    public int SupportWindowDays { get; set; }
    public int SupportFoundingResidentFloor { get; set; }
    public double SupportPopulationWeight { get; set; }
    public double SupportReproductionWeight { get; set; }
    public double SupportSocialWeight { get; set; }
    public double SupportSocialActionsPerMemberDay { get; set; }
    public double SupportLowThreshold { get; set; }
    public double SupportRecoveryThreshold { get; set; }
    public int SupportLowDaysForDissolution { get; set; }
    public double SupportRenewalPotentialThreshold { get; set; }
    public int SupportRenewalConsecutiveDays { get; set; }
    public double FissionPressureThreshold { get; set; }
    public int FissionPressureConsecutiveDays { get; set; }
    public int FissionResidentWindowDays { get; set; }
    public int FissionResidentDaysThreshold { get; set; }
    public int FissionCurrentResidentsMinimum { get; set; }
    public int FissionMinimumDistance { get; set; }
    public int FissionMaximumDistance { get; set; }
    public double FissionMigrantRatio { get; set; }
    public int FissionMinimumMigrants { get; set; }
    public double FissionFounderAffinity { get; set; }
    public double FissionCoreResidentAffinity { get; set; }
    public double MigrationBiasWeight { get; set; }
}

public sealed class InvasionConfig
{
    public double MobilizationBase { get; set; }
    public double MobilizationCrowdingFactor { get; set; }
    public double MobilizationMinimum { get; set; }
    public double MobilizationMaximum { get; set; }
    public double CoreCohortRatio { get; set; }
    public double AttackOccupationThreshold { get; set; }
    public double AdvanceBiasWeight { get; set; }
    public double DefenseBiasWeight { get; set; }
    public double AuraCohesionWeight { get; set; }
    public double CrowdingRearmPressureThreshold { get; set; }
    public int CrowdingRearmConsecutiveDays { get; set; }
    public double SevereInjuryHpRatio { get; set; }
    public int AttackOccupationConsecutiveDays { get; set; }
    public double AttackCollapseRatio { get; set; }
    public int AttackCollapseConsecutiveDays { get; set; }
    public int InfluenceClearConsecutiveDays { get; set; }
    public int StalemateDays { get; set; }
    public int MinimumForceSize { get; set; }
}

public sealed class AuraConfig
{
    public int Radius { get; set; }
    public double RestNeedDailyReduction { get; set; }
    public double EffectiveMultiplier { get; set; }
}

public sealed class EventHistoryConfig
{
    public int RecentEventCapacity { get; set; }
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
