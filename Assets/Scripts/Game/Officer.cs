using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;
using Rebellion.Util.Extensions;

namespace Rebellion.Game
{
    /// <summary>
    ///
    /// </summary>
    public enum OfficerRank
    {
        None,
        Commander,
        General,
        Admiral,
    }

    /// <summary>
    /// Force tier progression: None → Aware → Training → Experienced.
    /// </summary>
    public enum ForceTier
    {
        None = 0,
        Aware = 1,
        Training = 2,
        Experienced = 3,
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
        public bool IsForceSensitive { get; set; }
        public int Loyalty { get; set; }
        public int HyperdriveModifier { get; set; } // Han Solo speed bonus (subtracts from transit time)

        // Jedi Info
        [PersistableIgnore]
        public int JediProbability { get; set; }

        public int JediLevel { get; set; }
        public int JediLevelVariance { get; set; }

        /// <summary>
        /// Current Force tier. Set at game start based on JediLevel initial value.
        /// Advances as ForceExperience crosses thresholds (50 XP → Training, 150 XP → Experienced).
        /// </summary>
        public ForceTier ForceTier { get; set; } = ForceTier.None;

        /// <summary>
        /// Accumulated Force experience points. Starts from JediLevel at game initialization.
        /// XP accumulation mechanism currently unimplemented (reserved for future mission system).
        /// </summary>
        public int ForceExperience { get; set; } = 0;

        /// <summary>
        /// True if the opposing faction has detected this officer's Force ability.
        /// Detection checks run every DetectionCheckInterval ticks with probability based on ForceTier.
        /// </summary>
        public bool IsDiscoveredJedi { get; set; } = false;

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
        ///
        /// </summary>
        /// <param name="skill"></param>
        /// <returns></returns>
        public int GetSkillValue(MissionParticipantSkill skill)
        {
            return Skills[skill];
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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
        ///
        /// </summary>
        /// <returns></returns>
        public bool IsOnMission()
        {
            return GetParent() is Mission;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool IsMovable()
        {
            if (GetParent() is Mission mission)
                return mission.IsComplete() && Movement == null;
            return Movement == null;
        }
    }
}
