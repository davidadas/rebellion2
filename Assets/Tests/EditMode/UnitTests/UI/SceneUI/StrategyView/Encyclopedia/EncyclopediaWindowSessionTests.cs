using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Encyclopedia;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaWindowSessionTests
    {
        private EncyclopediaEntry _firstEntry;
        private EncyclopediaEntry _secondEntry;
        private EncyclopediaEntry _thirdEntry;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "EncyclopediaWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 10, 20, 300, 200, false, true, true);
            _firstEntry = CreateEntry("first", "Alpha");
            _secondEntry = CreateEntry("second", "Beta");
            _thirdEntry = CreateEntry("third", "Gamma");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EncyclopediaWindowSession(null));
        }

        [Test]
        public void Constructor_Window_ReturnsInitialIndexState()
        {
            EncyclopediaWindowSession session = CreateSession();

            Assert.AreSame(_window, session.Window);
            Assert.AreEqual(EncyclopediaWindowTab.AllDatabases, session.ActiveTab);
            Assert.IsFalse(session.Panel);
            Assert.IsEmpty(session.ProjectedEntries);
            Assert.AreEqual(string.Empty, session.SearchText);
            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
            Assert.AreEqual(EncyclopediaWindowTab.AllDatabases, session.State.ActiveTab);
        }

        [Test]
        public void SetProjectedEntries_SourceChanges_PreservesReadOnlySnapshot()
        {
            EncyclopediaWindowSession session = CreateSession();
            EncyclopediaEntry[] entries = { _firstEntry, _secondEntry };

            session.SetProjectedEntries(entries);
            entries[0] = _thirdEntry;

            Assert.AreEqual(2, session.ProjectedEntries.Count);
            Assert.AreSame(_firstEntry, session.ProjectedEntries[0]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EncyclopediaEntry>)session.ProjectedEntries)[0] = _thirdEntry
            );
        }

        [Test]
        public void SetProjectedEntries_EmptyProjection_ClearsSelectionAndPanel()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.ActivateRow(_firstEntry.TypeID);

            session.SetProjectedEntries(null);

            Assert.IsEmpty(session.ProjectedEntries);
            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
            Assert.IsFalse(session.Panel);
        }

        [Test]
        public void SetProjectedEntries_ReorderedProjection_PreservesSelectionIdentity()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });
            session.ActivateRow(_secondEntry.TypeID);

            session.SetProjectedEntries(new[] { _secondEntry, _firstEntry });

            Assert.AreEqual(0, session.SelectedIndex);
            Assert.AreEqual(_secondEntry.TypeID, session.SelectedTypeId);
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void SetProjectedEntries_SelectedEntryRemovedWhilePanelOpen_SelectsFirstEntry()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });
            session.ActivateRow(_secondEntry.TypeID);

            session.SetProjectedEntries(new[] { _thirdEntry });

            Assert.AreEqual(0, session.SelectedIndex);
            Assert.AreEqual(_thirdEntry.TypeID, session.SelectedTypeId);
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void OpenEntry_ProjectedEntry_SelectsTopicInAllDatabases()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SelectTab(EncyclopediaWindowTab.Ships);
            session.SetSearchText("beta");
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });

            session.OpenEntry(_secondEntry);

            Assert.AreEqual(EncyclopediaWindowTab.AllDatabases, session.ActiveTab);
            Assert.AreEqual(string.Empty, session.SearchText);
            Assert.AreEqual(1, session.SelectedIndex);
            Assert.AreEqual(_secondEntry.TypeID, session.SelectedTypeId);
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void OpenEntry_BeforeProjection_ReconcilesSelectionWhenEntriesArrive()
        {
            EncyclopediaWindowSession session = CreateSession();

            session.OpenEntry(_secondEntry);
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });

            Assert.AreEqual(1, session.SelectedIndex);
            Assert.AreEqual(_secondEntry.TypeID, session.SelectedTypeId);
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void OpenEntry_NullEntry_ReturnsToEmptyIndexSelection()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.ActivateRow(_firstEntry.TypeID);

            session.OpenEntry(null);

            Assert.AreEqual(EncyclopediaWindowTab.AllDatabases, session.ActiveTab);
            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
            Assert.IsFalse(session.Panel);
        }

        [Test]
        public void SetSearchText_Value_ClearsSelectionAndReturnsToIndex()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.ActivateRow(_firstEntry.TypeID);

            session.SetSearchText("alpha");

            Assert.AreEqual("alpha", session.SearchText);
            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
            Assert.IsFalse(session.Panel);
            session.SetSearchText(null);
            Assert.AreEqual(string.Empty, session.SearchText);
        }

        [Test]
        public void SelectTab_DifferentTab_ClearsIncompatibleState()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.SetSearchText("alpha");
            session.SetProjectedEntries(new[] { _firstEntry });
            session.ActivateRow(_firstEntry.TypeID);

            session.SelectTab(EncyclopediaWindowTab.Ships);

            Assert.AreEqual(EncyclopediaWindowTab.Ships, session.ActiveTab);
            Assert.AreEqual(string.Empty, session.SearchText);
            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
            Assert.IsFalse(session.Panel);
        }

        [Test]
        public void SelectTab_ActiveTab_PreservesState()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.ActivateRow(_firstEntry.TypeID);

            session.SelectTab(EncyclopediaWindowTab.AllDatabases);

            Assert.AreEqual(0, session.SelectedIndex);
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void SelectRow_VisibleEntry_SelectsWithoutOpeningTopic()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });

            session.SelectRow(_secondEntry.TypeID);

            Assert.AreEqual(1, session.SelectedIndex);
            Assert.AreEqual(_secondEntry.TypeID, session.SelectedTypeId);
            Assert.IsFalse(session.Panel);
        }

        [Test]
        public void SelectRow_MissingEntry_ClearsSelection()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });
            session.SelectRow(_firstEntry.TypeID);

            session.SelectRow("missing");

            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsNull(session.SelectedTypeId);
        }

        [Test]
        public void ActivateRow_VisibleEntry_OpensTopic()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });

            session.ActivateRow(_firstEntry.TypeID);

            Assert.AreEqual(0, session.SelectedIndex);
            Assert.IsTrue(session.Panel);
            session.ShowIndex();
            Assert.IsFalse(session.Panel);
            session.ShowTopic();
            Assert.IsTrue(session.Panel);
        }

        [Test]
        public void ActivateRow_MissingEntry_DoesNotOpenTopic()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry });

            session.ActivateRow("missing");

            Assert.AreEqual(-1, session.SelectedIndex);
            Assert.IsFalse(session.Panel);
        }

        [Test]
        public void MoveSelection_Entries_MovesWithinBounds()
        {
            EncyclopediaWindowSession session = CreateSession();
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry, _thirdEntry });

            bool entered = session.MoveSelection(1);
            bool moved = session.MoveSelection(1);
            bool bounded = session.MoveSelection(5);

            Assert.IsTrue(entered);
            Assert.IsTrue(moved);
            Assert.IsTrue(bounded);
            Assert.AreEqual(2, session.SelectedIndex);
            Assert.AreEqual(_thirdEntry.TypeID, session.SelectedTypeId);
            Assert.IsFalse(session.MoveSelection(1));
        }

        [Test]
        public void MoveSelection_EmptyProjection_ReturnsFalse()
        {
            EncyclopediaWindowSession session = CreateSession();

            bool moved = session.MoveSelection(1);

            Assert.IsFalse(moved);
            Assert.AreEqual(-1, session.SelectedIndex);
        }

        private static EncyclopediaEntry CreateEntry(string typeId, string displayName)
        {
            return new EncyclopediaEntry { TypeID = typeId, DisplayName = displayName };
        }

        private EncyclopediaWindowSession CreateSession()
        {
            return new EncyclopediaWindowSession(_window);
        }
    }
}
