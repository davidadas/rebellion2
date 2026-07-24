using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public sealed class StrategyMusicControllerTests
    {
        private const string _advantageTrack = "advantage";
        private const string _disadvantageTrack = "disadvantage";
        private const string _strongAdvantageTrack = "strong-advantage";
        private Faction _opponentFaction;
        private Faction _playerFaction;
        private Func<string> _selectNextTrack;
        private StrategyMusicController _controller;
        private StrategyMusicTheme _theme;
        private readonly Queue<int> _randomIndices = new Queue<int>();

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            _playerFaction = new Faction { InstanceID = "player" };
            _opponentFaction = new Faction { InstanceID = "opponent" };
            game.Factions.Add(_playerFaction);
            game.Factions.Add(_opponentFaction);
            game.Summary.PlayerFactionID = _playerFaction.InstanceID;

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
            _selectNextTrack = null;
            _controller = new StrategyMusicController(
                () => game,
                () => _theme,
                GetRandomIndex,
                selector => _selectNextTrack = selector
            );
        }

        [Test]
        public void Resume_FirstTrackUsesStrategicState()
        {
            AddColonizedPlanets(_playerFaction, 3);
            AddColonizedPlanets(_opponentFaction, 1);

            _controller.Resume();

            Assert.AreEqual(_strongAdvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_AfterStrategicTrackPlaysThreeNeutralTracks()
        {
            AddColonizedPlanets(_playerFaction, 3);
            AddColonizedPlanets(_opponentFaction, 1);
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
        public void Selection_TwoToOnePlanetRatioUsesAdvantageTrack()
        {
            AddColonizedPlanets(_playerFaction, 2);
            AddColonizedPlanets(_opponentFaction, 1);
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_OneToTwoPlanetRatioUsesDisadvantageTrack()
        {
            AddColonizedPlanets(_playerFaction, 1);
            AddColonizedPlanets(_opponentFaction, 2);
            _controller.Resume();

            Assert.AreEqual(_disadvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_OneToThreePlanetRatioUsesDisadvantageTrack()
        {
            AddColonizedPlanets(_playerFaction, 1);
            AddColonizedPlanets(_opponentFaction, 3);
            _controller.Resume();

            Assert.AreEqual(_disadvantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_MiddlePlanetRatioUsesRandomNeutralTrack()
        {
            AddColonizedPlanets(_playerFaction, 1);
            AddColonizedPlanets(_opponentFaction, 1);
            _randomIndices.Enqueue(2);
            _controller.Resume();

            Assert.AreEqual("neutral-3", _selectNextTrack());
        }

        [Test]
        public void Selection_NoOpponentPlanetsUsesConfiguredMultiplier()
        {
            AddColonizedPlanets(_playerFaction, 20);
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        [Test]
        public void Selection_UncolonizedPlanetsDoNotAffectRatio()
        {
            AddColonizedPlanets(_playerFaction, 2);
            AddColonizedPlanets(_opponentFaction, 1);
            _playerFaction.AddOwnedUnit(new Planet { InstanceID = "uncolonized" });
            _controller.Resume();

            Assert.AreEqual(_advantageTrack, _selectNextTrack());
        }

        private int GetRandomIndex(int minimum, int maximum)
        {
            int value = _randomIndices.Count > 0 ? _randomIndices.Dequeue() : minimum;
            Assert.That(value, Is.GreaterThanOrEqualTo(minimum).And.LessThan(maximum));
            return value;
        }

        private static void AddColonizedPlanets(Faction faction, int count)
        {
            for (int index = 0; index < count; index++)
            {
                faction.AddOwnedUnit(
                    new Planet
                    {
                        InstanceID = $"{faction.InstanceID}-planet-{index}",
                        OwnerInstanceID = faction.InstanceID,
                        IsColonized = true,
                    }
                );
            }
        }
    }
}
