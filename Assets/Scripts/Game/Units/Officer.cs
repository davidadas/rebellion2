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
    /// Groups the image assets that define an officer's current visual identity.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerImageSet
    {
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }

        public void MergeFrom(OfficerImageSet authored)
        {
            if (authored == null)
                return;
            DisplayImagePath = authored.DisplayImagePath ?? DisplayImagePath;
            SmallDisplayImagePath = authored.SmallDisplayImagePath ?? SmallDisplayImagePath;
            MessageImagePath = authored.MessageImagePath ?? MessageImagePath;
            EncyclopediaImagePath = authored.EncyclopediaImagePath ?? EncyclopediaImagePath;
        }
    }

    /// <summary>
    /// Groups the recordings an officer can use for simulation and message events.
    /// </summary>
    [PersistableObject]
    public sealed class OfficerVoiceSet
    {
        [PersistableMember(Name = "Order")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> OrderPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "PersonnelArrived")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> PersonnelArrivedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionSuccess")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionSuccessPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionFailure")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionFailurePaths { get; set; } = new List<string>();

        [PersistableMember(Name = "MissionAbort")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionAbortPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "Released")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ReleasedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "Recovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> RecoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "EnemyDetected")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> EnemyDetectedPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "ForceGrowth")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceGrowthPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "ForceUserDiscovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceUserDiscoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "TraitorDiscovered")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> TraitorDiscoveredPaths { get; set; } = new List<string>();

        [PersistableMember(Name = "RescueAttempt")]
        [PersistableCollectionItem(Name = "Path")]
        public List<string> RescueAttemptPaths { get; set; } = new List<string>();

        /// <summary>
        /// Returns the configured asset paths for one voice-line category.
        /// </summary>
        public IReadOnlyList<string> GetPaths(OfficerVoiceLineType type)
        {
            IReadOnlyList<string> paths = GetMutablePaths(type);
            return paths ?? Array.Empty<string>();
        }

        /// <summary>
        /// Replaces each voice category for which the authored set supplies at least one path.
        /// </summary>
        public void MergeFrom(OfficerVoiceSet authored)
        {
            if (authored == null)
                return;

            foreach (OfficerVoiceLineType type in Enum.GetValues(typeof(OfficerVoiceLineType)))
                ReplaceWhenAuthored(authored.GetMutablePaths(type), GetMutablePaths(type));
        }

        /// <summary>
        /// Returns the mutable collection that canonically stores one voice-line category.
        /// </summary>
        private List<string> GetMutablePaths(OfficerVoiceLineType type) =>
            type switch
            {
                OfficerVoiceLineType.Order => OrderPaths,
                OfficerVoiceLineType.PersonnelArrived => PersonnelArrivedPaths,
                OfficerVoiceLineType.MissionSuccess => MissionSuccessPaths,
                OfficerVoiceLineType.MissionFailure => MissionFailurePaths,
                OfficerVoiceLineType.MissionAbort => MissionAbortPaths,
                OfficerVoiceLineType.Released => ReleasedPaths,
                OfficerVoiceLineType.Recovered => RecoveredPaths,
                OfficerVoiceLineType.EnemyDetected => EnemyDetectedPaths,
                OfficerVoiceLineType.ForceGrowth => ForceGrowthPaths,
                OfficerVoiceLineType.ForceUserDiscovered => ForceUserDiscoveredPaths,
                OfficerVoiceLineType.TraitorDiscovered => TraitorDiscoveredPaths,
                OfficerVoiceLineType.RescueAttempt => RescueAttemptPaths,
                _ => null,
            };

        /// <summary>
        /// Replaces a destination category only when authored paths are present.
        /// </summary>
        private static void ReplaceWhenAuthored(List<string> authored, List<string> destination)
        {
            if (authored == null || authored.Count == 0)
                return;

            destination.Clear();
            destination.AddRange(authored);
        }
    }

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
    /// Defines the display labels assigned to Force-rating thresholds.
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
        TraitorDiscovered,
        RescueAttempt,
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

        public bool IsForceSensitive { get; set; }
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
        public bool IsRetired { get; set; }
        public string MissionReturnParentInstanceID { get; set; }
        public string MissionReturnLocationInstanceID { get; set; }
        public OfficerVoiceSet VoiceSet { get; set; } = new OfficerVoiceSet();
        public OfficerImageSet ImageSet { get; set; } = new OfficerImageSet();

        public void ApplyImageSet()
        {
            DisplayImagePath = ImageSet.DisplayImagePath ?? DisplayImagePath;
            SmallDisplayImagePath = ImageSet.SmallDisplayImagePath ?? SmallDisplayImagePath;
            MessageImagePath = ImageSet.MessageImagePath ?? MessageImagePath;
            EncyclopediaImagePath = ImageSet.EncyclopediaImagePath ?? EncyclopediaImagePath;
        }

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
            int officerRating = rating switch
            {
                OfficerRating.Diplomacy => ApplyForceRatingBonus(baseRating),
                OfficerRating.Espionage => ApplyForceRatingBonus(baseRating),
                OfficerRating.Combat => Math.Max(
                    0,
                    ApplyForceRatingBonus(baseRating) - InjuryPoints
                ),
                _ => baseRating,
            };
            return officerRating;
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
        /// Adds the specified amount to the base research value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to update.</param>
        /// <param name="amount">The amount to add.</param>
        public void IncrementBaseRating(ResearchDiscipline discipline, int amount = 1)
        {
            IncrementBaseRating(GetRatingForResearchDiscipline(discipline), amount);
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
            return IsForceSensitive
                && !IsForceEligible
                && !IsCaptured
                && !IsKilled
                && !IsOnMission();
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

        /// <summary>
        /// Selects one available voice path for an officer event.
        /// </summary>
        /// <param name="voiceLineType">The officer event requesting a voice response.</param>
        /// <param name="provider">The random source used when multiple paths are available.</param>
        /// <returns>The selected path, or null when no matching voice exists.</returns>
        public string GetVoicePath(
            OfficerVoiceLineType voiceLineType,
            IRandomNumberProvider provider
        )
        {
            return SelectVoicePath(VoiceSet.GetPaths(voiceLineType), provider);
        }

        /// <summary>
        /// Reports whether an officer event has at least one available voice path.
        /// </summary>
        /// <param name="voiceLineType">The officer event to inspect.</param>
        /// <returns>True when a matching voice path exists.</returns>
        public bool HasVoicePath(OfficerVoiceLineType voiceLineType)
        {
            IReadOnlyList<string> paths = VoiceSet.GetPaths(voiceLineType);
            return paths?.Count > 0;
        }

        /// <summary>
        /// Selects one path from an available voice collection.
        /// </summary>
        /// <param name="paths">The available voice paths.</param>
        /// <param name="provider">The optional random source.</param>
        /// <returns>The selected path, or null when the collection is empty.</returns>
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
