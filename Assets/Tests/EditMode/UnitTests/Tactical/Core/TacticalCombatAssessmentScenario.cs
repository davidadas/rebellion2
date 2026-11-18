using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.AI.Planners;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.AI;
using Rebellion.Tactical.Integration.Campaign;
using Rebellion.Tactical.Runtime.Audio;
using Rebellion.Util.Random;
using BitConverter = global::System.BitConverter;

namespace Rebellion.Tests.Tactical
{
    internal static class TacticalCombatAssessmentScenario
    {
        /// <summary>
        /// Captures an unarmed carrier whose initially docked fighter belongs to the Alliance.
        /// </summary>
        /// <param name="fighter">The fighter retained in the initial hangar root.</param>
        /// <returns>The encounter with a legal captured carrier relationship.</returns>
        internal static TacticalCombatSession CreateHangarSession(out TacticalCombatUnit fighter)
        {
            CapitalShip carrier = new()
            {
                InstanceID = "carrier",
                MaxHullStrength = 100000,
                CurrentHullStrength = 100000,
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter sourceFighter = new()
            {
                InstanceID = "docked-fighter",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 5,
                LaserRange = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            carrier.AddStarfighter(sourceFighter);
            sourceFighter.SetParent(carrier);
            fighter = new TacticalCombatUnit(
                TacticalCombatUnitCapture.Capture(sourceFighter),
                CombatSide.Attacker,
                2
            );
            TacticalCombatUnit opposing = CreateUnit(
                "opposing",
                CombatSide.Defender,
                1000001,
                0,
                false
            );
            return CreateAssessmentSession(
                new[]
                {
                    new TacticalCombatUnit(
                        TacticalCombatUnitCapture.Capture(carrier),
                        CombatSide.Attacker,
                        1
                    ),
                    fighter,
                    opposing,
                }
            );
        }

        /// <summary>
        /// Drives both public assessment phases for fixtures that do not require intervening session maintenance.
        /// </summary>
        /// <param name="automation">The captured tactical planner.</param>
        /// <param name="targeting">The encounter assessment shared with explicit orders.</param>
        /// <param name="side">The side being assessed and planned.</param>
        internal static void AdvanceAssessment(
            TacticalCombatPlanner automation,
            TacticalCombatTargeting targeting,
            TacticalCombatSide side
        )
        {
            targeting.PrepareCandidates(side);
            automation.Plan(side);
        }

        /// <summary>
        /// Builds one player-controlled ship opposite an automated capital-ship deployment.
        /// </summary>
        /// <param name="attackerStrength">The player's battery strength.</param>
        /// <param name="defenderStrength">The automated side's battery strength.</param>
        /// <param name="gravityWell">Whether the player blocks enemy withdrawal.</param>
        /// <param name="defenderCount">The automated deployment count.</param>
        /// <returns>The isolated encounter.</returns>
        internal static TacticalCombatSession CreateSession(
            int attackerStrength,
            int defenderStrength,
            bool gravityWell,
            int defenderCount
        )
        {
            List<TacticalCombatUnit> units = new();
            units.Add(
                CreateUnit("attacker", CombatSide.Attacker, 1, attackerStrength, gravityWell)
            );
            for (int index = 0; index < defenderCount; index++)
                units.Add(
                    CreateUnit(
                        "defender-" + index,
                        CombatSide.Defender,
                        1000001 + index,
                        defenderStrength,
                        false
                    )
                );
            List<TacticalCombatGroup> groups = units.ConvertAll(unit => new TacticalCombatGroup(
                unit.Index + 100,
                new[] { unit },
                unit.Side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ
            ));
            TacticalCombatSide attacker = new(
                CombatSide.Attacker,
                0,
                Vector3.UnitZ,
                true,
                0,
                .9f,
                null,
                null,
                0,
                0,
                false
            );
            TacticalCombatSide defender = new(
                CombatSide.Defender,
                1,
                -Vector3.UnitZ,
                true,
                0,
                .9f,
                null,
                null,
                0,
                0,
                true
            );
            return new TacticalCombatSession(
                units,
                groups,
                attacker,
                defender,
                CombatSide.Attacker,
                100,
                new LinearCongruentialRandomProvider(42),
                TacticalCombatAudio.Create
            );
        }

        /// <summary>
        /// Creates one automated side for direct assessment without enabling withdrawal.
        /// </summary>
        /// <param name="units">The captured units on both sides.</param>
        /// <returns>The isolated assessment encounter.</returns>
        internal static TacticalCombatSession CreateAssessmentSession(
            IReadOnlyList<TacticalCombatUnit> units
        )
        {
            List<TacticalCombatGroup> groups = new();
            foreach (TacticalCombatUnit unit in units)
                groups.Add(
                    new TacticalCombatGroup(
                        unit.Index + 100,
                        new[] { unit },
                        unit.Side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ
                    )
                );
            TacticalCombatSide attacker = new(
                CombatSide.Attacker,
                0,
                Vector3.UnitZ,
                false,
                0,
                .9f,
                null,
                null,
                0,
                0,
                false
            );
            TacticalCombatSide defender = new(
                CombatSide.Defender,
                1,
                -Vector3.UnitZ,
                false,
                0,
                .9f,
                null,
                null,
                0,
                0,
                true
            );
            return new TacticalCombatSession(
                units,
                groups,
                attacker,
                defender,
                CombatSide.Attacker,
                100,
                new LinearCongruentialRandomProvider(42),
                TacticalCombatAudio.Create
            );
        }

        /// <summary>
        /// Captures a partially damaged unarmed squadron whose recharge remains available.
        /// </summary>
        /// <param name="id">The campaign squadron identifier.</param>
        /// <param name="index">The battle registration identifier.</param>
        /// <param name="loss">The hull loss applied after entry.</param>
        /// <returns>The active squadron with its initial recharge sample intact.</returns>
        internal static TacticalCombatUnit CreateUnarmedFighter(string id, int index, float loss)
        {
            TacticalCombatUnit unit = new(
                TacticalCombatUnitCapture.Capture(
                    new Starfighter
                    {
                        InstanceID = id,
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 3,
                        Hyperdrive = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                ),
                CombatSide.Attacker,
                index
            );
            unit.Systems.ApplyHullLoss(loss);
            return unit;
        }

        /// <summary>
        /// Captures a durable capital ship with one directional weapon category.
        /// </summary>
        /// <param name="id">The campaign identity.</param>
        /// <param name="side">The deployed side.</param>
        /// <param name="index">The battle registration index.</param>
        /// <param name="strength">The battery and recharge strength.</param>
        /// <param name="gravityWell">Whether the ship projects a gravity well.</param>
        /// <returns>The deployed tactical unit.</returns>
        internal static TacticalCombatUnit CreateUnit(
            string id,
            CombatSide side,
            int index,
            int strength,
            bool gravityWell
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                MaxHullStrength = 100000,
                CurrentHullStrength = 100000,
                Hyperdrive = 1,
                SublightSpeed = 4,
                Maneuverability = 1,
                WeaponRecharge = strength,
                HasGravityWell = gravityWell,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                strength,
                strength,
                strength,
                strength,
                100,
            };
            return new TacticalCombatUnit(TacticalCombatUnitCapture.Capture(ship), side, index)
            {
                Position = new Vector3(0, 0, side == CombatSide.Attacker ? -40 : 40),
                Heading = side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ,
            };
        }

        /// <summary>
        /// Advances through initial deployment, automation, and delayed command application.
        /// </summary>
        /// <param name="session">The encounter under test.</param>
        internal static void Advance(TacticalCombatSession session)
        {
            for (int frame = 0; frame < 200; frame++)
                session.Advance(50);
        }
    }
}
