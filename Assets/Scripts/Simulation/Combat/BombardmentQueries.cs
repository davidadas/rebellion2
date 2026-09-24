using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Answers bombardment eligibility, strength, and target questions without changing game state.
    /// </summary>
    public sealed class BombardmentQueries
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates bombardment queries for the current game.
        /// </summary>
        /// <param name="game">The game state supplying bombardment configuration.</param>
        public BombardmentQueries(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Determines whether the supplied fleets can bombard the target planet.
        /// </summary>
        /// <param name="fleets">Fleets attempting the bombardment.</param>
        /// <param name="targetPlanet">Planet being targeted.</param>
        /// <param name="type">Bombardment mode being requested.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        public bool CanExecute(
            IReadOnlyList<Fleet> fleets,
            Planet targetPlanet,
            BombardmentType type
        )
        {
            if (!CanBombard(fleets, targetPlanet))
                return false;

            return type == BombardmentType.DestroyPlanet
                ? HasPlanetDestroyingShip(fleets)
                : CalculateBombardmentStrength(fleets) > 0;
        }

        /// <summary>
        /// Determines whether fleets satisfy the shared bombardment restrictions.
        /// </summary>
        /// <param name="fleets">Fleets attempting the bombardment.</param>
        /// <param name="targetPlanet">Planet being targeted.</param>
        /// <returns>True when the fleets can perform an ordinary bombardment.</returns>
        private static bool CanBombard(IReadOnlyList<Fleet> fleets, Planet targetPlanet)
        {
            if (
                targetPlanet?.IsDestroyed != false
                || fleets?.Any() != true
                || fleets.Any(fleet => fleet == null)
            )
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && targetPlanet?.GetOwnerInstanceID() != ownerId
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.GetParent() == targetPlanet
                )
                && GetActiveCapitalShips(fleets).Any();
        }

        /// <summary>
        /// Calculates the total effective bombardment strength of the attacking fleets.
        /// </summary>
        /// <param name="fleets">Fleets contributing ships and starfighters.</param>
        /// <returns>The combined bombardment strength after condition and leadership adjustments.</returns>
        private int CalculateBombardmentStrength(IReadOnlyList<Fleet> fleets)
        {
            return GetBombardmentStrength(fleets, _game.Config.Combat.Bombardment);
        }

        /// <summary>
        /// Calculates the total effective bombardment strength of the attacking fleets.
        /// </summary>
        /// <param name="fleets">Fleets contributing ships and starfighters.</param>
        /// <param name="config">Bombardment configuration.</param>
        /// <returns>The combined bombardment strength after condition and leadership adjustments.</returns>
        public static int GetBombardmentStrength(
            IEnumerable<Fleet> fleets,
            GameConfig.BombardmentConfig config
        )
        {
            if (fleets == null || config == null)
                return 0;

            int total = 0;

            foreach (Fleet fleet in fleets.Where(fleet => fleet != null))
            {
                int fleetStrength = 0;

                foreach (
                    CapitalShip ship in fleet
                        .GetChildren<CapitalShip>()
                        .Where(IsActiveBombardmentUnit)
                )
                {
                    fleetStrength += ScaleByCondition(
                        ship.Bombardment,
                        ship.CurrentHullStrength,
                        ship.MaxHullStrength
                    );
                    fleetStrength += ship.GetChildren<Starfighter>()
                        .Where(IsActiveBombardmentUnit)
                        .Sum(fighter =>
                            ScaleByCondition(
                                fighter.Bombardment,
                                fighter.CurrentSquadronSize,
                                fighter.MaxSquadronSize
                            )
                        );
                }

                total += fleetStrength * GetBombardmentMultiplier(fleet, config);
            }

            return total;
        }

        /// <summary>Returns projected bombardment strength for a fleet.</summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="config">Bombardment configuration.</param>
        /// <returns>The projected bombardment strength.</returns>
        internal static int GetProjectedBombardmentStrength(
            Fleet fleet,
            GameConfig.BombardmentConfig config
        )
        {
            if (fleet == null || config == null)
                return 0;

            return fleet
                    .GetChildren<CapitalShip>()
                    .Where(IsCommittedBombardmentUnit)
                    .Sum(GetProjectedCapitalShipBombardmentStrength)
                * GetBombardmentMultiplier(fleet, config);
        }

        /// <summary>Returns projected bombardment strength for one capital ship.</summary>
        /// <param name="fleet">The fleet containing the ship.</param>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <param name="config">Bombardment configuration.</param>
        /// <returns>The projected bombardment strength.</returns>
        internal static int GetProjectedCapitalShipBombardmentStrength(
            Fleet fleet,
            CapitalShip capitalShip,
            GameConfig.BombardmentConfig config
        )
        {
            if (fleet == null || capitalShip == null || config == null)
                return 0;

            return GetProjectedCapitalShipBombardmentStrength(capitalShip)
                * GetBombardmentMultiplier(fleet, config);
        }

        /// <summary>
        /// Calculates the total protection supplied by active planetary shield facilities.
        /// </summary>
        /// <param name="planet">Planet whose shields are evaluated.</param>
        /// <returns>The combined active shield strength.</returns>
        public static int GetBombardmentShieldStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActiveBombardmentUnit(building) && building.IsPlanetaryShieldGenerator()
                )
                .Sum(building => building.ShieldStrength);
        }

        /// <summary>
        /// Converts planetary shield strength to the scale used by unit bombardment ratings.
        /// </summary>
        /// <param name="shieldStrength">Combined active planetary shield strength.</param>
        /// <param name="config">Bombardment configuration.</param>
        /// <returns>The number of bombardment points absorbed by the shields.</returns>
        public static int GetBombardmentShieldResistance(
            int shieldStrength,
            GameConfig.BombardmentConfig config
        )
        {
            if (shieldStrength <= 0 || config?.ShieldStrengthDivisor <= 0)
                return 0;

            return (int)Math.Ceiling((double)shieldStrength / config.ShieldStrengthDivisor);
        }

        /// <summary>
        /// Returns whether a planet has an active facility capable of resisting orbital attack.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when an active planetary defense facility remains.</returns>
        public static bool HasActiveDefenseFacilities(Planet planet)
        {
            return planet
                    ?.GetAllBuildings()
                    .Any(building =>
                        IsActiveBombardmentUnit(building) && IsBombardmentDefenseFacility(building)
                    ) == true;
        }

        /// <summary>
        /// Returns whether a planet has an active military target for orbital bombardment.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <param name="defenderInstanceId">Faction whose military targets are considered.</param>
        /// <returns>True when a defending regiment or defense facility remains.</returns>
        public static bool HasActiveMilitaryTargets(Planet planet, string defenderInstanceId)
        {
            if (planet == null || string.IsNullOrEmpty(defenderInstanceId))
                return false;

            return HasActiveDefenseFacilities(planet)
                || planet
                    .GetAllRegiments()
                    .Any(regiment =>
                        IsActiveBombardmentUnit(regiment)
                        && regiment.GetOwnerInstanceID() == defenderInstanceId
                    );
        }

        /// <summary>
        /// Returns bombardment multiplier.
        /// </summary>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="config">The applicable configuration.</param>
        /// <returns>The calculated value.</returns>
        private static int GetBombardmentMultiplier(
            Fleet fleet,
            GameConfig.BombardmentConfig config
        )
        {
            int leadership = GetBombardmentLeadership(
                fleet.GetOfficers(),
                OfficerRank.Admiral,
                fleet.GetOwnerInstanceID()
            );
            return leadership / config.AttackerLeadershipDivisor + 1;
        }

        /// <summary>
        /// Returns projected capital ship bombardment strength.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private static int GetProjectedCapitalShipBombardmentStrength(CapitalShip capitalShip)
        {
            bool useCurrentCondition =
                capitalShip.GetParent() != null
                && capitalShip.ManufacturingStatus == ManufacturingStatus.Complete;
            int capitalShipStrength = useCurrentCondition
                ? ScaleByCondition(
                    capitalShip.Bombardment,
                    capitalShip.CurrentHullStrength,
                    capitalShip.MaxHullStrength
                )
                : capitalShip.Bombardment;
            int starfighterStrength = capitalShip
                .GetChildren<Starfighter>()
                .Where(IsCommittedBombardmentUnit)
                .Sum(starfighter =>
                    useCurrentCondition
                    && starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                        ? ScaleByCondition(
                            starfighter.Bombardment,
                            starfighter.CurrentSquadronSize,
                            starfighter.MaxSquadronSize
                        )
                        : starfighter.Bombardment
                );

            return capitalShipStrength + starfighterStrength;
        }

        /// <summary>
        /// Determines whether the attacking fleets contain an active planet-destroying ship.
        /// </summary>
        /// <param name="fleets">Attacking fleets to inspect.</param>
        /// <returns>True when an active configured ship type is present.</returns>
        internal static bool HasPlanetDestroyingShip(IEnumerable<Fleet> fleets)
        {
            return GetActiveCapitalShips(fleets).Any(ship => ship.CanDestroyPlanets);
        }

        /// <summary>
        /// Returns active capital ships from the supplied fleets.
        /// </summary>
        /// <param name="fleets">Fleets to inspect.</param>
        /// <returns>The active capital ships.</returns>
        internal static List<CapitalShip> GetActiveCapitalShips(IEnumerable<Fleet> fleets)
        {
            return fleets
                .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                .Where(IsActiveBombardmentUnit)
                .ToList();
        }

        /// <summary>
        /// Returns the leadership rating of the first eligible bombardment commander.
        /// </summary>
        /// <param name="officers">Officers to search.</param>
        /// <param name="rank">Required command rank.</param>
        /// <param name="ownerId">Required faction instance ID.</param>
        /// <returns>The commander's leadership rating, or zero when none is eligible.</returns>
        internal static int GetBombardmentLeadership(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerId
        )
        {
            Officer commander = officers.FirstOrDefault(officer =>
                officer.CurrentRank == rank
                && officer.GetOwnerInstanceID() == ownerId
                && !officer.IsKilled
            );
            return commander?.GetEffectiveRating(SkillRating.Leadership) ?? 0;
        }

        /// <summary>
        /// Scales a unit value by its current condition.
        /// </summary>
        /// <param name="value">Undamaged value.</param>
        /// <param name="current">Current condition.</param>
        /// <param name="maximum">Maximum condition.</param>
        /// <returns>The condition-adjusted value.</returns>
        internal static int ScaleByCondition(int value, int current, int maximum)
        {
            if (maximum <= 0)
                return value;

            int damage = maximum - Math.Min(maximum, Math.Max(0, current));
            return value - value * damage / maximum;
        }

        /// <summary>
        /// Determines whether a manufacturable unit is complete and stationary.
        /// </summary>
        /// <param name="unit">Unit to inspect.</param>
        /// <returns>True when the unit can participate in bombardment.</returns>
        internal static bool IsActiveBombardmentUnit(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        /// <summary>
        /// Returns whether committed bombardment unit.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool IsCommittedBombardmentUnit(IManufacturable unit)
        {
            return unit?.ManufacturingStatus
                is ManufacturingStatus.Complete
                    or ManufacturingStatus.Building;
        }

        /// <summary>
        /// Determines whether a capital ship can participate in bombardment.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship is active and has remaining hull strength.</returns>
        internal static bool IsActiveBombardmentUnit(CapitalShip ship)
        {
            return IsActiveBombardmentUnit((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a starfighter group can contribute to bombardment.
        /// </summary>
        /// <param name="fighter">Starfighter group to inspect.</param>
        /// <returns>True when the group is active and has remaining fighters.</returns>
        internal static bool IsActiveBombardmentUnit(Starfighter fighter)
        {
            return IsActiveBombardmentUnit((IManufacturable)fighter)
                && fighter.CurrentSquadronSize > 0;
        }

        /// <summary>
        /// Determines whether a building belongs to a bombardment defense target lane.
        /// </summary>
        /// <param name="building">Building to inspect.</param>
        /// <returns>True when the building is a planetary defense facility.</returns>
        internal static bool IsBombardmentDefenseFacility(Building building)
        {
            return building.IsDefenseFacility();
        }
    }
}
