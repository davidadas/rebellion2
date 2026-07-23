using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleAlertWindowProjectorTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNEMP1";

        [Test]
        public void Project_WithoutEncounter_ReturnsHiddenPresentationAtRequestedPosition()
        {
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Hidden,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                null,
                null,
                _playerFactionId,
                42,
                73,
                null
            );

            Assert.AreEqual(BattleAlertWindowMode.Hidden, data.Mode);
            Assert.AreEqual(42, data.X);
            Assert.AreEqual(73, data.Y);
            Assert.IsEmpty(data.ViewButtons);
            Assert.IsNull(data.Pending);
            Assert.IsNull(data.Result);
        }

        [Test]
        public void Project_PendingSummary_ReturnsBattleSummaryAndAvailablePlayerCommands()
        {
            var scene = CreateScene();
            PendingCombatResult pending = new PendingCombatResult
            {
                Planet = scene.Planet,
                AttackerFleet = scene.PlayerFleet,
                DefenderFleet = scene.OpponentFleet,
                AttackerCanRetreat = true,
                DefenderCanRetreat = false,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                10,
                20,
                scene.Context
            );

            Assert.AreEqual(BattleAlertWindowMode.Pending, data.Mode);
            Assert.AreEqual("Battle at Test World", data.Pending.Title);
            Assert.AreEqual("Battle Summary", data.Pending.Header);
            StringAssert.Contains("Alliance fleet", data.Pending.Summary);
            StringAssert.Contains("Imperial forces", data.Pending.Summary);
            Assert.IsEmpty(data.Pending.Rows);
            Assert.AreEqual(4, data.ViewButtons.Count);
            Assert.AreEqual(3, data.Pending.CommandButtons.Count);
            Assert.IsTrue(data.Pending.CommandButtons[0].Interactable);
            Assert.IsTrue(data.Pending.CommandButtons[1].Interactable);
            Assert.IsFalse(data.Pending.CommandButtons[2].Interactable);
            Assert.IsNotNull(data.BackgroundTexture);
            Assert.IsNotNull(data.FrameTexture);
        }

        [Test]
        public void Project_PendingSecondForces_ReturnsFleetHierarchyWithoutPlanetDuplicates()
        {
            var scene = CreateScene();
            PendingCombatResult pending = new PendingCombatResult
            {
                Planet = scene.Planet,
                AttackerFleet = scene.PlayerFleet,
                DefenderFleet = scene.OpponentFleet,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.SecondForces,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("Imperial Forces", data.Pending.Header);
            CollectionAssert.AreEqual(
                new[] { "Opponent Fleet", "Opponent Ship" },
                data.Pending.Rows.Select(row => row.Text)
            );
            Assert.IsTrue(data.Pending.Rows.All(row => row.IconTexture != null));
            Assert.IsTrue(data.ViewButtons[2].Interactable);
        }

        [Test]
        public void Project_PendingSecondForces_IncludesActivePlanetStarfighters()
        {
            var scene = CreateScene();
            scene.Planet.OwnerInstanceID = _opponentFactionId;
            Starfighter fighter = CreateStarfighter(
                "planet-fighter",
                _opponentFactionId,
                "Planetary Fighter"
            );
            scene.Game.AttachNode(fighter, scene.Planet);
            PendingCombatResult pending = new PendingCombatResult
            {
                Planet = scene.Planet,
                AttackerFleet = scene.PlayerFleet,
                DefenderFleet = scene.OpponentFleet,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData forces = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.SecondForces,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );
            BattleAlertWindowRenderData assets = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.SystemAssets,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            CollectionAssert.AreEqual(
                new[] { "Opponent Fleet", "Opponent Ship", "Planetary Fighter" },
                forces.Pending.Rows.Select(row => row.Text)
            );
            Assert.IsFalse(assets.Pending.Rows.Any(row => row.Text == "Planetary Fighter"));
        }

        [Test]
        public void Project_PendingSystemAssets_ExcludesFleetsAndIncludesPlanetUnits()
        {
            var scene = CreateScene();
            Building building = CreateBuilding("building", _playerFactionId, "Shipyard");
            scene.Game.AttachNode(building, scene.Planet);
            PendingCombatResult pending = new PendingCombatResult
            {
                Planet = scene.Planet,
                AttackerFleet = scene.PlayerFleet,
                DefenderFleet = scene.OpponentFleet,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.SystemAssets,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("System Assets", data.Pending.Header);
            CollectionAssert.AreEqual(
                new[] { "Shipyard" },
                data.Pending.Rows.Select(row => row.Text)
            );
            Assert.IsFalse(data.Pending.Rows.Any(row => row.Text.Contains("Fleet")));
        }

        [Test]
        public void Project_PendingEmptyPanel_ReturnsContextualEmptyState()
        {
            var scene = CreateScene(includeFleets: false);
            PendingCombatResult pending = new PendingCombatResult { Planet = scene.Planet };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData forces = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.FirstForces,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );
            BattleAlertWindowRenderData assets = projector.Project(
                BattleAlertWindowMode.Pending,
                BattleAlertPanel.SystemAssets,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                pending,
                null,
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("No units found.", forces.Pending.Rows[0].Text);
            Assert.AreEqual("No system assets found.", assets.Pending.Rows[0].Text);
        }

        [Test]
        public void Project_ResultSummary_PlayerFleetDestroyed_ReportsDestruction()
        {
            var scene = CreateScene();
            SpaceCombatResult result = CreateResult(
                scene.Planet,
                scene.PlayerFleet,
                scene.OpponentFleet,
                CombatSide.Defender,
                SpaceCombatSideOutcome.Destroyed,
                SpaceCombatSideOutcome.Active
            );
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual(BattleAlertWindowMode.Result, data.Mode);
            StringAssert.Contains("fleet was defeated", data.Result.Summary);
            StringAssert.Contains("fleet has been destroyed", data.Result.Summary);
            Assert.AreEqual(4, data.ViewButtons.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    new RectInt(418, 88, 41, 41),
                    new RectInt(418, 141, 41, 41),
                    new RectInt(418, 196, 41, 41),
                    new RectInt(418, 250, 41, 41),
                },
                data.ViewButtons.Select(button => button.Bounds)
            );
            Assert.IsNotNull(data.BackgroundTexture);
            Assert.IsNotNull(data.FrameTexture);
        }

        [Test]
        public void Project_ResultSummary_PlayerFleetWithdrawn_ReportsWithdrawal()
        {
            var scene = CreateScene();
            SpaceCombatResult result = CreateResult(
                scene.Planet,
                scene.PlayerFleet,
                scene.OpponentFleet,
                CombatSide.Defender,
                SpaceCombatSideOutcome.Withdrawn,
                SpaceCombatSideOutcome.Active
            );
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            StringAssert.Contains("fleet has withdrawn", data.Result.Summary);
            Assert.IsNotNull(data.BackgroundTexture);
        }

        [Test]
        public void Project_ResultSummary_DrawReportsNoVictor()
        {
            var scene = CreateScene();
            SpaceCombatResult result = CreateResult(
                scene.Planet,
                scene.PlayerFleet,
                scene.OpponentFleet,
                CombatSide.Draw,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Active
            );
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            StringAssert.Contains("indecisive", data.Result.Summary);
            StringAssert.Contains("no victor", data.Result.Summary);
        }

        [Test]
        public void Project_BombardmentResult_ReturnsSourceSummaryAndSixCategoryLayouts()
        {
            var scene = CreateScene();
            scene.Planet.OwnerInstanceID = _opponentFactionId;
            BombardmentResult result = new BombardmentResult
            {
                Planet = scene.Planet,
                AttackingFaction = scene.Game.GetFactionByOwnerInstanceID(_playerFactionId),
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData summary = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.CapitalShips,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );
            BattleAlertWindowRenderData details = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.FirstForces,
                BattleResultCategory.Manufacturing,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("Orbital bombardment of Test World", summary.Result.Title);
            Assert.AreEqual(
                "Alliance ships have conducted an orbital strike on the Imperial system of Test World.",
                summary.Result.Summary
            );
            Assert.IsNotNull(summary.BackgroundTexture);
            CollectionAssert.AreEqual(
                BattleResultCategoryCatalog.Ordered,
                details.Result.ResultCategories.Select(category => category.Category)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    (RectInt?)new RectInt(36, 59, 49, 41),
                    new RectInt(98, 59, 49, 41),
                    new RectInt(160, 59, 49, 41),
                    new RectInt(222, 59, 49, 41),
                    new RectInt(284, 59, 49, 41),
                    new RectInt(348, 59, 49, 41),
                },
                details.Result.ResultCategories.Select(category => category.Button.Bounds)
            );
            Assert.IsTrue(details.Result.UsesPlanetaryCategoryLayout);
        }

        [Test]
        public void Project_FailedPlanetaryAssault_ReturnsSourceSummaryAndAssaultArtwork()
        {
            var scene = CreateScene();
            scene.Planet.OwnerInstanceID = _opponentFactionId;
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = scene.Planet,
                AttackingFaction = scene.Game.GetFactionByOwnerInstanceID(_playerFactionId),
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Success = false,
            };
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Summary,
                BattleResultCategory.Troops,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("Assault on Test World", data.Result.Title);
            Assert.AreEqual(
                "Imperial Troops have defended Test World from an Alliance assault.",
                data.Result.Summary
            );
            Assert.IsNotNull(data.BackgroundTexture);
        }

        [Test]
        public void Project_ResultPersonnel_ReturnsPersonnelColumnsCategoriesAndTable()
        {
            var scene = CreateScene();
            Officer officer = CreateOfficer("officer", _playerFactionId, "Field Officer");
            scene.PlayerFleet.CapitalShips[0].Officers.Add(officer);
            SpaceCombatResult result = CreateResult(
                scene.Planet,
                scene.PlayerFleet,
                scene.OpponentFleet,
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.FirstForces,
                BattleResultCategory.Personnel,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            Assert.AreEqual("Alliance Forces", data.Result.ResultForceHeader);
            Assert.AreEqual("Personnel", data.Result.ResultTableTitle);
            CollectionAssert.AreEqual(
                new[] { "Survivors", "Captured", "Killed" },
                data.Result.ResultColumnHeaders
            );
            Assert.AreEqual(4, data.Result.ResultCategories.Count);
            Assert.IsFalse(data.Result.UsesPlanetaryCategoryLayout);
            Assert.IsTrue(data.Result.UsesPersonnelColumns);
            Assert.AreEqual("Field Officer", data.Result.ResultTable.Operational[0].Text);
            Assert.AreEqual("No Casualties", data.Result.ResultTable.Destroyed[0].Text);
        }

        [Test]
        public void Project_ResultDirect_ReturnsNavigationPromptAndButtonsWithoutTable()
        {
            var scene = CreateScene();
            SpaceCombatResult result = CreateResult(
                scene.Planet,
                scene.PlayerFleet,
                scene.OpponentFleet,
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );
            BattleAlertWindowProjector projector = new BattleAlertWindowProjector();

            BattleAlertWindowRenderData data = projector.Project(
                BattleAlertWindowMode.Result,
                BattleAlertPanel.Summary,
                BattleResultPanel.Direct,
                BattleResultCategory.CapitalShips,
                null,
                BattleResultPresentation.Create(result),
                _playerFactionId,
                0,
                0,
                scene.Context
            );

            StringAssert.Contains("go directly to", data.Result.Summary);
            Assert.AreEqual(2, data.Result.ResultDirectButtons.Count);
            Assert.IsTrue(data.Result.ResultDirectButtons.All(button => button.Interactable));
            Assert.IsNull(data.Result.ResultTable);
            Assert.IsEmpty(data.Result.ResultCategories);
        }

        [Test]
        public void GetBombardmentSummaryImagePath_CombatSnapshots_SelectLossArtwork()
        {
            BattleAlertWindowTheme theme = new BattleAlertWindowTheme
            {
                BombardmentAttackerLossesImagePath = "attacker-loss",
                BombardmentTargetLossesImagePath = "target-loss",
                BombardmentNoLossesImagePath = "no-loss",
            };
            BombardmentResult attackerLoss = new BombardmentResult();
            attackerLoss.AttackingUnits.Add(
                new CombatUnitSnapshot(new CapitalShip { InstanceID = "ship" }) { Damaged = true }
            );
            BombardmentResult targetLoss = new BombardmentResult();
            targetLoss.DefendingUnits.Add(
                new CombatUnitSnapshot(new Building { InstanceID = "building" })
                {
                    Destroyed = true,
                }
            );

            string attackerPath = BattleAlertWindowProjector.GetBombardmentSummaryImagePath(
                theme,
                attackerLoss
            );
            string targetPath = BattleAlertWindowProjector.GetBombardmentSummaryImagePath(
                theme,
                targetLoss
            );
            string noLossPath = BattleAlertWindowProjector.GetBombardmentSummaryImagePath(
                theme,
                new BombardmentResult()
            );

            Assert.AreEqual("attacker-loss", attackerPath);
            Assert.AreEqual("target-loss", targetPath);
            Assert.AreEqual("no-loss", noLossPath);
        }

        private static (
            GameRoot Game,
            UIContext Context,
            Planet Planet,
            GameFleet PlayerFleet,
            GameFleet OpponentFleet
        ) CreateScene(bool includeFleets = true)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Factions.Add(
                new Faction { InstanceID = _opponentFactionId, DisplayName = "Imperial" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Test World",
                IsColonized = true,
                OwnerInstanceID = _playerFactionId,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, system);

            GameFleet playerFleet = null;
            GameFleet opponentFleet = null;
            if (includeFleets)
            {
                playerFleet = CreateFleet(
                    "player-fleet",
                    _playerFactionId,
                    "Player Fleet",
                    "Player Ship"
                );
                opponentFleet = CreateFleet(
                    "opponent-fleet",
                    _opponentFactionId,
                    "Opponent Fleet",
                    "Opponent Ship"
                );
                game.AttachNode(playerFleet, planet);
                game.AttachNode(opponentFleet, planet);
            }

            UIContext context = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            return (game, context, planet, playerFleet, opponentFleet);
        }

        private static GameFleet CreateFleet(
            string instanceId,
            string ownerId,
            string displayName,
            string shipName
        )
        {
            CapitalShip ship = CreateCapitalShip(instanceId + "-ship", ownerId, shipName);
            return new GameFleet(ownerId, displayName, new[] { ship }.ToList())
            {
                InstanceID = instanceId,
            };
        }

        private static CapitalShip CreateCapitalShip(
            string instanceId,
            string ownerId,
            string displayName
        )
        {
            CapitalShip definition = ResourceManager
                .GetEntityData<CapitalShip>()
                .First(item => item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true);
            return new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = definition.TypeID,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
            };
        }

        private static Building CreateBuilding(
            string instanceId,
            string ownerId,
            string displayName
        )
        {
            Building definition = ResourceManager
                .GetEntityData<Building>()
                .First(item => item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true);
            return new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Officer CreateOfficer(string instanceId, string ownerId, string displayName)
        {
            Officer definition = ResourceManager
                .GetEntityData<Officer>()
                .First(item =>
                    item.OwnerInstanceID == ownerId
                    || item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true
                );
            return new Officer
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
            };
        }

        private static Starfighter CreateStarfighter(
            string instanceId,
            string ownerId,
            string displayName
        )
        {
            Starfighter definition = ResourceManager
                .GetEntityData<Starfighter>()
                .First(item => item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true);
            return new Starfighter
            {
                InstanceID = instanceId,
                TypeID = definition.TypeID,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = definition.MaxSquadronSize,
                CurrentSquadronSize = definition.MaxSquadronSize,
            };
        }

        private static SpaceCombatResult CreateResult(
            Planet planet,
            GameFleet playerFleet,
            GameFleet opponentFleet,
            CombatSide winner,
            SpaceCombatSideOutcome playerOutcome,
            SpaceCombatSideOutcome opponentOutcome
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                Planet = planet,
                AttackerFleet = playerFleet,
                DefenderFleet = opponentFleet,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Winner = winner,
                AttackerOutcome = playerOutcome,
                DefenderOutcome = opponentOutcome,
            };
            result.AttackingUnits.AddRange(
                CombatUnitSnapshot.CaptureFleetUnits(new[] { playerFleet })
            );
            result.DefendingUnits.AddRange(
                CombatUnitSnapshot.CaptureFleetUnits(new[] { opponentFleet })
            );
            return result;
        }
    }
}
