using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Bookmarks
{
    [TestFixture]
    public class BookmarkControllerTests
    {
        private const string _playerFactionId = "FNALL1";

        private BookmarkController _controller;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _controller = new BookmarkController(_uiContext);
        }

        [Test]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new BookmarkController(null));
        }

        [Test]
        public void BuildRenderData_EmptyController_ReturnsInactiveAuthoredSlots()
        {
            int expectedCount = _uiContext
                .GetPlayerFactionTheme()
                .StrategyBookmarkLayout.GetSlotCount();

            System.Collections.Generic.IReadOnlyList<BookmarkRenderData> data =
                _controller.BuildRenderData();

            Assert.AreEqual(expectedCount, data.Count);
            Assert.That(data, Has.All.Matches<BookmarkRenderData>(item => !item.Active));
            Assert.That(
                data,
                Has.All.Matches<BookmarkRenderData>(item => item.Label == string.Empty)
            );
            Assert.That(
                data,
                Has.All.Matches<BookmarkRenderData>(item => item.IconTexture == null)
            );
        }

        [Test]
        public void TryAdd_InvalidBookmark_RejectsWithoutConsumingSlot()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");

            bool nullPlanetAccepted = _controller.TryAdd(PlanetIcon.Fleet, 10, 20, null);
            bool emptyIconAccepted = _controller.TryAdd(PlanetIcon.None, 10, 20, planet);

            Assert.IsFalse(nullPlanetAccepted);
            Assert.IsFalse(emptyIconAccepted);
            Assert.That(
                _controller.BuildRenderData(),
                Has.All.Matches<BookmarkRenderData>(item => !item.Active)
            );
        }

        [Test]
        public void TryAdd_AvailableSlot_ProjectsBookmarkIntoFirstSlot()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");

            bool accepted = _controller.TryAdd(PlanetIcon.Fleet, 10, 20, planet);
            System.Collections.Generic.IReadOnlyList<BookmarkRenderData> data =
                _controller.BuildRenderData();

            Assert.IsTrue(accepted);
            Assert.IsTrue(data[0].Active);
            Assert.AreEqual("Coruscant", data[0].Label);
            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext.GetTheme(null).StrategyBookmarkIcons.FleetImagePath
                ),
                data[0].IconTexture
            );
            Assert.That(data, Has.Exactly(1).Matches<BookmarkRenderData>(item => item.Active));
        }

        [Test]
        public void TryAdd_FullController_RejectsAdditionalBookmark()
        {
            int slotCount = _controller.BuildRenderData().Count;
            for (int index = 0; index < slotCount; index++)
            {
                Assert.IsTrue(
                    _controller.TryAdd(
                        PlanetIcon.Fleet,
                        index,
                        index,
                        CreatePlanet($"planet-{index}", $"Planet {index}")
                    )
                );
            }

            bool accepted = _controller.TryAdd(
                PlanetIcon.Fleet,
                100,
                100,
                CreatePlanet("overflow", "Overflow")
            );

            Assert.IsFalse(accepted);
            Assert.That(
                _controller.BuildRenderData(),
                Has.All.Matches<BookmarkRenderData>(item => item.Active)
            );
        }

        [Test]
        public void TryTake_ValidOccupiedIndex_RemovesAndReturnsBookmark()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");
            _controller.TryAdd(PlanetIcon.Mission, 15, 25, planet);

            bool taken = _controller.TryTake(0, out BookmarkEntry bookmark);

            Assert.IsTrue(taken);
            Assert.AreSame(planet, bookmark.Planet);
            Assert.AreEqual(PlanetIcon.Mission, bookmark.Icon);
            Assert.AreEqual(15, bookmark.X);
            Assert.AreEqual(25, bookmark.Y);
            Assert.IsFalse(_controller.BuildRenderData()[0].Active);
        }

        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        [TestCase(0)]
        public void TryTake_InvalidOrEmptyIndex_ReturnsFalse(int index)
        {
            bool taken = _controller.TryTake(index, out BookmarkEntry bookmark);

            Assert.IsFalse(taken);
            Assert.IsNull(bookmark);
        }

        [Test]
        public void Take_MatchingPlanetAndIcon_RemovesOnlyMatchingBookmark()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");
            _controller.TryAdd(PlanetIcon.Fleet, 10, 20, planet);
            _controller.TryAdd(PlanetIcon.Mission, 30, 40, planet);

            BookmarkEntry taken = _controller.Take(planet, PlanetIcon.Mission);

            Assert.IsNotNull(taken);
            Assert.AreEqual(PlanetIcon.Mission, taken.Icon);
            Assert.IsTrue(_controller.BuildRenderData()[0].Active);
            Assert.IsFalse(_controller.BuildRenderData()[1].Active);
        }

        [Test]
        public void Take_MissingBookmark_ReturnsNullWithoutMutation()
        {
            GalaxyMapPlanet planet = CreatePlanet("planet-1", "Coruscant");
            _controller.TryAdd(PlanetIcon.Fleet, 10, 20, planet);

            BookmarkEntry taken = _controller.Take(planet, PlanetIcon.Defense);

            Assert.IsNull(taken);
            Assert.IsTrue(_controller.BuildRenderData()[0].Active);
        }

        [Test]
        public void ReconcilePlanets_MatchingPersistentId_ReplacesStaleProjection()
        {
            GalaxyMapPlanet original = CreatePlanet("planet-1", "Old Coruscant");
            GalaxyMapPlanet replacement = CreatePlanet("planet-1", "Coruscant");
            GalaxyMapSector sector = new GalaxyMapSector(null, new[] { replacement });
            _controller.TryAdd(PlanetIcon.Fleet, 10, 20, original);

            _controller.ReconcilePlanets(new[] { sector });
            _controller.TryTake(0, out BookmarkEntry bookmark);

            Assert.AreSame(replacement, bookmark.Planet);
            Assert.AreEqual("Coruscant", bookmark.Planet.Planet.GetDisplayName());
        }

        [Test]
        public void ReconcilePlanets_NullSectors_PreservesCurrentProjection()
        {
            GalaxyMapPlanet original = CreatePlanet("planet-1", "Coruscant");
            _controller.TryAdd(PlanetIcon.Fleet, 10, 20, original);

            _controller.ReconcilePlanets(null);
            _controller.TryTake(0, out BookmarkEntry bookmark);

            Assert.AreSame(original, bookmark.Planet);
        }

        private static GalaxyMapPlanet CreatePlanet(string instanceId, string displayName)
        {
            Planet planet = new Planet { InstanceID = instanceId, DisplayName = displayName };
            return new GalaxyMapPlanet(null, planet, string.Empty);
        }
    }
}
