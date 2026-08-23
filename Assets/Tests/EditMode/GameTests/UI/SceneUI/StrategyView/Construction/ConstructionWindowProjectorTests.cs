using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowProjectorTests
    {
        private const string _ownerId = "owner";

        private ConstructionWindowProjector _projector;
        private Dictionary<string, Texture2D> _textures;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            game.Summary.PlayerFactionID = _ownerId;
            _textures = new Dictionary<string, Texture2D>
            {
                { "active-title", new Texture2D(1, 1) },
                { "inactive-title", new Texture2D(1, 1) },
            };
            FactionThemes themes = new FactionThemes
            {
                new FactionTheme { FactionInstanceID = "DEFAULT" },
                new FactionTheme
                {
                    FactionInstanceID = _ownerId,
                    WindowTitleTheme = new WindowTitleTheme
                    {
                        ActiveImagePath = "active-title",
                        InactiveImagePath = "inactive-title",
                    },
                },
            };
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(themes),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>()),
                path => _textures.TryGetValue(path, out Texture2D texture) ? texture : null
            );
            _projector = new ConstructionWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _textures.Values)
                UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConstructionWindowProjector(null));
        }

        [Test]
        public void CreateRenderData_NullItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _projector.CreateRenderData(
                    0,
                    0,
                    _ownerId,
                    true,
                    null,
                    0,
                    1,
                    Array.Empty<int>(),
                    Array.Empty<ConstructionBuildEstimate>(),
                    false
                )
            );
        }

        [Test]
        public void CreateRenderData_NullStartSelections_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _projector.CreateRenderData(
                    0,
                    0,
                    _ownerId,
                    true,
                    Array.Empty<IManufacturable>(),
                    0,
                    1,
                    null,
                    Array.Empty<ConstructionBuildEstimate>(),
                    false
                )
            );
        }

        [Test]
        public void CreateRenderData_NullEstimates_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _projector.CreateRenderData(
                    0,
                    0,
                    _ownerId,
                    true,
                    Array.Empty<IManufacturable>(),
                    0,
                    1,
                    Array.Empty<int>(),
                    null,
                    false
                )
            );
        }

        [Test]
        public void CreateRenderData_UnavailableContext_ThrowsInvalidOperationException()
        {
            ConstructionWindowProjector projector = new ConstructionWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.CreateRenderData(
                    0,
                    0,
                    _ownerId,
                    true,
                    Array.Empty<IManufacturable>(),
                    0,
                    1,
                    Array.Empty<int>(),
                    Array.Empty<ConstructionBuildEstimate>(),
                    false
                )
            );
        }

        [Test]
        public void CreateRenderData_SelectedItem_ProjectsSelectionAndDropdownRows()
        {
            CapitalShip first = CreateCapitalShip("first", "First Ship", 30, 4);
            CapitalShip second = CreateCapitalShip("second", "Second Ship", 50, 6);
            IReadOnlyList<IManufacturable> items = new IManufacturable[] { first, second };

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                12,
                34,
                _ownerId,
                true,
                items,
                1,
                3,
                new[] { 1 },
                new ConstructionBuildEstimate[] { null, new ConstructionBuildEstimate(8, 13) },
                true
            );

            Assert.AreEqual(12, data.X);
            Assert.AreEqual(34, data.Y);
            Assert.AreSame(_textures["active-title"], data.TitleTexture);
            Assert.AreSame(_textures["second-status"], data.SelectedTexture);
            Assert.AreEqual("Second Ship", data.SelectedName);
            Assert.AreEqual(3, data.BuildCount);
            Assert.AreEqual("150", data.ConstructionCost);
            Assert.AreEqual("18", data.MaintenanceCost);
            Assert.AreEqual("8", data.CompletionEstimate);
            Assert.IsTrue(data.CompletionHasDays);
            Assert.AreEqual("13", data.DeploymentEstimate);
            Assert.IsTrue(data.DeploymentHasDays);
            Assert.IsTrue(data.DropdownOpen);
            Assert.IsTrue(data.CanStart);
            Assert.AreEqual(2, data.DropdownItems.Count);
            Assert.AreEqual("First Ship", data.DropdownItems[0].Label);
            Assert.AreSame(_textures["first-status"], data.DropdownItems[0].Texture);
            Assert.AreEqual(new Color32(128, 128, 128, 255), data.DropdownItems[0].LabelColor);
            Assert.AreEqual("Second Ship", data.DropdownItems[1].Label);
            Assert.AreEqual(new Color32(255, 255, 255, 255), data.DropdownItems[1].LabelColor);
        }

        [Test]
        public void CreateRenderData_Starfighter_UsesBattleResultArtwork()
        {
            Starfighter starfighter = CreateStarfighter("fighter", "Fighter");

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                true,
                new IManufacturable[] { starfighter },
                0,
                1,
                Array.Empty<int>(),
                Array.Empty<ConstructionBuildEstimate>(),
                false
            );

            Assert.AreSame(_textures["fighter-status"], data.SelectedTexture);
            Assert.AreSame(_textures["fighter-status"], data.DropdownItems[0].Texture);
        }

        [Test]
        public void CreateRenderData_MissingStatusArtwork_UsesFullDisplayArtwork()
        {
            CapitalShip ship = CreateCapitalShip("ship", "Ship", 1, 1);
            ship.BattleResultImagePath = "missing-status";

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                true,
                new IManufacturable[] { ship },
                0,
                1,
                Array.Empty<int>(),
                Array.Empty<ConstructionBuildEstimate>(),
                false
            );

            Assert.AreSame(_textures["ship-display"], data.SelectedTexture);
            Assert.AreSame(_textures["ship-display"], data.DropdownItems[0].Texture);
        }

        [Test]
        public void CreateRenderData_OutOfRangeEstimates_ClampsDisplayedValues()
        {
            CapitalShip ship = CreateCapitalShip("ship", "Ship", 1, 1);

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                false,
                new IManufacturable[] { ship },
                0,
                1,
                Array.Empty<int>(),
                new[] { new ConstructionBuildEstimate(10000, -1) },
                false
            );

            Assert.AreEqual("9999", data.CompletionEstimate);
            Assert.AreEqual("0", data.DeploymentEstimate);
            Assert.IsFalse(data.CanStart);
        }

        [Test]
        public void CreateRenderData_MissingEstimate_ProjectsUnavailableValues()
        {
            CapitalShip ship = CreateCapitalShip("ship", "Ship", 1, 1);

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                true,
                new IManufacturable[] { ship },
                0,
                1,
                new[] { 0 },
                Array.Empty<ConstructionBuildEstimate>(),
                false
            );

            Assert.AreEqual("N/A", data.CompletionEstimate);
            Assert.IsFalse(data.CompletionHasDays);
            Assert.AreEqual("N/A", data.DeploymentEstimate);
            Assert.IsFalse(data.DeploymentHasDays);
            Assert.IsTrue(data.CanStart);
        }

        [Test]
        public void CreateRenderData_CompletionOnlyEstimate_ShowsCompletionAndUnavailableDeployment()
        {
            CapitalShip ship = CreateCapitalShip("ship", "Ship", 1, 1);

            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                true,
                new IManufacturable[] { ship },
                0,
                1,
                new[] { 0 },
                new[] { new ConstructionBuildEstimate(8, null) },
                false
            );

            Assert.AreEqual("8", data.CompletionEstimate);
            Assert.IsTrue(data.CompletionHasDays);
            Assert.AreEqual("N/A", data.DeploymentEstimate);
            Assert.IsFalse(data.DeploymentHasDays);
        }

        [Test]
        public void CreateRenderData_EmptySelection_ProjectsHiddenSelectionState()
        {
            ConstructionWindowRenderData data = _projector.CreateRenderData(
                0,
                0,
                _ownerId,
                true,
                Array.Empty<IManufacturable>(),
                0,
                1,
                Array.Empty<int>(),
                Array.Empty<ConstructionBuildEstimate>(),
                false
            );

            Assert.IsFalse(data.HasSelection);
            Assert.IsNull(data.SelectedTexture);
            Assert.AreEqual(string.Empty, data.SelectedName);
            Assert.AreEqual(string.Empty, data.ConstructionCost);
            Assert.AreEqual(string.Empty, data.MaintenanceCost);
            Assert.AreEqual("N/A", data.CompletionEstimate);
            Assert.AreEqual("N/A", data.DeploymentEstimate);
            Assert.IsFalse(data.CanStart);
        }

        private CapitalShip CreateCapitalShip(
            string instanceId,
            string displayName,
            int constructionCost,
            int maintenanceCost
        )
        {
            AddTexture($"{instanceId}-display");
            AddTexture($"{instanceId}-small");
            AddTexture($"{instanceId}-status");
            return new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = $"{instanceId}-type",
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = $"{instanceId}-display",
                SmallDisplayImagePath = $"{instanceId}-small",
                BattleResultImagePath = $"{instanceId}-status",
                ConstructionCost = constructionCost,
                MaintenanceCost = maintenanceCost,
            };
        }

        private Starfighter CreateStarfighter(string instanceId, string displayName)
        {
            AddTexture($"{instanceId}-display");
            AddTexture($"{instanceId}-small");
            AddTexture($"{instanceId}-status");
            return new Starfighter
            {
                InstanceID = instanceId,
                TypeID = $"{instanceId}-type",
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = $"{instanceId}-display",
                SmallDisplayImagePath = $"{instanceId}-small",
                BattleResultImagePath = $"{instanceId}-status",
                ConstructionCost = 1,
                MaintenanceCost = 1,
            };
        }

        private void AddTexture(string path)
        {
            _textures.Add(path, new Texture2D(244, 100));
        }
    }
}
