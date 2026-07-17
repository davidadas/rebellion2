using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/ConstructionWindow.prefab";

        private Texture2D _texture;
        private ConstructionWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<ConstructionWindowView>();
            _texture = new Texture2D(90, 45);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_SelectedItemAndOpenDropdown_AppliesCompletePresentation()
        {
            ConstructionWindowRenderData data = CreateRenderData(
                new[]
                {
                    new StrategyDropdownItemRenderData(_texture, "First", Color.gray),
                    new StrategyDropdownItemRenderData(_texture, "Second", Color.white),
                },
                true,
                true
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(17, windowRect.x);
            Assert.AreEqual(29, windowRect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("SelectedItemImage").texture);
            Assert.AreEqual(
                "Selected",
                FindComponent<TextMeshProUGUI>("SelectedNameTextField").text
            );
            Assert.AreEqual("4", FindComponent<TextMeshProUGUI>("BuildCountTextField").text);
            Assert.AreEqual(
                "120",
                FindComponent<TextMeshProUGUI>("ConstructionCostTextField").text
            );
            Assert.AreEqual("16", FindComponent<TextMeshProUGUI>("MaintenanceCostTextField").text);
            Assert.AreEqual("9", FindComponent<TextMeshProUGUI>("CompletionValueTextField").text);
            Assert.AreEqual("12", FindComponent<TextMeshProUGUI>("DeploymentValueTextField").text);
            Assert.IsTrue(FindObject("CompletionDaysTextField").activeSelf);
            Assert.IsTrue(FindObject("DeploymentDaysTextField").activeSelf);
            Assert.IsTrue(FindObject("Dropdown").activeSelf);
            Assert.IsTrue(FindComponent<Button>("OkButtonImage").interactable);
            StrategyDropdownItemView[] rows = _viewObject
                .GetComponentsInChildren<StrategyDropdownItemView>(true)
                .Where(row =>
                    row.name.StartsWith("DropdownItemRow", StringComparison.Ordinal)
                    && row.name != "DropdownItemRowTemplate"
                )
                .OrderBy(row => row.Index)
                .ToArray();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("First", FindRowText(rows[0]).text);
            Assert.AreEqual("Second", FindRowText(rows[1]).text);
            Assert.IsTrue(rows[0].gameObject.activeSelf);
            Assert.IsTrue(rows[1].gameObject.activeSelf);
        }

        [Test]
        public void Render_EmptySelectionAfterOpenDropdown_HidesSelectionAndCachedRows()
        {
            _view.Render(
                CreateRenderData(
                    new[] { new StrategyDropdownItemRenderData(_texture, "First", Color.white) },
                    true,
                    true
                )
            );
            StrategyDropdownItemView row = _viewObject
                .GetComponentsInChildren<StrategyDropdownItemView>(true)
                .Single(item => item.name == "DropdownItemRow0");

            _view.Render(
                CreateRenderData(Array.Empty<StrategyDropdownItemRenderData>(), false, false)
            );

            Assert.IsFalse(FindObject("SelectedItemImage").activeSelf);
            Assert.IsFalse(FindObject("SelectedNameTextField").activeSelf);
            Assert.IsFalse(FindObject("BuildCountTextField").activeSelf);
            Assert.IsFalse(FindObject("CompletionDaysTextField").activeSelf);
            Assert.IsFalse(FindObject("DeploymentDaysTextField").activeSelf);
            Assert.IsFalse(FindObject("Dropdown").activeSelf);
            Assert.IsFalse(row.gameObject.activeSelf);
        }

        [Test]
        public void Render_UnavailableSelection_DisablesStartAndDayLabels()
        {
            ConstructionWindowRenderData data = new ConstructionWindowRenderData(
                0,
                0,
                null,
                _texture,
                "Selected",
                1,
                "1",
                "1",
                "N/A",
                false,
                "N/A",
                false,
                false,
                false,
                new[] { new StrategyDropdownItemRenderData(_texture, "Selected", Color.white) }
            );

            _view.Render(data);

            Assert.IsFalse(FindComponent<Button>("OkButtonImage").interactable);
            Assert.IsFalse(FindObject("CompletionDaysTextField").activeSelf);
            Assert.IsFalse(FindObject("DeploymentDaysTextField").activeSelf);
        }

        [Test]
        public void RequestMethods_SubscribedHandlers_EmitSemanticRequests()
        {
            int cancelCount = 0;
            int decrementCount = 0;
            int incrementCount = 0;
            int infoCount = 0;
            int startCount = 0;
            int toggleCount = 0;
            _view.CancelRequested += _ => cancelCount++;
            _view.DecrementRequested += _ => decrementCount++;
            _view.IncrementRequested += _ => incrementCount++;
            _view.InfoRequested += _ => infoCount++;
            _view.StartRequested += _ => startCount++;
            _view.ToggleDropdownRequested += _ => toggleCount++;

            _view.RequestCancel();
            _view.RequestDecrement();
            _view.RequestIncrement();
            _view.RequestInfo();
            _view.RequestStart();
            _view.RequestToggleDropdown();

            Assert.AreEqual(1, cancelCount);
            Assert.AreEqual(1, decrementCount);
            Assert.AreEqual(1, incrementCount);
            Assert.AreEqual(1, infoCount);
            Assert.AreEqual(1, startCount);
            Assert.AreEqual(1, toggleCount);
        }

        [Test]
        public void OnPointerClick_OpenDropdownAndLeftButton_RequestsDismissal()
        {
            int dismissCount = 0;
            _view.DismissDropdownRequested += _ => dismissCount++;
            _view.Render(
                CreateRenderData(
                    new[] { new StrategyDropdownItemRenderData(_texture, "First", Color.white) },
                    true,
                    true
                )
            );
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };
            PointerEventData leftClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            _view.OnPointerClick(rightClick);
            _view.OnPointerClick(leftClick);

            Assert.AreEqual(1, dismissCount);
        }

        [Test]
        public void GetDropdownScrollContentHeight_ItemCount_ScalesAuthoredRowHeight()
        {
            int oneRowHeight = _view.GetDropdownScrollContentHeight(1);

            int threeRowHeight = _view.GetDropdownScrollContentHeight(3);

            Assert.Greater(oneRowHeight, 0);
            Assert.AreEqual(oneRowHeight * 3, threeRowHeight);
        }

        private ConstructionWindowRenderData CreateRenderData(
            StrategyDropdownItemRenderData[] items,
            bool dropdownOpen,
            bool canStart
        )
        {
            return new ConstructionWindowRenderData(
                17,
                29,
                _texture,
                _texture,
                "Selected",
                4,
                "120",
                "16",
                "9",
                true,
                "12",
                true,
                dropdownOpen,
                canStart,
                items
            );
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindRowText(StrategyDropdownItemView row)
        {
            return row.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == "ItemTextField");
        }
    }
}
