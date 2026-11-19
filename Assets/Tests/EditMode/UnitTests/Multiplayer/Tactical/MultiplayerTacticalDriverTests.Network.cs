using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer.Tactical
{
    public sealed partial class MultiplayerTacticalDriverTests
    {
        [TestCase(100, 5)]
        [TestCase(250, 25)]
        [TestCase(100, 100)]
        public void Advance_ImpairedSnapshotDelivery_CompletesOneSharedBattleWithoutLosingEffects(
            int roundTripMilliseconds,
            int snapshotLossPercent
        )
        {
            using PeerPair peers = new("FNEMP1");
            peers.StartDay();
            peers.Begin(MultiplayerRole.Guest, false);
            int hostShots = 0;
            int guestShots = 0;
            peers.HostDriver.Session.ShotFired += _ => hostShots++;
            peers.GuestDriver.Session.ShotFired += _ => guestShots++;
            ImpairedBattleLink link = new(roundTripMilliseconds, snapshotLossPercent);

            for (int frame = 0; frame < 60000 && !peers.HostDriver.CompletionReady; frame++)
            {
                if (frame % 10 == 0 && !peers.HostDriver.Session.IsComplete)
                    peers.TargetManualForces();
                if (frame == 3000 && !peers.HostDriver.Session.IsComplete)
                    peers.HostDriver.Session.Selection.QueueWithdrawal();
                peers.AdvanceViews(0.05f);
                link.Pump(peers);
                Assert.IsNull(peers.Host.Failure, peers.Host.Failure?.ToString());
                Assert.IsNull(peers.Guest.Failure, peers.Guest.Failure?.ToString());
            }
            for (int frame = 0; frame < 40 && !peers.GuestDriver.CompletionReady; frame++)
            {
                peers.AdvanceViews(0.05f);
                link.Pump(peers);
            }

            Assert.IsTrue(
                peers.HostDriver.CompletionReady,
                Describe(peers.HostDriver.Session, null)
            );
            Assert.IsTrue(peers.GuestDriver.CompletionReady);
            Assert.Greater(hostShots, 0);
            Assert.AreEqual(hostShots, guestShots, "Reliable firing edges were lost or replayed.");
            Assert.Greater(link.DroppedSnapshots, 0);
            Assert.AreEqual(1, peers.HostResults);
            Assert.AreEqual(1, peers.GuestResults);
            Assert.AreEqual(
                2,
                peers.CompletionActions,
                "One ordered completion must execute once on each peer."
            );
            peers.AssertResultsEqual();
            peers.AssertSharedCampaign();
            TestContext.WriteLine(
                $"RTT={roundTripMilliseconds} ms, snapshot loss={snapshotLossPercent}%, dropped={link.DroppedSnapshots}, shots={hostShots}, peak queued={link.MaximumQueued}"
            );
        }

        /// <summary>
        /// Exercises the real domain drivers through independently delayed reliable and replaceable packet lanes.
        /// Loss applies to snapshots; reliable delivery models retransmission as additional ordered delay.
        /// </summary>
        private sealed class ImpairedBattleLink
        {
            private readonly int _oneWayFrames;
            private readonly int _lossPercent;
            private readonly Random _random = new(734);
            private readonly List<(
                int Due,
                int Order,
                bool ToHost,
                MultiplayerPacket Packet
            )> _pending = new();
            private int _frame;
            private int _order;
            private int _hostReliableDue;
            private int _guestReliableDue;
            public int DroppedSnapshots { get; private set; }
            public int MaximumQueued { get; private set; }

            /// <summary>
            /// Creates a deterministic impaired link with a 50 ms local update interval.
            /// </summary>
            /// <param name="roundTripMilliseconds">The base two-way network delay.</param>
            /// <param name="lossPercent">The percentage of independently replaceable snapshots to discard.</param>
            public ImpairedBattleLink(int roundTripMilliseconds, int lossPercent)
            {
                _oneWayFrames = Math.Max(1, (int)Math.Ceiling(roundTripMilliseconds / 100.0));
                _lossPercent = lossPercent;
            }

            /// <summary>
            /// Admits newly sent work, then delivers only packets whose independent network delay has elapsed.
            /// </summary>
            /// <param name="peers">The two real campaign and tactical runtimes.</param>
            public void Pump(PeerPair peers)
            {
                Capture(peers.ToHost, true);
                Capture(peers.ToGuest, false);
                _frame++;
                var ready = _pending
                    .Where(item => item.Due <= _frame)
                    .OrderBy(item => item.Due)
                    .ThenBy(item => item.Order)
                    .ToArray();
                _pending.RemoveAll(item => item.Due <= _frame);
                foreach (var item in ready)
                {
                    if (item.ToHost)
                    {
                        peers.ToHost.Enqueue(item.Packet);
                        peers.DeliverToHost();
                    }
                    else
                    {
                        peers.ToGuest.Enqueue(item.Packet);
                        peers.DeliverToGuest();
                    }
                    Capture(peers.ToHost, true);
                    Capture(peers.ToGuest, false);
                }
            }

            /// <summary>
            /// Models jitter, occasional reliable retransmission delay, and duplicated/reordered snapshot delivery.
            /// </summary>
            /// <param name="source">The outgoing application packets.</param>
            /// <param name="toHost">Whether this direction reaches the authority.</param>
            private void Capture(Queue<MultiplayerPacket> source, bool toHost)
            {
                while (source.Count != 0)
                {
                    MultiplayerPacket packet = source.Dequeue();
                    bool replaceable = packet.Type == MultiplayerMessageType.TacticalSnapshot;
                    if (replaceable && _random.Next(100) < _lossPercent)
                    {
                        DroppedSnapshots++;
                        continue;
                    }
                    int due = _frame + _oneWayFrames + _random.Next(3);
                    if (!replaceable)
                    {
                        if (_random.Next(20) == 0)
                            due += 3;
                        due = Math.Max(due, toHost ? _hostReliableDue : _guestReliableDue);
                        if (toHost)
                            _hostReliableDue = due;
                        else
                            _guestReliableDue = due;
                    }
                    _pending.Add((due, _order++, toHost, packet));
                    if (replaceable && _random.Next(8) == 0)
                        _pending.Add((due + 3, _order++, toHost, packet));
                    MaximumQueued = Math.Max(MaximumQueued, _pending.Count);
                }
            }
        }
    }
}
