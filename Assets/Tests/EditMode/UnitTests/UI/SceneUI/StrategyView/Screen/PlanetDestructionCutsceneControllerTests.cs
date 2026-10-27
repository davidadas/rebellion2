using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Runtime;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class PlanetDestructionCutsceneControllerTests
    {
        private GameResultPipeline _pipeline;
        private PlanetDestructionCutsceneController _controller;
        private List<string> _movies;
        private string _playerFactionId;
        private FactionTheme _empireTheme;

        /// <summary>
        /// Creates the ordered pipeline and viewer-specific movie mappings.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameResultBus bus = new GameResultBus();
            _pipeline = new GameResultPipeline(() => bus, () => null);
            _movies = new List<string>();
            _playerFactionId = "FNEMP1";
            _empireTheme = CreateTheme("FNEMP1", "empire");
            FactionThemeLibrary themes = new FactionThemeLibrary(
                new FactionThemes
                {
                    new FactionTheme { FactionInstanceID = "DEFAULT" },
                    _empireTheme,
                    CreateTheme("FNALL1", "alliance"),
                }
            );
            _controller = new PlanetDestructionCutsceneController(
                _pipeline,
                themes,
                () => _playerFactionId,
                _movies.Add
            );
        }

        /// <summary>
        /// Removes the pipeline observer after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
        }

        [TestCase("FNEMP1", "destroyer-a", "empire-a")]
        [TestCase("FNEMP1", "destroyer-b", "empire-b")]
        [TestCase("FNALL1", "destroyer-a", "alliance-a")]
        [TestCase("FNALL1", "destroyer-b", "alliance-b")]
        public void ProcessResults_ParticipatingPlayer_RequestsFiringShipMovie(
            string playerFactionId,
            string shipTypeId,
            string expectedMovie
        )
        {
            _playerFactionId = playerFactionId;
            BombardmentResult result = CreateDestruction(shipTypeId);

            _pipeline.ProcessResults(new[] { result }, false);

            CollectionAssert.AreEqual(new[] { expectedMovie }, _movies);
        }

        [Test]
        public void ProcessResults_OrdinaryBombardment_DoesNotRequestMovie()
        {
            BombardmentResult result = CreateDestruction();
            result.PlanetDestroyed = false;

            _pipeline.ProcessResults(new[] { result }, false);

            Assert.IsEmpty(_movies);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unconfigured-destroyer")]
        public void ProcessResults_UnmappedShip_DoesNotRequestMovie(string shipTypeId)
        {
            _pipeline.ProcessResults(new[] { CreateDestruction(shipTypeId) }, false);

            Assert.IsEmpty(_movies);
        }

        [Test]
        public void ProcessResults_PlayerNotInvolved_DoesNotRequestMovie()
        {
            BombardmentResult result = CreateDestruction();
            result.AttackerOwnerInstanceID = "another-attacker";
            result.DefenderOwnerInstanceID = "another-defender";

            _pipeline.ProcessResults(new[] { result }, false);

            Assert.IsEmpty(_movies);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing-theme")]
        public void ProcessResults_MissingViewerTheme_DoesNotRequestMovie(string playerFactionId)
        {
            _playerFactionId = playerFactionId;
            BombardmentResult result = CreateDestruction();
            result.AttackerOwnerInstanceID = playerFactionId;

            Assert.DoesNotThrow(() => _pipeline.ProcessResults(new[] { result }, false));

            Assert.IsEmpty(_movies);
        }

        [Test]
        public void ProcessResults_MissingMappings_DoesNotRequestMovie()
        {
            _empireTheme.PlanetDestructionCutscenes = null;

            _pipeline.ProcessResults(new[] { CreateDestruction() }, false);

            Assert.IsEmpty(_movies);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ProcessResults_EmptyMoviePath_DoesNotRequestMovie(string path)
        {
            _empireTheme.PlanetDestructionCutscenes[0].Path = path;

            _pipeline.ProcessResults(new[] { CreateDestruction() }, false);

            Assert.IsEmpty(_movies);
        }

        [Test]
        public void ProcessResults_DestructionEndsCampaign_RequestsMoviesInResultOrder()
        {
            _pipeline.HeadquartersLost += _ => _movies.Add("headquarters");
            _pipeline.VictoryDeclared += _ => _movies.Add("victory");

            _pipeline.ProcessResults(
                new GameResult[]
                {
                    CreateDestruction(),
                    new HeadquartersCapturedResult(),
                    new VictoryResult(),
                },
                false
            );

            CollectionAssert.AreEqual(new[] { "empire-a", "headquarters", "victory" }, _movies);
        }

        [Test]
        public void Dispose_LaterDestruction_DoesNotRequestMovie()
        {
            _controller.Dispose();

            _pipeline.ProcessResults(new[] { CreateDestruction() }, false);

            Assert.IsEmpty(_movies);
        }

        /// <summary>
        /// Creates two distinct ship-specific movie mappings for a viewing faction.
        /// </summary>
        /// <param name="factionId">The viewing faction identifier.</param>
        /// <param name="moviePrefix">The prefix distinguishing that faction's movies.</param>
        /// <returns>The configured presentation theme.</returns>
        private static FactionTheme CreateTheme(string factionId, string moviePrefix)
        {
            return new FactionTheme
            {
                FactionInstanceID = factionId,
                PlanetDestructionCutscenes = new List<PlanetDestructionCutscene>
                {
                    new PlanetDestructionCutscene
                    {
                        ShipTypeID = "destroyer-a",
                        Path = moviePrefix + "-a",
                    },
                    new PlanetDestructionCutscene
                    {
                        ShipTypeID = "destroyer-b",
                        Path = moviePrefix + "-b",
                    },
                },
            };
        }

        /// <summary>
        /// Creates a successful destruction result between the two configured factions.
        /// </summary>
        /// <param name="shipTypeId">The firing destroyer's type identifier.</param>
        /// <returns>The completed bombardment result.</returns>
        private static BombardmentResult CreateDestruction(string shipTypeId = "destroyer-a")
        {
            return new BombardmentResult
            {
                AttackerOwnerInstanceID = "FNEMP1",
                DefenderOwnerInstanceID = "FNALL1",
                PlanetDestroyed = true,
                PlanetDestroyerTypeID = shipTypeId,
                Type = BombardmentType.DestroyPlanet,
            };
        }
    }
}
