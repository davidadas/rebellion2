using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameMessageType = Rebellion.Game.Messages.MessageType;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessageWindowRowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";

        private Texture2D _normalIconTexture;
        private MessageWindowRowView _row;
        private Texture2D _selectedIconTexture;
        private Texture2D _selectionTexture;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _row = _windowObject.GetComponentsInChildren<MessageWindowRowView>(true).Single();
            _selectionTexture = new Texture2D(64, 16);
            _selectedIconTexture = new Texture2D(16, 16);
            _normalIconTexture = new Texture2D(16, 16);
            UIComponentTestHelper.InvokeLifecycle(_row, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_normalIconTexture);
            UnityEngine.Object.DestroyImmediate(_selectedIconTexture);
            UnityEngine.Object.DestroyImmediate(_selectionTexture);
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _row.Render(null, 0));
        }

        [Test]
        public void Render_SelectedRow_AppliesIdentitySelectionIconHeaderAndIndex()
        {
            TextMeshProUGUI header = FindChild<TextMeshProUGUI>(_row, "HeaderTextField");
            FontStyles fontStyle = header.fontStyle;
            FontWeight fontWeight = header.fontWeight;
            MessageWindowRowRenderData data = CreateRow(true, new Color32(12, 34, 56, 255));

            _row.Render(data, 4);

            Assert.AreEqual("message-7", _row.MessageId);
            Assert.AreEqual(4, _row.Index);
            Assert.IsTrue(_row.gameObject.activeSelf);
            Assert.AreSame(_selectionTexture, FindChild<RawImage>(_row, "SelectionImage").texture);
            Assert.AreSame(_selectedIconTexture, FindChild<RawImage>(_row, "IconImage").texture);
            Assert.AreEqual("Incoming transmission", header.text);
            Assert.AreEqual(new Color32(12, 34, 56, 255), (Color32)header.color);
            Assert.AreEqual(fontStyle, header.fontStyle);
            Assert.AreEqual(fontWeight, header.fontWeight);
        }

        [Test]
        public void Render_UnselectedRow_UsesNormalIconOffsetAndClearsSelection()
        {
            _row.Render(CreateRow(true, Color.white), 0);
            RectInt selectedRect = UILayout.GetSourceRect(
                FindChild<RawImage>(_row, "IconImage").rectTransform
            );

            _row.Render(CreateRow(false, Color.gray), 1);

            RectInt normalRect = UILayout.GetSourceRect(
                FindChild<RawImage>(_row, "IconImage").rectTransform
            );
            Assert.IsNull(FindChild<RawImage>(_row, "SelectionImage").texture);
            Assert.AreSame(_normalIconTexture, FindChild<RawImage>(_row, "IconImage").texture);
            Assert.AreEqual(selectedRect.x - 1, normalRect.x);
            Assert.AreEqual(selectedRect.y - 1, normalRect.y);
            Assert.AreEqual(1, _row.Index);
        }

        private MessageWindowRowRenderData CreateRow(bool selected, Color32 headerColor)
        {
            return new MessageWindowRowRenderData(
                "message-7",
                "Incoming transmission",
                GameMessageType.Mission,
                selected,
                true,
                _selectionTexture,
                _selectedIconTexture,
                _normalIconTexture,
                headerColor
            );
        }

        private static T FindChild<T>(Component parent, string objectName)
            where T : Component
        {
            return parent
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
