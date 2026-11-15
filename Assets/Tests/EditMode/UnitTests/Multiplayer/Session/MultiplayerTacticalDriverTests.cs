using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;
using Rebellion.Multiplayer;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerTacticalDriverTests
    {
        [TestCase("FNALL1", MultiplayerRole.Host, false)]
        [TestCase("FNALL1", MultiplayerRole.Guest, false)]
        [TestCase("FNEMP1", MultiplayerRole.Host, false)]
        [TestCase("FNEMP1", MultiplayerRole.Guest, false)]
        [TestCase("FNALL1", MultiplayerRole.Host, true)]
        [TestCase("FNEMP1", MultiplayerRole.Guest, true)]
        public void Begin_OrderedHumanResponses_PreservesViewsAndExplicitAutomation(
            string hostFaction,
            MultiplayerRole firstManual,
            bool otherAuto
        )
        {
            using PeerPair peers = new(hostFaction);
            peers.StartDay();

            peers.Begin(firstManual, otherAuto);
            for (int frame = 0; frame < 12; frame++)
                peers.Step(0.5f);

            foreach (GameSession simulation in peers.Simulations)
                Assert.IsFalse(simulation.Tick.IsSettled);
            Assert.IsFalse(peers.HostDriver.Session.IsReplica);
            Assert.IsTrue(peers.GuestDriver.Session.IsReplica);
            Assert.AreNotEqual(
                peers.HostDriver.Session.PlayerSide,
                peers.GuestDriver.Session.PlayerSide
            );
            Assert.AreEqual(
                peers.HostCommands.ActiveTacticalBattleID,
                peers.GuestCommands.ActiveTacticalBattleID
            );
            Assert.IsFalse(
                peers
                    .Driver(firstManual)
                    .Session.GetSide(peers.Driver(firstManual).Session.PlayerSide)
                    .IsAutomated
            );
            MultiplayerTacticalDriver other = peers.Driver(
                firstManual == MultiplayerRole.Host ? MultiplayerRole.Guest : MultiplayerRole.Host
            );
            Assert.AreEqual(otherAuto, other.Session.GetSide(other.Session.PlayerSide).IsAutomated);
            Assert.Throws<InvalidOperationException>(() => peers.GuestDriver.Session.Advance(50));
            Assert.IsNull(peers.Host.Failure);
            Assert.IsNull(peers.Guest.Failure);
        }

        [TestCase("FNALL1")]
        [TestCase("FNEMP1")]
        public void Advance_AuthoredCarrierBattle_ProjectsNaturalCombatAndCommitsOneSharedResult(
            string hostFaction
        )
        {
            using PeerPair peers = new(hostFaction);
            peers.StartDay();
            peers.Begin(MultiplayerRole.Guest, false);
            TacticalCombatSession host = peers.HostDriver.Session;
            TacticalCombatSession guest = peers.GuestDriver.Session;
            Dictionary<string, int> shots = host.Units.ToDictionary(
                unit => unit.InstanceID,
                _ => 0
            );
            int projectedShots = 0;
            host.ShotFired += shot => shots[shot.Source.InstanceID]++;
            guest.ShotFired += _ => projectedShots++;
            long hostRandom = ((SystemRandomProvider)peers.HostSimulation.Game.Random).CallCount;
            long guestRandom = ((SystemRandomProvider)peers.GuestSimulation.Game.Random).CallCount;

            for (int frame = 0; frame < 60000 && !host.IsComplete; frame++)
            {
                if (frame % 20 == 0)
                    peers.TargetManualForces();
                peers.AdvanceViews(0.1f);
                if (!host.IsComplete)
                    peers.Flush();
            }

            Assert.IsTrue(host.IsComplete, Describe(host, shots));
            foreach (CombatSide side in new[] { CombatSide.Attacker, CombatSide.Defender })
            {
                Assert.Greater(
                    host.Units.Where(unit => unit.Side == side && !unit.IsFighter)
                        .Sum(unit => shots[unit.InstanceID]),
                    0,
                    "A capital side never fired."
                );
                Assert.Greater(
                    host.Units.Where(unit => unit.Side == side && unit.IsFighter)
                        .Sum(unit => shots[unit.InstanceID]),
                    0,
                    "A fighter side never fired."
                );
            }
            Assert.Greater(projectedShots, 0);
            Assert.AreEqual(
                hostRandom,
                ((SystemRandomProvider)peers.HostSimulation.Game.Random).CallCount
            );
            Assert.AreEqual(
                guestRandom,
                ((SystemRandomProvider)peers.GuestSimulation.Game.Random).CallCount
            );
            peers.AssertCampaignParticipantsUnchanged();
            Assert.Zero(peers.HostResults);
            Assert.Zero(peers.GuestResults);
            MultiplayerPacket terminalFrame = peers.ToGuest.Single(packet =>
                packet.Type == MultiplayerMessageType.TacticalFrame
            );

            peers.AdvanceViews(1);
            peers.Host.Pump();
            Assert.Zero(peers.CompletionActions);
            Assert.IsNull(peers.HostDriver.CommittedResult);
            peers.DeliverToGuest();
            Assert.IsTrue(guest.IsComplete);
            Assert.IsFalse(peers.GuestDriver.CompletionReady);
            Assert.Zero(peers.CompletionActions);
            peers.DeliverToHost();
            peers.HostDriver.Advance(0);
            peers.Host.Pump();
            Assert.IsNull(peers.Host.Failure, peers.Host.Failure?.ToString());
            Assert.IsNotNull(
                peers.HostCompletionOutcome,
                $"No host completion outcome: victory={peers.Host.HasDeclaredVictory}, "
                    + $"writable={peers.HostSimulation.CanExecuteCommands}, settled={peers.HostSimulation.Tick.IsSettled}, "
                    + $"pending={peers.HostCommands.HasPendingDecision}, active={peers.HostCommands.ActiveTacticalBattleID}."
            );
            Assert.IsTrue(
                peers.HostCompletionOutcome.Accepted,
                peers.HostCompletionOutcome.RejectionReason
            );
            Assert.IsNotNull(peers.HostDriver.CommittedResult);
            Assert.IsFalse(peers.HostDriver.CompletionReady);
            peers.Flush();

            Assert.IsTrue(peers.HostDriver.CompletionReady);
            Assert.IsTrue(peers.GuestDriver.CompletionReady);
            peers.AssertResultsEqual();
            Assert.AreEqual(1, peers.HostResults);
            Assert.AreEqual(1, peers.GuestResults);
            Assert.AreEqual(1, peers.HostTicks);
            Assert.AreEqual(1, peers.GuestTicks);
            Assert.AreEqual(1, peers.HostResumptions);
            Assert.AreEqual(1, peers.GuestResumptions);
            peers.AssertSharedCampaign();

            peers.GuestDriver.Receive(terminalFrame);
            peers.GuestDriver.Pump();
            peers.Guest.Receive(peers.LastCompletionCommit);
            peers.Guest.Pump();
            peers.Flush();
            Assert.AreEqual(1, peers.HostResults);
            Assert.AreEqual(1, peers.GuestResults);
            Assert.AreEqual(1, peers.GuestTicks);
            Assert.AreEqual(1, peers.GuestResumptions);
        }

        [Test]
        public void Advance_TwoWithdrawnEncounters_ClosesEachBattleAndResumesSuspendedDayOnce()
        {
            using PeerPair peers = new("FNEMP1", encounterCount: 2);
            peers.StartDay();
            string previousID = null;
            for (int encounter = 0; encounter < 2; encounter++)
            {
                peers.Begin(encounter == 0 ? MultiplayerRole.Host : MultiplayerRole.Guest, false);
                string battleID = peers.HostCommands.ActiveTacticalBattleID;
                Assert.AreNotEqual(previousID, battleID);
                previousID = battleID;
                for (int frame = 0; frame < 100 && peers.HostDriver.Session.IsArriving; frame++)
                    peers.Step(0.1f);
                peers.HostDriver.Session.Selection.QueueWithdrawal();
                peers.Flush();
                for (int frame = 0; frame < 10000 && !peers.HostDriver.CompletionReady; frame++)
                    peers.Step(0.1f);
                Assert.IsTrue(
                    peers.HostDriver.CompletionReady,
                    Describe(peers.HostDriver.Session, null)
                );
                Assert.IsTrue(peers.GuestDriver.CompletionReady);
                peers.AssertResultsEqual();
                peers.AssertSharedCampaign();
                Assert.AreEqual(encounter + 1, peers.HostResults);
                Assert.AreEqual(encounter + 1, peers.GuestResults);
                Assert.AreEqual(encounter == 0 ? 0 : 1, peers.HostTicks);
                Assert.AreEqual(encounter == 0 ? 0 : 1, peers.HostResumptions);
                peers.HostDriver.Dispose();
                peers.GuestDriver.Dispose();
            }
            Assert.AreEqual(1, peers.GuestTicks);
            Assert.AreEqual(1, peers.GuestResumptions);
        }

        [Test]
        public void Dispose_UnfinishedPeerPresentation_StopsBothDriversBeforeCampaignMutation()
        {
            using PeerPair peers = new("FNALL1");
            peers.StartDay();
            peers.Begin(MultiplayerRole.Host, false);
            int hostTime = peers.HostDriver.Session.SystemTime;

            peers.GuestDriver.Dispose();
            peers.DeliverToHost();
            peers.AdvanceViews(10);

            Assert.IsNotNull(peers.Host.Failure);
            Assert.IsNotNull(peers.Guest.Failure);
            Assert.AreEqual(hostTime, peers.HostDriver.Session.SystemTime);
            Assert.IsFalse(peers.HostSimulation.CanCaptureSettledState);
            Assert.IsFalse(peers.GuestSimulation.CanCaptureSettledState);
            Assert.Zero(peers.HostResults);
            Assert.Zero(peers.GuestResults);
            peers.AssertCampaignParticipantsUnchanged();
        }

        [Test]
        public void Advance_DelayedProjectionReceipt_PreservesBriefingElapsedTime()
        {
            using PeerPair peers = new("FNALL1", station: true);
            peers.StartDay();
            peers.Begin(MultiplayerRole.Host, false);
            List<int> messages = new();
            peers.HostDriver.Session.Audio.NotificationRequested += (message, _) =>
                messages.Add(message);
            for (int frame = 0; frame < 100 && !messages.Contains(32); frame++)
                peers.Step(0.1f);
            Assert.Contains(32, messages);
            peers.AdvanceViews(0.1f);
            Assert.IsTrue(
                peers.ToGuest.Any(packet => packet.Type == MultiplayerMessageType.TacticalFrame)
            );
            for (int frame = 0; frame < 70; frame++)
                peers.AdvanceViews(0.1f);
            Assert.IsFalse(messages.Contains(144));

            peers.Flush();
            peers.HostDriver.Advance(0);

            Assert.Contains(144, messages);
            Assert.IsNull(peers.Host.Failure);
        }

        /// <summary>
        /// Describes a bounded natural battle when its real participants fail to finish.
        /// </summary>
        /// <param name="session">The battle whose current participants are described.</param>
        /// <param name="shots">The recorded shot counts, or null when firing is not being observed.</param>
        /// <returns>The current battle time and participant state.</returns>
        private static string Describe(
            TacticalCombatSession session,
            IReadOnlyDictionary<string, int> shots
        ) =>
            $"System time {session.SystemTime}; "
            + string.Join(
                "; ",
                session.Units.Select(unit =>
                    $"{unit.InstanceID}: {unit.Status}, hull={unit.Systems.Hull}, position={unit.Position}, "
                    + $"order={session.GetGroup(unit)?.ActiveOrder}, target={session.GetGroup(unit)?.PrimaryTarget?.InstanceID}, shots={shots?.GetValueOrDefault(unit.InstanceID)}"
                )
            );

        /// <summary>
        /// Owns independent authored campaign sessions and their real ordered combat drivers.
        /// </summary>
        private sealed class PeerPair : IDisposable
        {
            private readonly string _hostFaction;
            private readonly string _guestFaction;
            private readonly string _initialization = Guid.NewGuid().ToString("N");
            private readonly List<IDisposable> _observations = new();
            private readonly Dictionary<string, (int Hull, int Squads, string Parent)> _entry =
                new();
            private uint _now;
            public GameSession HostSimulation { get; }
            public GameSession GuestSimulation { get; }
            public IEnumerable<GameSession> Simulations =>
                new[] { HostSimulation, GuestSimulation };
            public MultiplayerTurnProtocol Host { get; }
            public MultiplayerTurnProtocol Guest { get; }
            public MultiplayerTacticalDriver HostDriver { get; private set; }
            public MultiplayerTacticalDriver GuestDriver { get; private set; }
            public SpaceCombatCommands HostCommands =>
                HostSimulation.GetService<SpaceCombatCommands>();
            public SpaceCombatCommands GuestCommands =>
                GuestSimulation.GetService<SpaceCombatCommands>();
            public Queue<MultiplayerPacket> ToHost { get; } = new();
            public Queue<MultiplayerPacket> ToGuest { get; } = new();
            public int HostResults { get; private set; }
            public int GuestResults { get; private set; }
            public int HostTicks { get; private set; }
            public int GuestTicks { get; private set; }
            public int HostResumptions { get; private set; }
            public int GuestResumptions { get; private set; }
            public int CompletionActions { get; private set; }
            public PlayerRequestOutcome HostCompletionOutcome { get; private set; }
            public MultiplayerPacket LastCompletionCommit { get; private set; }

            /// <summary>
            /// Restores two owned campaigns from one authored graph before attaching real replay boundaries.
            /// </summary>
            /// <param name="hostFaction">The faction controlled by the authoritative host.</param>
            /// <param name="encounterCount">The number of independent contested systems to create.</param>
            /// <param name="station">Whether to include a defending battle station.</param>
            public PeerPair(string hostFaction, int encounterCount = 1, bool station = false)
            {
                _hostFaction = hostFaction;
                _guestFaction = hostFaction == "FNALL1" ? "FNEMP1" : "FNALL1";
                GameRoot source = CreateSource(hostFaction, encounterCount, station);
                MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(source);
                GameRoot hostGame = MultiplayerSnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash
                );
                GameRoot guestGame = MultiplayerSnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash
                );
                hostGame.UseDeterministicRuntimeIdentifiers = true;
                guestGame.UseDeterministicRuntimeIdentifiers = true;
                hostGame.LocalPlayerFactionID = _hostFaction;
                guestGame.LocalPlayerFactionID = _guestFaction;
                HostSimulation = new GameSession(hostGame, TestContent.Data);
                GuestSimulation = new GameSession(guestGame, TestContent.Data);
                Host = new MultiplayerTurnProtocol(
                    MultiplayerRole.Host,
                    _hostFaction,
                    _guestFaction,
                    HostSimulation,
                    new GameManager(() => hostGame, HostSimulation.Tick),
                    ToGuest.Enqueue,
                    _initialization
                );
                Guest = new MultiplayerTurnProtocol(
                    MultiplayerRole.Guest,
                    _hostFaction,
                    _guestFaction,
                    GuestSimulation,
                    new GameManager(() => guestGame, GuestSimulation.Tick),
                    ToHost.Enqueue,
                    _initialization
                );
                Host.ActionApplied += (request, outcome) =>
                    HandleApplied(MultiplayerRole.Host, request, outcome);
                Guest.ActionApplied += (request, outcome) =>
                    HandleApplied(MultiplayerRole.Guest, request, outcome);
                HostSimulation.Tick.TickCompleted += () => HostTicks++;
                GuestSimulation.Tick.TickCompleted += () => GuestTicks++;
                HostSimulation.Tick.CombatResumed += () => HostResumptions++;
                GuestSimulation.Tick.CombatResumed += () => GuestResumptions++;
                _observations.Add(
                    HostSimulation.Results.Observe<SpaceCombatResult>(results =>
                        HostResults += results.Count
                    )
                );
                _observations.Add(
                    GuestSimulation.Results.Observe<SpaceCombatResult>(results =>
                        GuestResults += results.Count
                    )
                );
            }

            /// <summary>
            /// Starts the shared day and leaves both real tick processors suspended at combat admission.
            /// </summary>
            public void StartDay()
            {
                Assert.IsTrue(Host.TryBeginTick(true));
                DeliverToGuest();
                Assert.IsTrue(Guest.TryBeginTick(false));
                HostSimulation.Tick.ProcessTick();
                GuestSimulation.Tick.ProcessTick();
                Flush();
                Assert.IsTrue(HostCommands.TryGetPendingCombat(out _));
                Assert.IsTrue(GuestCommands.TryGetPendingCombat(out _));
            }

            /// <summary>
            /// Sends both human decisions through the ordered production request executor.
            /// </summary>
            /// <param name="firstManual">The participant who first requests manual combat.</param>
            /// <param name="otherAuto">Whether the other participant requests automatic control.</param>
            public void Begin(MultiplayerRole firstManual, bool otherAuto)
            {
                Assert.IsTrue(HostCommands.TryGetPendingCombat(out PendingCombatResult pending));
                var first = Turns(firstManual)
                    .ExecuteAsync(
                        new BeginTacticalCombatRequest
                        {
                            PlanetID = pending.PlanetInstanceID,
                            Tick = pending.Tick,
                        }
                    );
                Flush();
                Assert.IsTrue(first.Result.Accepted, first.Result.RejectionReason);
                Assert.IsNull(HostCommands.ActiveTacticalCombat);
                MultiplayerRole other =
                    firstManual == MultiplayerRole.Host
                        ? MultiplayerRole.Guest
                        : MultiplayerRole.Host;
                PlayerRequest response = otherAuto
                    ? new ResolveCombatRequest
                    {
                        PlanetID = pending.PlanetInstanceID,
                        Tick = pending.Tick,
                        Retreat = false,
                    }
                    : new BeginTacticalCombatRequest
                    {
                        PlanetID = pending.PlanetInstanceID,
                        Tick = pending.Tick,
                    };
                var second = Turns(other).ExecuteAsync(response);
                Flush();
                Assert.IsTrue(second.Result.Accepted, second.Result.RejectionReason);
                Assert.IsNotNull(HostDriver);
                Assert.IsNotNull(GuestDriver);
                _entry.Clear();
                foreach (TacticalCombatUnit unit in HostDriver.Session.Units)
                    _entry.Add(
                        unit.InstanceID,
                        (
                            unit.Source is CapitalShip ship ? ship.CurrentHullStrength : 0,
                            unit.Source is Starfighter fighter ? fighter.CurrentSquadronSize : 0,
                            unit.Source.ParentInstanceID
                        )
                    );
            }

            /// <summary>
            /// Returns the selected authenticated campaign request owner.
            /// </summary>
            /// <param name="role">The participant whose request protocol is required.</param>
            /// <returns>The participant's ordered campaign request protocol.</returns>
            private MultiplayerTurnProtocol Turns(MultiplayerRole role) =>
                role == MultiplayerRole.Host ? Host : Guest;

            /// <summary>
            /// Returns the selected participant's real presentation driver.
            /// </summary>
            /// <param name="role">The participant whose presentation driver is required.</param>
            /// <returns>The participant's current battle presentation driver.</returns>
            public MultiplayerTacticalDriver Driver(MultiplayerRole role) =>
                role == MultiplayerRole.Host ? HostDriver : GuestDriver;

            /// <summary>
            /// Advances the independent monotonic host clock and both local presentation drivers.
            /// </summary>
            /// <param name="seconds">The elapsed presentation time in seconds.</param>
            public void AdvanceViews(float seconds)
            {
                _now = unchecked(_now + (uint)(seconds * 1000));
                HostDriver.Advance(seconds);
                GuestDriver.Advance(seconds);
            }

            /// <summary>
            /// Completes one real view frame and its delayed reliable packet exchange.
            /// </summary>
            /// <param name="seconds">The elapsed presentation time in seconds.</param>
            public void Step(float seconds)
            {
                AdvanceViews(seconds);
                Flush();
            }

            /// <summary>
            /// Receives campaign packets and tactical packets without replaying tactical commands on the guest.
            /// </summary>
            public void DeliverToGuest()
            {
                while (ToGuest.Count != 0)
                {
                    MultiplayerPacket packet = ToGuest.Dequeue();
                    if (
                        packet.Type >= MultiplayerMessageType.TacticalReady
                        && packet.Type <= MultiplayerMessageType.TacticalMovieReceipt
                    )
                        GuestDriver.Receive(packet);
                    else
                    {
                        if (packet.Type == MultiplayerMessageType.PlayerActionCommit)
                        {
                            var metadata = UnityEngine.JsonUtility.FromJson<CommitMetadata>(
                                packet.Metadata
                            );
                            if (metadata.kind == PlayerRequestKind.CompleteTacticalCombat)
                                LastCompletionCommit = packet;
                        }
                        Guest.Receive(packet);
                    }
                }
                Guest.Pump();
                GuestDriver?.Pump();
            }

            /// <summary>
            /// Receives guest traffic before invoking either authoritative command pump.
            /// </summary>
            public void DeliverToHost()
            {
                while (ToHost.Count != 0)
                {
                    MultiplayerPacket packet = ToHost.Dequeue();
                    if (
                        packet.Type >= MultiplayerMessageType.TacticalReady
                        && packet.Type <= MultiplayerMessageType.TacticalMovieReceipt
                    )
                        HostDriver.Receive(packet);
                    else
                        Host.Receive(packet);
                }
                Host.Pump();
                HostDriver?.Pump();
            }

            /// <summary>
            /// Drains a bounded reliable exchange and exposes synchronization failures immediately.
            /// </summary>
            public void Flush()
            {
                for (int pass = 0; pass < 32; pass++)
                {
                    DeliverToHost();
                    DeliverToGuest();
                    Assert.IsNull(Host.Failure, Host.Failure?.ToString());
                    Assert.IsNull(Guest.Failure, Guest.Failure?.ToString());
                    if (ToHost.Count == 0 && ToGuest.Count == 0)
                        return;
                }
                Assert.Fail("The real driver pair did not settle its ordered packet exchange.");
            }

            /// <summary>
            /// Targets active opponents through each local view's semantic input boundary.
            /// </summary>
            public void TargetManualForces()
            {
                foreach (MultiplayerTacticalDriver driver in new[] { HostDriver, GuestDriver })
                {
                    TacticalCombatSession session = driver.Session;
                    if (session.IsArriving || session.IsComplete)
                        continue;
                    for (int category = 0; category < 12; category++)
                    {
                        TacticalCombatGroup group =
                            category < 8
                                ? session.GetObjectGroup(session.PlayerSide, category)
                                : session.GetFighterGroup(session.PlayerSide, category - 8);
                        if (
                            group?.RequestedMembers.Any(unit => unit.IsTargetable) != true
                            || group.PrimaryTarget?.IsTargetable == true
                            || group.RequestedTargets.Any(unit => unit.IsTargetable)
                        )
                            continue;
                        TacticalCombatUnit target =
                            session.Units.FirstOrDefault(unit =>
                                unit.Side != session.PlayerSide
                                && unit.IsTargetable
                                && unit.IsFighter == group.IsFighterGroup
                            )
                            ?? session.Units.FirstOrDefault(unit =>
                                unit.Side != session.PlayerSide && unit.IsTargetable
                            );
                        if (target == null)
                            continue;
                        session.Selection.QueueClear();
                        if (category < 8)
                            session.Selection.QueueObjectCategory(category);
                        else
                            session.Selection.QueueFighterCategory(category - 8);
                        session.Selection.QueueObject(target, false);
                    }
                }
            }

            /// <summary>
            /// Verifies projection and simulation leave all live campaign participants untouched until commit.
            /// </summary>
            public void AssertCampaignParticipantsUnchanged()
            {
                foreach (GameSession simulation in Simulations)
                foreach (var item in _entry)
                {
                    var node =
                        simulation.Game.GetSceneNodeByInstanceID<Rebellion.SceneGraph.ISceneNode>(
                            item.Key
                        );
                    Assert.IsNotNull(node);
                    Assert.AreEqual(item.Value.Parent, node.ParentInstanceID);
                    if (node is CapitalShip ship)
                        Assert.AreEqual(item.Value.Hull, ship.CurrentHullStrength);
                    if (node is Starfighter fighter)
                        Assert.AreEqual(item.Value.Squads, fighter.CurrentSquadronSize);
                }
            }

            /// <summary>
            /// Compares full imported hull, component, squadron and host assignments from both local campaign results.
            /// </summary>
            public void AssertResultsEqual()
            {
                SpaceCombatResult host = HostDriver.CommittedResult;
                SpaceCombatResult guest = GuestDriver.CommittedResult;
                Assert.AreEqual(host.Winner, guest.Winner);
                Assert.AreEqual(host.AttackerOutcome, guest.AttackerOutcome);
                Assert.AreEqual(host.DefenderOutcome, guest.DefenderOutcome);
                CollectionAssert.AreEqual(
                    host.ShipDamage.OrderBy(item => item.Ship.InstanceID)
                        .Select(item =>
                            (
                                item.Ship.InstanceID,
                                item.HullBefore,
                                item.HullAfter,
                                item.SystemDamage.Shields,
                                item.SystemDamage.Weapons,
                                item.SystemDamage.TractorBeam,
                                item.SystemDamage.Engines,
                                item.SystemDamage.PrimaryHyperdrive,
                                item.SystemDamage.BackupHyperdrive
                            )
                        ),
                    guest
                        .ShipDamage.OrderBy(item => item.Ship.InstanceID)
                        .Select(item =>
                            (
                                item.Ship.InstanceID,
                                item.HullBefore,
                                item.HullAfter,
                                item.SystemDamage.Shields,
                                item.SystemDamage.Weapons,
                                item.SystemDamage.TractorBeam,
                                item.SystemDamage.Engines,
                                item.SystemDamage.PrimaryHyperdrive,
                                item.SystemDamage.BackupHyperdrive
                            )
                        )
                );
                CollectionAssert.AreEqual(
                    host.FighterLosses.OrderBy(item => item.Fighter.InstanceID)
                        .Select(item =>
                            (item.Fighter.InstanceID, item.SquadsBefore, item.SquadsAfter)
                        ),
                    guest
                        .FighterLosses.OrderBy(item => item.Fighter.InstanceID)
                        .Select(item =>
                            (item.Fighter.InstanceID, item.SquadsBefore, item.SquadsAfter)
                        )
                );
                CollectionAssert.AreEqual(
                    host.FighterRecovery.OrderBy(item => item.Fighter.InstanceID)
                        .Select(item => (item.Fighter.InstanceID, item.Carrier?.InstanceID)),
                    guest
                        .FighterRecovery.OrderBy(item => item.Fighter.InstanceID)
                        .Select(item => (item.Fighter.InstanceID, item.Carrier?.InstanceID))
                );
            }

            /// <summary>
            /// Requires both production replay owners to retain the same final campaign graph.
            /// </summary>
            public void AssertSharedCampaign()
            {
                Assert.IsTrue(HostSimulation.Game.UseDeterministicRuntimeIdentifiers);
                Assert.IsTrue(GuestSimulation.Game.UseDeterministicRuntimeIdentifiers);
                Assert.AreEqual(HostSimulation.Game.IDCounter, GuestSimulation.Game.IDCounter);
                string host = MultiplayerSnapshotCodec.ComputeStateFingerprint(HostSimulation.Game);
                string guest = MultiplayerSnapshotCodec.ComputeStateFingerprint(
                    GuestSimulation.Game
                );
                Assert.AreEqual(host, guest, host == guest ? null : DescribeCampaignDifference());
            }

            /// <summary>
            /// Reports the first serialized model difference while retaining the complete fingerprint assertion.
            /// </summary>
            /// <returns>The independent peers' first differing XML region.</returns>
            private string DescribeCampaignDifference()
            {
                string host = System.Text.Encoding.UTF8.GetString(
                    MultiplayerSnapshotCodec.Serialize(typeof(GameRoot), HostSimulation.Game)
                );
                string guest = System.Text.Encoding.UTF8.GetString(
                    MultiplayerSnapshotCodec.Serialize(typeof(GameRoot), GuestSimulation.Game)
                );
                int index = 0;
                while (index < host.Length && index < guest.Length && host[index] == guest[index])
                    index++;
                int start = Math.Max(0, index - 160);
                return $"First serialized difference at {index}. Host: {host.Substring(start, Math.Min(420, host.Length - start))}; "
                    + $"guest: {guest.Substring(start, Math.Min(420, guest.Length - start))}";
            }

            /// <summary>
            /// Creates the same driver the coordinator creates after an accepted ordered human response.
            /// </summary>
            /// <param name="role">The participant whose campaign applied the request.</param>
            /// <param name="request">The ordered campaign request that was applied.</param>
            /// <param name="outcome">The local request execution outcome.</param>
            private void HandleApplied(
                MultiplayerRole role,
                PlayerRequest request,
                PlayerRequestOutcome outcome
            )
            {
                if (request is CompleteTacticalCombatRequest)
                {
                    CompletionActions++;
                    if (role == MultiplayerRole.Host)
                        HostCompletionOutcome = outcome;
                    return;
                }
                if (
                    !outcome.Accepted
                    || request is not (BeginTacticalCombatRequest or ResolveCombatRequest)
                )
                    return;
                bool host = role == MultiplayerRole.Host;
                GameSession simulation = host ? HostSimulation : GuestSimulation;
                SpaceCombatCommands commands = simulation.GetService<SpaceCombatCommands>();
                if (
                    commands.ActiveTacticalCombat == null
                    || ReferenceEquals(Driver(role)?.Session, commands.ActiveTacticalCombat)
                )
                    return;
                Driver(role)?.Release();
                MultiplayerTacticalDriver driver = new(
                    role,
                    _initialization,
                    commands.ActiveTacticalBattleID,
                    commands.ActiveTacticalCombat,
                    commands,
                    Turns(role),
                    host ? ToGuest.Enqueue : ToHost.Enqueue,
                    () => _now
                );
                if (host)
                    HostDriver = driver;
                else
                    GuestDriver = driver;
                driver.Prepare(new Arrival());
            }

            /// <summary>
            /// Releases connection callbacks before independent campaign sessions and result subscriptions.
            /// </summary>
            public void Dispose()
            {
                HostDriver?.Release();
                GuestDriver?.Release();
                Host.Dispose();
                Guest.Dispose();
                foreach (IDisposable observation in _observations)
                    observation.Dispose();
                HostSimulation.Dispose();
                GuestSimulation.Dispose();
            }

            /// <summary>
            /// Creates operational authored carriers and squadrons in independent contested systems.
            /// </summary>
            /// <param name="hostFaction">The faction controlled by the authoritative host.</param>
            /// <param name="encounterCount">The number of independent contested systems to create.</param>
            /// <param name="station">Whether to include a defending battle station.</param>
            /// <returns>The campaign graph shared by both participants at admission.</returns>
            private static GameRoot CreateSource(
                string hostFaction,
                int encounterCount,
                bool station
            )
            {
                GameRoot game = TestGame.Create(
                    new GameSummary
                    {
                        Seed = 314159,
                        PlayerFactionID = hostFaction,
                        VictoryCondition = GameVictoryCondition.Conquest,
                    },
                    TestContent.Data.GameConfig.CreateCopy()
                );
                game.Metadata.CampaignID = Guid.NewGuid().ToString("N");
                game.SetGameSpeed(TickSpeed.Fast);
                game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
                {
                    HostUserID = "host-user",
                    GuestUserID = "guest-user",
                    HostFactionID = hostFaction,
                    GuestFactionID = hostFaction == "FNALL1" ? "FNEMP1" : "FNALL1",
                };
                game.UseDeterministicRuntimeIdentifiers = true;
                PlanetSector sector = new()
                {
                    InstanceID = "combat-sector",
                    SectorType = PlanetSectorType.Core,
                };
                game.AttachNode(sector, game.Galaxy);
                foreach (string id in new[] { "FNALL1", "FNEMP1" })
                {
                    Faction faction = new() { InstanceID = id, HQInstanceID = "home-" + id };
                    TestContent
                        .Data.Factions.Single(item => item.InstanceID == id)
                        .CopyDefinitionTo(faction);
                    faction.HQInstanceID = "home-" + id;
                    game.GetFactions().Add(faction);
                    game.SetFactionController(id, id + "-player", PlayerControllerType.Human);
                    Planet home = new()
                    {
                        InstanceID = "home-" + id,
                        OwnerInstanceID = id,
                        IsColonized = true,
                        IsHeadquarters = true,
                        PositionX = id == "FNALL1" ? -300 : 300,
                    };
                    home.SetPopularSupport(id, 100);
                    game.AttachNode(home, sector);
                }
                PopulateScenarioOfficers(game);
                UnitFactory units = new(
                    TestContent.Data.Buildings,
                    TestContent.Data.CapitalShips,
                    TestContent.Data.Starfighters,
                    TestContent.Data.Regiments,
                    TestContent.Data.SpecialForces,
                    TestContent.Data.TacticalCombat
                );
                for (int encounter = 0; encounter < encounterCount; encounter++)
                {
                    Planet planet = new()
                    {
                        InstanceID = "battle-" + encounter,
                        OwnerInstanceID = "FNALL1",
                        IsColonized = true,
                        PositionX = encounter * 20,
                    };
                    planet.SetPopularSupport("FNALL1", 100);
                    game.AttachNode(planet, sector);
                    foreach (string id in new[] { "FNALL1", "FNEMP1" })
                    {
                        Fleet fleet = EntityFactory.CreateFleet($"fleet-{encounter}-{id}", id);
                        game.AttachNode(fleet, planet);
                        CapitalShip carrier = units.Create<CapitalShip>(
                            id == "FNEMP1" ? "CSEM006" : "ALCS008",
                            id
                        );
                        carrier.InstanceID = fleet.InstanceID + "-carrier";
                        game.AttachNode(carrier, fleet);
                        for (int squadron = 0; squadron < 2; squadron++)
                        {
                            Starfighter fighter = units.Create<Starfighter>(
                                id == "FNEMP1" ? "SFEM01" : "SFAL02",
                                id
                            );
                            fighter.InstanceID = fleet.InstanceID + "-fighter-" + squadron;
                            game.AttachNode(fighter, carrier);
                        }
                        if (station && id == "FNEMP1")
                        {
                            CapitalShip battleStation = units.Create<CapitalShip>(
                                TestContent
                                    .Data.CapitalShips.Single(ship => ship.CanDestroyPlanets)
                                    .TypeID,
                                id
                            );
                            battleStation.InstanceID = fleet.InstanceID + "-station";
                            game.AttachNode(battleStation, fleet);
                        }
                    }
                }
                VerifyAuthoredBindings(game);
                return game;
            }

            /// <summary>
            /// Deploys the scenario's entire guaranteed named roster at its faction homes outside combat.
            /// Ownership follows authored ownership or the sole recruiting faction, as in officer generation.
            /// </summary>
            /// <param name="game">The independent battle scenario receiving its required story actors.</param>
            private static void PopulateScenarioOfficers(GameRoot game)
            {
                Dictionary<string, Officer> templates = TestContent.Data.Officers.ToDictionary(
                    officer => officer.InstanceID,
                    StringComparer.Ordinal
                );
                Dictionary<string, Planet> homes = game.GetSceneNodesByType<Planet>()
                    .ToDictionary(planet => planet.InstanceID, StringComparer.Ordinal);
                foreach (
                    StartingOfficerRule rule in TestContent
                        .Data
                        .GenerationConfig
                        .Officers
                        .StartingOfficers
                )
                {
                    Officer officer = (Officer)templates[rule.OfficerInstanceID].CreateCopy();
                    officer.OwnerInstanceID ??= officer.RecruitingFactionInstanceIDs.Single();
                    game.AttachNode(officer, homes["home-" + officer.OwnerInstanceID]);
                }
            }

            /// <summary>
            /// Resolves every authored event-level binding before snapshotting the fixture, without scheduling
            /// actions or consuming the campaign random stream.
            /// </summary>
            /// <param name="game">The complete minimal world whose eager story prerequisites are checked.</param>
            private static void VerifyAuthoredBindings(GameRoot game)
            {
                IRandomNumberProvider selectionRandom = new SystemRandomProvider(123);
                foreach (var gameEvent in TestContent.Data.GameEvents)
                {
                    GameEvaluationContext context = new(gameEvent.InstanceID);
                    foreach (var binding in gameEvent.Bindings)
                        Assert.DoesNotThrow(
                            () => GameBindingResolver.Bind(binding, game, selectionRandom, context),
                            $"Authored binding prerequisite: {gameEvent.InstanceID}/{binding.As}"
                        );
                }
            }
        }

        private sealed class Arrival : ITacticalCombatArrivalPresentation
        {
            /// <summary>
            /// Accepts pause presentation changes without requiring a renderer.
            /// </summary>
            /// <param name="active">Whether the pause strobe is active.</param>
            public void SetPauseStrobe(bool active) { }

            /// <summary>
            /// Accepts a unit presentation reset without requiring a renderer.
            /// </summary>
            /// <param name="unitIndex">The index of the unit being reset.</param>
            public void Reset(int unitIndex) { }

            /// <summary>
            /// Accepts an arrival transform without requiring a renderer.
            /// </summary>
            /// <param name="unitIndex">The index of the unit whose transform changed.</param>
            /// <param name="position">The unit's arrival position.</param>
            /// <param name="direction">The unit's arrival direction.</param>
            public void SetTransform(
                int unitIndex,
                System.Numerics.Vector4 position,
                System.Numerics.Vector4 direction
            ) { }

            /// <summary>
            /// Accepts a unit presentation refresh without requiring a renderer.
            /// </summary>
            /// <param name="unitIndex">The index of the unit being refreshed.</param>
            public void Refresh(int unitIndex) { }
        }

        [Serializable]
        private sealed class CommitMetadata
        {
            public PlayerRequestKind kind;
        }
    }
}
