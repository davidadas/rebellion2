using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.Runtime
{
    [TestFixture]
    public class UIContextTests
    {
        private const string _playerFactionId = "FNALL1";

        private UIContext _context;
        private EncyclopediaCatalog _encyclopediaCatalog;
        private GameRoot _game;
        private FactionThemeLibrary _themeLibrary;

        [SetUp]
        public void SetUp()
        {
            _game = CreateGame(_playerFactionId);
            _themeLibrary = new FactionThemeLibrary();
            _encyclopediaCatalog = new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>());
            _context = new UIContext(_game, _themeLibrary, _encyclopediaCatalog);
        }

        [Test]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UIContext(null, _themeLibrary, _encyclopediaCatalog)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new UIContext(_game, null, _encyclopediaCatalog)
            );
            Assert.Throws<ArgumentNullException>(() => new UIContext(_game, _themeLibrary, null));
        }

        [Test]
        public void Properties_ConfiguredContext_ReturnSuppliedDependencies()
        {
            Assert.AreSame(_game, _context.Game);
            Assert.AreSame(_encyclopediaCatalog, _context.EncyclopediaCatalog);
            Assert.AreEqual(_playerFactionId, _context.GetPlayerFactionInstanceID());
            Assert.AreSame(
                _themeLibrary.GetTheme(_playerFactionId),
                _context.GetPlayerFactionTheme()
            );
        }

        [Test]
        public void ReplaceGame_ReplacementGame_UpdatesPlayerContext()
        {
            GameRoot replacement = CreateGame("FNEMP1");

            _context.ReplaceGame(replacement);

            Assert.AreSame(replacement, _context.Game);
            Assert.AreEqual("FNEMP1", _context.GetPlayerFactionInstanceID());
            Assert.AreSame(_themeLibrary.GetTheme("FNEMP1"), _context.GetPlayerFactionTheme());
        }

        [Test]
        public void ReplaceGame_NullGame_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _context.ReplaceGame(null));
        }

        [Test]
        public void ResolveFactionColor_KnownFaction_ReturnsConfiguredPrimaryColor()
        {
            Color color = _context.ResolveFactionColor(_playerFactionId);

            Assert.AreEqual(_themeLibrary.GetTheme(_playerFactionId).GetPrimaryColor(), color);
        }

        [Test]
        public void GetTexture_EmptyOrMissingPath_ReturnsNull()
        {
            Assert.IsNull(_context.GetTexture(null));
            Assert.IsNull(_context.GetTexture(string.Empty));
            Assert.IsNull(_context.GetTexture("Art/HD/UI/Missing/ui_missing_test_asset"));
            Assert.IsNull(_context.GetTexture("Art/HD/UI/Missing/ui_missing_test_asset"));
        }

        [Test]
        public void GetTexture_ConfiguredPath_CachesPointFilteredClampedTexture()
        {
            string path = _context.GetPlayerFactionTheme().ConfirmDialogTheme.BackgroundImagePath;

            Texture2D first = _context.GetTexture(path);
            Texture2D second = _context.GetTexture(path);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(FilterMode.Point, first.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, first.wrapMode);
        }

        [Test]
        public void GetEntityTexture_NullOrUnmappedEntity_ReturnsNull()
        {
            Assert.IsNull(_context.GetEntityTexture(null, false));
            Assert.IsNull(_context.GetEntityTexture(new Officer(), false));
        }

        [Test]
        public void GetEntityTexture_CompactPath_PrefersConfiguredSmallArtwork()
        {
            FactionTheme theme = _context.GetPlayerFactionTheme();
            Officer officer = new Officer
            {
                DisplayImagePath = theme.ConfirmDialogTheme.BackgroundImagePath,
                SmallDisplayImagePath = theme.GalaxyBackground.DestroyedPlanetIconPath,
            };

            Texture2D texture = _context.GetEntityTexture(officer, true);

            Assert.AreSame(
                _context.GetTexture(theme.GalaxyBackground.DestroyedPlanetIconPath),
                texture
            );
        }

        [Test]
        public void GetEntityStatusTexture_InjuredOfficer_ReturnsInjuryArtwork()
        {
            string path = _context.GetPlayerFactionTheme().ConfirmDialogTheme.BackgroundImagePath;
            Officer officer = new Officer { InjuryPoints = 1, InjuredImagePath = path };

            Texture2D texture = _context.GetEntityStatusTexture(officer, false);

            Assert.AreSame(_context.GetTexture(path), texture);
        }

        [Test]
        public void GetEntityCapturedOverlayTexture_CapturedOfficer_ReturnsConfiguredOverlay()
        {
            string path = _context.GetPlayerFactionTheme().ConfirmDialogTheme.BackgroundImagePath;
            Officer officer = new Officer { IsCaptured = true, CapturedOverlayImagePath = path };

            Texture2D texture = _context.GetEntityCapturedOverlayTexture(officer);

            Assert.AreSame(_context.GetTexture(path), texture);
            Assert.IsNull(_context.GetEntityCapturedOverlayTexture(new Officer()));
            Assert.IsNull(_context.GetEntityCapturedOverlayTexture(new Fleet()));
        }

        [Test]
        public void GetPlanetTexture_DestroyedPlanet_ReturnsFactionDestroyedPlanetArtwork()
        {
            Planet planet = new Planet { IsDestroyed = true };
            string path = _context.GetPlayerFactionTheme().GalaxyBackground.DestroyedPlanetIconPath;

            Texture2D texture = _context.GetPlanetTexture(planet, "unused");

            Assert.AreSame(_context.GetTexture(path), texture);
            Assert.IsNull(_context.GetPlanetTexture(null, path));
        }

        [Test]
        public void GetSprite_FleetWithoutOwner_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _context.GetSprite(new Fleet()));
        }

        [Test]
        public void GetSprite_NullNode_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _context.GetSprite(null));
        }

        private static GameRoot CreateGame(string playerFactionId)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = playerFactionId });
            game.Summary.PlayerFactionID = playerFactionId;
            return game;
        }
    }
}
