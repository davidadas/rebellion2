using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    public sealed class OptionsSaveListViewTests
    {
        private GameObject _root;
        private OptionsSaveListView _view;

        /// <summary>
        /// Instantiates the actual reusable save page through its authored public contract.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = UIComponentTestHelper.InstantiatePrefab(
                "Assets/Prefabs/UI/Common/MenuSaveList.prefab"
            );
            _view = _root.GetComponent<OptionsSaveListView>();
            _view.InitializeContent(TestContent.Assets);
        }

        /// <summary>
        /// Releases the instantiated page after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetLoadOnly_Enabled_HidesSaveCommand()
        {
            _view.SetLoadOnly(true);
            Render();
            Assert.That(_root.transform.Find("SaveIcon").gameObject.activeSelf, Is.False);
            Assert.That(_root.transform.Find("SaveIconDisabled").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SetLoadOnly_Enabled_HidesDeleteCommands()
        {
            _view.SetLoadOnly(true);
            Render();
            Assert.That(
                _root
                    .GetComponentsInChildren<Button>(true)
                    .Where(button =>
                        button.name.StartsWith("SlotDelete") && button.name != "SlotDeleteTemplate"
                    )
                    .All(button => !button.gameObject.activeSelf),
                Is.True
            );
        }

        [Test]
        public void Render_SelectedLoadOnlySave_EnablesCanonicalLoadCommand()
        {
            _view.SetLoadOnly(true);
            Render();
            Assert.That(
                _root.transform.Find("LoadIcon").GetComponent<Button>().interactable,
                Is.True
            );
        }

        [Test]
        public void Render_DefaultMode_PreservesDeleteCommands()
        {
            Render();
            Assert.That(
                _root
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "SlotDelete0")
                    .gameObject.activeSelf,
                Is.True
            );
        }

        /// <summary>
        /// Renders one real-shaped saved-game row using the public presenter API.
        /// </summary>
        private void Render()
        {
            _view.Render(
                new[]
                {
                    new OptionsSaveSlot("Campaign", "Today", null, false, "campaign", "Easy", 40),
                },
                0,
                false,
                true
            );
        }
    }
}
