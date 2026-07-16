using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Defines the command rank levels an officer can hold.
    /// </summary>
    public enum OfficerRank
    {
        None,
        Commander,
        General,
        Admiral,
    }

    /// <summary>
    /// Force rank display labels.
    /// </summary>
    public enum ForceRankLabel
    {
        None,
        Novice,
        Trainee,
        ForceStudent,
        ForceKnight,
        ForceMaster,
    }

    public enum OfficerVoiceLineType
    {
        Order,
        PersonnelArrived,
        MissionSuccess,
        MissionFailure,
        MissionAbort,
        Released,
        Recovered,
        EnemyDetected,
        ForceGrowth,
        ForceUserDiscovered,
        ForceAbilityRevealed,
        TraitorDiscovered,
        RescueAttempt,
        BountyAttack,
        DagobahCompleted,
        SeatOfPower,
    }

    /// <summary>
    /// Represents an officer that can be used in missions.
    /// </summary>
    public class Officer : LeafNode, IMissionParticipant, IMovable
    {
        private const int _ratingPercentScale = 100;

        // Research Info.
        public int ShipResearch { get; set; }
        public int TroopResearch { get; set; }
        public int FacilityResearch { get; set; }

        // Officer Info.
        public bool IsMain { get; set; }
        public bool IsRecruitable { get; set; }
        public bool IsCaptured { get; set; }
        public string CaptorInstanceID { get; set; }
        public bool CanEscape { get; set; }
        public bool IsKilled { get; set; }
        public bool CanBetray { get; set; }
        public bool IsTraitor { get; set; }
        public int Loyalty { get; set; }
        public int HyperdriveModifier { get; set; }

        // Injury Info.
        public int InjuryPoints { get; set; }
        public bool CanHeal { get; set; }
        public bool FastHeal { get; set; }

        // Force Info.
        public int JediProbability { get; set; }
        public int JediLevel { get; set; }
        public int JediLevelVariance { get; set; }
        public bool IsJediTrainer { get; set; }
        public bool GrowsForceOnMission { get; set; }

        [PersistableIgnore]
        public bool IsKnownJedi { get; set; }

        public bool IsJedi { get; set; }
        public bool IsForceEligible { get; set; }
        public int ForceValue { get; set; }
        public int ForceTrainingAdjustment { get; set; }

        [PersistableIgnore]
        public int ForceRank => ForceValue + ForceTrainingAdjustment;

        public bool IsDiscoveringForceUser { get; set; }

        // Rank Info.
        public OfficerRank[] AllowedRanks { get; set; }
        public OfficerRank CurrentRank { get; set; }

        // Owner Info.
        public string InitialParentTypeID { get; set; }
        public string InitialParentInstanceID { get; set; }

        // Variance Info.
        [PersistableIgnore]
        public int DiplomacyVariance { get; set; }

        [PersistableIgnore]
        public int EspionageVariance { get; set; }

        [PersistableIgnore]
        public int CombatVariance { get; set; }

        [PersistableIgnore]
        public int LeadershipVariance { get; set; }

        [PersistableIgnore]
        public int LoyaltyVariance { get; set; }

        [PersistableIgnore]
        public int FacilityResearchVariance { get; set; }

        [PersistableIgnore]
        public int TroopResearchVariance { get; set; }

        [PersistableIgnore]
        public int ShipResearchVariance { get; set; }

        // Movement Info.
        public MovementState Movement { get; set; }
        public bool UsesAdvancedVoiceLines { get; set; }
        public List<string> OrderVoicePaths { get; set; } = new List<string>();
        public List<string> PersonnelArrivedVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedPersonnelArrivedVoicePaths { get; set; } = new List<string>();
        public List<string> MissionSuccessVoicePaths { get; set; } = new List<string>();
        public List<string> MissionFailureVoicePaths { get; set; } = new List<string>();
        public List<string> MissionAbortVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedMissionAbortVoicePaths { get; set; } = new List<string>();
        public List<string> ReleasedVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedReleasedVoicePaths { get; set; } = new List<string>();
        public List<string> RecoveredVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedRecoveredVoicePaths { get; set; } = new List<string>();
        public List<string> EnemyDetectedVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedEnemyDetectedVoicePaths { get; set; } = new List<string>();
        public List<string> ForceGrowthVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedForceGrowthVoicePaths { get; set; } = new List<string>();
        public List<string> ForceUserDiscoveredVoicePaths { get; set; } = new List<string>();
        public List<string> ForceAbilityRevealedVoicePaths { get; set; } = new List<string>();
        public List<string> TraitorDiscoveredVoicePaths { get; set; } = new List<string>();
        public List<string> RescueAttemptVoicePaths { get; set; } = new List<string>();
        public List<string> AdvancedRescueAttemptVoicePaths { get; set; } = new List<string>();
        public List<string> BountyAttackVoicePaths { get; set; } = new List<string>();
        public List<string> DagobahCompletedVoicePaths { get; set; } = new List<string>();
        public List<string> SeatOfPowerVoicePaths { get; set; } = new List<string>();

        // Mission rating info.
        public Dictionary<OfficerRating, int> Ratings { get; set; } =
            new Dictionary<OfficerRating, int>
            {
                { OfficerRating.Diplomacy, 0 },
                { OfficerRating.Espionage, 0 },
                { OfficerRating.Combat, 0 },
                { OfficerRating.Leadership, 0 },
            };
        public bool CanImproveMissionRating => true;

        /// <summary>
        /// Returns whether this officer can perform a mission type.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID to inspect.</param>
        /// <returns>True if the officer can perform the mission type.</returns>
        public bool CanPerformMission(string missionTypeId) =>
            missionTypeId != MissionTypeIDs.Reconnaissance;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Officer() { }

        /// <summary>
        /// Returns the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to query.</param>
        /// <returns>The stored rating value.</returns>
        public int GetBaseRating(OfficerRating rating)
        {
            return rating switch
            {
                OfficerRating.ShipResearch => ShipResearch,
                OfficerRating.TroopResearch => TroopResearch,
                OfficerRating.FacilityResearch => FacilityResearch,
                OfficerRating.None => 0,
                _ => Ratings.TryGetValue(rating, out int value) ? value : 0,
            };
        }

        /// <summary>
        /// Sets the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to update.</param>
        /// <param name="value">The new rating value.</param>
        /// <returns>The stored rating value.</returns>
        public int SetBaseRating(OfficerRating rating, int value)
        {
            switch (rating)
            {
                case OfficerRating.ShipResearch:
                    ShipResearch = value;
                    return value;
                case OfficerRating.TroopResearch:
                    TroopResearch = value;
                    return value;
                case OfficerRating.FacilityResearch:
                    FacilityResearch = value;
                    return value;
                case OfficerRating.None:
                    return 0;
                default:
                    Ratings[rating] = value;
                    return value;
            }
        }

        /// <summary>
        /// Returns the officer's current value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to query.</param>
        /// <returns>The rating value after officer-specific modifiers.</returns>
        public int GetEffectiveRating(OfficerRating rating)
        {
            int baseRating = GetBaseRating(rating);
            return rating switch
            {
                OfficerRating.Diplomacy => ApplyForceRatingBonus(baseRating),
                OfficerRating.Espionage => ApplyForceRatingBonus(baseRating),
                OfficerRating.Combat => Math.Max(
                    0,
                    ApplyForceRatingBonus(baseRating) - InjuryPoints
                ),
                _ => baseRating,
            };
        }

        /// <summary>
        /// Increments the officer's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to increment.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(OfficerRating rating, int amount = 1)
        {
            SetBaseRating(rating, GetBaseRating(rating) + amount);
        }

        /// <summary>
        /// Returns the base research value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>The officer's base research value for that discipline.</returns>
        public int GetBaseRating(ResearchDiscipline discipline)
        {
            return GetBaseRating(GetRatingForResearchDiscipline(discipline));
        }

        /// <summary>
        /// Returns the base research value for the given manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The officer's base research value for that manufacturing type.</returns>
        public int GetBaseRating(ManufacturingType manufacturingType)
        {
            return GetBaseRating(manufacturingType.ToResearchDiscipline());
        }

        /// <summary>
        /// Adds the specified amount to the base research value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to update.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(ResearchDiscipline discipline, int amount = 1)
        {
            IncrementBaseRating(GetRatingForResearchDiscipline(discipline), amount);
        }

        /// <summary>
        /// Adds the specified amount to the base research value for the given manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to update.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(ManufacturingType manufacturingType, int amount = 1)
        {
            IncrementBaseRating(manufacturingType.ToResearchDiscipline(), amount);
        }

        /// <summary>
        /// Returns whether the officer is currently assigned to a mission.
        /// </summary>
        /// <returns>True if the officer's parent is a <see cref="Mission"/>; otherwise, false.</returns>
        public bool IsOnMission()
        {
            return GetParent() is Mission;
        }

        /// <summary>
        /// Returns whether the officer is a Jedi whose Force potential has not yet been revealed.
        /// </summary>
        /// <returns>True if this officer is an undiscovered Force user.</returns>
        public bool IsUndiscoveredForceUser()
        {
            return IsJedi && !IsForceEligible && !IsCaptured && !IsKilled && !IsOnMission();
        }

        /// <summary>
        /// Adds the specified amount to injury points, clamped to [0, maxInjury].
        /// </summary>
        /// <param name="amount">The amount of injury to apply.</param>
        /// <param name="maxInjury">Upper bound for injury points.</param>
        public void ApplyInjury(int amount, int maxInjury)
        {
            InjuryPoints = Math.Min(maxInjury, Math.Max(0, InjuryPoints + amount));
        }

        /// <summary>
        /// Subtracts the specified amount from injury points, floored at zero.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        public void Heal(int amount)
        {
            InjuryPoints = Math.Max(0, InjuryPoints - amount);
        }

        /// <summary>
        /// Maps a research discipline to the officer rating that stores its base value.
        /// </summary>
        /// <param name="discipline">The research discipline to map.</param>
        /// <returns>The corresponding officer rating, or <see cref="OfficerRating.None"/>.</returns>
        public static OfficerRating GetRatingForResearchDiscipline(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => OfficerRating.ShipResearch,
                ResearchDiscipline.FacilityDesign => OfficerRating.FacilityResearch,
                ResearchDiscipline.TroopTraining => OfficerRating.TroopResearch,
                _ => OfficerRating.None,
            };
        }

        /// <summary>
        /// Returns a rating value after applying this officer's Force bonus.
        /// </summary>
        /// <param name="baseRating">The stored rating value.</param>
        /// <returns>The Force-adjusted rating value.</returns>
        private int ApplyForceRatingBonus(int baseRating)
        {
            return baseRating + (baseRating * ForceRank) / _ratingPercentScale;
        }

        /// <summary>
        /// Returns whether the officer can be ordered to move.
        /// </summary>
        /// <returns>True if the officer has no active movement state and is not on an incomplete mission; otherwise, false.</returns>
        public bool IsMovable()
        {
            if (GetParent() is Mission mission)
                return mission.IsComplete() && Movement == null;
            return Movement == null;
        }

        public string GetVoicePath(
            OfficerVoiceLineType voiceLineType,
            IRandomNumberProvider provider
        )
        {
            return SelectVoicePath(GetVoicePaths(voiceLineType), provider);
        }

        public bool HasVoicePath(OfficerVoiceLineType voiceLineType)
        {
            IReadOnlyList<string> paths = GetVoicePaths(voiceLineType);
            return paths?.Count > 0;
        }

        private IReadOnlyList<string> GetVoicePaths(OfficerVoiceLineType voiceLineType)
        {
            IReadOnlyList<string> advancedPaths = voiceLineType switch
            {
                OfficerVoiceLineType.PersonnelArrived => AdvancedPersonnelArrivedVoicePaths,
                OfficerVoiceLineType.MissionAbort => AdvancedMissionAbortVoicePaths,
                OfficerVoiceLineType.Released => AdvancedReleasedVoicePaths,
                OfficerVoiceLineType.Recovered => AdvancedRecoveredVoicePaths,
                OfficerVoiceLineType.EnemyDetected => AdvancedEnemyDetectedVoicePaths,
                OfficerVoiceLineType.ForceGrowth => AdvancedForceGrowthVoicePaths,
                OfficerVoiceLineType.RescueAttempt => AdvancedRescueAttemptVoicePaths,
                _ => null,
            };
            if (UsesAdvancedVoiceLines && advancedPaths?.Count > 0)
                return advancedPaths;

            return voiceLineType switch
            {
                OfficerVoiceLineType.Order => OrderVoicePaths,
                OfficerVoiceLineType.PersonnelArrived => PersonnelArrivedVoicePaths,
                OfficerVoiceLineType.MissionSuccess => MissionSuccessVoicePaths,
                OfficerVoiceLineType.MissionFailure => MissionFailureVoicePaths,
                OfficerVoiceLineType.MissionAbort => MissionAbortVoicePaths,
                OfficerVoiceLineType.Released => ReleasedVoicePaths,
                OfficerVoiceLineType.Recovered => RecoveredVoicePaths,
                OfficerVoiceLineType.EnemyDetected => EnemyDetectedVoicePaths,
                OfficerVoiceLineType.ForceGrowth => ForceGrowthVoicePaths,
                OfficerVoiceLineType.ForceUserDiscovered => ForceUserDiscoveredVoicePaths,
                OfficerVoiceLineType.ForceAbilityRevealed => ForceAbilityRevealedVoicePaths,
                OfficerVoiceLineType.TraitorDiscovered => TraitorDiscoveredVoicePaths,
                OfficerVoiceLineType.RescueAttempt => RescueAttemptVoicePaths,
                OfficerVoiceLineType.BountyAttack => BountyAttackVoicePaths,
                OfficerVoiceLineType.DagobahCompleted => DagobahCompletedVoicePaths,
                OfficerVoiceLineType.SeatOfPower => SeatOfPowerVoicePaths,
                _ => null,
            };
        }

        private static string SelectVoicePath(
            IReadOnlyList<string> paths,
            IRandomNumberProvider provider
        )
        {
            if (paths == null || paths.Count == 0)
                return null;

            if (paths.Count == 1 || provider == null)
                return paths[0];

            return paths[provider.NextInt(0, paths.Count)];
        }
    }
}
