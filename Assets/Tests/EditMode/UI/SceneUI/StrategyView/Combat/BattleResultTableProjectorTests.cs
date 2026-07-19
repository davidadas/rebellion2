using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleResultTableProjectorTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNEMP1";

        [Test]
        public void Project_CapitalShipDamage_SeparatesSurvivorsAndDestroyedShips()
        {
            UIContext context = CreateContext();
            CapitalShip intact = CreateCapitalShip("intact", _playerFactionId, "Intact Ship");
            CapitalShip damaged = CreateCapitalShip("damaged", _playerFactionId, "Damaged Ship");
            CapitalShip destroyed = CreateCapitalShip(
                "destroyed",
                _playerFactionId,
                "Destroyed Ship"
            );
            GameFleet fleet = CreateFleet(_playerFactionId, intact, damaged);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = fleet,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                ShipDamage =
                {
                    new ShipDamageResult
                    {
                        Ship = damaged,
                        HullBefore = 100,
                        HullAfter = 50,
                    },
                    new ShipDamageResult
                    {
                        Ship = destroyed,
                        HullBefore = 100,
                        HullAfter = 0,
                    },
                },
            };
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                context,
                result,
                _playerFactionId,
                BattleResultCategory.CapitalShips
            );

            CollectionAssert.AreEqual(
                new[] { "Intact Ship", "Damaged Ship" },
                table.Operational.Select(item => item.Text)
            );
            CollectionAssert.AreEqual(
                new[] { "Destroyed Ship" },
                table.Destroyed.Select(item => item.Text)
            );
            Assert.IsNotNull(table.Operational[0].BaseTexture);
            Assert.IsNotNull(table.Operational[0].WithdrawingOverlayTexture);
            Assert.IsNull(table.Operational[0].DamagedOverlayTexture);
            Assert.IsNotNull(table.Operational[1].WithdrawingOverlayTexture);
            Assert.IsNotNull(table.Operational[1].DamagedOverlayTexture);
            Assert.IsNotNull(table.Destroyed[0].DamagedOverlayTexture);
        }

        [Test]
        public void Project_DuplicateCapitalShipDamage_KeepsOneOperationalRow()
        {
            UIContext context = CreateContext();
            CapitalShip ship = CreateCapitalShip("ship", _playerFactionId, "Ship");
            GameFleet fleet = CreateFleet(_playerFactionId, ship);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = fleet,
                AttackerOwnerInstanceID = _playerFactionId,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                ShipDamage =
                {
                    new ShipDamageResult
                    {
                        Ship = ship,
                        HullBefore = 100,
                        HullAfter = 75,
                    },
                },
            };
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                context,
                result,
                _playerFactionId,
                BattleResultCategory.CapitalShips
            );

            Assert.AreEqual(1, table.Operational.Count);
            Assert.AreEqual("Ship", table.Operational[0].Text);
            Assert.AreEqual("No Casualties", table.Destroyed[0].Text);
        }

        [Test]
        public void Project_StarfighterLosses_SeparatesSurvivingAndDestroyedSquadrons()
        {
            UIContext context = CreateContext();
            Starfighter damaged = CreateStarfighter(
                "damaged",
                _playerFactionId,
                "Damaged Squadron"
            );
            Starfighter destroyed = CreateStarfighter(
                "destroyed",
                _playerFactionId,
                "Destroyed Squadron"
            );
            CapitalShip carrier = CreateCapitalShip("carrier", _playerFactionId, "Carrier");
            carrier.Starfighters.Add(damaged);
            GameFleet fleet = CreateFleet(_playerFactionId, carrier);
            SpaceCombatResult result = new SpaceCombatResult
            {
                DefenderFleet = fleet,
                DefenderOwnerInstanceID = _playerFactionId,
                DefenderOutcome = SpaceCombatSideOutcome.Withdrawn,
                FighterLosses =
                {
                    new FighterLossResult
                    {
                        Fighter = damaged,
                        SquadsBefore = 12,
                        SquadsAfter = 6,
                    },
                    new FighterLossResult
                    {
                        Fighter = destroyed,
                        SquadsBefore = 12,
                        SquadsAfter = 0,
                    },
                },
            };
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                context,
                result,
                _playerFactionId,
                BattleResultCategory.Starfighters
            );

            Assert.AreEqual(1, table.Operational.Count);
            Assert.AreEqual("Damaged Squadron", table.Operational[0].Text);
            Assert.IsNotNull(table.Operational[0].BaseTexture);
            Assert.IsNotNull(table.Operational[0].WithdrawingOverlayTexture);
            Assert.IsNotNull(table.Operational[0].DamagedOverlayTexture);
            Assert.AreEqual(1, table.Destroyed.Count);
            Assert.AreEqual("Destroyed Squadron", table.Destroyed[0].Text);
            Assert.IsNotNull(table.Destroyed[0].DamagedOverlayTexture);
        }

        [Test]
        public void Project_Troops_ReturnsFleetRegimentsInCarrierOrder()
        {
            UIContext context = CreateContext();
            Regiment first = CreateRegiment("first", _playerFactionId, "First Regiment");
            Regiment second = CreateRegiment("second", _playerFactionId, "Second Regiment");
            CapitalShip carrier = CreateCapitalShip("carrier", _playerFactionId, "Carrier");
            carrier.Regiments.Add(first);
            carrier.Regiments.Add(second);
            GameFleet fleet = CreateFleet(_playerFactionId, carrier);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = fleet,
                AttackerOwnerInstanceID = _playerFactionId,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
            };
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                context,
                result,
                _playerFactionId,
                BattleResultCategory.Troops
            );

            CollectionAssert.AreEqual(
                new[] { "First Regiment", "Second Regiment" },
                table.Operational.Select(item => item.Text)
            );
            Assert.IsTrue(table.Operational.All(item => item.BaseTexture != null));
            Assert.AreEqual("No Casualties", table.Destroyed[0].Text);
        }

        [Test]
        public void Project_Personnel_CombinesOfficersThenSpecialForces()
        {
            UIContext context = CreateContext();
            Officer officer = CreateOfficer("officer", _playerFactionId, "Officer");
            SpecialForces specialForces = CreateSpecialForces(
                "special-forces",
                _playerFactionId,
                "Special Forces"
            );
            CapitalShip carrier = CreateCapitalShip("carrier", _playerFactionId, "Carrier");
            carrier.Officers.Add(officer);
            carrier.SpecialForces.Add(specialForces);
            GameFleet fleet = CreateFleet(_playerFactionId, carrier);
            SpaceCombatResult result = new SpaceCombatResult
            {
                DefenderFleet = fleet,
                DefenderOwnerInstanceID = _playerFactionId,
                DefenderOutcome = SpaceCombatSideOutcome.Active,
            };
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                context,
                result,
                _playerFactionId,
                BattleResultCategory.Personnel
            );

            CollectionAssert.AreEqual(
                new[] { "Officer", "Special Forces" },
                table.Operational.Select(item => item.Text)
            );
            Assert.AreEqual("No Casualties", table.Destroyed[0].Text);
        }

        [Test]
        public void Project_UnknownOwner_ReturnsBothEmptyStateRows()
        {
            BattleResultTableProjector projector = new BattleResultTableProjector();

            BattleResultTableRenderData table = projector.Project(
                CreateContext(),
                new SpaceCombatResult(),
                "unknown",
                BattleResultCategory.CapitalShips
            );

            Assert.AreEqual(1, table.Operational.Count);
            Assert.AreEqual("None", table.Operational[0].Text);
            Assert.AreEqual(1, table.Destroyed.Count);
            Assert.AreEqual("No Casualties", table.Destroyed[0].Text);
        }

        private static UIContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" }
            );
            game.Factions.Add(
                new Faction { InstanceID = _opponentFactionId, DisplayName = "Opponent" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            return new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
        }

        private static GameFleet CreateFleet(string ownerId, params CapitalShip[] ships)
        {
            return new GameFleet(ownerId, "Fleet", ships.ToList()) { InstanceID = "fleet" };
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
            };
        }

        private static Regiment CreateRegiment(
            string instanceId,
            string ownerId,
            string displayName
        )
        {
            Regiment definition = ResourceManager
                .GetEntityData<Regiment>()
                .First(item => item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true);
            return new Regiment
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
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

        private static SpecialForces CreateSpecialForces(
            string instanceId,
            string ownerId,
            string displayName
        )
        {
            SpecialForces definition = ResourceManager
                .GetEntityData<SpecialForces>()
                .First(item =>
                    item.OwnerInstanceID == ownerId
                    || item.AllowedOwnerInstanceIDs?.Contains(ownerId) == true
                );
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                DisplayName = displayName,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
            };
        }
    }
}
