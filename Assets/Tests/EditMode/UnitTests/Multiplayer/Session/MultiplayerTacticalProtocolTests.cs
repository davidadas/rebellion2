using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerTacticalProtocolTests
    {
        [Test]
        public void PublishFrame_BothViewsReady_WaitsForEachProjectionReceipt()
        {
            using PeerPair peers = new();
            peers.Host.Ready();
            peers.Deliver();
            peers.Host.PublishFrame();
            Assert.Zero(peers.Captures);
            peers.Guest.Ready();
            peers.Deliver();

            peers.Host.PublishFrame();
            peers.Host.PublishFrame();

            Assert.AreEqual(1, peers.Captures);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.Zero(peers.Applied.Count);
            peers.Deliver();
            Assert.AreEqual(1, peers.Applied.Single().Sequence);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [Test]
        public void ExecuteAsync_PeerStillLoading_DefersCommandUntilBothViewsAreReady()
        {
            using PeerPair peers = new();
            peers.Host.Ready();
            var completion = peers.Host.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Deliver();

            Assert.IsFalse(completion.IsCompleted);
            Assert.IsEmpty(peers.Actors);
            peers.Guest.Ready();
            peers.Deliver();
            Assert.IsTrue(completion.Result.Accepted);
            CollectionAssert.AreEqual(new[] { CombatSide.Attacker }, peers.Actors);
        }

        [Test]
        public void ExecuteAsync_BothPlayers_AuthenticatesTheSideWithoutReplicaExecution()
        {
            using PeerPair peers = new();
            peers.Ready();
            var host = peers.Host.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Order,
                    Order = TacticalCombatOrder.Stop,
                }
            );
            var guest = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Deliver();

            Assert.IsTrue(host.Result.Accepted);
            Assert.IsTrue(guest.Result.Accepted);
            CollectionAssert.AreEqual(
                new[] { CombatSide.Attacker, CombatSide.Defender },
                peers.Actors
            );
            Assert.Zero(peers.Applied.Count);
        }

        [Test]
        public void Receive_GuestClaimsHostRole_UsesConnectionRoleInstead()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Pause,
                    Paused = true,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            envelope.actor = MultiplayerRole.Host;
            peers.Host.Receive(
                new MultiplayerPacket(request.Type, JsonUtility.ToJson(envelope), request.Payload)
            );

            peers.Deliver();

            Assert.IsTrue(completion.Result.Accepted);
            CollectionAssert.AreEqual(new[] { CombatSide.Defender }, peers.Actors);
        }

        [Test]
        public void Receive_DuplicateCommand_RepliesWithoutApplyingAgain()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Peek();
            peers.Deliver();

            peers.Host.Receive(request);
            peers.Deliver();

            Assert.IsTrue(completion.Result.Accepted);
            Assert.AreEqual(1, peers.Actors.Count);
            Assert.IsNull(peers.Host.Failure);
        }

        [Test]
        public void Receive_DuplicateCommandChangesParameters_StopsBattle()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Peek();
            peers.Deliver();

            peers.Host.Receive(
                new MultiplayerPacket(
                    request.Type,
                    request.Metadata,
                    System.Text.Encoding.UTF8.GetBytes(
                        JsonUtility.ToJson(
                            new TacticalCombatNetworkCommand
                            {
                                Kind = TacticalCombatNetworkCommandKind.ToggleAutomation,
                            }
                        )
                    )
                )
            );
            peers.Host.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.AreEqual(1, peers.Actors.Count);
        }

        [Test]
        public void Receive_DuplicateProjection_DoesNotReplayItsEffects()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            MultiplayerPacket frame = peers.ToGuest.Peek();
            peers.Deliver();

            peers.Guest.Receive(frame);
            peers.Deliver();

            Assert.AreEqual(1, peers.Applied.Count);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Receive_StaleInitializationOrBattle_DropsBeforeReadingPayload(
            bool initialization
        )
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            if (initialization)
                envelope.initializationID = Guid.NewGuid().ToString("N");
            else
                envelope.battleID = Guid.NewGuid().ToString("N");

            peers.Host.Receive(
                new MultiplayerPacket(
                    request.Type,
                    JsonUtility.ToJson(envelope),
                    new byte[] { 0xff }
                )
            );
            peers.Host.Pump();

            Assert.IsNull(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_GapInGuestCommandIDs_RejectsBeforeMutation()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            envelope.requestID++;

            peers.Host.Receive(
                new MultiplayerPacket(request.Type, JsonUtility.ToJson(envelope), request.Payload)
            );
            peers.Host.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_ProjectionSkipsSequence_RejectsBeforeApplyingEffects()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            MultiplayerPacket packet = peers.ToGuest.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(packet.Metadata);
            envelope.sequence++;

            peers.Guest.Receive(
                new MultiplayerPacket(packet.Type, JsonUtility.ToJson(envelope), packet.Payload)
            );
            peers.Guest.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.Applied);
            Assert.IsFalse(peers.Host.CanAdvance);
        }

        [Test]
        public void Receive_ReceiptAcknowledgesFutureProjection_DoesNotReleaseBackpressure()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Guest.Receive(peers.ToGuest.Dequeue());
            peers.Guest.Pump();
            MultiplayerPacket packet = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(packet.Metadata);
            envelope.sequence++;

            peers.Host.Receive(new MultiplayerPacket(packet.Type, JsonUtility.ToJson(envelope)));

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.AreEqual(1, peers.Captures);
        }

        [Test]
        public void AcknowledgeMovie_OnlyOnePlayerFinished_RetainsSharedPause()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.HostMovieID = peers.GuestMovieID = 7;
            peers.Guest.AcknowledgeMovie(7);
            peers.Deliver();
            Assert.AreEqual(7, peers.HostMovieID);
            Assert.Zero(peers.CompletedMovies);

            peers.Host.AcknowledgeMovie(7);
            peers.Host.Pump();
            peers.Guest.AcknowledgeMovie(7);
            peers.Host.Pump();

            Assert.Zero(peers.HostMovieID);
            Assert.AreEqual(1, peers.CompletedMovies);
            peers.Host.PublishFrame();
            peers.Deliver();
            Assert.Zero(peers.GuestMovieID);
        }

        [Test]
        public void Dispose_UnconfirmedCommand_CompletesWithUnknownOutcome()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Guest.Dispose();

            Assert.IsFalse(completion.Result.OutcomeKnown);
            Assert.IsFalse(completion.Result.Accepted);
        }

        [Test]
        public void PublishFrame_TransportFailure_StopsWithoutAdvancingOrCapturingAgain()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.RejectHostSends = true;

            peers.Host.PublishFrame();
            peers.Host.PublishFrame();

            Assert.IsInstanceOf<IOException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.AreEqual(1, peers.Captures);
        }

        [Serializable]
        private sealed class WireEnvelope
        {
            public string initializationID;
            public string battleID;
            public long requestID;
            public long sequence;
            public MultiplayerRole actor;
            public bool accepted;
            public int movieID;
        }

        private sealed class PeerPair : IDisposable
        {
            public Queue<MultiplayerPacket> ToHost { get; } = new();
            public Queue<MultiplayerPacket> ToGuest { get; } = new();
            public List<CombatSide> Actors { get; } = new();
            public List<TacticalCombatNetworkFrame> Applied { get; } = new();
            public MultiplayerTacticalProtocol Host { get; }
            public MultiplayerTacticalProtocol Guest { get; }
            public int Captures { get; private set; }
            public int HostMovieID { get; set; }
            public int GuestMovieID { get; set; }
            public int CompletedMovies { get; private set; }
            public bool RejectHostSends { get; set; }

            /// <summary>
            /// Creates authenticated peers sharing one battle identity and queued transport.
            /// </summary>
            public PeerPair()
            {
                string initialization = Guid.NewGuid().ToString("N");
                string battle = Guid.NewGuid().ToString("N");
                Host = new MultiplayerTacticalProtocol(
                    MultiplayerRole.Host,
                    initialization,
                    battle,
                    CombatSide.Attacker,
                    (actor, _) =>
                    {
                        Actors.Add(actor);
                        return true;
                    },
                    sequence =>
                    {
                        Captures++;
                        return new TacticalCombatNetworkFrame
                        {
                            Sequence = sequence,
                            MovieID = HostMovieID,
                        };
                    },
                    _ => Assert.Fail("The host must not import a replica frame."),
                    () => HostMovieID,
                    id =>
                    {
                        Assert.AreEqual(HostMovieID, id);
                        HostMovieID = 0;
                        CompletedMovies++;
                        return true;
                    },
                    packet =>
                    {
                        if (RejectHostSends)
                            throw new IOException("Transport failed.");
                        ToGuest.Enqueue(packet);
                    }
                );
                Guest = new MultiplayerTacticalProtocol(
                    MultiplayerRole.Guest,
                    initialization,
                    battle,
                    CombatSide.Attacker,
                    (_, _) =>
                        throw new AssertionException("The guest must not execute combat commands."),
                    _ =>
                        throw new AssertionException(
                            "The guest must not capture authoritative combat."
                        ),
                    frame =>
                    {
                        Applied.Add(frame);
                        GuestMovieID = frame.MovieID;
                    },
                    () => GuestMovieID,
                    _ =>
                        throw new AssertionException(
                            "The guest must not release the authoritative movie."
                        ),
                    ToHost.Enqueue
                );
            }

            /// <summary>
            /// Announces both local views as ready and delivers their readiness exchange.
            /// </summary>
            public void Ready()
            {
                Host.Ready();
                Guest.Ready();
                Deliver();
            }

            /// <summary>
            /// Drains the bounded ordered packet exchange without advancing combat.
            /// </summary>
            public void Deliver()
            {
                for (int round = 0; round < 8; round++)
                {
                    while (ToHost.Count != 0)
                        Host.Receive(ToHost.Dequeue());
                    Host.Pump();
                    while (ToGuest.Count != 0)
                        Guest.Receive(ToGuest.Dequeue());
                    Guest.Pump();
                    if (ToHost.Count == 0 && ToGuest.Count == 0)
                        return;
                }
                Assert.Fail("The battle packet exchange did not settle.");
            }

            /// <summary>
            /// Releases both protocol endpoints and their pending requests.
            /// </summary>
            public void Dispose()
            {
                Host.Dispose();
                Guest.Dispose();
            }
        }
    }
}
