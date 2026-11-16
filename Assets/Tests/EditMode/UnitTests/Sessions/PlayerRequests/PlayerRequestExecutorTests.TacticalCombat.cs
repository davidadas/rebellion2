using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.PlayerRequests;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.PlayerRequests
{
    public sealed partial class PlayerRequestExecutorTests
    {
        private const string _tacticalRequestBattleID = "bd5c62ae67394d2089b13757d6d24387";

        [TestCase("FNEMP1")]
        [TestCase("FNALL1")]
        public void Execute_BeginSharedBattleByEitherActor_WaitsForBothCommanders(string firstActor)
        {
            (SpaceCombatCommands combat, PendingCombatResult pending, _, _) =
                PrepareTacticalRequestEncounter();
            string secondActor = firstActor == _opponentID ? _ownerID : _opponentID;

            PlayerRequestOutcome first = _executor.Execute(
                firstActor,
                CreateTacticalBeginRequest(pending)
            );

            Assert.IsTrue(first.Accepted);
            Assert.IsNull(first.SpaceCombatResult);
            Assert.IsTrue(combat.HasPendingDecision);
            Assert.IsNull(combat.ActiveTacticalCombat);
            Assert.IsTrue(combat.TryGetPendingCombat(out PendingCombatResult waiting));
            Assert.IsTrue(waiting.RequiresBothResponses);
            Assert.AreEqual(
                firstActor == waiting.AttackerOwnerInstanceID,
                waiting.AttackerHasResponded
            );
            Assert.AreEqual(
                firstActor == waiting.DefenderOwnerInstanceID,
                waiting.DefenderHasResponded
            );

            PlayerRequestOutcome second = _executor.Execute(
                secondActor,
                CreateTacticalBeginRequest(pending)
            );

            Assert.IsTrue(second.Accepted);
            Assert.IsNull(second.SpaceCombatResult);
            Assert.IsNotNull(combat.ActiveTacticalCombat);
            Assert.AreEqual(_tacticalRequestBattleID, combat.ActiveTacticalBattleID);
            Assert.IsTrue(combat.ActiveTacticalCombat.IsNetworked);
            Assert.IsFalse(combat.ActiveTacticalCombat.IsReplica);
            Assert.IsTrue(combat.HasPendingDecision);
        }

        [TestCase("planet")]
        [TestCase("tick")]
        [TestCase("battle")]
        [TestCase("unknown-actor")]
        [TestCase("nonparticipant")]
        [TestCase("single-player")]
        [TestCase("participants")]
        public void Execute_InvalidSharedBattleBegin_RejectsWithoutRecordingAResponse(
            string invalid
        )
        {
            (SpaceCombatCommands combat, PendingCombatResult pending, _, _) =
                PrepareTacticalRequestEncounter();
            BeginTacticalCombatRequest request = CreateTacticalBeginRequest(pending);
            string actor = _opponentID;
            switch (invalid)
            {
                case "planet":
                    request.PlanetID = _destination.InstanceID;
                    break;
                case "tick":
                    request.Tick--;
                    break;
                case "battle":
                    request.BattleID = "invalid-battle-identity";
                    break;
                case "unknown-actor":
                    actor = "unbound";
                    break;
                case "nonparticipant":
                    actor = "observer";
                    _game.GetFactions().Add(new Faction { InstanceID = actor });
                    _game.SetFactionController(
                        actor,
                        "observer-player",
                        PlayerControllerType.Human
                    );
                    break;
                case "single-player":
                    _game.Metadata.MultiplayerCampaign = null;
                    break;
                case "participants":
                    _game.Metadata.MultiplayerCampaign.GuestFactionID = "unbound";
                    break;
            }
            string before = CaptureTacticalCampaignState();

            PlayerRequestOutcome outcome = _executor.Execute(actor, request);

            Assert.IsFalse(outcome.Accepted);
            Assert.IsNotEmpty(outcome.RejectionReason);
            Assert.IsNull(outcome.SpaceCombatResult);
            Assert.AreEqual(before, CaptureTacticalCampaignState());
            Assert.IsNull(combat.ActiveTacticalCombat);
            Assert.IsTrue(combat.TryGetPendingCombat(out PendingCombatResult after));
            Assert.IsFalse(after.AttackerHasResponded);
            Assert.IsFalse(after.DefenderHasResponded);
        }

        [TestCase("move")]
        [TestCase("scrap")]
        [TestCase("resolve")]
        public void Execute_CampaignOrderDuringSharedBattle_RejectsBeforeMutatingCampaign(
            string kind
        )
        {
            (SpaceCombatCommands combat, PendingCombatResult pending, _, _) =
                PrepareTacticalRequestEncounter();
            TacticalCombatSession battle = AdmitTacticalRequestBattle(pending);
            Officer officer = CreateOfficer("outside-battle-officer", _opponentID, _destination);
            CreateFleet(
                "outside-battle-reserve",
                _opponentID,
                _destination,
                out CapitalShip reserve
            );
            PlayerRequest request = kind switch
            {
                "move" => new MoveRequest
                {
                    ItemIDs = new[] { officer.InstanceID },
                    DestinationID = _origin.InstanceID,
                },
                "scrap" => new ScrapRequest { ItemIDs = new[] { reserve.InstanceID } },
                _ => new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = false,
                },
            };
            string before = CaptureTacticalCampaignState();

            PlayerRequestOutcome outcome = _executor.Execute(_opponentID, request);

            Assert.IsFalse(outcome.Accepted);
            Assert.IsNotEmpty(outcome.RejectionReason);
            Assert.AreEqual(before, CaptureTacticalCampaignState());
            Assert.AreSame(_destination, officer.GetParent());
            Assert.AreSame(
                reserve,
                _game.GetSceneNodeByInstanceID<CapitalShip>(reserve.InstanceID)
            );
            Assert.AreSame(battle, combat.ActiveTacticalCombat);
            Assert.IsTrue(combat.HasPendingDecision);
        }

        [TestCase("FNEMP1", "FNALL1")]
        [TestCase("FNALL1", "FNEMP1")]
        public void Execute_PeerChatDuringSharedBattle_DeliversWithoutReleasingCombat(
            string sender,
            string recipient
        )
        {
            (SpaceCombatCommands combat, PendingCombatResult pending, _, _) =
                PrepareTacticalRequestEncounter();
            TacticalCombatSession battle = AdmitTacticalRequestBattle(pending);
            int sentHistory = _game
                .GetFactionByOwnerInstanceID(sender)
                .Messages.Values.Sum(messages => messages.Count);
            int receivedHistory = _game
                .GetFactionByOwnerInstanceID(recipient)
                .Messages[MessageType.Chat]
                .Count;

            PlayerRequestOutcome outcome = _executor.Execute(
                sender,
                new SendPeerChatRequest { Text = "Battle message" }
            );

            Assert.IsTrue(outcome.Accepted);
            Assert.AreEqual(
                receivedHistory + 1,
                _game.GetFactionByOwnerInstanceID(recipient).Messages[MessageType.Chat].Count
            );
            Assert.AreEqual(
                "Battle message",
                _game.GetFactionByOwnerInstanceID(recipient).Messages[MessageType.Chat].Last().Body
            );
            Assert.AreEqual(
                sentHistory,
                _game
                    .GetFactionByOwnerInstanceID(sender)
                    .Messages.Values.Sum(messages => messages.Count)
            );
            Assert.AreSame(battle, combat.ActiveTacticalCombat);
            Assert.IsTrue(combat.HasPendingDecision);
            Assert.IsFalse(battle.IsComplete);
        }

        [TestCase("planet")]
        [TestCase("tick")]
        [TestCase("battle")]
        public void Execute_StaleSharedBattleCompletion_PreservesActiveEncounter(string invalid)
        {
            (SpaceCombatCommands combat, PendingCombatResult pending, _, _) =
                PrepareTacticalRequestEncounter();
            TacticalCombatSession battle = AdmitTacticalRequestBattle(pending);
            CompleteTacticalCombatRequest request = CreateTacticalCompletionRequest(pending, null);
            switch (invalid)
            {
                case "planet":
                    request.PlanetID = _destination.InstanceID;
                    break;
                case "tick":
                    request.Tick--;
                    break;
                case "battle":
                    request.BattleID = "abd0a71d030b434fbd8b2d95aa71b461";
                    break;
            }
            string before = CaptureTacticalCampaignState();

            PlayerRequestOutcome outcome = _executor.Execute(_opponentID, request);

            Assert.IsFalse(outcome.Accepted);
            Assert.IsNotEmpty(outcome.RejectionReason);
            Assert.AreEqual(before, CaptureTacticalCampaignState());
            Assert.AreSame(battle, combat.ActiveTacticalCombat);
            Assert.AreEqual(_tacticalRequestBattleID, combat.ActiveTacticalBattleID);
            Assert.IsTrue(combat.HasPendingDecision);
        }

        [Test]
        public void Execute_CompletedSharedBattle_RequiresHostAndValidOutcomeBeforeImportingOnce()
        {
            (
                SpaceCombatCommands combat,
                PendingCombatResult pending,
                CapitalShip hostShip,
                CapitalShip guestShip
            ) = PrepareTacticalRequestEncounter();
            TacticalCombatSession battle = AdmitTacticalRequestBattle(pending);
            TacticalCombatUnit host = battle.GetUnit(hostShip.InstanceID);
            battle.SetTarget(battle.GetRequestedGroup(host), battle.GetUnit(guestShip.InstanceID));
            for (int frame = 0; frame < 12000 && !battle.IsComplete; frame++)
                battle.Advance(50);
            Assert.IsTrue(
                battle.IsComplete,
                "The battle must finish through its actual weapon and ending phases."
            );
            TacticalCombatOutcome final = battle.CaptureNetworkOutcome();
            Assert.AreEqual(1000, hostShip.CurrentHullStrength);
            Assert.AreEqual(10, guestShip.CurrentHullStrength);
            string before = CaptureTacticalCampaignState();
            List<GameResult> published = new();
            _session.Pipeline.ResultsResolved += published.AddRange;

            PlayerRequestOutcome guest = _executor.Execute(
                _ownerID,
                CreateTacticalCompletionRequest(pending, final)
            );

            Assert.IsFalse(guest.Accepted);
            Assert.AreEqual(before, CaptureTacticalCampaignState());
            Assert.AreSame(battle, combat.ActiveTacticalCombat);
            Assert.IsEmpty(published);

            PlayerRequestOutcome malformed = _executor.Execute(
                _opponentID,
                CreateTacticalCompletionRequest(pending, null)
            );

            Assert.IsFalse(malformed.Accepted);
            Assert.AreEqual(before, CaptureTacticalCampaignState());
            Assert.AreSame(battle, combat.ActiveTacticalCombat);
            Assert.IsEmpty(published);

            PlayerRequestOutcome accepted = _executor.Execute(
                _opponentID,
                CreateTacticalCompletionRequest(pending, final)
            );

            Assert.IsTrue(accepted.Accepted);
            Assert.IsNotNull(accepted.SpaceCombatResult);
            Assert.AreEqual(1, published.OfType<SpaceCombatResult>().Count());
            Assert.IsNull(combat.ActiveTacticalCombat);
            Assert.IsFalse(combat.HasPendingDecision);
            Assert.IsFalse(
                _executor
                    .Execute(_opponentID, CreateTacticalCompletionRequest(pending, final))
                    .Accepted
            );
            Assert.AreEqual(1, published.OfType<SpaceCombatResult>().Count());
        }

        /// <summary>
        /// Creates a pending authored encounter for the canonical admitted host and guest factions.
        /// </summary>
        /// <returns>The campaign command owner, pending identity, and opposing captured ships.</returns>
        private (
            SpaceCombatCommands Combat,
            PendingCombatResult Pending,
            CapitalShip Host,
            CapitalShip Guest
        ) PrepareTacticalRequestEncounter()
        {
            _session.Dispose();
            _game = TestGame.Create(TestContent.Data.GameConfig.CreateCopy());
            _game.Summary.PlayerFactionID = _opponentID;
            _game.LocalPlayerFactionID = _opponentID;
            _game.Random = new SystemRandomProvider(42);
            _game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
            {
                HostUserID = "host-user",
                GuestUserID = "guest-user",
                HostFactionID = _opponentID,
                GuestFactionID = _ownerID,
            };
            _actor = new Faction { InstanceID = _ownerID };
            _opponent = new Faction { InstanceID = _opponentID };
            _game.GetFactions().AddRange(new[] { _actor, _opponent });
            _game.SetFactionController(_opponentID, "host", PlayerControllerType.Human);
            _game.SetFactionController(_ownerID, "guest", PlayerControllerType.Human);
            PlanetSector sector = new() { InstanceID = "shared-sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _origin = CreatePlanet("shared-combat", _opponentID, sector, 0);
            _destination = CreatePlanet("host-home", _opponentID, sector, 100);
            _opponentPlanet = CreatePlanet("guest-home", _ownerID, sector, 200);
            CreateFleet("shared-host", _opponentID, _origin, out CapitalShip host);
            CreateFleet("shared-guest", _ownerID, _origin, out CapitalShip guest);
            host.TypeID = "CSEM006";
            host.MaxHullStrength = host.CurrentHullStrength = 1000;
            host.SublightSpeed = 4;
            host.WeaponRecharge = 1;
            host.ShieldRechargeRate = 5;
            host.SystemDamage.Weapons = 1;
            host.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100000, 0, 0, 0, 100 };
            guest.TypeID = "ALCS008";
            guest.MaxHullStrength = guest.CurrentHullStrength = 10;
            _session = TestContent.CreateGameSession(_game);
            _gameManager = new GameManager(() => _session.Game, _session.Tick);
            _executor = new PlayerRequestExecutor(_session, _gameManager);
            _session.Tick.ProcessTick();
            SpaceCombatCommands combat = _session.GetService<SpaceCombatCommands>();
            Assert.IsTrue(combat.TryGetPendingCombat(out PendingCombatResult pending));
            Assert.IsTrue(combat.CanTakeCommand);
            Assert.IsTrue(_session.CanExecuteCommands);
            return (combat, pending, host, guest);
        }

        /// <summary>
        /// Records both manual responses through authenticated public request execution.
        /// </summary>
        /// <param name="pending">The exact displayed encounter.</param>
        /// <returns>The admitted authoritative encounter.</returns>
        private TacticalCombatSession AdmitTacticalRequestBattle(PendingCombatResult pending)
        {
            Assert.IsTrue(
                _executor.Execute(_opponentID, CreateTacticalBeginRequest(pending)).Accepted
            );
            Assert.IsTrue(
                _executor.Execute(_ownerID, CreateTacticalBeginRequest(pending)).Accepted
            );
            TacticalCombatSession battle = _session
                .GetService<SpaceCombatCommands>()
                .ActiveTacticalCombat;
            Assert.IsNotNull(battle);
            return battle;
        }

        /// <summary>
        /// Captures the exact pending identity with controlled shared-entry random inputs.
        /// </summary>
        /// <param name="pending">The current pending encounter.</param>
        /// <returns>The manual response to execute.</returns>
        private static BeginTacticalCombatRequest CreateTacticalBeginRequest(
            PendingCombatResult pending
        ) =>
            new()
            {
                PlanetID = pending.PlanetInstanceID,
                Tick = pending.Tick,
                BattleID = _tacticalRequestBattleID,
                Seed = 42,
                ScenerySeed = 123,
            };

        /// <summary>
        /// Addresses completion to the same admitted battle without supplying campaign objects.
        /// </summary>
        /// <param name="pending">The battle's retained campaign decision.</param>
        /// <param name="outcome">The authoritative final outcome, or null to exercise rejection.</param>
        /// <returns>The completion request.</returns>
        private static CompleteTacticalCombatRequest CreateTacticalCompletionRequest(
            PendingCombatResult pending,
            TacticalCombatOutcome outcome
        ) =>
            new()
            {
                PlanetID = pending.PlanetInstanceID,
                Tick = pending.Tick,
                BattleID = _tacticalRequestBattleID,
                Outcome = outcome,
            };

        /// <summary>
        /// Hashes the complete persisted campaign to detect any mutation during rejected requests.
        /// </summary>
        /// <returns>The serialized campaign's SHA-256 digest.</returns>
        private string CaptureTacticalCampaignState()
        {
            using StringWriter writer = new();
            new GameSerializer(typeof(GameRoot)).Serialize(writer, _game);
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(
                hash.ComputeHash(Encoding.UTF8.GetBytes(writer.ToString()))
            );
        }
    }
}
