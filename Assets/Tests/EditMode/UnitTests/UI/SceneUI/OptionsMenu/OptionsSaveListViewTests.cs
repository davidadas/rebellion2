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

        [TestCase(true)]
        [TestCase(false)]
        public void Render_UnselectedSave_RowClickRefreshesLoadPermissionBeforeRerender(
            bool canLoad
        )
        {
            _view.Render(
                new[] { new OptionsSaveSlot("Campaign", "Today", null, false, "campaign") },
                -1,
                false,
                true,
                canLoad
            );
            Button load = _root.transform.Find("LoadIcon").GetComponent<Button>();
            int requests = 0;
            _view.LoadRequested += () => requests++;
            Assert.IsFalse(load.interactable);

            _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            load.onClick.Invoke();

            Assert.AreEqual(canLoad, load.interactable);
            Assert.AreEqual(canLoad ? 1 : 0, requests);
        }

        [Test]
        public void Render_SelectedSave_CreateNewRowImmediatelyBlocksLoad()
        {
            _view.Render(
                new[]
                {
                    new OptionsSaveSlot("Create New Save", string.Empty, null, true, null),
                    new OptionsSaveSlot("Campaign", "Today", null, false, "campaign"),
                },
                1,
                true,
                true
            );
            Button load = _root.transform.Find("LoadIcon").GetComponent<Button>();
            int requests = 0;
            _view.LoadRequested += () => requests++;
            Assert.IsTrue(load.interactable);

            _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();
            load.onClick.Invoke();

            Assert.IsFalse(load.interactable);
            Assert.AreEqual(0, requests);
        }

        [Test]
        public void SetLoadOnlyLayout_RowSelected_KeepsCommandArtworkHidden()
        {
            _view.SetLoadOnlyLayout(320, 180);
            _view.Render(
                new[] { new OptionsSaveSlot("Campaign", "Today", null, false, "campaign") },
                -1,
                false,
                true
            );

            _root
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SlotRow0")
                .onClick.Invoke();

            Assert.IsFalse(_root.transform.Find("SaveIconDisabled").gameObject.activeSelf);
            Assert.IsFalse(_root.transform.Find("LoadIconDisabled").gameObject.activeSelf);
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
