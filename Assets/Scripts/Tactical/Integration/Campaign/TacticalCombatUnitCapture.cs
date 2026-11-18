using System;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;

namespace Rebellion.Tactical.Integration.Campaign
{
    /// <summary>
    /// Captures campaign participants at the encounter boundary without passing their graph into battle.
    /// </summary>
    public static class TacticalCombatUnitCapture
    {
        /// <summary>
        /// Captures the capital ship's installed hardware, payload, readiness, and planetary protection.
        /// </summary>
        /// <param name="ship">The campaign participant.</param>
        /// <returns>The detached starting values for a new tactical participant.</returns>
        public static TacticalCombatUnitSetup Capture(CapitalShip ship)
        {
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));
            int[,] strengths = new int[4, 3];
            int[] sourceArcs = { 0, 3, 1, 2 };
            PrimaryWeaponType[] sourceWeapons =
            {
                PrimaryWeaponType.LaserCannon,
                PrimaryWeaponType.IonCannon,
                PrimaryWeaponType.Turbolaser,
            };
            float[] ranges = new float[3];
            for (int weapon = 0; weapon < 3; weapon++)
            {
                int[] values = ship.PrimaryWeapons[sourceWeapons[weapon]];
                ranges[weapon] = values[CapitalShip.PrimaryWeaponRangeIndex];
                for (int arc = 0; arc < 4; arc++)
                    strengths[arc, weapon] = values[sourceArcs[arc]];
            }
            bool protectedByFacility = false;
            if (ship.CanDestroyPlanets)
            {
                Planet planet = ship.GetParentOfType<Planet>();
                if (planet != null)
                    foreach (Building facility in planet.GetChildren<Building>())
                        protectedByFacility |=
                            facility.ManufacturingStatus == ManufacturingStatus.Complete
                            && facility.Movement == null
                            && facility.ProtectedUnitTypeIDs?.Contains(ship.TypeID) == true;
            }
            return new TacticalCombatUnitSetup
            {
                InstanceID = ship.InstanceID,
                TypeID = ship.TypeID,
                DisplayName = ship.GetDisplayName(),
                IsFighter = false,
                IsBattleStation = ship.CanDestroyPlanets,
                IsUnderConstruction =
                    ship.CanDestroyPlanets
                    && ship.ManufacturingStatus != ManufacturingStatus.Complete,
                HasPlanetaryProtection = protectedByFacility,
                HasPrimaryHyperdrive = ship.Hyperdrive != 0,
                PayloadCount =
                    ship.GetChildren<Officer>().Count
                    + ship.GetChildren<Regiment>().Count
                    + ship.GetChildren<SpecialForces>().Count,
                MaximumHull = ship.MaxHullStrength,
                InitialHull = ship.CurrentHullStrength,
                MaximumShields = ship.MaxShieldStrength,
                WeaponRecharge = ship.WeaponRecharge,
                ShieldRecharge = ship.ShieldRechargeRate,
                SublightSpeed = ship.SublightSpeed,
                Maneuverability = ship.Maneuverability,
                TractorBeamPower = ship.TractorBeamPower,
                DamageControl = ship.DamageControl,
                SystemDamage = ship.SystemDamage?.CreateCopy(),
                WeaponStrengths = strengths,
                WeaponRanges = ranges,
                TractorRange = ship.TractorBeamnRange,
                HasGravityWell = ship.HasGravityWell,
                FighterCapacity =
                    ship.StarfighterCapacity
                    - ship.GetChildren<Starfighter>(includeDisabled: true).Count
                    + ship.GetChildren<Starfighter>().Count(SpaceCombatQueries.IsActiveStarfighter),
            };
        }

        /// <summary>
        /// Captures squadron hardware, entry losses, and its initial carrier identity.
        /// </summary>
        /// <param name="fighter">The campaign squadron.</param>
        /// <returns>The detached starting values for a new tactical participant.</returns>
        public static TacticalCombatUnitSetup Capture(Starfighter fighter)
        {
            if (fighter == null)
                throw new ArgumentNullException(nameof(fighter));
            int[,] strengths = new int[4, 3];
            strengths[0, 0] = fighter.LaserCannon * fighter.MaxSquadronSize;
            strengths[0, 1] = fighter.IonCannon * fighter.MaxSquadronSize;
            return new TacticalCombatUnitSetup
            {
                InstanceID = fighter.InstanceID,
                TypeID = fighter.TypeID,
                DisplayName = fighter.GetDisplayName(),
                IsFighter = true,
                HasPrimaryHyperdrive = fighter.Hyperdrive != 0,
                MaximumHull = fighter.MaxSquadronSize * 8,
                InitialHull = fighter.CurrentSquadronSize * 8,
                InitialSquadronSize = fighter.CurrentSquadronSize,
                InitialCarrierID = (fighter.GetParent() as CapitalShip)?.InstanceID,
                MaximumShields = fighter.ShieldStrength * fighter.MaxSquadronSize,
                SublightSpeed = fighter.SublightSpeed,
                Maneuverability = fighter.Agility,
                WeaponStrengths = strengths,
                WeaponRanges = new float[] { fighter.LaserRange, fighter.IonRange, 0 },
                TorpedoStrength = fighter.Torpedoes * fighter.MaxSquadronSize,
                TorpedoRange = fighter.TorpedoRange,
            };
        }
    }
}
