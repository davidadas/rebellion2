using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    /// <summary>
    /// Verifies physical candidate scope independently of encounter probability and consequences.
    /// </summary>
    [TestFixture]
    public sealed class MissionForceEncounterScopeTests
    {
        private GameRoot _game;
        private Planet _planet;
        private StubMission _mission;
        private Officer _participant;
        private Officer _defender;
        private CapitalShip _ship;

        /// <summary>
        /// Creates one hostile planet, an active fleet, and officers on each side.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            _game.Config.Jedi.MissionParticipantEncounterMinimum = 1;
            _game.Config.Jedi.MissionDefenderEncounterMinimum = 60;
            PlanetSector sector = new() { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet { InstanceID = "planet", OwnerInstanceID = "rebels" };
            _game.AttachNode(_planet, sector);
            Fleet fleet = new() { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            _game.AttachNode(fleet, _planet);
            _ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_ship, fleet);
            _mission = new StubMission("empire", _planet.InstanceID);
            _game.AttachNode(_mission, _planet);
            _participant = EntityFactory.CreateOfficer("participant", "empire");
            _participant.ForceValue = 50;
            _participant.MissionReturnLocationInstanceID = "origin";
            _game.AttachNode(_participant, _mission);
            _defender = EntityFactory.CreateOfficer("defender", "rebels");
            _defender.ForceValue = 80;
            _game.AttachNode(_defender, _planet);
        }

        [TestCase((int)MissionEncounterPhase.DepartureStart, true)]
        [TestCase((int)MissionEncounterPhase.DepartureComplete, false)]
        [TestCase((int)MissionEncounterPhase.Arrival, true)]
        [TestCase((int)MissionEncounterPhase.PreObjective, true)]
        public void GetParticipants_GroundOfficerAtCheckpoint_UsesPhysicalScope(
            int phase,
            bool included
        )
        {
            MissionForceEncounterScope scope = CreateScope((MissionEncounterPhase)phase);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.AreEqual(included, selected.Contains(_participant));
        }

        [Test]
        public void GetParticipants_GroundOfficerDepartingForAnotherPlanet_IncludesParticipant()
        {
            _mission.LocationInstanceID = "destination";
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.DepartureComplete);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.AreSame(_participant, selected.Single());
        }

        [Test]
        public void GetParticipants_SamePlanetArrival_ExcludesGroundParticipant()
        {
            _participant.MissionReturnLocationInstanceID = _planet.InstanceID;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.Arrival);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.IsEmpty(selected);
        }

        [TestCase((int)MissionEncounterPhase.DepartureStart, false)]
        [TestCase((int)MissionEncounterPhase.DepartureComplete, true)]
        [TestCase((int)MissionEncounterPhase.Arrival, true)]
        [TestCase((int)MissionEncounterPhase.PreObjective, true)]
        public void GetParticipants_ShipboardOfficerAtCheckpoint_UsesPhysicalScope(
            int phase,
            bool included
        )
        {
            _game.ChangeOwnership(_ship, "empire");
            _game.MoveNode(_participant, _ship);
            MissionForceEncounterScope scope = CreateScope((MissionEncounterPhase)phase);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.AreEqual(included, selected.Contains(_participant));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void GetParticipants_SensitivityAndEligibilityFlags_DoNotChangeRankQualification(
            bool sensitive,
            bool eligible
        )
        {
            _participant.IsForceSensitive = sensitive;
            _participant.IsForceEligible = eligible;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetParticipants(
                Array.Empty<IMissionParticipant>(),
                new[] { _participant },
                _game.Config.Jedi
            );

            Assert.AreSame(_participant, selected.Single());
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(-1, false)]
        public void GetParticipants_ConfiguredRankThreshold_UsesSignedEffectiveRank(
            int rank,
            bool included
        )
        {
            _participant.ForceValue = rank;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.AreEqual(included, selected.Contains(_participant));
        }

        [TestCase(null)]
        [TestCase("")]
        public void GetDefenders_UnownedPlanet_ExcludesGroundEncounter(string ownerID)
        {
            _planet.OwnerInstanceID = ownerID;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.IsEmpty(selected);
        }

        [Test]
        public void GetDefenders_OfficerAssignedToAnotherMission_IncludesPhysicalGroundDefender()
        {
            StubMission defendingMission = new("rebels", _planet.InstanceID);
            _game.AttachNode(defendingMission, _planet);
            _game.MoveNode(_defender, defendingMission);
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.AreSame(_defender, selected.Single());
        }

        [Test]
        public void GetDefenders_InjuredOfficerWithoutForceFlags_IncludesQualifiedDefender()
        {
            _defender.InjuryPoints = 40;
            _defender.IsForceSensitive = false;
            _defender.IsForceEligible = false;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.AreSame(_defender, selected.Single());
        }

        [TestCase(59, false)]
        [TestCase(60, true)]
        [TestCase(-1, false)]
        public void GetDefenders_ConfiguredRankThreshold_UsesSignedEffectiveRank(
            int rank,
            bool included
        )
        {
            _defender.ForceValue = rank;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.AreEqual(included, selected.Contains(_defender));
        }

        [TestCase((int)MissionEncounterPhase.Arrival, true, false)]
        [TestCase((int)MissionEncounterPhase.Arrival, false, true)]
        [TestCase((int)MissionEncounterPhase.PreObjective, true, true)]
        public void GetDefenders_ApproachProtection_AppliesOnlyBeforeObjective(
            int phase,
            bool blocked,
            bool included
        )
        {
            _game.MoveNode(_defender, _ship);
            MissionForceEncounterScope scope = CreateScope(
                (MissionEncounterPhase)phase,
                planetaryObjective: false,
                blocked: blocked
            );

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.AreEqual(included, selected.Contains(_defender));
        }

        [Test]
        public void GetDefenders_NoHostileFleet_ExcludesShipboardDefenders()
        {
            _game.MoveNode(_defender, _ship);
            MissionForceEncounterScope scope = new(
                _mission,
                _planet,
                MissionEncounterPhase.Arrival,
                false,
                false,
                false
            );

            Assert.IsEmpty(scope.GetDefenders(_game.RegisteredOfficers, _game.Config.Jedi));
        }

        [Test]
        public void GetDefenders_FriendlyPlanet_ExcludesGroundDefenders()
        {
            _planet.OwnerInstanceID = "empire";
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            Assert.IsEmpty(scope.GetDefenders(_game.RegisteredOfficers, _game.Config.Jedi));
        }

        [Test]
        public void GetDefenders_CapturedOfficer_ExcludesDefender()
        {
            _defender.IsCaptured = true;
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.PreObjective);

            Assert.IsEmpty(scope.GetDefenders(_game.RegisteredOfficers, _game.Config.Jedi));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetDefenders_OfficerCarriedInTransit_ExcludesDefender(bool fleetTransit)
        {
            _game.MoveNode(_defender, _ship);
            IMovable carrier = fleetTransit ? (Fleet)_ship.GetParent() : _ship;
            carrier.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 1 };
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.Arrival);

            List<Officer> selected = scope.GetDefenders(
                _game.RegisteredOfficers,
                _game.Config.Jedi
            );

            Assert.IsEmpty(selected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetParticipants_OfficerCarriedInTransit_ExcludesParticipant(bool fleetTransit)
        {
            _game.ChangeOwnership(_ship, "empire");
            _game.MoveNode(_participant, _ship);
            IMovable carrier = fleetTransit ? (Fleet)_ship.GetParent() : _ship;
            carrier.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 1 };
            MissionForceEncounterScope scope = CreateScope(MissionEncounterPhase.DepartureComplete);

            List<Officer> selected = scope.GetParticipants(
                new[] { _participant },
                Array.Empty<IMissionParticipant>(),
                _game.Config.Jedi
            );

            Assert.IsEmpty(selected);
        }

        /// <summary>
        /// Creates a checkpoint with a stationary hostile fleet and the requested target scope.
        /// </summary>
        /// <param name="phase">The checkpoint being evaluated.</param>
        /// <param name="planetaryObjective">Whether the objective lies directly on the planet.</param>
        /// <param name="blocked">Whether the approach has a friendly detection blocker.</param>
        /// <returns>The candidate scope.</returns>
        private MissionForceEncounterScope CreateScope(
            MissionEncounterPhase phase,
            bool planetaryObjective = true,
            bool blocked = false
        ) => new(_mission, _planet, phase, planetaryObjective, true, blocked);
    }
}
