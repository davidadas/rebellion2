using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu
{
    [TestFixture]
    public class SaveMenuLaunchContextTests
    {
        [SetUp]
        public void SetUp()
        {
            SaveMenuLaunchContext.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            SaveMenuLaunchContext.Reset();
        }

        [Test]
        public void OpenFromMainMenu_CurrentContext_DisablesSavingAndReturnsToMainMenu()
        {
            SaveMenuLaunchContext.OpenFromStrategyView();

            SaveMenuLaunchContext.OpenFromMainMenu();

            Assert.IsFalse(SaveMenuLaunchContext.CanSave);
            Assert.AreEqual(
                SaveMenuLaunchContext.MainMenuSceneName,
                SaveMenuLaunchContext.ReturnSceneName
            );
        }

        [Test]
        public void OpenFromStrategyView_CurrentContext_EnablesSavingAndReturnsToStrategy()
        {
            SaveMenuLaunchContext.OpenFromStrategyView();

            Assert.IsTrue(SaveMenuLaunchContext.CanSave);
            Assert.AreEqual(
                SaveMenuLaunchContext.StrategyViewSceneName,
                SaveMenuLaunchContext.ReturnSceneName
            );
        }

        [Test]
        public void Reset_StrategyContext_RestoresMainMenuDefaults()
        {
            SaveMenuLaunchContext.OpenFromStrategyView();

            SaveMenuLaunchContext.Reset();

            Assert.IsFalse(SaveMenuLaunchContext.CanSave);
            Assert.AreEqual(
                SaveMenuLaunchContext.MainMenuSceneName,
                SaveMenuLaunchContext.ReturnSceneName
            );
        }
    }
}
