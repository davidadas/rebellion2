using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation.Tactical;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Simulation.Tactical
{
    [TestFixture]
    public sealed class TacticalCarrierOperationsTests
    {
        private ActiveBattle _battle;
        private CapitalShip _sourceCarrier;
        private CombatUnit _carrier;
        private CombatUnit[] _fighters;
        private TacticalCarrierOperations _operations;

        /// <summary>
        /// Creates one carrier with a full-strength squadron represented by a single battle unit.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _battle = new ActiveBattle { Kind = BattleKind.Space, FlightRandomSeed = 71 };
            _sourceCarrier = new CapitalShip
            {
                InstanceID = "CARRIER",
                OwnerInstanceID = "OWNER",
                StarfighterCapacity = 3,
            };
            _carrier = _battle.AddCombatants(_sourceCarrier)[0];
            _fighters = AddSquadron("SQUADRON");
            _operations = CreateOperations(_battle, _carrier, new IntervalRandom(0));
        }

        /// <summary>
        /// Checks each release interval launches one unit without expanding its squadron strength.
        /// </summary>
        /// <param name="draw">The inclusive interval draw.</param>
        /// <param name="delay">The expected unpaused delay.</param>
        [TestCase(0, 5f)]
        [TestCase(8, 13f)]
        public void Advance_ReleaseInterval_LaunchesOneSquadronUnit(int draw, float delay)
        {
            _operations = CreateOperations(_battle, _carrier, new IntervalRandom(draw));
            int cues = 0;
            _operations.SquadronLaunched += _ => cues++;
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(delay - 0.25f);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Queued)
            );
            _operations.Advance(0.25f);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Launching)
            );
            Assert.That(cues, Is.EqualTo(1));
            Assert.That(_carrier.LaunchQueue, Is.Empty);
            Assert.That(_fighters.Length, Is.EqualTo(1));
            Assert.That(_operations.GetManifest(_carrier), Is.EqualTo(_fighters));
            Assert.That(((Starfighter)_fighters[0].GetUnit()).CurrentSquadronSize, Is.EqualTo(12));
        }

        /// <summary>
        /// Checks repeated requests cannot duplicate queued squadron releases.
        /// </summary>
        [Test]
        public void Launch_RepeatedSelection_QueuesSquadronOnce()
        {
            _operations.Launch(new[] { _carrier, _fighters[0] }, "OWNER");
            _operations.Launch(new[] { _carrier }, "OWNER");
            Assert.That(_carrier.LaunchQueue, Is.EqualTo(new[] { "SQUADRON" }));
        }

        /// <summary>
        /// Checks commands cannot launch another faction's carried squadrons.
        /// </summary>
        [Test]
        public void Launch_HostileCarrier_DoesNotQueue()
        {
            Assert.IsFalse(_operations.CanLaunch(new[] { _carrier }, "ENEMY"));
            _operations.Launch(new[] { _carrier }, "ENEMY");
            Assert.That(_carrier.LaunchQueue, Is.Empty);
        }

        /// <summary>
        /// Checks the authored bay is required instead of inventing a departure point.
        /// </summary>
        [Test]
        public void Launch_MissingBay_DoesNotQueue()
        {
            _operations = new TacticalCarrierOperations(
                _battle,
                new Dictionary<CombatUnit, Vector3[][]>()
            );
            Assert.IsFalse(_operations.CanLaunch(new[] { _carrier }, "OWNER"));
            _operations.Launch(new[] { _carrier }, "OWNER");
            Assert.That(_carrier.LaunchQueue, Is.Empty);
        }

        /// <summary>
        /// Checks launch admission excludes fighters unable to move through their bay.
        /// </summary>
        [Test]
        public void Launch_ImmobileSquadron_StaysDocked()
        {
            foreach (CombatUnit fighter in _fighters)
                ((Starfighter)fighter.GetUnit()).SublightSpeed = 0;
            Assert.IsFalse(_operations.CanLaunch(new[] { _carrier }, "OWNER"));
            _operations.Launch(new[] { _carrier }, "OWNER");
            Assert.That(_carrier.LaunchQueue, Is.Empty);
        }

        /// <summary>
        /// Checks cancelling queued departures clears both saved requests and fighter states.
        /// </summary>
        [Test]
        public void Recall_QueuedSquadron_CancelsDeparture()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Recall(new[] { _carrier }, "OWNER");
            _operations.Advance(20f);
            Assert.That(_carrier.LaunchQueue, Is.Empty);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Docked)
            );
        }

        /// <summary>
        /// Checks the squadron's single representation traverses the bay at its configured speed.
        /// </summary>
        [Test]
        public void Advance_DepartingSquadron_UsesSublightSpeed()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(5f);
            _operations.Advance(0.5f);
            Assert.That(_fighters[0].Position.Z, Is.EqualTo(5f).Within(0.0001f));
        }

        /// <summary>
        /// Checks hidden cargo follows the carrier's translation and heading.
        /// </summary>
        [Test]
        public void Advance_DockedFighters_FollowsMovingCarrier()
        {
            _carrier.Position.X = 100f;
            _carrier.Forward = new BattleVector3 { X = 1f };
            _operations.Advance(1f);
            Assert.That(_fighters.Select(fighter => fighter.Position.X), Has.All.EqualTo(100f));
        }

        /// <summary>
        /// Checks pausing the battle clock leaves the squadron release deadline unchanged.
        /// </summary>
        [Test]
        public void Advance_ZeroElapsed_PreservesCountdown()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(0f);
            Assert.That(_carrier.LaunchCountdown, Is.EqualTo(5f));
        }

        /// <summary>
        /// Checks a deployed squadron returns through the bay and becomes launchable again.
        /// </summary>
        [Test]
        public void Recall_DeployedSquadron_RecoversAllFighters()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(5f);
            _operations.Advance(3f);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Deployed)
            );
            _operations.Recall(new[] { _carrier }, "OWNER");
            for (int frame = 0; frame < 2000; frame++)
                _operations.Advance(0.05f);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Docked)
            );
            Assert.IsTrue(_operations.CanLaunch(new[] { _carrier }, "OWNER"));
        }

        /// <summary>
        /// Checks a saved pending release resumes its remaining deadline rather than starting over.
        /// </summary>
        [Test]
        public void Advance_RestoredQueue_PreservesRemainingCountdown()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(2f);
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(_battle)
            );
            CombatUnit carrier = restored
                .GetParticipants()[0]
                .GetCombatants()
                .Single(unit => unit.GetUnit() is CapitalShip);
            TacticalCarrierOperations operations = CreateOperations(
                restored,
                carrier,
                new IntervalRandom(0)
            );
            operations.Advance(3f);
            Assert.That(
                restored
                    .GetParticipants()[0]
                    .GetCombatants()
                    .Where(unit => unit.GetUnit() is Starfighter)
                    .Select(unit => unit.FlightState),
                Has.All.EqualTo(FighterFlightState.Launching)
            );
        }

        /// <summary>
        /// Checks invalid authored routes fail before any flight is advanced.
        /// </summary>
        [Test]
        public void Constructor_DuplicateWaypoints_RejectsRoute()
        {
            Assert.Throws<ArgumentException>(() =>
                new TacticalCarrierOperations(
                    _battle,
                    new Dictionary<CombatUnit, Vector3[][]>
                    {
                        [_carrier] = new[] { new[] { Vector3.Zero, Vector3.Zero } },
                    }
                )
            );
        }

        /// <summary>
        /// Checks faster followers cannot catch a slower squadron while sharing the departure lane.
        /// </summary>
        [Test]
        public void Advance_FasterFollowingSquadron_PreservesBayClearance()
        {
            CombatUnit[] following = AddSquadron("FOLLOWING");
            foreach (CombatUnit fighter in following)
                ((Starfighter)fighter.GetUnit()).SublightSpeed = 20;
            _operations = new TacticalCarrierOperations(
                _battle,
                new Dictionary<CombatUnit, Vector3[][]>
                {
                    [_carrier] = new[] { new[] { Vector3.Zero, new Vector3(0f, 0f, 200f) } },
                },
                new IntervalRandom(0)
            );
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(5f);
            _operations.Advance(5f);
            for (int frame = 0; frame < 300; frame++)
            {
                _operations.Advance(0.1f);
                if (
                    _fighters[0].FlightState == FighterFlightState.Launching
                    && following[0].FlightState == FighterFlightState.Launching
                    && following[0].FlightDelay == 0f
                )
                    Assert.That(
                        _fighters[0].Position.Z - following[0].Position.Z,
                        Is.GreaterThanOrEqualTo(MathF.Sqrt(3f) - 0.001f)
                    );
            }
        }

        /// <summary>
        /// Checks recalling an already moving fighter completes its departure before reversing the bay route.
        /// </summary>
        [Test]
        public void Recall_DepartingSquadron_ClearsBayBeforeRecovery()
        {
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(5f);
            _operations.Advance(0.1f);
            _operations.Recall(new[] { _carrier }, "OWNER");
            Assert.That(_fighters[0].FlightState, Is.EqualTo(FighterFlightState.Launching));
            Assert.IsTrue(_fighters[0].RecallRequested);
            for (int frame = 0; frame < 2000; frame++)
                _operations.Advance(0.05f);
            Assert.That(
                _fighters.Select(fighter => fighter.FlightState),
                Has.All.EqualTo(FighterFlightState.Docked)
            );
        }

        /// <summary>
        /// Checks additional squadrons stay queued until their own release interval.
        /// </summary>
        [Test]
        public void Advance_MultipleSquadrons_ReleasesOnePerInterval()
        {
            CombatUnit next = AddSquadron("NEXT").Single();
            _operations = CreateOperations(_battle, _carrier, new IntervalRandom(0));
            _operations.Launch(new[] { _carrier }, "OWNER");
            _operations.Advance(5f);
            Assert.That(_fighters[0].FlightState, Is.EqualTo(FighterFlightState.Launching));
            Assert.That(next.FlightState, Is.EqualTo(FighterFlightState.Queued));
            _operations.Advance(5f);
            Assert.That(next.FlightState, Is.EqualTo(FighterFlightState.Launching));
            Assert.That(_operations.GetManifest(_carrier).Count, Is.EqualTo(2));
        }

        /// <summary>
        /// Adds an explicitly carried squadron using the strategic parent contract.
        /// </summary>
        /// <param name="id">The strategic squadron identity.</param>
        /// <returns>The squadron's single tactical representation.</returns>
        private CombatUnit[] AddSquadron(string id)
        {
            Starfighter squadron = new Starfighter
            {
                InstanceID = id,
                OwnerInstanceID = "OWNER",
                CurrentSquadronSize = 12,
                MaxSquadronSize = 12,
                SublightSpeed = 10,
                ModelSize = new ModelDimensions
                {
                    Width = 1f,
                    Height = 1f,
                    Depth = 1f,
                },
            };
            _sourceCarrier.AddChild(squadron);
            squadron.SetParent(_sourceCarrier);
            return _battle.AddCombatants(squadron).ToArray();
        }

        /// <summary>
        /// Binds one straight carrier-local route to a battle snapshot.
        /// </summary>
        /// <param name="battle">The battle snapshot.</param>
        /// <param name="carrier">Its carrier combatant.</param>
        /// <param name="random">The interval draw provider.</param>
        /// <returns>The indexed operations owner.</returns>
        private static TacticalCarrierOperations CreateOperations(
            ActiveBattle battle,
            CombatUnit carrier,
            IRandomNumberProvider random
        ) =>
            new TacticalCarrierOperations(
                battle,
                new Dictionary<CombatUnit, Vector3[][]>
                {
                    [carrier] = new[]
                    {
                        new[] { Vector3.Zero, new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 20f) },
                    },
                },
                random
            );

        private sealed class IntervalRandom : IRandomNumberProvider
        {
            private readonly int _value;

            /// <summary>
            /// Fixes the release interval draw for controlled boundary checks.
            /// </summary>
            /// <param name="value">The draw returned within the requested range.</param>
            public IntervalRandom(int value) => _value = value;

            /// <summary>
            /// Returns a fixed fractional draw.
            /// </summary>
            /// <returns>Zero.</returns>
            public double NextDouble() => 0d;

            /// <summary>
            /// Returns the fixture's fixed bounded interval draw.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The fixed draw.</returns>
            public int NextInt(int min, int max)
            {
                Assert.That(_value, Is.GreaterThanOrEqualTo(min).And.LessThan(max));
                return _value;
            }
        }
    }
}
