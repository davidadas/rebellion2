using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Places entering combatants and partitions their formations in battle registration order.
    /// </summary>
    internal static class TacticalCombatDeployment
    {
        /// <summary>
        /// Captures a capital ship's installed-weapon class and supported movement ordering at battle entry.
        /// </summary>
        internal readonly struct CapitalShipRank
        {
            public CombatUnit Unit { get; }
            public int Category { get; }
            public float Score { get; }

            /// <summary>
            /// Captures the immutable deployment assessment used by initial placement and automatic regrouping.
            /// </summary>
            /// <param name="unit">The capital ship being deployed.</param>
            public CapitalShipRank(CombatUnit unit)
            {
                Unit = unit;
                float movement = TacticalCombatMotion.GetSpeed(unit);
                bool armed = false;
                bool laser = false;
                float maximum = 0;
                for (int arc = 0; arc < 4; arc++)
                for (int profile = 0; profile < 3; profile++)
                {
                    TacticalWeaponType weapon = profile switch
                    {
                        0 => TacticalWeaponType.IonCannon,
                        1 => TacticalWeaponType.LaserCannon,
                        _ => TacticalWeaponType.Turbolaser,
                    };
                    if (unit.GetWeaponStrength((TacticalWeaponArc)arc, weapon) == 0)
                        continue;
                    armed = true;
                    laser |= weapon == TacticalWeaponType.LaserCannon;
                    float range = unit.GetWeaponRange((TacticalWeaponArc)arc, weapon);
                    if (maximum < range)
                        maximum = range;
                }
                Category =
                    laser ? 1
                    : armed ? 0
                    : 2;
                Score = -(float)((double)movement - (double)maximum * -10000f);
            }

            /// <summary>
            /// Orders captured deployment scores and resolves equal scores by unsigned battle registration.
            /// </summary>
            /// <param name="first">The first capital deployment.</param>
            /// <param name="second">The second capital deployment.</param>
            /// <returns>The deployment ordering.</returns>
            public static int Compare(CapitalShipRank first, CapitalShipRank second)
            {
                if (first.Score < second.Score)
                    return -1;
                if (first.Score > second.Score)
                    return 1;
                return ((uint)first.Unit.Index).CompareTo((uint)second.Unit.Index);
            }
        }

        /// <summary>
        /// Creates a detached participant and applies the standard deployment rules.
        /// </summary>
        /// <param name="source">The campaign ship or squadron.</param>
        /// <param name="side">The owning side in this encounter.</param>
        /// <param name="index">The registration order.</param>
        /// <returns>The initialized battle participant.</returns>
        internal static CombatUnit CreateUnit(BaseSceneNode source, CombatSide side, int index)
        {
            CombatUnit unit = CombatUnit.Create(source);
            InitializeUnit(unit, null, side, index);
            return unit;
        }

        /// <summary>
        /// Initializes the detached participant with its tactical registration and ship equipment.
        /// </summary>
        /// <param name="unit">The battle participant being evaluated or updated.</param>
        /// <param name="definition">The content entry supplying equipment defaults.</param>
        /// <param name="side">The side identified by the encounter.</param>
        /// <param name="index">The tactical registration index.</param>
        internal static void InitializeUnit(
            CombatUnit unit,
            TacticalCombatUnitDefinition definition,
            CombatSide side,
            int index
        )
        {
            BaseSceneNode battleUnit = unit.Unit;
            CapitalShip ship = battleUnit as CapitalShip;
            Starfighter fighter = battleUnit as Starfighter;
            if (ship != null)
            {
                definition?.MigrateCapitalShip(ship);
                ship.InstallLegacyEquipment();
            }
            else
                fighter.MigrateBackupHyperdrive(definition?.BackupHyperdrive ?? 0);

            bool isFighter = fighter != null;
            bool isBattleStation = ship?.CanDestroyPlanets == true;
            bool isUnderConstruction =
                isBattleStation && ship.ManufacturingStatus != ManufacturingStatus.Complete;
            float maximumHull = isFighter ? fighter.MaxSquadronSize * 8 : ship.MaxHullStrength;
            float initialHull = isFighter
                ? fighter.CurrentSquadronSize * 8
                : ship.CurrentHullStrength;
            float maximumShields = isFighter
                ? fighter.ShieldStrength * fighter.MaxSquadronSize
                : ship.MaxShieldStrength;
            float weaponRecharge = isFighter ? 0.25f : (float)((double)ship.WeaponRecharge * 3.75f);
            float shieldRecharge = isFighter ? 1 : ship.ShieldRechargeRate;
            float movementBudget = isFighter
                ? (float)((double)fighter.SublightSpeed * 0.4f)
                : (float)((double)ship.SublightSpeed * 0.4f * 0.6f);
            float maneuverability = isFighter ? fighter.Agility : ship.Maneuverability;
            float tractorBeamPower = isFighter ? 0 : ship.TractorBeamPower;
            float damageControl = isFighter ? 0 : ship.DamageControl;

            unit.Side = side;
            unit.Index = index;
            unit.CarrierID = unit.CarrierSourceUnitInstanceID;
            unit.InitializeCombatPools(
                maximumHull,
                initialHull,
                maximumShields,
                weaponRecharge,
                shieldRecharge,
                movementBudget,
                maneuverability,
                tractorBeamPower,
                unit.CountInstalledHyperdrives(),
                damageControl
            );
            if (unit.HasComponentDamage)
            {
                // Existing weapon damage does not seed repair work; later weapon hits add events.
                unit.DamageEventCount =
                    unit.GetDamage(TacticalShipSystem.Engines)
                    + unit.GetDamage(TacticalShipSystem.Shields)
                    + unit.GetDamage(TacticalShipSystem.Hyperdrive)
                    + unit.GetDamage(TacticalShipSystem.TractorBeam);
            }
            TacticalCombatFireControl.InitializeBatteries(unit);
            CapitalShipGeometry geometry = ship?.PhysicalGeometry;
            unit.MaximumTurnRate = geometry?.MaximumTurnRate ?? 0;
            unit.HalfHeight = isFighter
                ? 0.3f
                : geometry?.HalfHeight ?? CapitalShipGeometry.DefaultHalfHeight;
            unit.CollisionRadius = isFighter
                ? 1.2f
                : geometry?.CollisionRadius ?? CapitalShipGeometry.DefaultCollisionRadius;
            unit.Heading = Vector3.UnitZ;
            unit.CommandBudget = 1;
            if (geometry is { HullWidth: > 0, HullDepth: > 0 })
                unit.HullDimensions = new Vector3(
                    geometry.HullWidth,
                    geometry.HullHeight,
                    geometry.HullDepth
                );
            unit.DesiredHeading = unit.Heading;
            unit.Status =
                isFighter && unit.CarrierSourceUnitInstanceID != null && !unit.HasHyperdrive
                    ? CombatUnitStatus.Docked
                : isUnderConstruction ? CombatUnitStatus.UnderConstruction
                : CombatUnitStatus.Active;
        }

        /// <summary>
        /// Assigns alternating entry positions to capital ships and active fighter squadrons.
        /// </summary>
        /// <param name="units">The side's units in registration order.</param>
        /// <param name="forward">The authored initial heading.</param>
        /// <param name="depth">The battle's deployment span.</param>
        internal static void PlaceUnits(IEnumerable<CombatUnit> units, Vector3 forward, float depth)
        {
            int capitalIndex = 0;
            int fighterIndex = 0;
            foreach (CombatUnit unit in units)
            {
                if (unit.IsBattleStation)
                    continue;
                unit.Heading = forward;
                unit.DesiredHeading = forward;
                if (unit.Status == CombatUnitStatus.Docked)
                    continue;
                int index = unit.IsFighter ? fighterIndex++ : capitalIndex++;
                float x = index == 0 ? 0 : ((index + 1) / 2) * 5 * (index % 2 == 1 ? 1 : -1);
                unit.Position = new Vector3(
                    x,
                    0,
                    forward.Z * (-depth * 0.5f + (unit.IsFighter ? 20 : 0))
                );
            }
        }

        /// <summary>
        /// Creates deployed fighter categories before partitioning the side's command groups.
        /// </summary>
        /// <param name="units">The side's units in registration order.</param>
        /// <param name="groups">The groups receiving the new deployment.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The last registration ID assigned to the side.</param>
        /// <param name="getFighterCategory">Reads an entering fighter's authored category.</param>
        internal static void CreateGroups(
            IEnumerable<CombatUnit> units,
            List<TacticalCombatGroup> groups,
            Vector3 forward,
            int counter,
            Func<CombatUnit, int> getFighterCategory
        )
        {
            List<CombatUnit>[] fighters = { new(), new(), new(), new() };
            List<CapitalShipRank>[] capitals = { new(), new(), new() };
            foreach (CombatUnit unit in units)
            {
                if (unit.IsBattleStation)
                    continue;
                if (unit.IsFighter)
                {
                    int category = getFighterCategory(unit);
                    if (category is < 0 or > 3)
                        throw new InvalidDataException(
                            $"Fighter group category is invalid for '{unit.TypeID}'."
                        );
                    fighters[category].Add(unit);
                }
                else
                {
                    CapitalShipRank deployment = new(unit);
                    capitals[deployment.Category].Add(deployment);
                }
            }

            CreateFighterGroups(fighters, groups, forward, ref counter);
            foreach (List<CapitalShipRank> partition in capitals)
                partition.Sort(CapitalShipRank.Compare);
            int firstCount = CreateCapitalGroups(capitals[0], groups, 0, 3, forward, ref counter);
            int secondCount = CreateCapitalGroups(
                capitals[1],
                groups,
                firstCount,
                firstCount + 3,
                forward,
                ref counter
            );
            CreateCapitalGroups(
                capitals[2],
                groups,
                firstCount + secondCount,
                7,
                forward,
                ref counter
            );
        }

        /// <summary>
        /// Registers populated fighter categories before splitting their leading halves into empty categories.
        /// </summary>
        /// <param name="fighters">The four authored fighter buckets in registration order.</param>
        /// <param name="groups">The battle groups receiving newly registered categories.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The side's shared registration counter.</param>
        private static void CreateFighterGroups(
            IReadOnlyList<List<CombatUnit>> fighters,
            List<TacticalCombatGroup> groups,
            Vector3 forward,
            ref int counter
        )
        {
            TacticalCombatGroup[] deployed = new TacticalCombatGroup[4];
            for (int category = 0; category < deployed.Length; category++)
                if (fighters[category].Count > 0)
                {
                    deployed[category] = new TacticalCombatGroup(
                        ++counter,
                        fighters[category],
                        forward
                    )
                    {
                        FighterCategory = category,
                    };
                    groups.Add(deployed[category]);
                }
            for (int category = 0; category < deployed.Length; category++)
            {
                if (deployed[category] != null)
                    continue;
                int largest = 0;
                for (int candidate = 1; candidate < deployed.Length; candidate++)
                    if (
                        (deployed[candidate]?.Members.Count ?? 0)
                        > (deployed[largest]?.Members.Count ?? 0)
                    )
                        largest = candidate;
                TacticalCombatGroup source = deployed[largest];
                int count = source?.Members.Count ?? 0;
                if (count < 8)
                    continue;
                List<CombatUnit> moved = new(count / 2);
                for (int index = 0; index < count / 2; index++)
                    moved.Add(source.Members[index]);
                foreach (CombatUnit unit in moved)
                {
                    source.RemoveRequestedMember(unit);
                    source.RemoveMember(unit);
                }
                deployed[category] = new TacticalCombatGroup(++counter, moved, forward)
                {
                    FighterCategory = category,
                };
                groups.Add(deployed[category]);
            }
        }

        /// <summary>
        /// Partitions one scored capital roster across its available command slots and aligns each addition.
        /// </summary>
        /// <param name="ships">The scored capital ships in deployment order.</param>
        /// <param name="groups">The battle groups receiving the partition.</param>
        /// <param name="lower">The first available command slot.</param>
        /// <param name="upper">The final available command slot.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The side's shared registration counter.</param>
        /// <returns>The number of command groups created.</returns>
        private static int CreateCapitalGroups(
            IReadOnlyList<CapitalShipRank> ships,
            List<TacticalCombatGroup> groups,
            int lower,
            int upper,
            Vector3 forward,
            ref int counter
        )
        {
            (int selected, int spacing) = GetCapitalGroupLayout(ships.Count, lower, upper);
            int created = 0;
            TacticalCombatGroup group = null;
            for (int index = 0; index < ships.Count; index++)
            {
                CombatUnit unit = ships[index].Unit;
                if (index % spacing == 0 && created < selected)
                {
                    int slot = lower + created++;
                    float y = slot switch
                    {
                        0 => 0,
                        1 => 8,
                        2 => -8,
                        3 => 16,
                        4 => -16,
                        5 => 24,
                        6 => -24,
                        _ => 32,
                    };
                    unit.Position = new Vector3(0, y, unit.Position.Z);
                    group = new TacticalCombatGroup(++counter, new[] { unit }, forward);
                    groups.Add(group);
                }
                else
                {
                    group.AddRequestedMember(unit);
                    group.AddMember(unit);
                }
                AlignCapitalGroup(group);
            }
            return ships.Count == 0 ? 0 : selected;
        }

        /// <summary>
        /// Applies the marker's stored member offsets after each capital joins its deployment group.
        /// </summary>
        /// <param name="group">The active capital group being aligned.</param>
        private static void AlignCapitalGroup(TacticalCombatGroup group)
        {
            foreach (CombatUnit unit in group.Members)
            {
                Vector3 position = unit.Position;
                Vector3 offset = group.Formation.GetMemberMovement(unit);
                unit.Position = new Vector3(
                    (float)((double)position.X + offset.X),
                    (float)((double)position.Y + offset.Y),
                    (float)((double)position.Z + offset.Z)
                );
            }
        }

        /// <summary>
        /// Partitions one weapon category across the available command slots using the original 16-bit ship count.
        /// </summary>
        /// <param name="shipCount">The number of ships in the ordered category.</param>
        /// <param name="lower">The first available command slot.</param>
        /// <param name="upper">The final available command slot.</param>
        /// <returns>The number of groups to form and the spacing between group leaders.</returns>
        internal static (int Count, int Spacing) GetCapitalGroupLayout(
            int shipCount,
            int lower,
            int upper
        )
        {
            int count = shipCount & 0xffff;
            int limit = 1;
            while (limit <= upper && limit * limit <= count)
                limit++;
            int selected = Math.Min(count < 2 ? 1 : limit - 1, upper - lower + 1);
            return (selected, count / selected);
        }
    }
}
