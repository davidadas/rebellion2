using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public sealed class StrategyUnitCardStatsOverlayBuilderTests
    {
        [Test]
        public void Build_Officer_ProjectsRatingsCapabilitiesResearchAndRanks()
        {
            Officer officer = new Officer
            {
                IsMain = true,
                IsJediTrainer = true,
                ShipResearch = 10,
                TroopResearch = 20,
                FacilityResearch = 30,
                AllowedRanks = new[] { OfficerRank.Admiral },
                Ratings = new Dictionary<SkillRating, int>
                {
                    [SkillRating.Diplomacy] = 31,
                    [SkillRating.Espionage] = 10,
                    [SkillRating.Combat] = 20,
                    [SkillRating.Leadership] = 40,
                },
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                officer
            );

            Assert.AreEqual(61, overlay.Width);
            Assert.AreEqual(25, overlay.Height);
            AssertPixel(overlay, 2, 1, StrategyUnitCardStatsOverlayBuilder.Cyan);
            AssertPixel(overlay, 3, 1, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 0, 3, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 1, 5, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 3, 7, StrategyUnitCardStatsOverlayBuilder.LightGreen);
            AssertPixel(overlay, 1, 11, StrategyUnitCardStatsOverlayBuilder.White);
            AssertPixel(overlay, 0, 18, StrategyUnitCardStatsOverlayBuilder.Blue);
            AssertPixel(overlay, 1, 20, StrategyUnitCardStatsOverlayBuilder.Orange);
            AssertPixel(overlay, 2, 22, StrategyUnitCardStatsOverlayBuilder.ResearchYellow);
            AssertPixel(overlay, 52, 1, StrategyUnitCardStatsOverlayBuilder.JediGreen);
            AssertPixel(overlay, 56, 1, StrategyUnitCardStatsOverlayBuilder.Gold);
            AssertPixel(overlay, 56, 10, StrategyUnitCardStatsOverlayBuilder.Gray);
        }

        [Test]
        public void Build_Officer_SeatOfPowerAppendsFiftyPercentDarkGreenBonus()
        {
            GameRoot game = new GameRoot();
            Planet coruscant = new Planet { InstanceID = "CORUSCANT", OwnerInstanceID = "FNEMP1" };
            Officer emperor = new Officer
            {
                InstanceID = "EMPEROR_PALPATINE",
                OwnerInstanceID = "FNEMP1",
            };
            coruscant.AddChild(emperor);
            emperor.SetParent(coruscant);
            game.NodesByInstanceID[coruscant.InstanceID] = coruscant;
            game.NodesByInstanceID[emperor.InstanceID] = emperor;

            Officer officer = new Officer
            {
                OwnerInstanceID = "FNEMP1",
                Ratings = new Dictionary<SkillRating, int> { [SkillRating.Leadership] = 100 },
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                officer,
                game
            );

            AssertPixel(overlay, 9, 7, StrategyUnitCardStatsOverlayBuilder.LightGreen);
            AssertPixel(overlay, 10, 7, StrategyUnitCardStatsOverlayBuilder.DarkGreen);
            AssertPixel(overlay, 14, 7, StrategyUnitCardStatsOverlayBuilder.DarkGreen);
            AssertPixel(overlay, 15, 7, StrategyUnitCardStatsOverlayBuilder.Black);
        }

        [Test]
        public void Build_Officer_EmperorInCoruscantFleetDoesNotApplySeatOfPowerBonus()
        {
            GameRoot game = new GameRoot();
            Planet coruscant = new Planet { InstanceID = "CORUSCANT", OwnerInstanceID = "FNEMP1" };
            Rebellion.Game.Units.Fleet fleet = new Rebellion.Game.Units.Fleet
            {
                InstanceID = "FLEET",
                OwnerInstanceID = "FNEMP1",
            };
            CapitalShip ship = new CapitalShip { InstanceID = "SHIP", OwnerInstanceID = "FNEMP1" };
            Officer emperor = new Officer
            {
                InstanceID = "EMPEROR_PALPATINE",
                OwnerInstanceID = "FNEMP1",
            };
            coruscant.AddChild(fleet);
            fleet.SetParent(coruscant);
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            ship.AddChild(emperor);
            emperor.SetParent(ship);
            game.NodesByInstanceID[coruscant.InstanceID] = coruscant;
            game.NodesByInstanceID[emperor.InstanceID] = emperor;

            Officer officer = new Officer
            {
                OwnerInstanceID = "FNEMP1",
                Ratings = new Dictionary<SkillRating, int> { [SkillRating.Leadership] = 100 },
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                officer,
                game
            );

            AssertPixel(overlay, 9, 7, StrategyUnitCardStatsOverlayBuilder.LightGreen);
            AssertPixel(overlay, 10, 7, StrategyUnitCardStatsOverlayBuilder.Black);
        }

        [Test]
        public void Build_SpecialForces_ProjectsMissionCapabilityGlyphs()
        {
            SpecialForces specialForces = new SpecialForces
            {
                AllowedMissionTypeIDs = new List<string>
                {
                    MissionTypeIDs.Espionage,
                    MissionTypeIDs.Sabotage,
                    MissionTypeIDs.InciteUprising,
                    MissionTypeIDs.Abduction,
                    MissionTypeIDs.Assassination,
                    MissionTypeIDs.Reconnaissance,
                },
                Ratings = new Dictionary<SkillRating, int> { [SkillRating.Combat] = 50 },
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                specialForces
            );

            Assert.AreEqual(61, overlay.Width);
            AssertPixel(overlay, 1, 1, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 1, 7, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 1, 13, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 1, 19, StrategyUnitCardStatsOverlayBuilder.LightGreen);
            AssertPixel(overlay, 57, 1, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 57, 10, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 57, 19, StrategyUnitCardStatsOverlayBuilder.Gold);
        }

        [Test]
        public void Build_Regiment_ProjectsScaledCombatAndDefenseBars()
        {
            Regiment regiment = new Regiment
            {
                AttackRating = 31,
                DefenseRating = 10,
                DetectionRating = 25,
                UprisingDefense = 4,
                BombardmentDefense = 8,
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                regiment
            );

            Assert.AreEqual(61, overlay.Width);
            AssertPixel(overlay, 4, 1, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 30, 1, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 31, 1, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 0, 3, StrategyUnitCardStatsOverlayBuilder.Cyan);
            AssertPixel(overlay, 4, 8, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 3, 10, StrategyUnitCardStatsOverlayBuilder.LightGreen);
            AssertPixel(overlay, 7, 21, StrategyUnitCardStatsOverlayBuilder.Gray);
        }

        [Test]
        public void Build_Starfighter_ProjectsWeaponsDefensesSpeedAndHyperdrive()
        {
            Starfighter starfighter = new Starfighter
            {
                LaserCannon = 31,
                IonCannon = 10,
                Torpedoes = 20,
                ShieldStrength = 4,
                DetectionRating = 20,
                Bombardment = 8,
                SublightSpeed = 6,
                Hyperdrive = 1,
            };

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                starfighter
            );

            Assert.AreEqual(66, overlay.Width);
            AssertPixel(overlay, 4, 1, StrategyUnitCardStatsOverlayBuilder.Gold);
            AssertPixel(overlay, 30, 1, StrategyUnitCardStatsOverlayBuilder.Gold);
            AssertPixel(overlay, 31, 1, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 0, 3, StrategyUnitCardStatsOverlayBuilder.Cyan);
            AssertPixel(overlay, 0, 5, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 3, 10, StrategyUnitCardStatsOverlayBuilder.Blue);
            AssertPixel(overlay, 6, 15, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 7, 17, StrategyUnitCardStatsOverlayBuilder.Gray);
            AssertPixel(overlay, 5, 22, StrategyUnitCardStatsOverlayBuilder.White);
            AssertPixel(overlay, 62, 21, StrategyUnitCardStatsOverlayBuilder.White);
        }

        [Test]
        public void Build_CapitalShip_ProjectsWeaponsDurabilityDefensesAndCapabilities()
        {
            CapitalShip capitalShip = new CapitalShip
            {
                ConstructionCost = 100,
                MaxHullStrength = 5000,
                MaxShieldStrength = 401,
                DetectionRating = 27,
                Bombardment = 8,
                SublightSpeed = 6,
                HasGravityWell = true,
                StarfighterCapacity = 12,
                RegimentCapacity = 3,
            };
            capitalShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon][(int)PrimaryWeaponArc.Fore] =
                310;
            capitalShip.PrimaryWeapons[PrimaryWeaponType.IonCannon][(int)PrimaryWeaponArc.Fore] =
                100;
            capitalShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser][(int)PrimaryWeaponArc.Fore] =
                20;

            StrategyUnitCardStatsOverlayData overlay = StrategyUnitCardStatsOverlayBuilder.Build(
                capitalShip
            );

            Assert.AreEqual(66, overlay.Width);
            AssertPixel(overlay, 3, 1, StrategyUnitCardStatsOverlayBuilder.Gold);
            AssertPixel(overlay, 4, 1, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 0, 3, StrategyUnitCardStatsOverlayBuilder.Cyan);
            AssertPixel(overlay, 0, 5, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 0, 8, StrategyUnitCardStatsOverlayBuilder.HullBrown);
            AssertPixel(overlay, 1, 8, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 4, 8, StrategyUnitCardStatsOverlayBuilder.HullBrown);
            AssertPixel(overlay, 2, 10, StrategyUnitCardStatsOverlayBuilder.Blue);
            AssertPixel(overlay, 3, 13, StrategyUnitCardStatsOverlayBuilder.DarkGreen);
            AssertPixel(overlay, 4, 13, StrategyUnitCardStatsOverlayBuilder.Black);
            AssertPixel(overlay, 4, 15, StrategyUnitCardStatsOverlayBuilder.Purple);
            AssertPixel(overlay, 7, 17, StrategyUnitCardStatsOverlayBuilder.Gray);
            AssertPixel(overlay, 5, 22, StrategyUnitCardStatsOverlayBuilder.White);
            AssertPixel(overlay, 62, 1, StrategyUnitCardStatsOverlayBuilder.Red);
            AssertPixel(overlay, 59, 13, StrategyUnitCardStatsOverlayBuilder.Blue);
            AssertPixel(overlay, 62, 19, StrategyUnitCardStatsOverlayBuilder.Orange);
        }

        /// <summary>
        /// Asserts one source-space overlay pixel.
        /// </summary>
        /// <param name="overlay">The overlay under test.</param>
        /// <param name="x">The source-space horizontal coordinate.</param>
        /// <param name="y">The source-space vertical coordinate.</param>
        /// <param name="expected">The expected pixel color.</param>
        private static void AssertPixel(
            StrategyUnitCardStatsOverlayData overlay,
            int x,
            int y,
            Color32 expected
        )
        {
            Assert.AreEqual(expected, overlay.GetPixel(x, y), $"Unexpected pixel at ({x}, {y}).");
        }
    }
}
