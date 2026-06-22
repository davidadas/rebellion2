using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Represents a special forces unit that can be used in missions.
    /// </summary>
    public class SpecialForces : LeafNode, IMissionParticipant, IManufacturable, IMovable
    {
        // Construction Info.
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Manufacturing Info.
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info.
        public MovementState Movement { get; set; }

        // Mission Qualification.
        public List<MissionType> AllowedMissionTypes { get; set; } = new List<MissionType>();

        // Mission rating info.
        [PersistableMember(Name = "Skills")]
        public Dictionary<OfficerRating, int> Ratings { get; set; } =
            new Dictionary<OfficerRating, int>
            {
                { OfficerRating.Diplomacy, 0 },
                { OfficerRating.Espionage, 0 },
                { OfficerRating.Combat, 0 },
                { OfficerRating.Leadership, 0 },
            };
        public bool CanImproveMissionRating => false;

        /// <summary>
        /// Returns whether this unit can perform a mission type.
        /// </summary>
        /// <param name="missionType">The mission type to inspect.</param>
        /// <returns>True if this unit is allowed to perform the mission type.</returns>
        public bool CanPerformMission(MissionType missionType) =>
            AllowedMissionTypes.Contains(missionType);

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SpecialForces() { }

        /// <summary>
        /// Returns the manufacturing type for this unit.
        /// </summary>
        /// <returns>The manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Troop;
        }

        /// <summary>
        /// Returns this unit's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to read.</param>
        /// <returns>The stored rating value.</returns>
        public int GetBaseRating(OfficerRating rating)
        {
            return Ratings.TryGetValue(rating, out int value) ? value : 0;
        }

        /// <summary>
        /// Returns this unit's current value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to read.</param>
        /// <returns>The effective rating value.</returns>
        public int GetEffectiveRating(OfficerRating rating)
        {
            return GetBaseRating(rating);
        }

        /// <summary>
        /// Sets this unit's stored value for the specified rating.
        /// </summary>
        /// <param name="rating">The rating to update.</param>
        /// <param name="value">The new rating value.</param>
        /// <returns>The stored rating value.</returns>
        public int SetBaseRating(OfficerRating rating, int value)
        {
            Ratings[rating] = value;
            return value;
        }

        /// <summary>
        /// Returns whether the special forces unit can be ordered to move.
        /// </summary>
        /// <returns>True if the unit is not in transit and not on a mission; otherwise, false.</returns>
        public bool IsMovable()
        {
            return Movement == null && !IsOnMission();
        }

        /// <summary>
        /// Returns whether the special forces unit is currently assigned to a mission.
        /// </summary>
        /// <returns>True if the unit's parent is a <see cref="Mission"/>; otherwise, false.</returns>
        public bool IsOnMission()
        {
            return GetParent() is Mission;
        }
    }
}
