using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.ContextMenus
{
    [TestFixture]
    public class StrategyMenuCommandTests
    {
        /// <summary>
        /// Verifies constructor complete command copies children and derives icon column.
        /// </summary>
        [Test]
        public void Constructor_CompleteCommand_CopiesChildrenAndDerivesIconColumn()
        {
            StrategyMenuCommand child = new StrategyMenuCommand(
                StrategyMenuAction.Encyclopedia,
                "Child",
                true
            );
            List<StrategyMenuCommand> children = new List<StrategyMenuCommand> { child };

            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyMenuAction.Status,
                "Parent",
                true,
                StrategyContextMenuIconKeys.CheckMark,
                children
            );
            children.Clear();

            Assert.AreEqual(StrategyMenuAction.Status, command.Action);
            Assert.AreEqual("Parent", command.Text);
            Assert.IsTrue(command.Enabled);
            Assert.AreEqual(StrategyContextMenuIconKeys.CheckMark, command.IconKey);
            Assert.AreEqual(1, command.SubmenuCommands.Count);
            Assert.AreSame(child, command.SubmenuCommands[0]);
            Assert.AreSame(child, command.ChildCommands[0]);
            Assert.IsTrue(command.HasIcon);
            Assert.IsTrue(command.IsSubmenu);
            Assert.IsTrue(command.UsesIconColumn);
        }

        /// <summary>
        /// Verifies submenu constructor command without children remains submenu.
        /// </summary>
        [Test]
        public void SubmenuConstructor_CommandWithoutChildren_RemainsSubmenu()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                "Options",
                false,
                Array.Empty<StrategyMenuCommand>()
            );

            Assert.AreEqual(StrategyMenuAction.None, command.Action);
            Assert.IsFalse(command.Enabled);
            Assert.IsFalse(command.HasIcon);
            Assert.IsTrue(command.IsSubmenu);
            Assert.IsTrue(command.UsesIconColumn);
        }

        /// <summary>
        /// Verifies try get game speed speed action returns mapped speed.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="expected">The expected.</param>
        [TestCase(StrategyMenuAction.GameSpeedPause, TickSpeed.Paused)]
        [TestCase(StrategyMenuAction.GameSpeedVerySlow, TickSpeed.VerySlow)]
        [TestCase(StrategyMenuAction.GameSpeedSlow, TickSpeed.Slow)]
        [TestCase(StrategyMenuAction.GameSpeedMedium, TickSpeed.Medium)]
        [TestCase(StrategyMenuAction.GameSpeedFast, TickSpeed.Fast)]
        public void TryGetGameSpeed_SpeedAction_ReturnsMappedSpeed(
            StrategyMenuAction action,
            TickSpeed expected
        )
        {
            bool matched = action.TryGetGameSpeed(out TickSpeed speed);

            Assert.IsTrue(matched);
            Assert.AreEqual(expected, speed);
        }

        /// <summary>
        /// Verifies try get game speed non speed action returns false.
        /// </summary>
        [Test]
        public void TryGetGameSpeed_NonSpeedAction_ReturnsFalse()
        {
            bool matched = StrategyMenuAction.Status.TryGetGameSpeed(out TickSpeed speed);

            Assert.IsFalse(matched);
            Assert.AreEqual(default(TickSpeed), speed);
        }

        /// <summary>
        /// Verifies get speed icon key source speed returns mapped icon.
        /// </summary>
        /// <param name="sourceSpeed">The source speed.</param>
        /// <param name="expected">The expected.</param>
        [TestCase(-1, StrategyContextMenuIconKeys.PausedSpeed)]
        [TestCase(0, StrategyContextMenuIconKeys.PausedSpeed)]
        [TestCase(1, StrategyContextMenuIconKeys.VerySlowSpeed)]
        [TestCase(2, StrategyContextMenuIconKeys.SlowSpeed)]
        [TestCase(3, StrategyContextMenuIconKeys.MediumSpeed)]
        [TestCase(4, StrategyContextMenuIconKeys.FastSpeed)]
        [TestCase(5, StrategyContextMenuIconKeys.PausedSpeed)]
        public void GetSpeedIconKey_SourceSpeed_ReturnsMappedIcon(int sourceSpeed, int expected)
        {
            int iconKey = StrategyContextMenuIconKeys.GetSpeedIconKey(sourceSpeed);

            Assert.AreEqual(expected, iconKey);
        }

        /// <summary>
        /// Verifies try get speed speed icon returns mapped source speed.
        /// </summary>
        /// <param name="iconKey">The icon key.</param>
        /// <param name="expected">The expected.</param>
        [TestCase(StrategyContextMenuIconKeys.PausedSpeed, 0)]
        [TestCase(StrategyContextMenuIconKeys.VerySlowSpeed, 1)]
        [TestCase(StrategyContextMenuIconKeys.SlowSpeed, 2)]
        [TestCase(StrategyContextMenuIconKeys.MediumSpeed, 3)]
        [TestCase(StrategyContextMenuIconKeys.FastSpeed, 4)]
        public void TryGetSpeed_SpeedIcon_ReturnsMappedSourceSpeed(int iconKey, int expected)
        {
            bool matched = StrategyContextMenuIconKeys.TryGetSpeed(iconKey, out int sourceSpeed);

            Assert.IsTrue(matched);
            Assert.AreEqual(expected, sourceSpeed);
        }

        /// <summary>
        /// Verifies try get speed non speed icon returns false.
        /// </summary>
        [Test]
        public void TryGetSpeed_NonSpeedIcon_ReturnsFalse()
        {
            bool matched = StrategyContextMenuIconKeys.TryGetSpeed(
                StrategyContextMenuIconKeys.CheckMark,
                out int sourceSpeed
            );

            Assert.IsFalse(matched);
            Assert.AreEqual(-1, sourceSpeed);
        }

        /// <summary>
        /// Verifies can move items owned idle officer returns true.
        /// </summary>
        [Test]
        public void CanMoveItems_OwnedIdleOfficer_ReturnsTrue()
        {
            Officer officer = CreateOfficer("player");

            bool canMove = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { officer },
                "player"
            );

            Assert.IsTrue(canMove);
        }

        /// <summary>
        /// Verifies can move items moving or enemy item returns false.
        /// </summary>
        [Test]
        public void CanMoveItems_MovingOrEnemyItem_ReturnsFalse()
        {
            Officer movingOfficer = CreateOfficer("player");
            movingOfficer.Movement = new MovementState();
            Officer enemyOfficer = CreateOfficer("enemy");

            bool movingCanMove = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { movingOfficer },
                "player"
            );
            bool enemyCanMove = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { enemyOfficer },
                "player"
            );

            Assert.IsFalse(movingCanMove);
            Assert.IsFalse(enemyCanMove);
        }

        /// <summary>
        /// Verifies can move items item carried by moving fleet returns false.
        /// </summary>
        [Test]
        public void CanMoveItems_ItemCarriedByMovingFleet_ReturnsFalse()
        {
            Officer officer = CreateOfficer("player");
            CapitalShip ship = new CapitalShip();
            GameFleet fleet = new GameFleet { Movement = new MovementState() };
            ship.SetParent(fleet);
            officer.SetParent(ship);

            bool canMove = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { officer },
                "player"
            );

            Assert.IsFalse(canMove);
        }

        /// <summary>
        /// Verifies can move items captured officer requires player escort.
        /// </summary>
        [Test]
        public void CanMoveItems_CapturedOfficer_RequiresPlayerEscort()
        {
            Officer captive = CreateOfficer("enemy");
            captive.IsCaptured = true;
            captive.CaptorInstanceID = "player";
            Officer escort = CreateOfficer("player");

            bool withoutEscort = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { captive },
                "player"
            );
            bool withEscort = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { captive, escort },
                "player"
            );

            Assert.IsFalse(withoutEscort);
            Assert.IsTrue(withEscort);
        }

        /// <summary>
        /// Verifies can move items capital ship under construction returns true.
        /// </summary>
        [Test]
        public void CanMoveItems_CapitalShipUnderConstruction_ReturnsTrue()
        {
            CapitalShip capitalShip = new CapitalShip
            {
                OwnerInstanceID = "player",
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            bool canMove = StrategyContextMenuAvailability.CanMoveItems(
                new ISceneNode[] { capitalShip },
                "player"
            );

            Assert.IsTrue(canMove);
        }

        /// <summary>
        /// Verifies can move items null or empty selection returns false.
        /// </summary>
        [Test]
        public void CanMoveItems_NullOrEmptySelection_ReturnsFalse()
        {
            bool nullResult = StrategyContextMenuAvailability.CanMoveItems(null, "player");
            bool emptyResult = StrategyContextMenuAvailability.CanMoveItems(
                Array.Empty<ISceneNode>(),
                "player"
            );

            Assert.IsFalse(nullResult);
            Assert.IsFalse(emptyResult);
        }

        /// <summary>
        /// Verifies can create mission owned officer and special forces returns true.
        /// </summary>
        [Test]
        public void CanCreateMission_OwnedOfficerAndSpecialForces_ReturnsTrue()
        {
            Officer officer = CreateOfficer("player");
            SpecialForces specialForces = new SpecialForces
            {
                OwnerInstanceID = "player",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            bool canCreate = StrategyContextMenuAvailability.CanCreateMission(
                new ISceneNode[] { officer, specialForces },
                "player"
            );

            Assert.IsTrue(canCreate);
        }

        /// <summary>
        /// Verifies can create mission officer carried by moving fleet returns false.
        /// </summary>
        [Test]
        public void CanCreateMission_OfficerCarriedByMovingFleet_ReturnsFalse()
        {
            Officer officer = CreateOfficer("player");
            CapitalShip ship = new CapitalShip();
            GameFleet fleet = new GameFleet { Movement = new MovementState() };
            ship.SetParent(fleet);
            officer.SetParent(ship);

            bool canCreate = StrategyContextMenuAvailability.CanCreateMission(
                new ISceneNode[] { officer },
                "player"
            );

            Assert.IsFalse(canCreate);
        }

        /// <summary>
        /// Verifies can create mission invalid participant state or type returns false.
        /// </summary>
        [Test]
        public void CanCreateMission_InvalidParticipantStateOrType_ReturnsFalse()
        {
            Officer injuredOfficer = CreateOfficer("player");
            injuredOfficer.InjuryPoints = 1;
            SpecialForces buildingForces = new SpecialForces
            {
                OwnerInstanceID = "player",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Regiment regiment = new Regiment
            {
                OwnerInstanceID = "player",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            bool injuredResult = StrategyContextMenuAvailability.CanCreateMission(
                new ISceneNode[] { injuredOfficer },
                "player"
            );
            bool buildingResult = StrategyContextMenuAvailability.CanCreateMission(
                new ISceneNode[] { buildingForces },
                "player"
            );
            bool invalidTypeResult = StrategyContextMenuAvailability.CanCreateMission(
                new ISceneNode[] { regiment },
                "player"
            );

            Assert.IsFalse(injuredResult);
            Assert.IsFalse(buildingResult);
            Assert.IsFalse(invalidTypeResult);
        }

        /// <summary>
        /// Verifies player controls item captured officer uses captor identity.
        /// </summary>
        [Test]
        public void PlayerControlsItem_CapturedOfficer_UsesCaptorIdentity()
        {
            Officer officer = CreateOfficer("enemy");
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "player";

            bool playerControls = StrategyContextMenuAvailability.PlayerControlsItem(
                officer,
                "player"
            );
            bool ownerControls = StrategyContextMenuAvailability.PlayerControlsItem(
                officer,
                "enemy"
            );

            Assert.IsTrue(playerControls);
            Assert.IsFalse(ownerControls);
        }

        /// <summary>
        /// Verifies can toggle idle bar tracking enabled owned supported entities returns true.
        /// </summary>
        [Test]
        public void CanToggleIdleBarTracking_EnabledOwnedSupportedEntities_ReturnsTrue()
        {
            Assert.IsTrue(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    CreateOfficer("player"),
                    "player",
                    true
                )
            );
            Assert.IsTrue(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    new SpecialForces { OwnerInstanceID = "player" },
                    "player",
                    true
                )
            );
            Assert.IsTrue(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    new Planet { OwnerInstanceID = "player" },
                    "player",
                    true
                )
            );
        }

        /// <summary>
        /// Verifies can toggle idle bar tracking disabled enemy or unsupported entity returns false.
        /// </summary>
        [Test]
        public void CanToggleIdleBarTracking_DisabledEnemyOrUnsupportedEntity_ReturnsFalse()
        {
            Assert.IsFalse(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    CreateOfficer("player"),
                    "player",
                    false
                )
            );
            Assert.IsFalse(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    CreateOfficer("enemy"),
                    "player",
                    true
                )
            );
            Assert.IsFalse(
                StrategyContextMenuAvailability.CanToggleIdleBarTracking(
                    new Regiment { OwnerInstanceID = "player" },
                    "player",
                    true
                )
            );
        }

        /// <summary>
        /// Creates officer.
        /// </summary>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created officer.</returns>
        private static Officer CreateOfficer(string ownerId)
        {
            return new Officer { OwnerInstanceID = ownerId };
        }
    }
}
