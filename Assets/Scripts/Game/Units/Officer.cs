using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.SceneGraph;
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

    /// <summary>
    /// Represents an officer that can be used in missions.
    /// </summary>
    public class Officer : LeafNode, IMissionParticipant, IMovable
    {
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

        // Mission Skill Info.
        public Dictionary<MissionParticipantSkill, int> Skills { get; set; } =
            new Dictionary<MissionParticipantSkill, int>
            {
                { MissionParticipantSkill.Diplomacy, 0 },
                { MissionParticipantSkill.Espionage, 0 },
                { MissionParticipantSkill.Combat, 0 },
                { MissionParticipantSkill.Leadership, 0 },
            };
        public bool CanImproveMissionSkill => true;

        /// <summary>
        /// Returns whether this officer can perform a mission type.
        /// </summary>
        /// <param name="missionType">The mission type to inspect.</param>
        /// <returns>True for every mission type.</returns>
        public bool CanPerformMission(MissionType missionType) => true;

        /// <summary>
        /// Sets this officer's value for a mission skill.
        /// </summary>
        /// <param name="skill">The mission skill to update.</param>
        /// <param name="value">The new skill value.</param>
        public void SetMissionSkillValue(MissionParticipantSkill skill, int value) =>
            Skills[skill] = value;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Officer() { }

        /// <summary>
        /// Returns the officer's current value for the specified mission skill.
        /// </summary>
        /// <param name="skill">The mission skill to query.</param>
        /// <returns>The skill value.</returns>
        public int GetSkillValue(MissionParticipantSkill skill)
        {
            return Skills[skill];
        }

        /// <summary>
        /// Sets the officer's value for the specified mission skill.
        /// </summary>
        /// <param name="skill">The mission skill to update.</param>
        /// <param name="value">The new skill value.</param>
        /// <returns>The new skill value.</returns>
        public int SetSkillValue(MissionParticipantSkill skill, int value)
        {
            return Skills[skill] = value;
        }

        /// <summary>
        /// Returns the research skill value for the given discipline.
        /// </summary>
        /// <param name="discipline">The research discipline to query.</param>
        /// <returns>The officer's research skill value for that discipline.</returns>
        public int GetResearchSkill(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => ShipResearch,
                ResearchDiscipline.FacilityDesign => FacilityResearch,
                ResearchDiscipline.TroopTraining => TroopResearch,
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the research skill value for the given manufacturing type.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type to query.</param>
        /// <returns>The officer's research skill value for that manufacturing type.</returns>
        public int GetResearchSkill(ManufacturingType manufacturingType)
        {
            return GetResearchSkill(manufacturingType.ToResearchDiscipline());
        }

        /// <summary>
        /// Increments the research skill for the given discipline by the specified amount.
        /// </summary>
        /// <param name="discipline">The research discipline whose skill to increment.</param>
        /// <param name="amount">Amount to add to the skill.</param>
        public void IncrementResearchSkill(ResearchDiscipline discipline, int amount = 1)
        {
            switch (discipline)
            {
                case ResearchDiscipline.ShipDesign:
                    ShipResearch += amount;
                    break;
                case ResearchDiscipline.FacilityDesign:
                    FacilityResearch += amount;
                    break;
                case ResearchDiscipline.TroopTraining:
                    TroopResearch += amount;
                    break;
            }
        }

        /// <summary>
        /// Increments the research skill for the given manufacturing type by the specified amount.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type whose skill to increment.</param>
        /// <param name="amount">Amount to add to the skill.</param>
        public void IncrementResearchSkill(ManufacturingType manufacturingType, int amount = 1)
        {
            IncrementResearchSkill(manufacturingType.ToResearchDiscipline(), amount);
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
        /// Returns the officer's combat skill reduced by current injury, floored at zero.
        /// </summary>
        /// <returns>The effective combat value after injury penalty.</returns>
        public int GetEffectiveCombat()
        {
            return Math.Max(0, Skills[MissionParticipantSkill.Combat] - InjuryPoints);
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
    }
}
