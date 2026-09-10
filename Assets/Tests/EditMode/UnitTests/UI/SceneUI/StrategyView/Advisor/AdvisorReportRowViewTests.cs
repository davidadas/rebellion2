using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Advisor
{
    [TestFixture]
    public class AdvisorReportRowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/AdvisorReportWindow.prefab";

        private GameObject _rootObject;
        private Texture2D _texture;
        private AdvisorReportRowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject
                .GetComponentsInChildren<AdvisorReportRowView>(true)
                .Single(row => row.name == "OverviewRowTemplate");
            _texture = new Texture2D(48, 48);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_Data_AppliesTextureTextAndVisibility()
        {
            AdvisorReportRowRenderData data = new AdvisorReportRowRenderData(
                _texture,
                "Primary",
                "Secondary"
            );

            _view.Render(data);

            RawImage image = FindComponent<RawImage>("Image");
            Assert.AreSame(_texture, image.texture);
            Assert.AreEqual("Primary", FindComponent<TextMeshProUGUI>("PrimaryTextField").text);
            Assert.AreEqual("Secondary", FindComponent<TextMeshProUGUI>("SecondaryTextField").text);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _view
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
