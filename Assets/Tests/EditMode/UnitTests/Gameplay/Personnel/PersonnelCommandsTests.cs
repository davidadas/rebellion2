using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
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
            officer.Movement = TestMovement.Create();
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

        [TestCase(false, false, "first", 65)]
        [TestCase(false, false, "second", 65)]
        [TestCase(true, false, "first", 85)]
        [TestCase(true, false, "second", 65)]
        [TestCase(false, true, "first", 65)]
        [TestCase(false, true, "second", 85)]
        [TestCase(true, true, "first", 65)]
        [TestCase(true, true, "second", 65)]
        public void ApplyGlobalLoyaltyShift_RecruitmentFactionFlags_ChoosesRelativeDirection(
            bool first,
            bool second,
            string favored,
            int expected
        )
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, null);
            if (first)
                officer.RecruitingFactionInstanceIDs.Add("first");
            if (second)
                officer.RecruitingFactionInstanceIDs.Add("second");

            ApplyLoyaltyShift(10, favored, favored == "first" ? "second" : "first");

            Assert.AreEqual(expected, officer.Loyalty);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_ChangedCurrentOwner_KeepsRecruitmentFactionDirection()
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, "first");
            officer.OwnerInstanceID = "second";

            ApplyLoyaltyShift(10, "first", "second");

            Assert.AreEqual(85, officer.Loyalty);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_ChangedRecruitmentFlags_UsesTheSameFlagsAsRecruitment()
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, "first");
            officer.RecruitingFactionInstanceIDs.Clear();
            officer.RecruitingFactionInstanceIDs.Add("second");

            ApplyLoyaltyShift(10, "first", "second");

            Assert.AreEqual(65, officer.Loyalty);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_ProtectedOfficer_DoesNotChangeLoyalty()
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, null, false);

            ApplyLoyaltyShift(int.MaxValue, "first", "second");

            Assert.AreEqual(75, officer.Loyalty);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_ZeroChange_DoesNotChangeLoyalty()
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, null);

            ApplyLoyaltyShift(0, "first", "second");

            Assert.AreEqual(75, officer.Loyalty);
        }

        [TestCase("first", "")]
        [TestCase("", "second")]
        [TestCase("first", "first")]
        public void ApplyGlobalLoyaltyShift_InvalidCombatantPair_DoesNotChangeLoyalty(
            string favored,
            string opposing
        )
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, "first");

            ApplyLoyaltyShift(10, favored, opposing);

            Assert.AreEqual(75, officer.Loyalty);
        }

        [TestCase(75, 10, 85)]
        [TestCase(75, -10, 65)]
        [TestCase(75, 1000, 100)]
        [TestCase(75, -1000, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(100, 1, 100)]
        [TestCase(75, int.MaxValue, 0)]
        [TestCase(75, int.MinValue, 0)]
        public void ApplyGlobalLoyaltyShift_SignedAdjustment_WrapsBeforeClamping(
            int initial,
            int adjustment,
            int expected
        )
        {
            Officer officer = CreateBattleLoyaltyOfficer(initial, "first");

            ApplyLoyaltyShift(adjustment, "first", "second");

            Assert.AreEqual(expected, officer.Loyalty);
        }

        [Test]
        public void ApplyGlobalLoyaltyShift_OpposingMinimumChange_RetainsLow32Negation()
        {
            Officer officer = CreateBattleLoyaltyOfficer(75, "second");

            ApplyLoyaltyShift(int.MinValue, "first", "second");

            Assert.AreEqual(0, officer.Loyalty);
        }

        [TestCase(32768, 1, 0)]
        [TestCase(65535, 2, 1)]
        [TestCase(65536, 1, 1)]
        [TestCase(-32769, -1, 100)]
        public void ApplyGlobalLoyaltyShift_SerializedBase_ReadsSigned16BitsBeforeAddition(
            int initial,
            int adjustment,
            int expected
        )
        {
            Officer officer = CreateBattleLoyaltyOfficer(initial, "first");

            ApplyLoyaltyShift(adjustment, "first", "second");

            Assert.AreEqual(expected, officer.Loyalty);
        }

        /// <summary>
        /// Creates an officer with one retained recruitment-faction flag and a saved base loyalty.
        /// </summary>
        /// <param name="loyalty">The saved base loyalty.</param>
        /// <param name="faction">The flagged faction, or null for neither combatant.</param>
        /// <param name="canBetray">Whether loyalty changes are allowed.</param>
        /// <returns>The officer.</returns>
        private Officer CreateBattleLoyaltyOfficer(
            int loyalty,
            string faction,
            bool canBetray = true
        )
        {
            Officer officer = SerializationHelper.Deserialize<Officer>(
                new XElement(
                    "Officer",
                    new XElement("CanBetray", canBetray),
                    new XElement("Loyalty", loyalty)
                ).ToString()
            );
            if (faction != null)
                officer.RecruitingFactionInstanceIDs.Add(faction);
            officer.InstanceID = "battle-officer";
            officer.OwnerInstanceID = _ownerId;
            _game.AttachNode(officer, _planet);
            return officer;
        }

        /// <summary>
        /// Applies battle aftermath through the normal personnel command.
        /// </summary>
        /// <param name="adjustment">The shift relative to the favored faction.</param>
        /// <param name="favored">The favored faction's ID.</param>
        /// <param name="opposing">The opposing faction's ID.</param>
        private void ApplyLoyaltyShift(int adjustment, string favored, string opposing) =>
            _commands.ApplyGlobalLoyaltyShift(
                new Faction { InstanceID = favored },
                adjustment,
                new Faction { InstanceID = opposing }
            );

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

        [TestCase(40, 80, 80, 1)]
        [TestCase(80, 40, 80, 0)]
        [TestCase(81, 0, 80, -1)]
        [TestCase(0, 81, 80, 2)]
        [TestCase(0, -15, 10, -2)]
        [TestCase(-101, 99, 10, 29)]
        [TestCase(40, 80, -80, -1)]
        [TestCase(0, int.MinValue + 1, -1, -2)]
        [TestCase(int.MinValue, int.MinValue, int.MinValue, 1)]
        public void CalculateBattleLossLoyalty_FractionalLossWeights_TruncatesEachDivisionTowardZero(
            int favoredLoss,
            int opposingLoss,
            int divisor,
            int expected
        )
        {
            Assert.AreEqual(
                expected,
                PersonnelCommands.CalculateBattleLossLoyalty(favoredLoss, opposingLoss, divisor)
            );
        }

        [TestCase(int.MinValue, 0, int.MinValue)]
        [TestCase(0, int.MaxValue, -2)]
        [TestCase(int.MinValue, int.MaxValue, int.MaxValue - 1)]
        [TestCase(int.MaxValue, int.MinValue, int.MinValue + 1)]
        public void CalculateBattleLossLoyalty_OverflowingLossWeights_WrapsDifferenceAndFinalSum(
            int favoredLoss,
            int opposingLoss,
            int expected
        )
        {
            Assert.AreEqual(
                expected,
                PersonnelCommands.CalculateBattleLossLoyalty(favoredLoss, opposingLoss, 1)
            );
        }

        [Test]
        public void CalculateBattleLossLoyalty_ZeroDivisor_PropagatesDivisionFault()
        {
            Assert.Throws<DivideByZeroException>(() =>
                PersonnelCommands.CalculateBattleLossLoyalty(0, 0, 0)
            );
        }

        [TestCase(int.MinValue, 0)]
        [TestCase(0, int.MinValue)]
        [TestCase(int.MinValue, int.MinValue)]
        public void CalculateBattleLossLoyalty_UnrepresentableQuotient_PropagatesDivisionFault(
            int favoredLoss,
            int opposingLoss
        )
        {
            Assert.Throws<OverflowException>(() =>
                PersonnelCommands.CalculateBattleLossLoyalty(favoredLoss, opposingLoss, -1)
            );
        }
    }
}
