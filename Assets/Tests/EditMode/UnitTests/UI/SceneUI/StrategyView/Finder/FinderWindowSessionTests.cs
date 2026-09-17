using NUnit.Framework;
using Rebellion.Game.Galaxy;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowSessionTests
    {
        private GameObject _windowObject;
        private UIWindow _window;
        private FinderWindowSession _session;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject("FinderWindow", typeof(RectTransform), typeof(UIWindow));
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 12, 34, 320, 240, false, true, false);
            _session = new FinderWindowSession(_window, FinderMode.Personnel);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies constructor null window throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new FinderWindowSession(null, FinderMode.Systems)
            );
        }

        /// <summary>
        /// Verifies constructor mode initializes default state.
        /// </summary>
        [Test]
        public void Constructor_Mode_InitializesDefaultState()
        {
            Assert.AreSame(_window, _session.Window);
            Assert.AreEqual(FinderMode.Personnel, _session.Mode);
            Assert.AreEqual(0, _session.ActiveTab);
            Assert.IsFalse(_session.Panel);
            Assert.AreEqual(-1, _session.SelectedIndex);
            Assert.IsNull(_session.SelectedRow);
            Assert.AreEqual(string.Empty, _session.SearchText);
            Assert.IsEmpty(_session.ProjectedTabs);
            Assert.IsEmpty(_session.ProjectedRows);
        }

        /// <summary>
        /// Verifies constructor fleet mode selects player faction tab.
        /// </summary>
        [Test]
        public void Constructor_FleetMode_SelectsPlayerFactionTab()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Fleets);

            Assert.AreEqual(1, session.ActiveTab);
        }

        /// <summary>
        /// Verifies reconcile tab count empty then populated selects first tab.
        /// </summary>
        [Test]
        public void ReconcileTabCount_EmptyThenPopulated_SelectsFirstTab()
        {
            _session.ReconcileTabCount(0);
            _session.ReconcileTabCount(3);

            Assert.AreEqual(0, _session.ActiveTab);
        }

        /// <summary>
        /// Verifies reconcile tab count active tab beyond projection clamps to last tab.
        /// </summary>
        [Test]
        public void ReconcileTabCount_ActiveTabBeyondProjection_ClampsToLastTab()
        {
            _session.SelectTab(4);

            _session.ReconcileTabCount(2);

            Assert.AreEqual(1, _session.ActiveTab);
        }

        /// <summary>
        /// Verifies set projection source collections change preserves session snapshots.
        /// </summary>
        [Test]
        public void SetProjection_SourceCollectionsChange_PreservesSessionSnapshots()
        {
            FinderWindowTab[] tabs = { FinderWindowTab.All() };
            FinderWindowRow[] rows = { CreateRow("first", "First") };

            _session.SetProjection(tabs, rows);
            tabs[0] = FinderWindowTab.Neutral();
            rows[0] = CreateRow("replacement", "Replacement");

            Assert.IsTrue(_session.ProjectedTabs[0].IsAll);
            Assert.AreEqual("first", _session.ProjectedRows[0].Identity);
        }

        /// <summary>
        /// Verifies set projection selected identity moves preserves selection by identity.
        /// </summary>
        [Test]
        public void SetProjection_SelectedIdentityMoves_PreservesSelectionByIdentity()
        {
            FinderWindowRow first = CreateRow("first", "First");
            FinderWindowRow selected = CreateRow("selected", "Selected");
            _session.SetProjection(new[] { FinderWindowTab.All() }, new[] { first, selected });
            _session.SelectRow("selected");

            _session.SetProjection(new[] { FinderWindowTab.All() }, new[] { selected, first });

            Assert.AreEqual(0, _session.SelectedIndex);
            Assert.AreSame(selected, _session.SelectedRow);
        }

        /// <summary>
        /// Verifies set projection selected identity removed clears selection.
        /// </summary>
        [Test]
        public void SetProjection_SelectedIdentityRemoved_ClearsSelection()
        {
            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SelectRow("selected");

            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("other", "Other") }
            );

            Assert.AreEqual(-1, _session.SelectedIndex);
            Assert.IsNull(_session.SelectedRow);
        }

        /// <summary>
        /// Verifies set search text value clears selection and retains filter.
        /// </summary>
        [Test]
        public void SetSearchText_Value_ClearsSelectionAndRetainsFilter()
        {
            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SelectRow("selected");

            _session.SetSearchText("sel");

            Assert.AreEqual("sel", _session.SearchText);
            Assert.AreEqual(-1, _session.SelectedIndex);
        }

        /// <summary>
        /// Verifies set search text null normalizes to empty string.
        /// </summary>
        [Test]
        public void SetSearchText_Null_NormalizesToEmptyString()
        {
            _session.SetSearchText(null);

            Assert.AreEqual(string.Empty, _session.SearchText);
        }

        /// <summary>
        /// Verifies select panel value resets search and selection.
        /// </summary>
        [Test]
        public void SelectPanel_Value_ResetsSearchAndSelection()
        {
            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SetSearchText("Selected");
            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SelectRow("selected");

            _session.SelectPanel(true);

            Assert.IsTrue(_session.Panel);
            Assert.AreEqual(string.Empty, _session.SearchText);
            Assert.AreEqual(-1, _session.SelectedIndex);
        }

        /// <summary>
        /// Verifies select tab current tab preserves search and selection.
        /// </summary>
        [Test]
        public void SelectTab_CurrentTab_PreservesSearchAndSelection()
        {
            FinderWindowRow selected = CreateRow("selected", "Selected");
            _session.SetProjection(new[] { FinderWindowTab.All() }, new[] { selected });
            _session.SetSearchText("Selected");
            _session.SetProjection(new[] { FinderWindowTab.All() }, new[] { selected });
            _session.SelectRow("selected");

            _session.SelectTab(0);

            Assert.AreEqual("Selected", _session.SearchText);
            Assert.AreSame(selected, _session.SelectedRow);
        }

        /// <summary>
        /// Verifies select tab different tab resets search and selection.
        /// </summary>
        [Test]
        public void SelectTab_DifferentTab_ResetsSearchAndSelection()
        {
            _session.SetProjection(
                new[] { FinderWindowTab.All(), FinderWindowTab.Neutral() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SetSearchText("Selected");
            _session.SetProjection(
                new[] { FinderWindowTab.All(), FinderWindowTab.Neutral() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SelectRow("selected");

            _session.SelectTab(1);

            Assert.AreEqual(1, _session.ActiveTab);
            Assert.AreEqual(string.Empty, _session.SearchText);
            Assert.AreEqual(-1, _session.SelectedIndex);
        }

        /// <summary>
        /// Verifies select row unknown identity clears selection.
        /// </summary>
        [Test]
        public void SelectRow_UnknownIdentity_ClearsSelection()
        {
            _session.SetProjection(
                new[] { FinderWindowTab.All() },
                new[] { CreateRow("selected", "Selected") }
            );
            _session.SelectRow("selected");

            _session.SelectRow("missing");

            Assert.AreEqual(-1, _session.SelectedIndex);
            Assert.IsNull(_session.SelectedRow);
        }

        /// <summary>
        /// Verifies state current session returns complete snapshot.
        /// </summary>
        [Test]
        public void State_CurrentSession_ReturnsCompleteSnapshot()
        {
            _session.SelectPanel(true);
            _session.SelectTab(2);
            _session.SetSearchText("query");

            FinderWindowState state = _session.State;

            Assert.AreEqual(FinderMode.Personnel, state.Mode);
            Assert.IsTrue(state.Panel);
            Assert.AreEqual(2, state.ActiveTab);
            Assert.AreEqual(-1, state.SelectedIndex);
            Assert.AreEqual("query", state.SearchText);
        }

        /// <summary>
        /// Creates row.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="name">The name.</param>
        /// <returns>The created row.</returns>
        private static FinderWindowRow CreateRow(string instanceId, string name)
        {
            Planet planet = new Planet { InstanceID = instanceId, DisplayName = name };
            return new FinderWindowRow(
                name,
                new GalaxyMapPlanet(new GalaxyPlanetSector(), planet, string.Empty),
                node: planet
            );
        }
    }
}
