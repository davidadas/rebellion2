using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Classifies and scores targets considered by AI sabotage planning.
    /// </summary>
    internal sealed class AISabotageTargetPolicy
    {
        // Strategic Context.
        private readonly AIAssessment _assessment;
        private readonly GameConfig.AIMissionPlanningConfig _config;

        /// <summary>
        /// Creates a sabotage policy backed by the current strategic assessment.
        /// </summary>
        /// <param name="assessment">The current strategic assessment.</param>
        /// <param name="config">The sabotage planning configuration.</param>
        internal AISabotageTargetPolicy(
            AIAssessment assessment,
            GameConfig.AIMissionPlanningConfig config
        )
        {
            _assessment = assessment;
            _config = config;
        }

        /// <summary>
        /// Calculates the configured scoring bonus for a sabotage target.
        /// </summary>
        /// <param name="planet">The target planet.</param>
        /// <param name="target">The target unit or facility.</param>
        /// <returns>The target's scoring bonus.</returns>
        internal int GetPriorityBonus(Planet planet, IManufacturable target)
        {
            if (_config == null || planet == null || target == null)
                return 0;

            bool isAttackTarget = _assessment.IsAttackPreparationTarget(planet);
            if (target is Building building)
            {
                int buildingPriorityBonus = _config.SabotageInfrastructureBonus;
                if (IsPlanetaryDefenseBuilding(building))
                    buildingPriorityBonus += _config.SabotageDefenseBonus;

                if (IsShieldGenerator(building))
                    buildingPriorityBonus += _config.SabotageShieldBonus;

                if (isAttackTarget && IsPlanetaryDefenseBuilding(building))
                {
                    buildingPriorityBonus +=
                        _config.SabotageAttackTargetBonus + _config.SabotageAttackDefenseBonus;
                }

                return buildingPriorityBonus;
            }

            int priorityBonus = target switch
            {
                Regiment when IsGarrisonedAtPlanet(planet, target) =>
                    _config.SabotageGarrisonRegimentBonus
                        + (
                            HasOppositionSupportMajority(planet)
                                ? _config.SabotageFavoredSupportRegimentBonus
                                : 0
                        ),
                Starfighter when IsGarrisonedAtPlanet(planet, target) =>
                    _config.SabotageGarrisonStarfighterBonus,
                _ => _config.SabotageOtherUnitBonus,
            };
            return isAttackTarget
                ? priorityBonus + _config.SabotageAttackTargetBonus
                : priorityBonus;
        }

        /// <summary>
        /// Returns the fixed military priority of a sabotage target.
        /// </summary>
        /// <param name="target">The target unit or facility.</param>
        /// <returns>A larger value for targets that should be destroyed first.</returns>
        internal static int GetPriority(IManufacturable target)
        {
            if (target is Building building)
            {
                if (IsShieldGenerator(building))
                    return 4;

                return IsPlanetaryDefenseBuilding(building) ? 3 : 0;
            }

            return target switch
            {
                Regiment => 2,
                Starfighter => 1,
                _ => 0,
            };
        }

        /// <summary>
        /// Returns whether the AI faction has more support than the planet's owner.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when opposition support exceeds owner support.</returns>
        private bool HasOppositionSupportMajority(Planet planet)
        {
            string ownerInstanceId = planet?.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerInstanceId)
                && _assessment.GetFactionPopularSupport(planet)
                    > planet.GetPopularSupport(ownerInstanceId);
        }

        /// <summary>
        /// Returns whether a target is directly stationed on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="target">The target unit.</param>
        /// <returns>True when the target is a direct child of the planet.</returns>
        private static bool IsGarrisonedAtPlanet(Planet planet, IManufacturable target)
        {
            return target.GetParent() is Planet parent && parent.InstanceID == planet.InstanceID;
        }

        /// <summary>
        /// Returns whether a building contributes to planetary defense.
        /// </summary>
        /// <param name="building">The building to inspect.</param>
        /// <returns>True for shield and weapon facilities.</returns>
        private static bool IsPlanetaryDefenseBuilding(Building building)
        {
            return building?.GetBuildingType() is BuildingType.Defense or BuildingType.Weapon;
        }

        /// <summary>
        /// Returns whether a building generates a planetary shield.
        /// </summary>
        /// <param name="building">The building to inspect.</param>
        /// <returns>True for ordinary and Death Star shield generators.</returns>
        private static bool IsShieldGenerator(Building building)
        {
            return building?.DefenseFacilityClass
                is DefenseFacilityClass.Shield
                    or DefenseFacilityClass.DeathStarShield;
        }
    }
}
