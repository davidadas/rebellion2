using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatAutoResolverTests
    {
        [Test]
        public void Resolve_EquivalentBattles_ReturnsDeterministicResults()
        {
            SpaceCombatResult first = ResolveSymmetricBattle();
            SpaceCombatResult second = ResolveSymmetricBattle();

            Assert.AreEqual(first.AttackerOutcome, second.AttackerOutcome);
            Assert.AreEqual(first.DefenderOutcome, second.DefenderOutcome);
            CollectionAssert.AreEqual(GetResolvedShipHulls(first), GetResolvedShipHulls(second));
        }

        [TestCase(0)]
        [TestCase(100)]
        public void Resolve_AdmiralLeadership_DoesNotSupplyWeaponRecharge(int leadership)
        {
            CapitalShip attacker = CreateShip("attacker", 100, 20);
            attacker.WeaponRecharge = 0;
            CapitalShip target = CreatePassiveTarget("target", 100);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>(),
                attackerCommand: new SpaceCombatCommandModifiers(leadership, 0)
            );

            Assert.AreEqual(100, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(0, 10, false, 98)]
        [TestCase(0, 10, true, 82)]
        [TestCase(10, 10, true, 84)]
        [TestCase(13, 10, true, 84)]
        [TestCase(40, 10, true, 90)]
        [TestCase(50, 10, true, 92)]
        [TestCase(50, 10, false, 92)]
        [TestCase(80, 10, true, 98)]
        [TestCase(86, 10, true, 98)]
        [TestCase(90, 10, true, 98)]
        [TestCase(100, 10, true, 98)]
        [TestCase(100, 25, true, 90)]
        [TestCase(50, 0, true, 98)]
        [TestCase(50, -1, true, 98)]
        public void Resolve_AdmiralLeadership_UsesCapitalManeuverBudget(
            int leadership,
            int divisor,
            bool bindAdmiral,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 8;
            attacker.WeaponRecharge = 8;
            Starfighter target = CreateFighter("target", 100, 0);
            target.Agility = 3;
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AdmiralLeadershipDivisor = divisor;
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target },
                attackerCommand: new SpaceCombatCommandModifiers(
                    leadership,
                    0,
                    bindAdmiral ? attacker : null
                )
            );

            Assert.AreEqual(expected, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_FractionalAdmiralManeuver_RoundsBeforeComparingTarget()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 8;
            attacker.WeaponRecharge = 80;
            Starfighter target = CreateFighter("target", 12, 0);
            target.Agility = 8;
            target.ShieldStrength = 0;

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target },
                attackerCommand: new SpaceCombatCommandModifiers(27, 0, attacker)
            );

            Assert.AreEqual(4, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [TestCase(true, true, 90)]
        [TestCase(true, false, 98)]
        [TestCase(false, true, 98)]
        public void Resolve_AdmiralCarrierAvailability_ControlsCapitalManeuverBonus(
            bool carrierParticipates,
            bool carrierSurvives,
            int expectedCraft
        )
        {
            CapitalShip flagship = CreatePassiveTarget("flagship", 100);
            if (!carrierSurvives)
                flagship.CurrentHullStrength = 0;
            CapitalShip escort = CreateShip("escort", 100, 0);
            escort.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 8;
            escort.WeaponRecharge = 8;
            Starfighter target = CreateFighter("target", 100, 0);
            target.Agility = 3;
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AdmiralLeadershipDivisor = 25;
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                carrierParticipates ? new[] { flagship, escort } : new[] { escort },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target },
                attackerCommand: new SpaceCombatCommandModifiers(100, 0, flagship)
            );

            Assert.AreEqual(expectedCraft, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [TestCase(0)]
        [TestCase(100)]
        public void Resolve_CommanderCombat_DoesNotMultiplySquadronFirepower(int combat)
        {
            Starfighter attacker = CreateFighter("attacker", 12, 10);
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { target },
                Array.Empty<Starfighter>(),
                attackerCommand: new SpaceCombatCommandModifiers(0, combat)
            );

            Assert.AreEqual(88, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(0)]
        [TestCase(100)]
        public void Resolve_CommanderCombat_DoesNotChangeFighterTurnRate(int combat)
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 8;
            attacker.WeaponRecharge = 8;
            Starfighter target = CreateFighter("target", 100, 0);
            target.Agility = 3;
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target },
                defenderCommand: new SpaceCombatCommandModifiers(0, combat)
            );

            Assert.AreEqual(98, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Resolve_IdenticalWeaponArcs_DoNotMultiplySustainedOutput(bool everyArc)
        {
            CapitalShip attacker = CreateShip("attacker", 100, 100, everyArc);
            attacker.WeaponRecharge = 20;
            CapitalShip target = CreatePassiveTarget("target", 1000);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(980, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(0.25, 95)]
        [TestCase(1, 80)]
        [TestCase(0, 100)]
        [TestCase(-1, 100)]
        public void Resolve_CapitalShipLaserCannonAgainstCapitalShip_UsesConfiguredMultiplier(
            double multiplier,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            attacker.WeaponRecharge = 20;
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = multiplier;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(expected, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(PrimaryWeaponType.LaserCannon, 8)]
        [TestCase(PrimaryWeaponType.Turbolaser, 9)]
        [TestCase(PrimaryWeaponType.IonCannon, 10)]
        public void Resolve_CapitalShipAgainstFighters_UsesWeaponCategoryEffectiveness(
            PrimaryWeaponType weapon,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[weapon][0] = 20;
            attacker.WeaponRecharge = 2;
            Starfighter target = CreateFighter("target", 10, 0);
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.TurbolaserFighterDamageMultiplier = 0.5;
            config.AutoResolveFighterHullPerCraft = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target }
            );

            Assert.AreEqual(expected, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CapitalLaserFormationWithMixedTargets_PrefersFighterCandidates()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 0);
            attacker.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 20;
            attacker.WeaponRecharge = 8;
            CapitalShip targetShip = CreatePassiveTarget("ship", 100);
            Starfighter targetFighter = CreateFighter("fighter", 12, 0);
            targetFighter.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterHullPerCraft = 1;
            config.LaserCannonCapitalDamageMultiplier = 0.1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { targetShip },
                new[] { targetFighter }
            );

            Assert.AreEqual(100, GetShipOutcome(result, targetShip).HullAfter);
            Assert.AreEqual(4, GetFighterOutcome(result, targetFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_LaserSquadronWithMixedTargets_PrefersFighterCandidates()
        {
            Starfighter attacker = CreateFighter("attacker", 12, 10);
            CapitalShip targetShip = CreatePassiveTarget("ship", 100);
            Starfighter targetFighter = CreateFighter("fighter", 12, 0);
            targetFighter.Agility = 8;
            targetFighter.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterHullPerCraft = 1;
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;
            config.LaserCannonCapitalDamageMultiplier = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { targetShip },
                new[] { targetFighter }
            );

            Assert.AreEqual(100, GetShipOutcome(result, targetShip).HullAfter);
            Assert.AreEqual(10, GetFighterOutcome(result, targetFighter).SquadronSizeAfter);
        }

        [TestCase("officer")]
        [TestCase("regiment")]
        [TestCase("special-forces")]
        public void Resolve_DirectUsableCargo_PrioritizesTheLaterOtherwiseIdenticalTarget(
            string cargoKind
        )
        {
            var battle = ResolveCargoPriorityBattle(ship =>
            {
                ISceneNode cargo = CreatePriorityCargo(cargoKind);
                ship.AddChild(cargo);
                cargo.SetParent(ship);
            });

            Assert.AreEqual(100, GetShipOutcome(battle.Result, battle.First).HullAfter);
            Assert.AreEqual(90, GetShipOutcome(battle.Result, battle.Second).HullAfter);
        }

        [TestCase("officer", "in-transit")]
        [TestCase("regiment", "in-transit")]
        [TestCase("special-forces", "in-transit")]
        [TestCase("regiment", "incomplete")]
        [TestCase("special-forces", "incomplete")]
        [TestCase("officer", "disabled")]
        [TestCase("regiment", "disabled")]
        [TestCase("special-forces", "disabled")]
        public void Resolve_UnusableCargo_DoesNotChangeTargetPriority(
            string cargoKind,
            string cargoState
        )
        {
            var battle = ResolveCargoPriorityBattle(ship =>
            {
                ISceneNode cargo = CreatePriorityCargo(cargoKind);
                if (cargoState == "in-transit")
                    ((IMovable)cargo).Movement = TestMovement.Create();
                else if (cargoState == "incomplete")
                    ((IManufacturable)cargo).ManufacturingStatus = ManufacturingStatus.Building;
                else if (cargoState == "disabled")
                    cargo.IsEnabled = false;

                ship.AddChild(cargo);
                cargo.SetParent(ship);
            });

            Assert.AreEqual(90, GetShipOutcome(battle.Result, battle.First).HullAfter);
            Assert.AreEqual(100, GetShipOutcome(battle.Result, battle.Second).HullAfter);
        }

        [Test]
        public void Resolve_InjuredForeignCaptive_IsStillPriorityCargo()
        {
            var battle = ResolveCargoPriorityBattle(ship =>
            {
                Officer captive = new Officer
                {
                    InstanceID = "captive",
                    OwnerInstanceID = "foreign-owner",
                    InjuryPoints = 20,
                    IsCaptured = true,
                };
                ship.AddChild(captive);
                captive.SetParent(ship);
            });

            Assert.AreEqual(100, GetShipOutcome(battle.Result, battle.First).HullAfter);
            Assert.AreEqual(90, GetShipOutcome(battle.Result, battle.Second).HullAfter);
        }

        [TestCase(0)]
        [TestCase(100000)]
        public void Resolve_WeaponRange_DoesNotGateSustainedFire(int range)
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            attacker.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = range;
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(90, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_RechargeLimitedBank_AppliesItsRateDuringTheFiringPhase()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 100);
            attacker.WeaponRecharge = 5;
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(1, result.IterationsCompleted);
            Assert.AreEqual(95, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_AdditionalFighterTypeWithSparseGroups_CompletesTheFiringPhase()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            Starfighter[] designs = Enumerable
                .Range(0, 4)
                .Select(index => new Starfighter
                {
                    TypeID = "configured-type-" + index,
                    FormationPriority = index,
                    ManufacturingFactionInstanceIDs = new List<string> { "test-faction" },
                })
                .ToArray();
            Starfighter[] fighters = Enumerable
                .Range(0, 32)
                .Select(index =>
                {
                    Starfighter fighter = CreateFighter("fighter-" + index, 12, 1);
                    fighter.TypeID = "additional-type";
                    fighter.OwnerInstanceID = "test-faction";
                    return fighter;
                })
                .ToArray();
            CapitalShip target = CreatePassiveTarget("target", 1000);

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                fighters,
                new[] { target },
                Array.Empty<Starfighter>(),
                random: new FixedCombatRNG(50),
                starfighterTypes: designs
            );

            Assert.AreEqual(1, result.IterationsCompleted);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.IsEmpty(result.FighterLosses);
        }

        [Test]
        public void Resolve_DifferentFighterTypesWithTheSameRole_KeepSeparateFormations()
        {
            Starfighter first = CreateFighter("first", 12, 1);
            Starfighter second = CreateFighter("second", 12, 1);
            first.TypeID = "first-design";
            second.TypeID = "second-design";
            foreach (Starfighter fighter in new[] { first, second })
            {
                fighter.CurrentSquadronSize = 1;
                fighter.Roles.Add(StarfighterRole.Bomber);
            }
            CapitalShip target = CreatePassiveTarget("target", 100);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                Array.Empty<CapitalShip>(),
                new[] { first, second },
                new[] { target },
                Array.Empty<Starfighter>(),
                random: new FixedCombatRNG(50)
            );

            Assert.AreEqual(98, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_RepeatedFighterDesign_DoesNotDuplicateTheFormation()
        {
            Starfighter fighter = CreateFighter("fighter", 12, 1);
            fighter.TypeID = "design";
            fighter.OwnerInstanceID = "owner";
            fighter.CurrentSquadronSize = 1;
            Starfighter design = new Starfighter
            {
                TypeID = fighter.TypeID,
                ManufacturingFactionInstanceIDs = new List<string> { "owner" },
            };
            CapitalShip target = CreatePassiveTarget("target", 100);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                Array.Empty<CapitalShip>(),
                new[] { fighter },
                new[] { target },
                Array.Empty<Starfighter>(),
                starfighterTypes: new[] { design, design }
            );

            Assert.AreEqual(99, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_UnusedAutomaticDesigns_DoNotChangeTheFormations()
        {
            Starfighter[] fighters = Enumerable
                .Range(0, 32)
                .Select(index =>
                {
                    Starfighter fighter = CreateFighter("fighter-" + index, 12, 1);
                    fighter.TypeID = "present-design";
                    fighter.OwnerInstanceID = "owner";
                    fighter.CurrentSquadronSize = 1;
                    return fighter;
                })
                .ToArray();
            Starfighter[] unused = Enumerable
                .Range(0, 20)
                .Select(index => new Starfighter
                {
                    TypeID = "unused-design-" + index,
                    ManufacturingFactionInstanceIDs = new List<string> { "owner" },
                    Roles = new List<StarfighterRole> { StarfighterRole.Bomber },
                })
                .ToArray();
            CapitalShip firstTarget = CreatePassiveTarget("target", 100);
            CapitalShip secondTarget = CreatePassiveTarget("target", 100);

            SpaceCombatResult withoutUnused = ResolveSingleStep(
                CreateConfig(),
                Array.Empty<CapitalShip>(),
                fighters,
                new[] { firstTarget },
                Array.Empty<Starfighter>()
            );
            SpaceCombatResult withUnused = ResolveSingleStep(
                CreateConfig(),
                Array.Empty<CapitalShip>(),
                fighters,
                new[] { secondTarget },
                Array.Empty<Starfighter>(),
                starfighterTypes: unused
            );

            Assert.AreEqual(
                GetShipOutcome(withoutUnused, firstTarget).HullAfter,
                GetShipOutcome(withUnused, secondTarget).HullAfter
            );
        }

        [TestCase(3, 99)]
        [TestCase(4, 98)]
        public void Resolve_SmallGroupedAttacks_ApplyHullMinimumOncePerFormation(
            int shipCount,
            int expectedHull
        )
        {
            CapitalShip[] attackers = Enumerable
                .Range(0, shipCount)
                .Select(index => CreateShip("attacker-" + index, 100, 1))
                .ToArray();
            foreach (CapitalShip ship in attackers)
                ship.CurrentHullStrength = 1;
            CapitalShip target = CreatePassiveTarget("target", 100);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                attackers,
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>(),
                random: new FixedCombatRNG(50)
            );

            Assert.AreEqual(expectedHull, GetShipOutcome(result, target).HullAfter);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Resolve_InitiativeSideDestroysOpponent_PreventsReturnFire(
            bool attackerHasInitiative
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 100);
            CapitalShip defender = CreateShip("defender", 100, 100);

            SpaceCombatResult result = Resolve(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { defender },
                Array.Empty<Starfighter>(),
                random: new FixedCombatRNG(50),
                attackerHasInitiative: attackerHasInitiative
            );

            Assert.AreEqual(
                attackerHasInitiative ? 100 : 0,
                GetShipOutcome(result, attacker).HullAfter
            );
            Assert.AreEqual(
                attackerHasInitiative ? 0 : 100,
                GetShipOutcome(result, defender).HullAfter
            );
            Assert.AreEqual(
                attackerHasInitiative ? CombatSide.Attacker : CombatSide.Defender,
                result.Winner
            );
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [TestCase(12, 88)]
        [TestCase(6, 94)]
        public void Resolve_ReducedSquadron_UsesSurvivingCraftOnce(int craft, int expected)
        {
            Starfighter attacker = CreateFighter("attacker", 12, 10);
            attacker.CurrentSquadronSize = craft;
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;
            config.LaserCannonCapitalDamageMultiplier = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(expected, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(0.25, 97)]
        [TestCase(1, 88)]
        [TestCase(0, 100)]
        public void Resolve_FighterLaserAgainstCapitalShip_UsesConfiguredMultiplier(
            double multiplier,
            int expected
        )
        {
            Starfighter attacker = CreateFighter("attacker", 12, 10);
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;
            config.LaserCannonCapitalDamageMultiplier = multiplier;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(expected, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(0, 96)]
        [TestCase(1, 98)]
        public void Resolve_FighterTorpedoes_RequireExposedCapitalHull(int shields, int expected)
        {
            Starfighter attacker = CreateFighter("attacker", 12, 10);
            attacker.Torpedoes = 2;
            CapitalShip target = CreatePassiveTarget("target", 100);
            target.MaxShieldStrength = shields;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;
            config.AutoResolveTorpedoDamagePerStrength = 0.5;
            config.LaserCannonCapitalDamageMultiplier = 0.25;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(expected, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_IonWeaponsWithoutShields_DamageCapitalHull()
        {
            Starfighter attacker = CreateFighter("attacker", 12, 0);
            attacker.IonCannon = 10;
            CapitalShip target = CreatePassiveTarget("target", 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterWeaponRechargeMultiplier = 1;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                Array.Empty<CapitalShip>(),
                new[] { attacker },
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(88, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(4, 99)]
        [TestCase(5, 100)]
        [TestCase(6, 100)]
        public void Resolve_ShieldOverflow_RequiresPositiveOverflowForHullDamage(
            int shields,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 5);
            CapitalShip target = CreatePassiveTarget("target", 100);
            target.MaxShieldStrength = shields;

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(expected, GetShipOutcome(result, target).HullAfter);
        }

        [Test]
        public void Resolve_DamagedCapitalShip_ReducesSustainedOutput()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 20);
            attacker.CurrentHullStrength = 50;
            CapitalShip target = CreatePassiveTarget("target", 100);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>()
            );

            Assert.AreEqual(90, GetShipOutcome(result, target).HullAfter);
        }

        [TestCase(100, 6)]
        [TestCase(0, 3)]
        public void Resolve_ShieldSystemHits_ReduceRechargeUntilStrengthIsSuppressed(
            int roll,
            int expectedIterations
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            attacker.MaxShieldStrength = 1000;
            attacker.ShieldRechargeRate = 1000;
            CapitalShip target = CreateShip("target", 100, 10);
            target.MaxShieldStrength = 100;
            target.ShieldRechargeRate = 20;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 5;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { target },
                Array.Empty<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(roll)
            );

            Assert.AreEqual(expectedIterations, result.IterationsCompleted);
            Assert.AreEqual(100, GetShipOutcome(result, target).HullAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_WeaponSystemHits_StopFireWithoutDestroyingTheShip()
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            attacker.MaxShieldStrength = 1000;
            CapitalShip defender = CreateShip("defender", 1000, 10);
            defender.ShieldRechargeRate = 100;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 5;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { defender },
                Array.Empty<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(80),
                attackerHasInitiative: true
            );

            Assert.AreEqual(4, result.IterationsCompleted);
            Assert.AreEqual(100, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(960, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
        }

        [TestCase(120, 6)]
        [TestCase(121, 5)]
        public void Resolve_DamagedSquadron_StartsWithFullShieldsBeforeItsFirstRecovery(
            int incomingDamage,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            Starfighter target = CreateFighter("target", 12, 0);
            target.CurrentSquadronSize = 6;
            target.ShieldStrength = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveWeaponDamagePerStrength = incomingDamage / 10.0;
            config.AutoResolveFighterShieldRechargePerCraft = 0;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target }
            );

            Assert.AreEqual(expected, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [TestCase(4, 0)]
        [TestCase(8, 1)]
        public void Resolve_FighterHullPerCraft_ControlsDurability(int hullPerCraft, int expected)
        {
            CapitalShip attacker = CreateShip("attacker", 100, 8);
            Starfighter target = CreateFighter("target", 2, 0);
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveFighterHullPerCraft = hullPerCraft;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target }
            );

            Assert.AreEqual(expected, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        public void Resolve_FighterShieldRechargePerCraft_PreservesCraftUntilForcedWithdrawal(
            double recharge,
            int expectedCraft
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 3);
            attacker.MaxShieldStrength = 100;
            attacker.ShieldRechargeRate = 100;
            Starfighter target = CreateFighter("target", 2, 1);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveWeaponDamagePerStrength = 0.5;
            config.AutoResolveFighterShieldRechargePerCraft = recharge;
            config.AutoResolveStagnationIterations = 3;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target },
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(100),
                attackerHasInitiative: true
            );

            Assert.AreEqual(4, result.IterationsCompleted);
            Assert.AreEqual(expectedCraft, GetFighterOutcome(result, target).SquadronSizeAfter);
            Assert.IsTrue(GetFighterOutcome(result, target).Withdrew);
        }

        [TestCase(12, 4, 11)]
        [TestCase(1, 7.5, 1)]
        public void Resolve_FractionalCraftLoss_TruncatesCountAndKeepsLastSurvivor(
            int craft,
            double damage,
            int expected
        )
        {
            CapitalShip attacker = CreateShip("attacker", 100, 10);
            Starfighter target = CreateFighter("target", craft, 0);
            target.ShieldStrength = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveWeaponDamagePerStrength = damage / 10;

            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                Array.Empty<Starfighter>(),
                Array.Empty<CapitalShip>(),
                new[] { target }
            );

            Assert.AreEqual(expected, GetFighterOutcome(result, target).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_HeavyLineShipAgainstThreeLaserEscorts_DefeatsEscorts()
        {
            CapitalShip[] escorts =
            {
                CreateLaserEscort("escort-1"),
                CreateLaserEscort("escort-2"),
                CreateLaserEscort("escort-3"),
            };
            CapitalShip lineShip = CreateHeavyLineShip("line-ship");

            SpaceCombatResult result = Resolve(escorts, new[] { lineShip });

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.Greater(GetShipOutcome(result, lineShip).HullAfter, 0);
        }

        [Test]
        public void Resolve_DamagedCapitalShip_TakesLongerToDefeatEquivalentTarget()
        {
            CapitalShip undamaged = CreateShip("undamaged", hull: 100, weaponStrength: 10);
            CapitalShip damaged = CreateShip("damaged", hull: 100, weaponStrength: 10);
            damaged.CurrentHullStrength = 50;
            CapitalShip firstTarget = CreatePassiveTarget("first-target", hull: 1000);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", hull: 1000);

            SpaceCombatResult first = Resolve(new[] { undamaged }, new[] { firstTarget });
            SpaceCombatResult second = Resolve(new[] { damaged }, new[] { secondTarget });

            Assert.Less(first.IterationsCompleted, second.IterationsCompleted);
        }

        [Test]
        public void Resolve_PositiveFractionalAttack_InflictsMinimumDamage()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            attacker.CurrentHullStrength = 1;
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            SpaceCombatResult result = ResolveSingleStep(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>()
            );

            Assert.AreEqual(99, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_ShieldsRemain_ProtectsCapitalShipHull()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 100;
            defender.ShieldRechargeRate = 1;

            SpaceCombatResult result = Resolve(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { defender },
                Array.Empty<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(100)
            );

            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_ShieldRechargeWithoutShieldCapacity_DoesNotProtectHull()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 0;
            defender.ShieldRechargeRate = 100;

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { attacker },
                Array.Empty<Starfighter>(),
                new[] { defender },
                Array.Empty<Starfighter>()
            );

            Assert.Less(GetShipOutcome(result, defender).HullAfter, 100);
        }

        [Test]
        public void Resolve_ShieldsDepleteWithoutEscape_ContinuesUntilHullIsDestroyed()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);
            defender.MaxShieldStrength = 500;

            SpaceCombatResult result = Resolve(new[] { attacker }, new[] { defender });

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
        }

        [TestCase(false, 1)]
        [TestCase(true, 4096)]
        public void Resolve_LegacyIterationLimit_ContinuesBeyond4096Iterations(
            bool reverseSides,
            int legacyLimit
        )
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            attacker.MaxShieldStrength = 100;
            attacker.ShieldRechargeRate = 100;
            CapitalShip defender = CreateShip("defender", hull: 6000, weaponStrength: 1);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 8000;
            XmlDocument document = new XmlDocument();
            document.LoadXml(SerializationHelper.Serialize(config));
            XmlElement limit = document.CreateElement("AutoResolveMaximumIterations");
            limit.InnerText = legacyLimit.ToString();
            document.DocumentElement.AppendChild(limit);
            config = SerializationHelper.Deserialize<GameConfig.SpaceCombatConfig>(
                document.OuterXml
            );

            SpaceCombatResult result = Resolve(
                config,
                new[] { reverseSides ? defender : attacker },
                new List<Starfighter>(),
                new[] { reverseSides ? attacker : defender },
                new List<Starfighter>(),
                random: new FixedCombatRNG(100)
            );

            Assert.Greater(result.IterationsCompleted, 4096);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(100, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(
                reverseSides ? CombatSide.Defender : CombatSide.Attacker,
                result.Winner
            );
        }

        [Test]
        public void Resolve_ShieldRechargeEqualToIncomingFire_SuppressesStrengthForWithdrawal()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 20);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 20);
            foreach (CapitalShip ship in new[] { attacker, defender })
            {
                ship.MaxShieldStrength = 100;
                ship.ShieldRechargeRate = 10;
                ship.WeaponRecharge = 10;
            }
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 5;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(100)
            );

            Assert.AreEqual(1, result.IterationsCompleted);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.AreEqual(100, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_UnarmedForcesWithEscape_WithdrawOnTheirFirstUpdate()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 100);
            CapitalShip defender = CreatePassiveTarget("defender", hull: 100);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 7;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(1, result.IterationsCompleted);
            Assert.AreEqual(CombatSide.Draw, result.Winner);
            Assert.AreEqual(100, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(100, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_IntactForceBelowOpposingStrengthThreshold_FiresBeforeWithdrawing()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            attacker.ShieldRechargeRate = 1000;
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 32);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.34;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(0)
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(968, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_ForceAtOpposingStrengthThreshold_FiresBeforeStalemateResolution()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 33);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(0)
            );

            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 1000);
        }

        [Test]
        public void Resolve_ZeroStrengthForce_WithdrawsBeforeFiring()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            CapitalShip defender = CreatePassiveTarget("defender", hull: 1000);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(1000, GetShipOutcome(result, defender).HullAfter);
        }

        [Test]
        public void Resolve_PositiveStrengthForceAgainstZeroStrengthOpponent_DoesNotWithdraw()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 1);
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 1.01;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(0)
            );

            Assert.IsFalse(GetShipOutcome(result, defender).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
        }

        [Test]
        public void Resolve_MixedOpposingTargets_UsesCapitalTargetStrengthForWithdrawal()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 40);
            Starfighter attackerFighter = CreateFighter(
                "attacker-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            CapitalShip defender = CreateShip("defender", hull: 1000, weaponStrength: 0);
            defender.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 60;
            defender.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            defender.WeaponRecharge = 60;
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new[] { attackerFighter },
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(0)
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(6, GetFighterOutcome(result, attackerFighter).SquadronSizeAfter);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void Resolve_ForceReachesOpposingStrengthThreshold_CompletesAccordingToWithdrawalAvailability(
            bool canWithdraw,
            bool expectsWithdrawal
        )
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: canWithdraw,
                random: new FixedCombatRNG(0)
            );

            Assert.AreEqual(
                expectsWithdrawal
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Destroyed,
                result.DefenderOutcome
            );
        }

        [Test]
        public void Resolve_FighterForceFallsBelowOpposingStrengthThreshold_WithdrawsForce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 10000, weaponStrength: 10);
            Starfighter defender = CreateFighter("defender", squadronSize: 12, weaponStrength: 1);
            defender.ShieldStrength = 0;
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.LaserCannonCapitalDamageMultiplier = 1;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new List<CapitalShip>(),
                new[] { defender },
                attackerCanWithdraw: false,
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.That(GetFighterOutcome(result, defender).SquadronSizeAfter, Is.InRange(1, 11));
        }

        [Test]
        public void Resolve_TorpedoOnlyFighterWithoutPrimaryWeapons_WithdrawsBelowThreshold()
        {
            Starfighter attacker = CreateFighter("attacker", squadronSize: 1, weaponStrength: 0);
            attacker.SublightSpeed = 10;
            attacker.Torpedoes = 60;
            attacker.TorpedoRange = 100;
            CapitalShip defender = CreateShip("defender", hull: 40, weaponStrength: 1);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { attacker },
                new[] { defender },
                new List<Starfighter>(),
                attackerCanWithdraw: true,
                defenderCanWithdraw: false
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_ThreeCapitalShipsWithdrawWithSlowNonHyperdriveShip_DoesNotDelayWithdrawal()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            attacker.ShieldRechargeRate = 1000;
            CapitalShip firstWithdrawingShip = CreateShip(
                "first-withdrawing-ship",
                hull: 1000,
                weaponStrength: 1
            );
            firstWithdrawingShip.SublightSpeed = 10;
            CapitalShip secondWithdrawingShip = CreateShip(
                "second-withdrawing-ship",
                hull: 1000,
                weaponStrength: 1
            );
            secondWithdrawingShip.SublightSpeed = 10;
            CapitalShip strandedShip = CreateShip("stranded-ship", hull: 100, weaponStrength: 1);
            strandedShip.Hyperdrive = 0;
            strandedShip.SublightSpeed = 1;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstWithdrawingShip, secondWithdrawingShip, strandedShip },
                    new List<Starfighter>(),
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, firstWithdrawingShip).Withdrew);
            Assert.IsTrue(GetShipOutcome(result, secondWithdrawingShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, strandedShip).Withdrew);
            Assert.AreEqual(0, GetShipOutcome(result, strandedShip).HullAfter);
            Assert.AreEqual(997, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_FleetWithdrawalInterruptedByVictory_DoesNotWithdrawPartialFleet()
        {
            CapitalShip attacker = CreatePassiveTarget("attacker", hull: 1);
            CapitalShip fastFleetShip = CreateShip("fast-fleet-ship", hull: 100, weaponStrength: 0);
            fastFleetShip.SublightSpeed = 20;
            fastFleetShip.StarfighterCapacity = 1;
            CapitalShip slowFleetShip = CreateShip("slow-fleet-ship", hull: 100, weaponStrength: 0);
            slowFleetShip.SublightSpeed = 10;
            Starfighter carriedFighter = CreateFighter(
                "carried-fighter",
                squadronSize: 1,
                weaponStrength: 0
            );
            carriedFighter.SublightSpeed = 30;
            CapitalShip coveringShip = CreateShip("covering-ship", hull: 100, weaponStrength: 1);
            Fleet fleet = new Fleet();
            fleet.AddChild(fastFleetShip);
            fleet.AddChild(slowFleetShip);
            fastFleetShip.AddChild(carriedFighter);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { fastFleetShip, slowFleetShip, coveringShip },
                    new[] { carriedFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.IsFalse(GetShipOutcome(result, fastFleetShip).Withdrew);
            Assert.IsFalse(GetShipOutcome(result, slowFleetShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, carriedFighter).Withdrew);
        }

        [Test]
        public void Resolve_ForceWithdrawsAfterItsFiringPhase_LeavesBeforeTheNextIteration()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 5);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 10;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true,
                random: new FixedCombatRNG(0),
                attackerHasInitiative: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(95, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(99, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_ZeroSublightSpeed_RetainsCommandMobilityForWithdrawal()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 5);
            CapitalShip fastDefender = CreatePassiveTarget("fast-defender", hull: 100);
            fastDefender.SublightSpeed = 20;
            CapitalShip slowDefender = CreatePassiveTarget("slow-defender", hull: 100);
            slowDefender.SublightSpeed = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 1;
            config.AutoResolveRetreatStrengthRatio = 1.01;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { fastDefender, slowDefender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.IsTrue(GetShipOutcome(result, fastDefender).Withdrew);
            Assert.IsTrue(GetShipOutcome(result, slowDefender).Withdrew);
            Assert.AreEqual(100, GetShipOutcome(result, slowDefender).HullAfter);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_NoHyperdrive_StrandsUnitWithoutWithdrawingOpposingForce()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            attacker.ShieldRechargeRate = 100;
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 1);
            defender.SublightSpeed = 0;
            defender.Hyperdrive = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 2;
            config.AutoResolveRetreatStrengthRatio = 0.33;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.IsFalse(GetShipOutcome(result, attacker).Withdrew);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
        }

        [Test]
        public void Resolve_CarriedNonHyperdriveFighterWithdraws_PreservesFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 1);
            CapitalShip carrier = CreateShip("carrier", hull: 100, weaponStrength: 1);
            carrier.StarfighterCapacity = 1;
            carrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(carrier);
            carrier.SetParent(fleet);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config)
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, carrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierInOtherFleetHasRecoveryCapacity_PreservesNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            destroyedCarrier.CurrentHullStrength = 0;
            CapitalShip otherFleetCarrier = CreateShip(
                "other-fleet-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            otherFleetCarrier.StarfighterCapacity = 1;
            otherFleetCarrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet strandedFleet = new Fleet();
            Fleet recoveryFleet = new Fleet();
            strandedFleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(strandedFleet);
            destroyedCarrier.AddChild(fighter);
            fighter.SetParent(destroyedCarrier);
            recoveryFleet.AddChild(otherFleetCarrier);
            otherFleetCarrier.SetParent(recoveryFleet);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, otherFleetCarrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, otherFleetCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierDestroyedWithoutRecoveryCapacity_DestroysNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip carrier = CreateShip("carrier", hull: 1, weaponStrength: 1);
            carrier.StarfighterCapacity = 1;
            carrier.SublightSpeed = 10;
            carrier.CurrentHullStrength = 0;
            CapitalShip escapeShip = CreateShip("escape-ship", hull: 1000, weaponStrength: 1);
            escapeShip.StarfighterCapacity = 0;
            escapeShip.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(carrier);
            carrier.SetParent(fleet);
            fleet.AddChild(escapeShip);
            escapeShip.SetParent(fleet);
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { carrier, escapeShip },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.IsTrue(GetShipOutcome(result, escapeShip).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_FightersWithDestroyedStrategicParents_FireBeforeRecoveryFails()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 100);
            attacker.WeaponRecharge = 100;
            CapitalShip firstCarrier = CreatePassiveTarget("first-carrier", hull: 1);
            firstCarrier.StarfighterCapacity = 1;
            firstCarrier.SublightSpeed = 1;
            CapitalShip secondCarrier = CreatePassiveTarget("second-carrier", hull: 1);
            secondCarrier.StarfighterCapacity = 1;
            secondCarrier.SublightSpeed = 1;
            firstCarrier.CurrentHullStrength = 0;
            secondCarrier.CurrentHullStrength = 0;
            Starfighter firstFighter = CreateFighter(
                "first-fighter",
                squadronSize: 12,
                weaponStrength: 10
            );
            firstFighter.Hyperdrive = 0;
            firstFighter.SublightSpeed = 10;
            Starfighter secondFighter = CreateFighter(
                "second-fighter",
                squadronSize: 12,
                weaponStrength: 10
            );
            secondFighter.Hyperdrive = 0;
            secondFighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(firstCarrier);
            firstCarrier.SetParent(fleet);
            fleet.AddChild(secondCarrier);
            secondCarrier.SetParent(fleet);
            firstCarrier.AddChild(firstFighter);
            firstFighter.SetParent(firstCarrier);
            secondCarrier.AddChild(secondFighter);
            secondFighter.SetParent(secondCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { firstCarrier, secondCarrier },
                    new[] { firstFighter, secondFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, firstCarrier).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, secondCarrier).HullAfter);
            Assert.IsFalse(GetFighterOutcome(result, firstFighter).Withdrew);
            Assert.IsFalse(GetFighterOutcome(result, secondFighter).Withdrew);
            Assert.AreEqual(0, GetFighterOutcome(result, firstFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetFighterOutcome(result, secondFighter).SquadronSizeAfter);
            Assert.Less(GetShipOutcome(result, attacker).HullAfter, 1000);
        }

        [Test]
        public void Resolve_RecoveryCarrierBayOccupiedByNonparticipatingFighter_UsesTacticalCapacity()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            destroyedCarrier.CurrentHullStrength = 0;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter strandedFighter = CreateFighter(
                "stranded-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            strandedFighter.Hyperdrive = 0;
            strandedFighter.ShieldStrength = 100;
            strandedFighter.SublightSpeed = 10;
            Starfighter inTransitFighter = CreateFighter(
                "in-transit-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            inTransitFighter.Movement = TestMovement.Create();
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(strandedFighter);
            strandedFighter.SetParent(destroyedCarrier);
            recoveryCarrier.AddChild(inTransitFighter);
            inTransitFighter.SetParent(recoveryCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { strandedFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, strandedFighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, strandedFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_CarrierDestroyedWithSpareRecoveryCapacity_WithdrawsNonHyperdriveFighter()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            destroyedCarrier.CurrentHullStrength = 0;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 0);
            fighter.Hyperdrive = 0;
            fighter.ShieldStrength = 100;
            fighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(fighter);
            fighter.SetParent(destroyedCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { fighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, fighter).Withdrew);
            Assert.AreEqual(12, GetFighterOutcome(result, fighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_HyperdriveFighterOccupiesRecoveryCarrier_WithdrawsBothFighters()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 1000, weaponStrength: 10);
            CapitalShip destroyedCarrier = CreateShip(
                "destroyed-carrier",
                hull: 1,
                weaponStrength: 1
            );
            destroyedCarrier.StarfighterCapacity = 1;
            destroyedCarrier.SublightSpeed = 10;
            destroyedCarrier.CurrentHullStrength = 0;
            CapitalShip recoveryCarrier = CreateShip(
                "recovery-carrier",
                hull: 1000,
                weaponStrength: 1
            );
            recoveryCarrier.StarfighterCapacity = 1;
            recoveryCarrier.SublightSpeed = 10;
            Starfighter hyperdriveFighter = CreateFighter(
                "hyperdrive-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            hyperdriveFighter.Hyperdrive = 1;
            hyperdriveFighter.ShieldStrength = 100;
            hyperdriveFighter.SublightSpeed = 10;
            Starfighter nonHyperdriveFighter = CreateFighter(
                "non-hyperdrive-fighter",
                squadronSize: 12,
                weaponStrength: 0
            );
            nonHyperdriveFighter.Hyperdrive = 0;
            nonHyperdriveFighter.ShieldStrength = 100;
            nonHyperdriveFighter.SublightSpeed = 10;
            Fleet fleet = new Fleet();
            fleet.AddChild(destroyedCarrier);
            destroyedCarrier.SetParent(fleet);
            fleet.AddChild(recoveryCarrier);
            recoveryCarrier.SetParent(fleet);
            destroyedCarrier.AddChild(nonHyperdriveFighter);
            nonHyperdriveFighter.SetParent(destroyedCarrier);
            recoveryCarrier.AddChild(hyperdriveFighter);
            hyperdriveFighter.SetParent(recoveryCarrier);
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 1.01;
            SpaceCombatResult result = CreateResolver(config, new FixedCombatRNG(0))
                .Resolve(
                    new[] { attacker },
                    new List<Starfighter>(),
                    new[] { destroyedCarrier, recoveryCarrier },
                    new[] { hyperdriveFighter, nonHyperdriveFighter },
                    attackerCanWithdraw: false,
                    defenderCanWithdraw: true
                );

            Assert.AreEqual(0, GetShipOutcome(result, destroyedCarrier).HullAfter);
            Assert.IsTrue(GetShipOutcome(result, recoveryCarrier).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, hyperdriveFighter).Withdrew);
            Assert.IsTrue(GetFighterOutcome(result, nonHyperdriveFighter).Withdrew);
        }

        [Test]
        public void Resolve_UnarmedForcesWithoutWithdrawal_DestroysBothForces()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 0);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 0);

            SpaceCombatResult result = Resolve(new[] { attacker }, new[] { defender });

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(1200, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_DamagedShipsCannotRechargeWeaponsOrWithdraw_DestroysBothSides()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            attacker.CurrentHullStrength = 10;
            attacker.Hyperdrive = 0;
            attacker.WeaponRecharge = 0;
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 10);
            defender.CurrentHullStrength = 10;
            defender.Hyperdrive = 0;
            defender.WeaponRecharge = 0;
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 2;

            SpaceCombatResult result = Resolve(
                config,
                new[] { attacker },
                new List<Starfighter>(),
                new[] { defender },
                new List<Starfighter>()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(0, GetShipOutcome(result, attacker).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, defender).HullAfter);
            Assert.AreEqual(2, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_PlanetDestroyer_FiresImmediatelyAndUsesConfiguredRechargeTiming()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.PlanetDestroyerSuperlaserRechargeIterations = 3;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            CapitalShip[] targets =
            {
                CreatePassiveTarget("first", 100000),
                CreatePassiveTarget("second", 100000),
                CreatePassiveTarget("third", 100000),
            };

            SpaceCombatResult result = Resolve(
                config,
                new[] { planetDestroyer },
                new List<Starfighter>(),
                targets,
                new List<Starfighter>()
            );

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.IsTrue(targets.All(target => GetShipOutcome(result, target).HullAfter == 0));
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == planetDestroyer));
            Assert.AreEqual(9, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_MultiplePlanetDestroyers_TargetDistinctShipsInStableOrder()
        {
            CapitalShip firstPlanetDestroyer = CreatePlanetDestroyer("first-planet-destroyer");
            CapitalShip secondPlanetDestroyer = CreatePlanetDestroyer("second-planet-destroyer");
            CapitalShip firstTarget = CreatePassiveTarget("first-target", 100000);
            CapitalShip secondTarget = CreatePassiveTarget("second-target", 100000);

            SpaceCombatResult result = Resolve(
                new[] { firstPlanetDestroyer, secondPlanetDestroyer },
                new[] { firstTarget, secondTarget }
            );

            Assert.AreEqual(0, GetShipOutcome(result, firstTarget).HullAfter);
            Assert.AreEqual(0, GetShipOutcome(result, secondTarget).HullAfter);
            Assert.AreEqual(1, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_SingleSquadronAttackRun_SucceedsAfterConfiguredDurationWithoutFinalLoss()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.PlanetDestroyerAttackRunIterations = 3;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 1 })
            );

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(
                0,
                result.ShipDamage.Single(damage => damage.Ship == planetDestroyer).HullAfter
            );
            Assert.IsFalse(result.FighterLosses.Any());
            Assert.AreEqual(3, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_AttackRunSelectionDamageBeforeSuccess_DoesNotPersistIntoTheRun()
        {
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = Resolve(
                CreateConfig(),
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 10, 1, 1 })
            );

            Assert.AreEqual(12, GetFighterOutcome(result, torpedoFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetShipOutcome(result, planetDestroyer).HullAfter);
        }

        [Test]
        public void Resolve_DamagedAttackRunSquadron_UsesConfiguredCapacityForSelection()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.PlanetDestroyerAttackRunIterations = 1;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);
            torpedoFighter.CurrentSquadronSize = 1;
            int[] selectionRolls = Enumerable
                .Range(0, config.AttackRunSelectionMaximumEvaluations)
                .SelectMany(_ => new[] { 1, 10 })
                .ToArray();

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(selectionRolls)
            );

            Assert.AreEqual(0, GetShipOutcome(result, planetDestroyer).HullAfter);
            Assert.AreEqual(1, GetFighterOutcome(result, torpedoFighter).SquadronSizeAfter);
        }

        [Test]
        public void Resolve_AttackRunSelectionMaximumEvaluations_ConfiguresRetryLimit()
        {
            GameConfig.SpaceCombatConfig singleEvaluationConfig = CreateConfig();
            singleEvaluationConfig.PlanetDestroyerAttackRunIterations = 1;
            singleEvaluationConfig.AttackRunSelectionMaximumEvaluations = 1;
            GameConfig.SpaceCombatConfig retryConfig = singleEvaluationConfig.CreateCopy();
            retryConfig.AttackRunSelectionMaximumEvaluations = 2;
            CapitalShip firstPlanetDestroyer = CreatePlanetDestroyer("first-planet-destroyer");
            CapitalShip secondPlanetDestroyer = CreatePlanetDestroyer("second-planet-destroyer");
            Starfighter firstFighter = CreateTorpedoFighter("first-fighter", 12);
            Starfighter secondFighter = CreateTorpedoFighter("second-fighter", 12);

            SpaceCombatResult singleEvaluationResult = Resolve(
                singleEvaluationConfig,
                new List<CapitalShip>(),
                new[] { firstFighter },
                new[] { firstPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 100, 1, 1 })
            );
            SpaceCombatResult retryResult = Resolve(
                retryConfig,
                new List<CapitalShip>(),
                new[] { secondFighter },
                new[] { secondPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 100, 1, 1 })
            );

            Assert.AreEqual(
                0,
                GetFighterOutcome(singleEvaluationResult, firstFighter).SquadronSizeAfter
            );
            Assert.AreEqual(0, GetShipOutcome(retryResult, secondPlanetDestroyer).HullAfter);
        }

        [Test]
        public void Resolve_AttackRunSelectionThresholdMultiplier_ConfiguresSelectionChance()
        {
            GameConfig.SpaceCombatConfig lowThresholdConfig = CreateConfig();
            lowThresholdConfig.PlanetDestroyerAttackRunIterations = 1;
            lowThresholdConfig.AttackRunSelectionMaximumEvaluations = 1;
            lowThresholdConfig.AttackRunSelectionThresholdMultiplier = 0.01;
            GameConfig.SpaceCombatConfig highThresholdConfig = lowThresholdConfig.CreateCopy();
            highThresholdConfig.AttackRunSelectionThresholdMultiplier = 1;
            CapitalShip firstPlanetDestroyer = CreatePlanetDestroyer("first-planet-destroyer");
            CapitalShip secondPlanetDestroyer = CreatePlanetDestroyer("second-planet-destroyer");
            Starfighter firstFighter = CreateTorpedoFighter("first-fighter", 12);
            Starfighter secondFighter = CreateTorpedoFighter("second-fighter", 12);

            SpaceCombatResult lowThresholdResult = Resolve(
                lowThresholdConfig,
                new List<CapitalShip>(),
                new[] { firstFighter },
                new[] { firstPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 2 })
            );
            SpaceCombatResult highThresholdResult = Resolve(
                highThresholdConfig,
                new List<CapitalShip>(),
                new[] { secondFighter },
                new[] { secondPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 2 })
            );

            Assert.AreEqual(
                0,
                GetFighterOutcome(lowThresholdResult, firstFighter).SquadronSizeAfter
            );
            Assert.AreEqual(
                0,
                GetShipOutcome(highThresholdResult, secondPlanetDestroyer).HullAfter
            );
        }

        [Test]
        public void Resolve_AttackRunTorpedoBudgetMultiplier_ConfiguresSelectionChance()
        {
            GameConfig.SpaceCombatConfig lowBudgetConfig = CreateConfig();
            lowBudgetConfig.PlanetDestroyerAttackRunIterations = 1;
            lowBudgetConfig.AttackRunSelectionMaximumEvaluations = 1;
            lowBudgetConfig.AttackRunSelectionThresholdMultiplier = 1;
            lowBudgetConfig.AttackRunTorpedoBudgetMultiplier = 0.01;
            GameConfig.SpaceCombatConfig highBudgetConfig = lowBudgetConfig.CreateCopy();
            highBudgetConfig.AttackRunTorpedoBudgetMultiplier = 1;
            CapitalShip firstPlanetDestroyer = CreatePlanetDestroyer("first-planet-destroyer");
            CapitalShip secondPlanetDestroyer = CreatePlanetDestroyer("second-planet-destroyer");
            Starfighter firstFighter = CreateTorpedoFighter("first-fighter", 12);
            Starfighter secondFighter = CreateTorpedoFighter("second-fighter", 12);

            SpaceCombatResult lowBudgetResult = Resolve(
                lowBudgetConfig,
                new List<CapitalShip>(),
                new[] { firstFighter },
                new[] { firstPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 2 })
            );
            SpaceCombatResult highBudgetResult = Resolve(
                highBudgetConfig,
                new List<CapitalShip>(),
                new[] { secondFighter },
                new[] { secondPlanetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 2 })
            );

            Assert.AreEqual(0, GetFighterOutcome(lowBudgetResult, firstFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetShipOutcome(highBudgetResult, secondPlanetDestroyer).HullAfter);
        }

        [Test]
        public void Resolve_FailedAttackRun_DestroysEveryParticipatingSquadron()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter first = CreateTorpedoFighter("first", 1);
            Starfighter second = CreateTorpedoFighter("second", 1);
            first.Torpedoes = 1;
            second.Torpedoes = 1;

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { first, second },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 10, 10 })
            );

            Assert.AreEqual(2, result.FighterLosses.Count);
            Assert.IsTrue(result.FighterLosses.All(loss => loss.SquadsAfter == 0));
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == planetDestroyer));
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(120, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_SuccessfulAttackRunWithMultipleSquadrons_DestroysStableFirstHalf()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter first = CreateTorpedoFighter("first", 12);
            Starfighter second = CreateTorpedoFighter("second", 12);
            Starfighter third = CreateTorpedoFighter("third", 12);
            Starfighter fourth = CreateTorpedoFighter("fourth", 12);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { first, second, third, fourth },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 1 })
            );

            Assert.AreEqual(0, GetFighterOutcome(result, first).SquadronSizeAfter);
            Assert.AreEqual(0, GetFighterOutcome(result, second).SquadronSizeAfter);
            Assert.AreEqual(12, GetFighterOutcome(result, third).SquadronSizeAfter);
            Assert.AreEqual(12, GetFighterOutcome(result, fourth).SquadronSizeAfter);
            Assert.AreEqual(0, GetShipOutcome(result, planetDestroyer).HullAfter);
        }

        [Test]
        public void Resolve_AttackRunLongerThanStagnationWindow_CompletesBeforeStalemateResolution()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 20;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 1 })
            );

            Assert.AreEqual(
                0,
                result.ShipDamage.Single(damage => damage.Ship == planetDestroyer).HullAfter
            );
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(120, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_ShieldedPlanetDestroyer_IsNotAttackedAndItsEnemyLeaves()
        {
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = CreateResolver()
                .Resolve(
                    new List<CapitalShip>(),
                    new[] { torpedoFighter },
                    new[] { planetDestroyer },
                    new List<Starfighter>(),
                    attackerCanWithdraw: true,
                    defenderCanWithdraw: false,
                    shieldedPlanetDestroyers: new[] { planetDestroyer }
                );

            Assert.IsFalse(result.FighterLosses.Any());
            Assert.IsFalse(result.ShipDamage.Any());
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.Less(result.IterationsCompleted, 30);
        }

        [Test]
        public void Resolve_PlanetDestroyerAndFightersUnableToDamageEachOther_RemainInAnIndecisiveBattle()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 2;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 10);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { fighter },
                new[] { planetDestroyer },
                new List<Starfighter>()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Draw, result.Winner);
            Assert.IsFalse(result.FighterLosses.Any());
            Assert.IsFalse(result.ShipDamage.Any());
        }

        [Test]
        public void Resolve_PlanetDestroyerAndFightersUnableToDamageEachOther_WithdrawsEligiblePlanetDestroyer()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 2;
            config.PlanetDestroyerWithdrawalIterations = 3;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            Starfighter fighter = CreateFighter("fighter", squadronSize: 12, weaponStrength: 10);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { fighter },
                new[] { planetDestroyer },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.IsTrue(result.WithdrawnUnits.Contains(planetDestroyer));
            Assert.AreEqual(4, result.IterationsCompleted);
        }

        [Test]
        public void Resolve_OutgunnedSideWithMobilePlanetDestroyer_UsesPlanetDestroyerWithdrawalCountdown()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveRetreatStrengthRatio = 0.33;
            config.AutoResolveStagnationIterations = 2;
            config.PlanetDestroyerWithdrawalIterations = 3;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            CapitalShip escort = CreateShip("escort", hull: 10, weaponStrength: 1);
            escort.SublightSpeed = 10;
            CapitalShip raider = CreateShip("raider", hull: 100000, weaponStrength: 500);

            SpaceCombatResult result = Resolve(
                config,
                new[] { raider },
                new List<Starfighter>(),
                new[] { planetDestroyer, escort },
                new List<Starfighter>(),
                defenderCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.DefenderOutcome);
            Assert.IsTrue(GetShipOutcome(result, planetDestroyer).Withdrew);
            Assert.AreEqual(
                4,
                result.IterationsCompleted,
                "The planet destroyer withdrawal countdown must remain tactical progress."
            );
        }

        [Test]
        public void Resolve_SquadronsOnAttackRun_AreNotTargetedUntilRunCompletes()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            CapitalShip picket = CreateShip("picket", hull: 1000, weaponStrength: 0);
            picket.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 500;
            picket.PrimaryWeapons[PrimaryWeaponType.LaserCannon][4] = 100;
            picket.WeaponRecharge = 500;
            Starfighter torpedoFighter = CreateTorpedoFighter("y-wing", 12);

            SpaceCombatResult result = Resolve(
                config,
                new List<CapitalShip>(),
                new[] { torpedoFighter },
                new[] { planetDestroyer, picket },
                new List<Starfighter>(),
                random: new SequenceRNG(new[] { 1, 1 })
            );

            Assert.AreEqual(0, GetFighterOutcome(result, torpedoFighter).SquadronSizeAfter);
            Assert.AreEqual(0, GetShipOutcome(result, planetDestroyer).HullAfter);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
            Assert.AreEqual(
                config.PlanetDestroyerAttackRunIterations + 1,
                result.IterationsCompleted
            );
        }

        [Test]
        public void Resolve_FleetFleeingAlonePlanetDestroyer_LeavesBeforeSuperlaserTargetsIt()
        {
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            CapitalShip first = CreatePassiveTarget("first", 100000);
            first.SublightSpeed = 4;
            CapitalShip second = CreatePassiveTarget("second", 100000);
            second.SublightSpeed = 4;
            CapitalShip[] fleet = { first, second };

            SpaceCombatResult result = Resolve(
                CreateConfig(),
                fleet,
                new List<Starfighter>(),
                new[] { planetDestroyer },
                new List<Starfighter>(),
                attackerCanWithdraw: true
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(0, fleet.Count(ship => GetShipOutcome(result, ship).HullAfter == 0));
            Assert.AreEqual(2, fleet.Count(ship => GetShipOutcome(result, ship).Withdrew));
            Assert.IsFalse(result.ShipDamage.Any(damage => damage.Ship == planetDestroyer));
        }

        [Test]
        public void Resolve_PlanetDestroyerWithOnlyCapitalTargets_DestroysTargetsBeforeStalemate()
        {
            GameConfig.SpaceCombatConfig config = CreateConfig();
            config.AutoResolveStagnationIterations = 20;
            CapitalShip planetDestroyer = CreatePlanetDestroyer("planet-destroyer");
            CapitalShip target = CreatePassiveTarget("target", 100000);

            SpaceCombatResult result = Resolve(
                config,
                new[] { planetDestroyer },
                new List<Starfighter>(),
                new[] { target, CreatePassiveTarget("other", 100000) },
                new List<Starfighter>()
            );

            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.IsFalse(result.WithdrawnUnits.Contains(planetDestroyer));
            Assert.AreEqual(302, result.IterationsCompleted);
        }

        /// <summary>
        /// Creates a planet-destroying capital ship, which fights as a planet destroyer.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreatePlanetDestroyer(string instanceId)
        {
            CapitalShip planetDestroyer = CreateShip(instanceId, hull: 25000, weaponStrength: 1400);
            planetDestroyer.TypeID = "planet-destroyer";
            planetDestroyer.CanDestroyPlanets = true;
            return planetDestroyer;
        }

        /// <summary>
        /// Creates a fighter squadron that carries torpedoes and flies at a typical speed.
        /// </summary>
        /// <param name="instanceId">The instance identifier.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <returns>The created squadron.</returns>
        private static Starfighter CreateTorpedoFighter(string instanceId, int squadronSize)
        {
            Starfighter fighter = CreateFighter(instanceId, squadronSize, 0);
            fighter.SublightSpeed = 8;
            fighter.Torpedoes = 4;
            fighter.TorpedoRange = 7;
            return fighter;
        }

        /// <summary>
        /// Resolves symmetric battle.
        /// </summary>
        /// <returns>The resolved symmetric battle.</returns>
        private static SpaceCombatResult ResolveSymmetricBattle()
        {
            CapitalShip attacker = CreateShip("attacker", hull: 100, weaponStrength: 10);
            CapitalShip defender = CreateShip("defender", hull: 100, weaponStrength: 10);
            return Resolve(new[] { attacker }, new[] { defender });
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<CapitalShip> defenderShips,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false
        )
        {
            return Resolve(
                attackerShips,
                new List<Starfighter>(),
                defenderShips,
                new List<Starfighter>(),
                attackerCanWithdraw,
                defenderCanWithdraw
            );
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="attackerFighters">The attacker fighters.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="defenderFighters">The defender fighters.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false
        )
        {
            return Resolve(
                CreateConfig(),
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerCanWithdraw,
                defenderCanWithdraw
            );
        }

        /// <summary>
        /// Resolves the requested operation.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="attackerShips">The attacker ships.</param>
        /// <param name="attackerFighters">The attacker fighters.</param>
        /// <param name="defenderShips">The defender ships.</param>
        /// <param name="defenderFighters">The defender fighters.</param>
        /// <param name="attackerCanWithdraw">Whether attacker can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether defender can withdraw.</param>
        /// <param name="random">The random.</param>
        /// <param name="attackerCommand">The attacker's tactical command modifiers.</param>
        /// <param name="defenderCommand">The defender's tactical command modifiers.</param>
        /// <param name="attackerHasInitiative">Whether the attacker acts first.</param>
        /// <param name="starfighterTypes">Optional fighter designs defining fixed formation slots.</param>
        /// <returns>The resolved value.</returns>
        private static SpaceCombatResult Resolve(
            GameConfig.SpaceCombatConfig config,
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw = false,
            bool defenderCanWithdraw = false,
            IRandomNumberProvider random = null,
            SpaceCombatCommandModifiers attackerCommand = default,
            SpaceCombatCommandModifiers defenderCommand = default,
            bool attackerHasInitiative = false,
            IReadOnlyList<Starfighter> starfighterTypes = null
        )
        {
            return CreateResolver(config, random, starfighterTypes)
                .Resolve(
                    attackerShips,
                    attackerFighters,
                    defenderShips,
                    defenderFighters,
                    attackerCanWithdraw,
                    defenderCanWithdraw,
                    attackerCommand,
                    defenderCommand,
                    attackerHasInitiative: attackerHasInitiative
                );
        }

        /// <summary>
        /// Creates config.
        /// </summary>
        /// <returns>The created config.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AdmiralLeadershipDivisor = 10,
                CommanderCombatDivisor = 20,
                LaserCannonCapitalDamageMultiplier = 1.0 / 6.0,
                TurbolaserFighterDamageMultiplier = 1,
                AutoResolveWeaponDamagePerStrength = 1,
                AutoResolveCapitalWeaponRechargeMultiplier = 1,
                AutoResolveTorpedoDamagePerStrength = 1,
                AutoResolveFighterWeaponRechargeMultiplier = 3.751,
                AutoResolveFighterShieldRechargePerCraft = 0.025,
                AutoResolveFighterHullPerCraft = 8,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0,
                AutoResolveMinimumManeuverRatio = 0.1,
                AttackRunSelectionMaximumEvaluations = 1001,
                AttackRunSelectionThresholdMultiplier = 0.5,
                AttackRunTorpedoBudgetMultiplier = 0.53333336,
                PlanetDestroyerSuperlaserRechargeIterations = 300,
                PlanetDestroyerAttackRunIterations = 120,
                PlanetDestroyerWithdrawalIterations = 150,
            };
        }

        /// <summary>
        /// Creates resolver.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="random">The random.</param>
        /// <param name="starfighterTypes">Optional fighter designs defining fixed formation slots.</param>
        /// <returns>The created resolver.</returns>
        private static SpaceCombatAutoResolver CreateResolver(
            GameConfig.SpaceCombatConfig config = null,
            IRandomNumberProvider random = null,
            IReadOnlyList<Starfighter> starfighterTypes = null
        )
        {
            return new SpaceCombatAutoResolver(
                config ?? CreateConfig(),
                random ?? new SystemRandomProvider(1894809716),
                starfighterTypes
            );
        }

        /// <summary>
        /// Creates ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="hull">The hull.</param>
        /// <param name="weaponStrength">The weapon strength.</param>
        /// <param name="armEveryArc">Whether arm every arc.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(
            string instanceId,
            int hull,
            int weaponStrength,
            bool armEveryArc = false
        )
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = Math.Max(weaponStrength, 0),
            };
            int armedArcCount = armEveryArc ? 4 : 1;
            for (int arc = 0; arc < armedArcCount; arc++)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][arc] = weaponStrength;
            if (weaponStrength > 0)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 100;
            return ship;
        }

        /// <summary>
        /// Creates laser escort.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created laser escort.</returns>
        private static CapitalShip CreateLaserEscort(string instanceId)
        {
            CapitalShip ship = CreateShip(instanceId, hull: 500, weaponStrength: 0);
            ship.MaxShieldStrength = 200;
            ship.ShieldRechargeRate = 10;
            ship.SublightSpeed = 6;
            ship.Maneuverability = 3;
            ship.WeaponRecharge = 8;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 120, 90, 120, 120, 17 };
            return ship;
        }

        /// <summary>
        /// Creates heavy line ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created heavy line ship.</returns>
        private static CapitalShip CreateHeavyLineShip(string instanceId)
        {
            CapitalShip ship = CreateShip(instanceId, hull: 2750, weaponStrength: 0);
            ship.MaxShieldStrength = 300;
            ship.ShieldRechargeRate = 15;
            ship.SublightSpeed = 4;
            ship.Maneuverability = 1;
            ship.WeaponRecharge = 20;
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100, 40, 60, 60, 50 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 100, 40, 40, 40, 35 };
            return ship;
        }

        /// <summary>
        /// Creates fighter.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="squadronSize">The squadron size.</param>
        /// <param name="weaponStrength">The weapon strength.</param>
        /// <returns>The created fighter.</returns>
        private static Starfighter CreateFighter(
            string instanceId,
            int squadronSize,
            int weaponStrength
        )
        {
            return new Starfighter
            {
                InstanceID = instanceId,
                MaxSquadronSize = squadronSize,
                CurrentSquadronSize = squadronSize,
                ShieldStrength = 1,
                Hyperdrive = 1,
                LaserCannon = weaponStrength,
                LaserRange = weaponStrength > 0 ? 100 : 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates passive target.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="hull">The hull.</param>
        /// <returns>The created passive target.</returns>
        private static CapitalShip CreatePassiveTarget(string instanceId, int hull)
        {
            return CreateShip(instanceId, hull, weaponStrength: 0);
        }

        /// <summary>
        /// Creates a single firing phase against otherwise identical targets in stable order.
        /// </summary>
        /// <param name="configureCargo">Populates the later target before tactical state is captured.</param>
        /// <returns>The battle result and its earlier and later targets.</returns>
        private static (
            SpaceCombatResult Result,
            CapitalShip First,
            CapitalShip Second
        ) ResolveCargoPriorityBattle(Action<CapitalShip> configureCargo)
        {
            CapitalShip first = CreatePassiveTarget("target-1", 100);
            CapitalShip second = CreatePassiveTarget("target-2", 100);
            foreach (CapitalShip ship in new[] { first, second })
            {
                ship.OwnerInstanceID = "target-owner";
                ship.RegimentCapacity = 1;
            }
            configureCargo(second);

            SpaceCombatResult result = ResolveSingleStep(
                CreateConfig(),
                new[] { CreateShip("attacker", 100, 10) },
                Array.Empty<Starfighter>(),
                new[] { first, second },
                Array.Empty<Starfighter>()
            );
            return (result, first, second);
        }

        /// <summary>
        /// Creates one usable cargo unit with the synthetic target's owner.
        /// </summary>
        /// <param name="kind">The officer, regiment, or special-forces cargo category.</param>
        /// <returns>The enabled and stationary cargo unit.</returns>
        private static ISceneNode CreatePriorityCargo(string kind)
        {
            return kind switch
            {
                "officer" => new Officer { InstanceID = "cargo", OwnerInstanceID = "target-owner" },
                "regiment" => new Regiment
                {
                    InstanceID = "cargo",
                    OwnerInstanceID = "target-owner",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                "special-forces" => new SpecialForces
                {
                    InstanceID = "cargo",
                    OwnerInstanceID = "target-owner",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        /// <summary>
        /// Gets ship outcome.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="ship">The ship.</param>
        /// <returns>The requested ship outcome.</returns>
        private static (int HullAfter, bool Withdrew) GetShipOutcome(
            SpaceCombatResult result,
            CapitalShip ship
        )
        {
            ShipDamageResult damage = result.ShipDamage.SingleOrDefault(outcome =>
                outcome.Ship == ship
            );
            return (
                damage?.HullAfter ?? ship.CurrentHullStrength,
                result.WithdrawnUnits.Contains(ship)
            );
        }

        /// <summary>
        /// Gets fighter outcome.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="fighter">The fighter.</param>
        /// <returns>The requested fighter outcome.</returns>
        private static (int SquadronSizeAfter, bool Withdrew) GetFighterOutcome(
            SpaceCombatResult result,
            Starfighter fighter
        )
        {
            FighterLossResult loss = result.FighterLosses.SingleOrDefault(outcome =>
                outcome.Fighter == fighter
            );
            return (
                loss?.SquadsAfter ?? fighter.CurrentSquadronSize,
                result.WithdrawnUnits.Contains(fighter)
            );
        }

        /// <summary>
        /// Returns committed hull values in stable damage-record order.
        /// </summary>
        /// <param name="result">The resolved combat result.</param>
        /// <returns>The committed post-combat hull values.</returns>
        private static IEnumerable<int> GetResolvedShipHulls(SpaceCombatResult result)
        {
            return result.ShipDamage.Select(damage => damage.HullAfter);
        }

        /// <summary>
        /// Resolves one firing step against passive targets, preserving survivors at the stall.
        /// </summary>
        /// <param name="config">The combat rules.</param>
        /// <param name="ships">The firing ships.</param>
        /// <param name="fighters">The firing squadrons.</param>
        /// <param name="targetShips">The passive ships.</param>
        /// <param name="targetFighters">The passive squadrons.</param>
        /// <param name="attackerCommand">The firing force's command ratings.</param>
        /// <param name="defenderCommand">The receiving force's command ratings.</param>
        /// <param name="random">Optional rolls that isolate the behavior under test.</param>
        /// <param name="starfighterTypes">Optional fighter designs defining fixed formation slots.</param>
        /// <returns>The resolved step.</returns>
        private static SpaceCombatResult ResolveSingleStep(
            GameConfig.SpaceCombatConfig config,
            IReadOnlyList<CapitalShip> ships,
            IReadOnlyList<Starfighter> fighters,
            IReadOnlyList<CapitalShip> targetShips,
            IReadOnlyList<Starfighter> targetFighters,
            SpaceCombatCommandModifiers attackerCommand = default,
            SpaceCombatCommandModifiers defenderCommand = default,
            IRandomNumberProvider random = null,
            IReadOnlyList<Starfighter> starfighterTypes = null
        )
        {
            config.AutoResolveStagnationIterations = 1;
            foreach (CapitalShip target in targetShips)
            {
                target.TypeID = "single-step-ship-" + target.InstanceID;
                config.HyperdriveSystemCounts[target.TypeID] = 2;
            }
            foreach (Starfighter target in targetFighters)
            {
                target.TypeID = "single-step-fighter-" + target.InstanceID;
                config.HyperdriveSystemCounts[target.TypeID] = 2;
            }
            return Resolve(
                config,
                ships,
                fighters,
                targetShips,
                targetFighters,
                defenderCanWithdraw: true,
                random: random ?? new FixedCombatRNG(100),
                attackerCommand: attackerCommand,
                defenderCommand: defenderCommand,
                attackerHasInitiative: true,
                starfighterTypes: starfighterTypes
            );
        }

        private sealed class FixedCombatRNG : IRandomNumberProvider
        {
            private readonly int _roll;

            /// <summary>
            /// Uses one reproducible combat roll to isolate a damage rule.
            /// </summary>
            /// <param name="roll">The roll to return within the requested range.</param>
            internal FixedCombatRNG(int roll)
            {
                _roll = roll;
            }

            /// <summary>
            /// Supplies a reproducible fractional roll.
            /// </summary>
            /// <returns>A zero fractional roll.</returns>
            public double NextDouble()
            {
                return 0;
            }

            /// <summary>
            /// Supplies the fixed roll within the requested bounds.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The clamped fixed roll.</returns>
            public int NextInt(int min, int max)
            {
                return Math.Clamp(_roll, min, max - 1);
            }
        }
    }
}
