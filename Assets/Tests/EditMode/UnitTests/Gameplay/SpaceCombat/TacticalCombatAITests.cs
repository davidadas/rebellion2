using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatAITests
    {
        [Test]
        public void Plan_OperationalStationWithUnorderedCharge_QueuesSuperlaser()
        {
            TacticalCombatUnit target = CreateUnit("target", CombatSide.Attacker, 1, 20, false);
            TacticalCombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    MaxHullStrength = 100000,
                    CurrentHullStrength = 100000,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { target, station });
            session.BattleStation.Advance(float.NaN);
            TacticalCombatAI automation = new(session);

            AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.BattleStation.Charge);
        }

        [Test]
        public void Plan_AutomatedSide_AssignsOpposingCapitalTarget()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            TacticalCombatUnit defender = session.GetUnit("defender-0");
            Assert.AreSame(session.GetUnit("attacker"), session.GetGroup(defender).PrimaryTarget);
        }

        [Test]
        public void Plan_PlayerSide_LeavesDesignatedTargetListUnchanged()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            Assert.IsEmpty(session.GetGroup(session.GetUnit("attacker")).RequestedTargets);
        }

        [Test]
        public void Plan_UnarmedAutomatedSide_StartsAvailableWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, false, 1);
            Advance(session);
            Assert.IsTrue(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Plan_OpposingGravityWell_PreventsAutomatedWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, true, 1);
            Advance(session);
            Assert.IsFalse(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Plan_FourEquivalentCapitalShips_CreatesTwoBalancedGroups()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 4);
            Advance(session);
            TacticalCombatGroup[] groups = session
                .Groups.Where(group => group.Side == CombatSide.Defender)
                .ToArray();
            Assert.AreEqual(2, groups.Length);
            Assert.IsTrue(groups.All(group => group.Members.Count == 2));
        }

        [TestCase(1000f)]
        [TestCase(999.9990234375f)]
        public void GetTargetMetric_DistantTarget_StoresTravelArithmeticBeforeAddingPenalty(float x)
        {
            TacticalCombatUnit attacker = CreateUnit(
                "metric-attacker",
                CombatSide.Attacker,
                1,
                20,
                false
            );
            TacticalCombatUnit target = CreateUnit(
                "metric-target",
                CombatSide.Defender,
                1000001,
                20,
                false
            );
            target.Systems.ApplyHullLoss(99999);
            TacticalCombatGroup group = new(2, new[] { attacker }, Vector3.UnitZ);
            group.Formation.Refresh(0);
            target.Position = group.Formation.Position + new Vector3(x, 1234, 5678);
            float attack = 0;

            float metric = TacticalCombatAI.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(0x4043b8ae, System.BitConverter.SingleToInt32Bits(metric));
        }

        [Test]
        public void GetTargetMetric_HullAndShields_StoresWeightedHullBeforeAddingShields()
        {
            TacticalCombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            TacticalCombatUnit target = new(
                new CapitalShip
                {
                    InstanceID = "target",
                    MaxHullStrength = 7,
                    CurrentHullStrength = 7,
                    MaxShieldStrength = 4,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = group.Formation.Position,
            };
            float attack = 1;

            float metric = TacticalCombatAI.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(0x4108cccc, System.BitConverter.SingleToInt32Bits(metric));
            Assert.AreEqual(1, attack);
        }

        [Test]
        public void GetTargetMetric_RechargeRoundsToAccumulatedAttack_RejectsExhaustedAttack()
        {
            TacticalCombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            TacticalCombatUnit target = new(
                new Starfighter
                {
                    InstanceID = "target",
                    MaxSquadronSize = 1,
                    CurrentSquadronSize = 1,
                    Hyperdrive = 1,
                },
                CombatSide.Defender,
                1000001
            );
            target.Systems.ApplyHullLoss(5);
            float attack = (float)target.Systems.GetShieldRecharge();

            float metric = TacticalCombatAI.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(-1, metric);
        }

        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(float.NaN, float.NaN, float.NaN)]
        public void GetTargetMetric_OnlyZeroOrUnorderedSeparationComponents_OmitsTravelPenalty(
            float x,
            float y,
            float z
        )
        {
            TacticalCombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            TacticalCombatUnit target = CreateUnit(
                "target",
                CombatSide.Defender,
                1000001,
                0,
                false
            );
            target.Position = group.Formation.Position + new Vector3(x, y, z);
            float attack = 1;

            float metric = TacticalCombatAI.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(64999.99609375f, metric);
        }

        [Test]
        public void GetTargetMetric_LargeFiniteSeparation_StoresOverflowedSquaredDistance()
        {
            TacticalCombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            group.Formation.Refresh(0);
            TacticalCombatUnit target = CreateUnit(
                "target",
                CombatSide.Defender,
                1000001,
                0,
                false
            );
            target.Position = group.Formation.Position + Vector3.UnitX * float.MaxValue;
            float attack = 1;

            float metric = TacticalCombatAI.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(float.PositiveInfinity, metric);
        }

        [Test]
        public void Plan_LargeCapitalPriority_OrdersAppendedFighterByStoredScoreTie()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit capital = CreateUnit(
                "priority-capital",
                CombatSide.Attacker,
                2,
                100000000,
                false
            );
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "priority-fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Hyperdrive = 1,
                    SublightSpeed = 9,
                    Agility = 5,
                    LaserCannon = 5,
                    LaserRange = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, capital, fighter }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatAI automation = new(session);

            AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));

            Assert.AreSame(fighter, group.RequestedTargets.Single());
        }

        [Test]
        public void Plan_EngineDamageAfterEntry_PreservesCapturedCapitalPartitionOrder()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 4);
            TacticalCombatUnit damaged = session.GetUnit("defender-0");
            for (int damage = 0; damage < 4; damage++)
                damaged.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Advance(session);

            CollectionAssert.AreEqual(
                new[] { "defender-0", "defender-1" },
                session.GetGroup(damaged).Members.Select(unit => unit.InstanceID)
            );
        }

        [Test]
        public void Plan_SixUpdateCadence_ReassignsOnlyOnNextSixthUpdate()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit preferred = CreateUnit(
                "preferred",
                CombatSide.Attacker,
                1,
                40,
                false
            );
            TacticalCombatUnit secondary = CreateUnit(
                "secondary",
                CombatSide.Attacker,
                2,
                20,
                false
            );
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, preferred, secondary }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatAI automation = new(session);
            AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));
            session.SetTarget(group, secondary);

            for (int update = 0; update < 5; update++)
                AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));
            Assert.AreSame(secondary, group.RequestedTargets.Single());
            AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));

            Assert.AreSame(preferred, group.RequestedTargets.Single());
        }

        [Test]
        public void Plan_PlayerAutomation_PublishesRequestedCommandGroupCategory()
        {
            TacticalCombatUnit player = CreateUnit("player", CombatSide.Attacker, 1, 20, false);
            TacticalCombatUnit opposing = CreateUnit(
                "opposing",
                CombatSide.Defender,
                1000001,
                20,
                false
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { player, opposing });
            session.SetAutomated(CombatSide.Attacker, true);
            TacticalCombatGroup previous = session.GetGroup(player);
            TacticalCombatAI automation = new(session);

            AdvanceAssessment(automation, session.GetSide(CombatSide.Attacker));

            TacticalCombatGroup requested = session.GetRequestedGroup(player);
            Assert.AreNotSame(previous, requested);
            Assert.AreSame(requested, session.GetObjectGroup(CombatSide.Attacker, 0));
            Assert.AreSame(previous, session.GetGroup(player));
        }

        [Test]
        public void Plan_PartiallyDamagedUnarmedFighters_PrioritizesRetainedRechargeRatio()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit earlier = CreateUnarmedFighter("earlier", 1, 3.8450623f);
            TacticalCombatUnit preferred = CreateUnarmedFighter("preferred", 2, 3.8450603f);
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, earlier, preferred }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatAI automation = new(session);

            AdvanceAssessment(automation, session.GetSide(CombatSide.Defender));

            Assert.AreSame(preferred, group.RequestedTargets.Single());
        }

        [Test]
        public void PrepareCandidates_AutomatedSide_DoesNotIssuePlanningCommands()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            TacticalCombatUnit unit = session.GetUnit("defender-0");
            TacticalCombatGroup group = session.GetGroup(unit);
            TacticalCombatAI automation = new(session);

            automation.PrepareCandidates(session.GetSide(CombatSide.Defender));

            Assert.IsEmpty(group.RequestedTargets);
        }

        [Test]
        public void AssessStrength_DepartingCapital_RetainsItsDisplayedAttackBudget()
        {
            TacticalCombatUnit unit = CreateUnit(
                "departing",
                CombatSide.Defender,
                1000001,
                20,
                false
            );
            unit.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            TacticalCombatSession session = CreateAssessmentSession(new[] { unit });
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_RecoveryCompletedFighter_ExcludesItsAttackBudget()
        {
            TacticalCombatUnit unit = new(
                new Starfighter
                {
                    InstanceID = "recovered",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 5,
                    Hyperdrive = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                1
            );
            unit.Status = TacticalCombatUnitStatus.Recovered;
            TacticalCombatSession session = CreateAssessmentSession(new[] { unit });
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Attacker));

            Assert.AreEqual(0, session.GetSide(CombatSide.Attacker).AttackBudget);
        }

        [Test]
        public void AssessStrength_GlobalInitialHangars_AddsOpposingDockedFighterOnlyToEmpire()
        {
            TacticalCombatSession session = CreateHangarSession(out TacticalCombatUnit fighter);
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Attacker));
            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Attacker).AttackBudget);
            Assert.That(session.GetSide(CombatSide.Defender).AttackBudget, Is.GreaterThan(0));
            Assert.AreEqual(TacticalCombatUnitStatus.Docked, fighter.Status);
        }

        [Test]
        public void AssessStrength_InitialFighterLaunch_RemovesItFromGlobalHangarAssessment()
        {
            TacticalCombatSession session = CreateHangarSession(out TacticalCombatUnit fighter);
            TacticalCombatAI automation = new(session);
            for (
                int frame = 0;
                frame < 600 && fighter.Status == TacticalCombatUnitStatus.Docked;
                frame++
            )
                session.Advance(50);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, fighter.Status);

            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_AppliedPrimaryTargetWithNoShieldRecharge_SuppressesCapital()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit opposing = CreateUnit("opposing", CombatSide.Attacker, 1, 20, false);
            TacticalCombatSession session = CreateAssessmentSession(new[] { own, opposing });
            session.GetGroup(opposing).SetTarget(own);
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_RequestedTargetBeforeApplication_DoesNotSuppressCapital()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit opposing = CreateUnit("opposing", CombatSide.Attacker, 1, 20, false);
            TacticalCombatSession session = CreateAssessmentSession(new[] { own, opposing });
            session.SetTarget(session.GetGroup(opposing), own);
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_OperationalEmpireStation_AddsItsFixedAttackBudget()
        {
            TacticalCombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { station });
            TacticalCombatAI automation = new(session);

            automation.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20000000, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        /// <summary>
        /// Captures an unarmed carrier whose initially docked fighter belongs to the Alliance.
        /// </summary>
        /// <param name="fighter">The fighter retained in the initial hangar root.</param>
        /// <returns>The encounter with a legal captured carrier relationship.</returns>
        private static TacticalCombatSession CreateHangarSession(out TacticalCombatUnit fighter)
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
            fighter = new TacticalCombatUnit(sourceFighter, CombatSide.Attacker, 2);
            TacticalCombatUnit opposing = CreateUnit(
                "opposing",
                CombatSide.Defender,
                1000001,
                0,
                false
            );
            return CreateAssessmentSession(
                new[] { new TacticalCombatUnit(carrier, CombatSide.Attacker, 1), fighter, opposing }
            );
        }

        /// <summary>
        /// Drives both public assessment phases for fixtures that do not require intervening session maintenance.
        /// </summary>
        /// <param name="automation">The captured tactical planner.</param>
        /// <param name="side">The side being assessed and planned.</param>
        private static void AdvanceAssessment(TacticalCombatAI automation, TacticalCombatSide side)
        {
            automation.PrepareCandidates(side);
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
        private static TacticalCombatSession CreateSession(
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
                new TacticalCombatRandom(42)
            );
        }

        /// <summary>
        /// Creates one automated side for direct assessment without enabling withdrawal.
        /// </summary>
        /// <param name="units">The captured units on both sides.</param>
        /// <returns>The isolated assessment encounter.</returns>
        private static TacticalCombatSession CreateAssessmentSession(
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
                new TacticalCombatRandom(42)
            );
        }

        /// <summary>
        /// Captures a partially damaged unarmed squadron whose recharge remains available.
        /// </summary>
        /// <param name="id">The campaign squadron identifier.</param>
        /// <param name="index">The battle registration identifier.</param>
        /// <param name="loss">The hull loss applied after entry.</param>
        /// <returns>The active squadron with its initial recharge sample intact.</returns>
        private static TacticalCombatUnit CreateUnarmedFighter(string id, int index, float loss)
        {
            TacticalCombatUnit unit = new(
                new Starfighter
                {
                    InstanceID = id,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 3,
                    Hyperdrive = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
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
        private static TacticalCombatUnit CreateUnit(
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
            return new TacticalCombatUnit(ship, side, index)
            {
                Position = new Vector3(0, 0, side == CombatSide.Attacker ? -40 : 40),
                Heading = side == CombatSide.Attacker ? Vector3.UnitZ : -Vector3.UnitZ,
            };
        }

        /// <summary>
        /// Advances through initial deployment, automation, and delayed command application.
        /// </summary>
        /// <param name="session">The encounter under test.</param>
        private static void Advance(TacticalCombatSession session)
        {
            for (int frame = 0; frame < 200; frame++)
                session.Advance(50);
        }
    }
}
