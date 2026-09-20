using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Represents a scene node that exposes officer-style ratings to mission systems.
    /// </summary>
    public interface IMissionParticipant : ISceneNode, IMovable
    {
        public string MissionReturnParentInstanceID { get; set; }
        public string MissionReturnLocationInstanceID { get; set; }

        // Mission ratings.
        public Dictionary<SkillRating, int> Ratings { get; set; }
        public bool CanImproveMissionRating { get; }

        /// <summary>
        /// Returns the stored rating value before temporary modifiers are applied.
        /// </summary>
        /// <param name="rating">The rating to read.</param>
        /// <returns>The stored rating value.</returns>
        public int GetBaseRating(SkillRating rating);

        /// <summary>
        /// Returns the rating value after applicable runtime modifiers.
        /// </summary>
        /// <param name="rating">The rating to read.</param>
        /// <returns>The effective rating value.</returns>
        public int GetEffectiveRating(SkillRating rating);

        /// <summary>
        /// Assigns a stored rating value, overwriting any prior value.
        /// </summary>
        /// <param name="rating">The rating to assign.</param>
        /// <param name="value">The new value.</param>
        /// <returns>The stored value.</returns>
        public int SetBaseRating(SkillRating rating, int value);

        /// <summary>
        /// Returns whether this participant is qualified to perform the given mission type.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID to check eligibility for.</param>
        /// <returns>True if this participant can perform the mission type.</returns>
        public bool CanPerformMission(string missionTypeId);

        /// <summary>
        /// Returns whether this participant is currently assigned to a mission.
        /// </summary>
        /// <returns>True if currently assigned to a mission.</returns>
        public bool IsOnMission();
    }
}
