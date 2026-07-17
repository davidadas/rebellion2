using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string _ownerId = "FNALL1";

        private ConstructionWindowProjector _projector;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _ownerId });
            game.Summary.PlayerFactionID = _ownerId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _projector = new ConstructionWindowProjector(() => _uiContext);
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
            Assert.IsNotNull(data.TitleTexture);
            Assert.AreSame(_uiContext.GetEntityTexture(second, false), data.SelectedTexture);
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
            Assert.AreEqual(new Color32(128, 128, 128, 255), data.DropdownItems[0].LabelColor);
            Assert.AreEqual("Second Ship", data.DropdownItems[1].Label);
            Assert.AreEqual(new Color32(255, 255, 255, 255), data.DropdownItems[1].LabelColor);
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

        private static CapitalShip CreateCapitalShip(
            string instanceId,
            string displayName,
            int constructionCost,
            int maintenanceCost
        )
        {
            CapitalShip definition = ResourceManager
                .GetEntityData<CapitalShip>()
                .First(ship => ship.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            return new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = definition.TypeID,
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                ConstructionCost = constructionCost,
                MaintenanceCost = maintenanceCost,
            };
        }
    }
}
