using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
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
    /// Force rank display labels derived from force_rank thresholds.
    /// Matches original REBEXE.EXE get_character_str_force_ranking.
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
        // Research Info
        public int ShipResearch { get; set; }
        public int TroopResearch { get; set; }
        public int FacilityResearch { get; set; }

        // Officer Info
        public bool IsMain { get; set; }
        public bool IsRecruitable { get; set; }
        public bool IsCaptured { get; set; }
        public string CaptorInstanceID { get; set; }
        public bool IsKilled { get; set; }
        public bool CanBetray { get; set; }
        public bool IsTraitor { get; set; }
        public int Loyalty { get; set; }
        public int HyperdriveModifier { get; set; } // Han Solo speed bonus (subtracts from transit time)

        // Jedi / Force Info (data record fields, not persisted in saves)
        [PersistableIgnore]
        public int JediProbability { get; set; }

        [PersistableIgnore]
        public int JediLevel { get; set; }

        [PersistableIgnore]
        public int JediLevelVariance { get; set; }

        [PersistableIgnore]
        public bool IsJediTeacher { get; set; }

        /// <summary>
        /// Template flag: true for characters who start the game as known Jedi
        /// (Luke, Vader, Emperor). Maps to original template jedi=1 field.
        /// These characters get ForceValue and IsForceEligible at game start.
        /// </summary>
        [PersistableIgnore]
        public bool IsKnownJedi { get; set; }

        /// <summary>
        /// True if Force potential was activated at game start (passed JediProbability roll).
        /// Maps to original jedi flag (bit 0x20). Both known and potential Jedi have this set.
        /// </summary>
        public bool IsJedi { get; set; }

        /// <summary>
        /// True if this character's Force ability has been discovered/activated.
        /// Maps to original force-eligible flag (bit 0x10). Known Jedi have this at start.
        /// Potential Jedi gain this when discovered by a known Jedi during a mission.
        /// Gates ForceValue initialization and mission force growth.
        /// </summary>
        public bool IsForceEligible { get; set; }

        /// <summary>
        /// Base force power. Grows by +1 per successful mission (GENERAL_PARAM_3081).
        /// Only initialized when IsForceEligible is true.
        /// Known Jedi: set at game start from JediLevel + roll(JediLevelVariance).
        /// Potential Jedi: set to 0 until discovered, then initialized from template.
        /// </summary>
        public int ForceValue { get; set; }

        /// <summary>
        /// Bonus from Jedi training missions (teacher/student catch-up mechanic).
        /// </summary>
        public int ForceTrainingAdjustment { get; set; }

        /// <summary>
        /// Derived force rank = ForceValue + ForceTrainingAdjustment.
        /// Determines tier label and all threshold checks.
        /// </summary>
        [PersistableIgnore]
        public int ForceRank => ForceValue + ForceTrainingAdjustment;

        /// <summary>
        /// True if this Jedi is actively scanning for hidden force users nearby.
        /// Set deterministically when ForceRank >= DiscoveringForceUserThreshold (80)
        /// and the character is force-eligible, not captured, and not on a mission.
        /// </summary>
        public bool IsDiscoveringForceUser { get; set; }


        // Rank Info
        public OfficerRank[] AllowedRanks { get; set; }
        public OfficerRank CurrentRank { get; set; }

        // Owner Info
        public string InitialParentInstanceID { get; set; }

        // Variance Info
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

        // Movement Info
        public MovementState Movement { get; set; }

        // Mission Skill Related
        public Dictionary<MissionParticipantSkill, int> Skills { get; set; } =
            new Dictionary<MissionParticipantSkill, int>
            {
                { MissionParticipantSkill.Diplomacy, 0 },
                { MissionParticipantSkill.Espionage, 0 },
                { MissionParticipantSkill.Combat, 0 },
                { MissionParticipantSkill.Leadership, 0 },
            };
        public bool CanImproveMissionSkill => true;

        public void SetMissionSkillValue(MissionParticipantSkill skill, int value) =>
            Skills[skill] = value;

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public Officer() { }

        /// <summary>
        /// Returns the officer's current value for the specified mission skill.
        /// </summary>
        /// <param name="skill"></param>
        /// <returns>The skill value.</returns>
        public int GetSkillValue(MissionParticipantSkill skill)
        {
            return Skills[skill];
        }

        /// <summary>
        /// Sets the officer's value for the specified mission skill.
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="value"></param>
        /// <returns>The new skill value.</returns>
        public int SetSkillValue(MissionParticipantSkill skill, int value)
        {
            return Skills[skill] = value;
        }

        /// <summary>
        /// Returns the research skill value for the given manufacturing type.
        /// </summary>
        public int GetResearchSkill(ManufacturingType type)
        {
            return type switch
            {
                ManufacturingType.Ship => ShipResearch,
                ManufacturingType.Building => FacilityResearch,
                ManufacturingType.Troop => TroopResearch,
                _ => 0,
            };
        }

        /// <summary>
        /// Increments the research skill for the given manufacturing type by the specified amount.
        /// </summary>
        public void IncrementResearchSkill(ManufacturingType type, int amount = 1)
        {
            switch (type)
            {
                case ManufacturingType.Ship:
                    ShipResearch += amount;
                    break;
                case ManufacturingType.Building:
                    FacilityResearch += amount;
                    break;
                case ManufacturingType.Troop:
                    TroopResearch += amount;
                    break;
            }
        }

        /// <summary>
        /// Returns the display label for this officer's current force rank.
        /// Thresholds match original REBEXE.EXE: 0-9=None, 10-19=Novice,
        /// 20-79=Trainee, 80-99=ForceStudent, 100-119=ForceKnight, 120+=ForceMaster.
        /// </summary>
        public ForceRankLabel GetForceRankLabel()
        {
            int rank = ForceRank;
            if (rank >= 120)
                return ForceRankLabel.ForceMaster;
            if (rank >= 100)
                return ForceRankLabel.ForceKnight;
            if (rank >= 80)
                return ForceRankLabel.ForceStudent;
            if (rank >= 20)
                return ForceRankLabel.Trainee;
            if (rank >= 10)
                return ForceRankLabel.Novice;
            return ForceRankLabel.None;
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
