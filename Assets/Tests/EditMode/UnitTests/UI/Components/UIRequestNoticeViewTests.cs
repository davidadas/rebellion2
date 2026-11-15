using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class UIRequestNoticeViewTests
    {
        private GameObject _root;
        private UIRequestNoticeView _view;

        /// <summary>
        /// Creates the authored notice through its strategy view prefab.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
            );
            _view = _root.GetComponentInChildren<UIRequestNoticeView>(true);
        }

        /// <summary>
        /// Releases the instantiated strategy view after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Render_PlayerNameContainingMarkup_DisplaysLiteralNotice()
        {
            const string message = "<size=0>X is requesting Fast speed.";

            _view.Render(message, Color.white, true);
            TextMeshProUGUI label = _view.GetComponentInChildren<TextMeshProUGUI>(true);
            label.ForceMeshUpdate(true, true);

            Assert.AreEqual(message, label.GetParsedText());
        }
    }
}
