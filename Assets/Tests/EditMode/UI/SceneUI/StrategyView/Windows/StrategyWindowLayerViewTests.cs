using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class StrategyWindowLayerViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private StrategyWindowLayerView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void PrefabProperties_AuthoredLayer_ExposeEveryRequiredWindowPrefab()
        {
            Assert.IsNotNull(_view.PlanetSystemWindowPrefab);
            Assert.IsNotNull(_view.FacilityWindowPrefab);
            Assert.IsNotNull(_view.DefenseWindowPrefab);
            Assert.IsNotNull(_view.FleetWindowPrefab);
            Assert.IsNotNull(_view.MissionsWindowPrefab);
            Assert.IsNotNull(_view.ConstructionWindowPrefab);
            Assert.IsNotNull(_view.MissionCreateWindowPrefab);
            Assert.IsNotNull(_view.StatusWindowPrefab);
            Assert.IsNotNull(_view.AdvisorReportWindowPrefab);
            Assert.IsNotNull(_view.MessagesWindowPrefab);
            Assert.IsNotNull(_view.ConfirmDialogWindowPrefab);
            Assert.IsNotNull(_view.BattleAlertWindowPrefab);
            Assert.IsNotNull(_view.FinderWindowPrefab);
            Assert.IsNotNull(_view.EncyclopediaWindowPrefab);
            Assert.AreNotEqual(Vector2Int.zero, _view.ConstructionWindowOffset);
            Assert.Greater(_view.ItemDragStartDistance, 0);
        }

        [Test]
        public void GetWindowParent_KnownModality_ReturnsAuthoredLayer()
        {
            RectTransform modeless = GetField<RectTransform>("modelessWindowLayer");
            RectTransform modal = GetField<RectTransform>("modalWindowLayer");

            Transform modelessParent = _view.GetWindowParent(false);
            Transform modalParent = _view.GetWindowParent(true);

            Assert.AreSame(modeless, modelessParent);
            Assert.AreSame(modal, modalParent);
        }

        [Test]
        public void GetSurfaceSize_AuthoredLayer_ReturnsFixedSourceDimensions()
        {
            RectTransform rect = _view.transform as RectTransform;
            Vector2Int expected = new Vector2Int(
                Mathf.RoundToInt(rect.sizeDelta.x),
                Mathf.RoundToInt(rect.sizeDelta.y)
            );

            Vector2Int size = _view.GetSurfaceSize();

            Assert.AreEqual(expected, size);
            Assert.Greater(size.x, 0);
            Assert.Greater(size.y, 0);
        }

        [Test]
        public void GetWindowSize_NullView_ThrowsMissingReferenceException()
        {
            Assert.Throws<MissingReferenceException>(() => _view.GetWindowSize(null));
        }

        [Test]
        public void GetWindowSize_AuthoredPrefab_ReturnsFixedPrefabDimensions()
        {
            RectTransform rect = _view.PlanetSystemWindowPrefab.transform as RectTransform;
            Vector2Int expected = new Vector2Int(
                Mathf.RoundToInt(rect.sizeDelta.x),
                Mathf.RoundToInt(rect.sizeDelta.y)
            );

            Vector2Int size = _view.GetWindowSize(_view.PlanetSystemWindowPrefab);

            Assert.AreEqual(expected, size);
            Assert.Greater(size.x, 0);
            Assert.Greater(size.y, 0);
        }

        [Test]
        public void RenderModalState_Active_ShowsAndOrdersInputBlockerAndDimmer()
        {
            RawImage blocker = GetField<RawImage>("modalInputBlockerImage");
            RawImage dimmer = GetField<RawImage>("modalBackgroundDimImage");

            _view.RenderModalState(true);

            Assert.IsTrue(blocker.gameObject.activeSelf);
            Assert.IsTrue(dimmer.gameObject.activeSelf);
            Assert.AreEqual(0, blocker.transform.GetSiblingIndex());
            Assert.AreEqual(1, dimmer.transform.GetSiblingIndex());
        }

        [Test]
        public void RenderModalState_Inactive_HidesInputBlockerAndDimmer()
        {
            RawImage blocker = GetField<RawImage>("modalInputBlockerImage");
            RawImage dimmer = GetField<RawImage>("modalBackgroundDimImage");
            _view.RenderModalState(true);

            _view.RenderModalState(false);

            Assert.IsFalse(blocker.gameObject.activeSelf);
            Assert.IsFalse(dimmer.gameObject.activeSelf);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(StrategyWindowLayerView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
