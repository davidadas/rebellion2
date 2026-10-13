using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public sealed class StrategyControllerTests
    {
        private GameObject _gameObject;

        /// <summary>
        /// Destroys the test controller after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void CancelIdleBarItemDrag_PresentationNotActivated_DoesNothing()
        {
            _gameObject = new GameObject("StrategyControllerTest");
            StrategyController controller = _gameObject.AddComponent<StrategyController>();

            Assert.DoesNotThrow(() => ((IIdleBarActions)controller).CancelIdleBarItemDrag());
        }
    }
}
