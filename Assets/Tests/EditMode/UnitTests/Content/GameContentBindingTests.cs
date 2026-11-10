using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.Content
{
    public sealed class GameContentBindingTests
    {
        private ContentPack _contentPack;

        /// <summary>
        /// Uses the installed fixture content for compatibility cases.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _contentPack = TestContent.Pack;
        }

        [Test]
        public void Prepare_DifferentContentIdentity_RejectsBeforeChangingFactionSettings()
        {
            ContentPack pack = TestContent.Pack;
            GameRoot game = new GameRoot { Summary = pack.CreateDefaultGameSummary() };
            game.Summary.PackVersion = "different";
            FactionSettings settings = new FactionSettings();
            Faction faction = new Faction
            {
                InstanceID = pack.GameData.Factions[0].InstanceID,
                Settings = settings,
            };
            game.GetFactions().Add(faction);

            Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(pack).Prepare(game)
            );
            Assert.AreSame(settings, faction.Settings);
        }

        [Test]
        public void Prepare_LegacyFactionSettings_RestoresIndependentAuthoredSettings()
        {
            ContentPack pack = TestContent.Pack;
            Faction template = pack.GameData.Factions[0];
            GameRoot game = new GameRoot { Summary = pack.CreateDefaultGameSummary() };
            Faction faction = new Faction
            {
                InstanceID = template.InstanceID,
                Settings = new FactionSettings { GarrisonEfficiency = 99 },
            };
            game.GetFactions().Add(faction);

            new GameContentBinding(pack).Prepare(game);

            Assert.AreEqual(
                template.Settings.GarrisonEfficiency,
                faction.Settings.GarrisonEfficiency
            );
            Assert.AreNotSame(template.Settings, faction.Settings);
        }

        [Test]
        public void Prepare_StaleGalaxyCoordinates_RestoresAuthoredLayout()
        {
            ContentPack pack = TestContent.Pack;
            PlanetSector template = pack.GameData.PlanetSectors[0];
            GameRoot game = new GameRoot { Summary = pack.CreateDefaultGameSummary() };
            PlanetSector sector = new PlanetSector
            {
                InstanceID = template.InstanceID,
                TypeID = template.TypeID,
                PositionX = template.PositionX + 100,
                PositionY = template.PositionY + 100,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            new GameContentBinding(pack).Prepare(game);

            Assert.AreEqual(template.PositionX, sector.PositionX);
            Assert.AreEqual(template.PositionY, sector.PositionY);
        }

        [Test]
        public void Prepare_MatchingIdentity_DoesNotThrow()
        {
            GameRoot game = CreateGame();

            Assert.DoesNotThrow(() => new GameContentBinding(_contentPack).Prepare(game));
        }

        [Test]
        public void Prepare_MissingIdentity_ThrowsInvalidOperationException()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };

            Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(_contentPack).Prepare(game)
            );
        }

        [Test]
        public void Prepare_DifferentPack_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(_contentPack).Prepare(game)
            );
        }

        [Test]
        public void Prepare_DifferentVersion_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackVersion = "different-version";

            Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(_contentPack).Prepare(game)
            );
        }

        [Test]
        public void Prepare_DifferentScenario_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ScenarioID = "different-scenario";

            Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(_contentPack).Prepare(game)
            );
        }

        [Test]
        public void Prepare_SaveHasDifferentMods_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ModIDs = new[] { "missing-mod" };
            game.Summary.ModVersions = new[] { "1.0.0" };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                new GameContentBinding(_contentPack).Prepare(game)
            );
            StringAssert.Contains("mods [missing-mod@1.0.0]", exception.Message);
            StringAssert.Contains("mods [] is active", exception.Message);
        }

        /// <summary>
        /// Creates a world matching the active content identity.
        /// </summary>
        /// <returns>The world used by compatibility cases.</returns>
        private GameRoot CreateGame() =>
            new GameRoot { Summary = _contentPack.CreateDefaultGameSummary() };
    }
}
