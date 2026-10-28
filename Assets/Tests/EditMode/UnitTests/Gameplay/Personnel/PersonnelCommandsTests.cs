using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class PersonnelCommandsTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private PersonnelCommands _commands;
        private Planet _planet;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(_planet, sector);
            _commands = new PersonnelCommands(_game, new PersonnelQueries(_game));
        }

        [Test]
        public void Constructor_NullQueries_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PersonnelCommands(_game, null)
            );

            Assert.AreEqual("queries", exception.ParamName);
        }

        [Test]
        public void KillOfficer_ActiveOfficer_MarksKilledAndRetainsIdentity()
        {
            Officer officer = CreateOfficer("killed-officer");
            officer.Movement = new MovementState();
            _game.AttachNode(officer, _planet);

            _commands.KillOfficer(officer);

            Assert.IsTrue(officer.IsKilled);
            Assert.IsNull(officer.Movement);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.IsFalse(officer.IsActive());
            Assert.AreSame(
                officer,
                _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID, includeDisabled: true)
            );
            CollectionAssert.DoesNotContain(_game.GetSceneNodesByType<Officer>(), officer);
        }

        [Test]
        public void Retire_OwnedPersonnel_RemovesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            SpecialForces specialForces = CreateSpecialForces("special-forces");
            _game.AttachNode(officer, _planet);
            _game.AttachNode(specialForces, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer, specialForces }, _ownerId);

            Assert.IsTrue(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, specialForces.GetParent());
            Assert.IsFalse(officer.IsActive());
            Assert.IsFalse(specialForces.IsActive());
            Assert.IsTrue(officer.IsRetired);
            Assert.IsTrue(specialForces.IsRetired);
        }

        [Test]
        public void Retire_InvalidMember_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            Officer mainOfficer = CreateOfficer("main-officer");
            mainOfficer.IsMain = true;
            _game.AttachNode(officer, _planet);
            _game.AttachNode(mainOfficer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer, mainOfficer }, _ownerId);

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(_planet, mainOfficer.GetParent());
        }

        [Test]
        public void Retire_UnauthorizedOwner_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { officer }, "other-owner");

            Assert.IsFalse(retired);
            Assert.AreSame(_planet, officer.GetParent());
            Assert.AreSame(officer, _game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID));
        }

        [Test]
        public void KillOfficer_NullOfficer_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                _commands.KillOfficer(null)
            );

            Assert.AreEqual("officer", exception.ParamName);
        }

        [Test]
        public void Retire_DuplicateIdentity_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(
                new ISceneNode[] { officer, CreateOfficer(officer.InstanceID) },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void Retire_UnregisteredMember_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(
                new ISceneNode[] { officer, CreateOfficer("missing") },
                _ownerId
            );

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void Retire_SnapshotSelection_RetiresOnlyLivePersonnel()
        {
            Officer officer = CreateOfficer("officer");
            Officer snapshot = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);

            bool retired = _commands.Retire(new ISceneNode[] { snapshot }, _ownerId);

            Assert.IsTrue(retired);
            Assert.IsTrue(officer.IsRetired);
            Assert.IsFalse(snapshot.IsRetired);
        }

        [Test]
        public void Retire_OfficerCapturedAfterEligibilityCheck_PreservesPersonnel()
        {
            Officer officer = CreateOfficer("officer");
            _game.AttachNode(officer, _planet);
            ISceneNode[] selection = { officer };
            Assert.IsTrue(new PersonnelQueries(_game).CanRetire(selection, _ownerId));
            officer.IsCaptured = true;

            bool retired = _commands.Retire(selection, _ownerId);

            Assert.IsFalse(retired);
            Assert.IsTrue(officer.IsActive());
            Assert.IsFalse(officer.IsRetired);
        }

        [Test]
        public void TrySetRank_EligibleOfficer_AssignsPostAndPublishesResults()
        {
            Officer officer = CreateOfficer("candidate");
            officer.AllowedRanks = new[] { OfficerRank.Commander };
            _game.AttachNode(officer, _planet);
            List<GameResult> published = null;
            _commands.ResultsProduced += results => published = results.ToList();

            bool changed = _commands.TrySetRank(
                officer.InstanceID,
                OfficerRank.Commander,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.Commander, officer.CurrentRank);
            Assert.AreEqual(1, published.OfType<CommandKindChangedResult>().Count());
            Assert.AreSame(
                _planet,
                published.OfType<OfficerCommandingResult>().Single().CommandTarget
            );
        }

        [Test]
        public void TrySetRank_ActiveRank_ResignsFromPost()
        {
            Officer officer = CreateOfficer("candidate");
            officer.AllowedRanks = new[] { OfficerRank.Commander };
            officer.CurrentRank = OfficerRank.Commander;
            _game.AttachNode(officer, _planet);

            bool changed = _commands.TrySetRank(
                officer.InstanceID,
                OfficerRank.Commander,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.None, officer.CurrentRank);
        }

        [Test]
        public void TrySetRank_PostAlreadyFilled_ReplacesIncumbent()
        {
            Officer incumbent = CreateOfficer("incumbent");
            incumbent.AllowedRanks = new[] { OfficerRank.General };
            incumbent.CurrentRank = OfficerRank.General;
            Officer replacement = CreateOfficer("replacement");
            replacement.AllowedRanks = new[] { OfficerRank.General };
            _game.AttachNode(incumbent, _planet);
            _game.AttachNode(replacement, _planet);

            bool changed = _commands.TrySetRank(
                replacement.InstanceID,
                OfficerRank.General,
                _ownerId
            );

            Assert.IsTrue(changed);
            Assert.AreEqual(OfficerRank.None, incumbent.CurrentRank);
            Assert.AreEqual(OfficerRank.General, replacement.CurrentRank);
        }

        [Test]
        public void TrySetRank_IneligibleRank_PreservesCurrentPost()
        {
            Officer officer = CreateOfficer("candidate");
            officer.AllowedRanks = new[] { OfficerRank.General };
            officer.CurrentRank = OfficerRank.General;
            _game.AttachNode(officer, _planet);

            bool changed = _commands.TrySetRank(officer.InstanceID, OfficerRank.Admiral, _ownerId);

            Assert.IsFalse(changed);
            Assert.AreEqual(OfficerRank.General, officer.CurrentRank);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_RegisteredOfficers_UsesFactionRelativeSign()
        {
            Faction favoredFaction = _game.GetFactions().Single();
            Faction opposingFaction = new Faction { InstanceID = "opposing" };
            _game.GetFactions().Add(opposingFaction);
            Officer favoredOfficer = EntityFactory.CreateOfficer(
                "favored",
                favoredFaction.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            Officer opposingOfficer = EntityFactory.CreateOfficer(
                "opposing",
                opposingFaction.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            Planet opposingPlanet = new Planet
            {
                InstanceID = "opposing-planet",
                OwnerInstanceID = opposingFaction.InstanceID,
                IsColonized = true,
            };
            _game.AttachNode(opposingPlanet, _planet.GetParent());
            _game.AttachNode(favoredOfficer, _planet);
            _game.AttachNode(opposingOfficer, opposingPlanet);

            _commands.ApplyGlobalLoyaltyShift(favoredFaction, 7);

            Assert.AreEqual(57, favoredOfficer.Loyalty);
            Assert.AreEqual(43, opposingOfficer.Loyalty);
        }

        /// <summary>
        /// Creates officer.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created officer.</returns>
        private static Officer CreateOfficer(string instanceId)
        {
            return new Officer { InstanceID = instanceId, OwnerInstanceID = _ownerId };
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created special forces.</returns>
        private static SpecialForces CreateSpecialForces(string instanceId)
        {
            return new SpecialForces
            {
                InstanceID = instanceId,
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
