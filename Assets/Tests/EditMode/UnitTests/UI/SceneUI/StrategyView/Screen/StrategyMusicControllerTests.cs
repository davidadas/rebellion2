using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public sealed class StrategyMusicControllerTests
    {
        private const string _advantageTrack = "advantage";
        private const string _disadvantageTrack = "disadvantage";
        private const string _opponentFactionId = "opponent";
        private const string _playerFactionId = "player";
        private const string _strongAdvantageTrack = "strong-advantage";
        private Func<string> _selectNextTrack;
        private StrategyMusicController _controller;
        private StrategyMusicTheme _theme;
        private int _stopMusicCalls;
        private readonly Queue<int> _randomIndices = new Queue<int>();
        private readonly List<Planet> _visiblePlanets = new List<Planet>();

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _theme = new StrategyMusicTheme
            {
                NeutralTrackPaths = new List<string> { "neutral-1", "neutral-2", "neutral-3" },
                StrongAdvantageTrackPath = _strongAdvantageTrack,
                AdvantageTrackPath = _advantageTrack,
                DisadvantageTrackPath = _disadvantageTrack,
                NeutralTracksBetweenStrategicTracks = 3,
                PlanetRatioScale = 100,
                NoOpponentPlanetMultiplier = 10,
                StrongAdvantageMinimumRatio = 300,
                AdvantageMinimumRatio = 200,
                DisadvantageMaximumRatio = 50,
            };
            _randomIndices.Clear();
            _visiblePlanets.Clear();
            _selectNextTrack = null;
            _stopMusicCalls = 0;
            _controller = new StrategyMusicController(
                () => _visiblePlanets,
                () => _playerFactionId,
                () => _theme,
                GetRandomIndex,
                selector => _selectNextTrack = selector,
                () => _stopMusicCalls++
            );
        }

        [Test]
        public void Resume_FirstTrack_UsesStrategicState()
        {
            AddColonizedPlanets(_playerFactionId, 3);
            AddColonizedPlanets(_opponentFactionId, 1);

            _controller.Resume();

            Assert.AreEqual(_strongAdvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_AfterStrategicTrack_PlaysThreeNeutralTracks()
        {
            AddColonizedPlanets(_playerFactionId, 3);
            AddColonizedPlanets(_opponentFactionId, 1);
            _randomIndices.Enqueue(0);
            _randomIndices.Enqueue(1);
            _randomIndices.Enqueue(2);
            _controller.Resume();

            string[] tracks =
            {
                _selectNextTrack(),
                _selectNextTrack(),
                _selectNextTrack(),
                _selectNextTrack(),
                _selectNextTrack(),
            };

            CollectionAssert.AreEqual(
                new[]
                {
                    _strongAdvantageTrack,
                    "neutral-1",
                    "neutral-2",
                    "neutral-3",
                    _strongAdvantageTrack,
                },
                tracks
            );
        }

        [Test]
        public void Selection_TwoToOnePlanetRatio_UsesAdvantageTrack()
        {
            AddColonizedPlanets(_playerFactionId, 2);
            AddColonizedPlanets(_opponentFactionId, 1);
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_OneToTwoPlanetRatio_UsesDisadvantageTrack()
        {
            AddColonizedPlanets(_playerFactionId, 1);
            AddColonizedPlanets(_opponentFactionId, 2);
            _controller.Resume();

            Assert.AreEqual(_disadvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_OneToThreePlanetRatio_UsesDisadvantageTrack()
        {
            AddColonizedPlanets(_playerFactionId, 1);
            AddColonizedPlanets(_opponentFactionId, 3);
            _controller.Resume();

            Assert.AreEqual(_disadvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_MiddlePlanetRatio_UsesRandomNeutralTrack()
        {
            AddColonizedPlanets(_playerFactionId, 1);
            AddColonizedPlanets(_opponentFactionId, 1);
            _randomIndices.Enqueue(2);
            _controller.Resume();

            Assert.AreEqual("neutral-3", _selectNextTrack());
        }

        [Test]
        public void Selection_NoOpponentPlanets_UsesConfiguredMultiplier()
        {
            AddColonizedPlanets(_playerFactionId, 20);
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_UncolonizedPlanets_DoNotAffectRatio()
        {
            AddColonizedPlanets(_playerFactionId, 2);
            AddColonizedPlanets(_opponentFactionId, 1);
            _visiblePlanets.Add(
                new Planet { InstanceID = "uncolonized", OwnerInstanceID = _playerFactionId }
            );
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_UnknownOwnerPlanets_DoNotAffectRatio()
        {
            AddColonizedPlanets(_playerFactionId, 2);
            AddColonizedPlanets(_opponentFactionId, 1);
            _visiblePlanets.Add(new Planet { InstanceID = "unknown", IsColonized = true });
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Reset_Default_StopsMusicAndRestartsCadenceWithStrategicTrack()
        {
            AddColonizedPlanets(_playerFactionId, 3);
            AddColonizedPlanets(_opponentFactionId, 1);
            _controller.Resume();
            Assert.AreEqual(_strongAdvantageTrack, _selectNextTrack());
            Assert.AreEqual("neutral-1", _selectNextTrack());

            _controller.Reset();
            _controller.Resume();

            Assert.AreEqual(1, _stopMusicCalls);
            Assert.AreEqual(_strongAdvantageTrack, _selectNextTrack());
        }

        /// <summary>
        /// Gets random index.
        /// </summary>
        /// <param name="minimum">The minimum.</param>
        /// <param name="maximum">The maximum.</param>
        /// <returns>The requested random index.</returns>
        private int GetRandomIndex(int minimum, int maximum)
        {
            int value = _randomIndices.Count > 0 ? _randomIndices.Dequeue() : minimum;
            Assert.That(value, Is.GreaterThanOrEqualTo(minimum).And.LessThan(maximum));
            return value;
        }

        /// <summary>
        /// Adds colonized planets.
        /// </summary>
        /// <param name="factionId">The owning faction identifier.</param>
        /// <param name="count">The count.</param>
        private void AddColonizedPlanets(string factionId, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _visiblePlanets.Add(
                    new Planet
                    {
                        InstanceID = $"{factionId}-planet-{index}",
                        OwnerInstanceID = factionId,
                        IsColonized = true,
                    }
                );
            }
        }
    }
}
