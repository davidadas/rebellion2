using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using Rebellion.Util.Serialization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalSelectionViewTests
    {
        private GameObject _instance;
        private TacticalSelectionView _view;
        private UnityEngine.Camera _camera;
        private Transform _presentation;
        private CombatUnit _combatant;
        private Texture2D _texture;

        /// <summary>
        /// Creates the generated overlay and an independently mutable battle ship.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _view = _instance.GetComponentInChildren<TacticalSelectionView>();
            _camera = _instance.GetComponent<TacticalViewController>().BattleCamera;
            _camera.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -40f),
                Quaternion.identity
            );
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1000f;
            GameObject hull = new GameObject("Hull");
            hull.transform.SetParent(_instance.transform, false);
            _presentation = hull.transform;
            _combatant = CombatUnit.Create(
                new CapitalShip
                {
                    DisplayName = "Test Cruiser",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 75,
                    MaxShieldStrength = 50,
                    ModelSize = new ModelDimensions
                    {
                        Width = 10f,
                        Height = 6f,
                        Depth = 14f,
                    },
                }
            );
        }

        /// <summary>
        /// Releases the generated overlay and test presentation.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
            if (_texture)
                UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_SelectedShip_ShowsCurrentDurabilityWithoutTinyNameLabel()
        {
            _combatant.Condition.CurrentShieldStrength = 20;

            _view.Render(_combatant, _presentation, _camera);

            Assert.IsFalse(
                _view.GetComponentsInChildren<TMP_Text>().Any(label => label.name == "UnitName")
            );
            Assert.AreEqual("x01", FindLabel("UnitCount").text);
            Assert.AreEqual("Hull: 75 / 100", FindLabel("HullLabel").text);
            Assert.AreEqual("Shields: 20 / 50", FindLabel("ShieldLabel").text);
            Assert.That(FindFill("HullBar").anchorMax.x, Is.EqualTo(0.75f));
            Assert.That(FindFill("ShieldBar").anchorMax.x, Is.EqualTo(0.4f));
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_ChangedBattleStats_RefreshesExistingSelection()
        {
            _view.Render(_combatant, _presentation, _camera);
            _combatant.Condition.CurrentHullStrength = 30;
            _combatant.Condition.CurrentShieldStrength = 0;

            _view.Render(_combatant, _presentation, _camera);

            Assert.AreEqual("Hull: 30 / 100", FindLabel("HullLabel").text);
            Assert.AreEqual("Shields: 0 / 50", FindLabel("ShieldLabel").text);
            Assert.That(FindFill("HullBar").anchorMax.x, Is.EqualTo(0.3f));
            Assert.That(FindFill("ShieldBar").anchorMax.x, Is.Zero);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_ConfiguredFighter_UsesPerFighterHullAndBattleShields()
        {
            CombatUnit fighter = CombatUnit.Create(
                new Starfighter
                {
                    MaxHullStrength = 20,
                    CurrentHullStrength = 12,
                    ShieldStrength = 5,
                    ModelSize = new ModelDimensions
                    {
                        Width = 2f,
                        Height = 1f,
                        Depth = 3f,
                    },
                }
            );
            fighter.Condition.CurrentShieldStrength = 3;

            _view.Render(fighter, _presentation, _camera);

            Assert.AreEqual("Hull: 12 / 20", FindLabel("HullLabel").text);
            Assert.AreEqual("Shields: 3 / 5", FindLabel("ShieldLabel").text);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_UnconfiguredFighterHull_DoesNotShowInventedHealth()
        {
            CombatUnit fighter = CombatUnit.Create(
                new Starfighter
                {
                    ModelSize = new ModelDimensions
                    {
                        Width = 2f,
                        Height = 1f,
                        Depth = 3f,
                    },
                }
            );

            _view.Render(fighter, _presentation, _camera);

            Assert.AreEqual("Hull: Not configured", FindLabel("HullLabel").text);
            Assert.That(FindFill("HullBar").anchorMax.x, Is.Zero);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_UnchangedDurability_DoesNotDirtyText()
        {
            _view.Render(_combatant, _presentation, _camera);
            TMP_Text hull = FindLabel("HullLabel");
            TMP_Text shields = FindLabel("ShieldLabel");
            hull.havePropertiesChanged = false;
            shields.havePropertiesChanged = false;

            _view.Render(_combatant, _presentation, _camera);

            Assert.IsFalse(hull.havePropertiesChanged);
            Assert.IsFalse(shields.havePropertiesChanged);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_NoShieldCapacity_ShowsNoShields()
        {
            _combatant.Data.MaxShieldStrength = 0;

            _view.Render(_combatant, _presentation, _camera);

            Assert.AreEqual("Shields: None", FindLabel("ShieldLabel").text);
            Assert.That(FindFill("ShieldBar").anchorMax.x, Is.Zero);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_UnrecordedShields_DistinguishesUnknownFromDepleted()
        {
            _combatant.Condition.CurrentShieldStrength = null;

            _view.Render(_combatant, _presentation, _camera);

            Assert.AreEqual("Shields: Not recorded", FindLabel("ShieldLabel").text);
            Assert.That(FindFill("ShieldBar").anchorMax.x, Is.Zero);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        /// <param name="value">The recorded durability.</param>
        /// <param name="fraction">The clamped fill fraction.</param>
        [TestCase(-10, 0f)]
        [TestCase(150, 1f)]
        public void Render_OutOfRangeDurability_ClampsBarsWithoutChangingBattleState(
            int value,
            float fraction
        )
        {
            _combatant.Condition.CurrentHullStrength = value;
            _combatant.Condition.CurrentShieldStrength = value;

            _view.Render(_combatant, _presentation, _camera);

            Assert.AreEqual(fraction, FindFill("HullBar").anchorMax.x);
            Assert.AreEqual(fraction, FindFill("ShieldBar").anchorMax.x);
            Assert.AreEqual(value, _combatant.Condition.CurrentHullStrength);
            Assert.AreEqual(value, _combatant.Condition.CurrentShieldStrength);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_NoSelection_HidesBracketsAndKeepsEmptyDock()
        {
            _view.Render(_combatant, _presentation, _camera);

            _view.Render(null, null, null);

            Assert.IsFalse(_view.transform.Find("SelectionFrame").gameObject.activeSelf);
            Assert.IsTrue(_view.transform.Find("BattleDock").gameObject.activeSelf);
            Assert.AreEqual(0, _view.GetComponentsInChildren<TacticalUnitCardView>().Length);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_CameraMoves_UpdatesProjectedBrackets()
        {
            _view.Render(_combatant, _presentation, _camera);
            RectTransform frame = (RectTransform)_view.transform.Find("SelectionFrame");
            Vector2 previous = frame.anchoredPosition;
            _camera.transform.position += Vector3.right * 4f;

            _view.Render(_combatant, _presentation, _camera);

            Assert.That(frame.anchoredPosition.x, Is.LessThan(previous.x));
        }

        /// <summary>
        /// Verifies projected bracket geometry at multiple aspect ratios.
        /// </summary>
        /// <param name="aspect">The camera aspect ratio.</param>
        [TestCase(4f / 3f)]
        [TestCase(16f / 9f)]
        [TestCase(21f / 9f)]
        public void Render_VisibleHull_BracketsEncloseProjectedGeometry(float aspect)
        {
            _camera.aspect = aspect;
            Canvas.ForceUpdateCanvases();

            _view.Render(_combatant, _presentation, _camera);
            Canvas.ForceUpdateCanvases();

            RectTransform frame = (RectTransform)_view.transform.Find("SelectionFrame");
            Vector3[] corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            for (int index = 0; index < 8; index++)
            {
                Vector3 corner = new Vector3(
                    (index & 1) == 0 ? -5f : 5f,
                    (index & 2) == 0 ? -3f : 3f,
                    (index & 4) == 0 ? -7f : 7f
                );
                Vector3 projected = _camera.WorldToScreenPoint(corner);
                Assert.That(projected.x, Is.InRange(minimum.x, maximum.x));
                Assert.That(projected.y, Is.InRange(minimum.y, maximum.y));
            }
        }

        /// <summary>
        /// Verifies offscreen selection retains the roster card.
        /// </summary>
        /// <param name="x">The hull X coordinate.</param>
        /// <param name="y">The hull Y coordinate.</param>
        /// <param name="z">The hull Z coordinate.</param>
        [TestCase(0f, 0f, -60f)]
        [TestCase(1000f, 0f, 0f)]
        public void Render_OffscreenHull_HidesBracketsButKeepsStatus(float x, float y, float z)
        {
            _presentation.position = new Vector3(x, y, z);

            _view.Render(_combatant, _presentation, _camera);

            Assert.IsFalse(_view.transform.Find("SelectionFrame").gameObject.activeSelf);
            Assert.IsTrue(_view.transform.Find("BattleDock").gameObject.activeSelf);
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void Render_SelectionGraphics_DoNotInterceptClicksOutsidePanel()
        {
            _view.Render(_combatant, _presentation, _camera);

            foreach (
                Graphic graphic in _view
                    .transform.Find("SelectionFrame")
                    .GetComponentsInChildren<Graphic>()
            )
                Assert.IsFalse(graphic.raycastTarget, graphic.name);
            Assert.IsFalse(_view.transform.Find("BattleDock").GetComponent<Graphic>().enabled);
            Assert.IsTrue(
                _view
                    .transform.Find("BattleDock/RosterHousing")
                    .GetComponent<Graphic>()
                    .raycastTarget
            );
        }

        /// <summary>
        /// Verifies a hovered hostile uses the same projected durability presentation as a selected ship.
        /// </summary>
        [Test]
        public void RenderContacts_HoveredHostile_UsesProjectedShipDurabilityFrame()
        {
            _combatant.Condition.CurrentShieldStrength = 20;
            Dictionary<CombatUnit, Transform> presentations = new Dictionary<CombatUnit, Transform>
            {
                [_combatant] = _presentation,
            };

            _view.RenderContacts(Array.Empty<CombatUnit>(), _combatant, presentations, _camera);

            Transform frame = _view.transform.Find("HoverFrame");
            Assert.IsTrue(frame.gameObject.activeSelf);
            Assert.That(((RectTransform)frame.Find("HullBar/Fill")).anchorMax.x, Is.EqualTo(0.75f));
            Assert.That(
                ((RectTransform)frame.Find("ShieldBar/Fill")).anchorMax.x,
                Is.EqualTo(0.4f)
            );
            Assert.IsNull(_view.transform.Find("HoverDurability"));
        }

        /// <summary>
        /// Verifies the selected-unit presentation contract.
        /// </summary>
        [Test]
        public void RenderSelection_TwoThenThreeShips_KeepsRosterWellWidth()
        {
            RectTransform well = (RectTransform)_view.transform.Find("BattleDock/RosterHousing");
            Canvas.ForceUpdateCanvases();
            float width = well.rect.width;
            CombatUnit second = CopyCombatant(_combatant);
            CombatUnit third = CopyCombatant(_combatant);
            Dictionary<CombatUnit, Transform> roots = new Dictionary<CombatUnit, Transform>
            {
                [_combatant] = _presentation,
                [second] = _presentation,
                [third] = _presentation,
            };
            _view.RenderSelection(new[] { _combatant, second }, roots, _camera);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(2, _view.GetComponentsInChildren<TacticalUnitCardView>().Length);
            Assert.AreEqual(width, well.rect.width);

            _view.RenderSelection(new[] { _combatant, second, third }, roots, _camera);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(3, _view.GetComponentsInChildren<TacticalUnitCardView>().Length);
            Assert.AreEqual(width, well.rect.width);
        }

        /// <summary>
        /// Verifies fixed-height, edge-anchored hardware on wide screens.
        /// </summary>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        [TestCase(1280, 720)]
        [TestCase(2560, 1080)]
        [TestCase(3840, 1080)]
        public void RenderSelection_WideScreens_KeepsControlsInsideBottomDock(int width, int height)
        {
            Canvas canvas = _view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            RenderTexture target = new RenderTexture(width, height, 0);
            try
            {
                _camera.targetTexture = target;
                canvas.worldCamera = _camera;
                Canvas.ForceUpdateCanvases();
                RectTransform dock = (RectTransform)_view.transform.Find("BattleDock");
                RectTransform instruments = (RectTransform)dock.Find("InstrumentsHousing");
                RectTransform commands = (RectTransform)dock.Find("CommandsHousing");
                RectTransform follow = (RectTransform)dock.Find("FollowSelected");
                Assert.AreEqual(288f, dock.rect.height);
                Assert.That(instruments.rect.width, Is.EqualTo(250f));
                Assert.That(commands.rect.width, Is.EqualTo(218f));
                Assert.That(
                    ((RectTransform)dock.Find("RosterHousing")).rect.width,
                    Is.GreaterThan(0f)
                );
                Assert.AreEqual(Vector2.zero, dock.anchorMin);
                Assert.AreEqual(new Vector2(1f, 0f), dock.anchorMax);
                Assert.AreEqual(8f, dock.offsetMin.x);
                Assert.AreEqual(-8f, dock.offsetMax.x);
                Assert.That(
                    dock.rect.width,
                    Is.EqualTo(((RectTransform)dock.parent).rect.width - 16f).Within(0.1f)
                );
                foreach (RectTransform panel in new[] { instruments, commands, follow })
                {
                    Vector3[] corners = new Vector3[4];
                    panel.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 local = dock.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(dock.rect.xMin, dock.rect.xMax));
                        Assert.That(local.y, Is.InRange(dock.rect.yMin, dock.rect.yMax));
                    }
                }
            }
            finally
            {
                _camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Verifies all nine placeholder buttons fit in a regular three-by-three grid.
        /// </summary>
        [Test]
        public void RenderSelection_CommandGrid_KeepsNineButtonsInsidePanel()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform panel = (RectTransform)_view.transform.Find("BattleDock/CommandsHousing");
            TacticalCommandButton[] buttons =
                panel.GetComponentsInChildren<TacticalCommandButton>();

            Assert.AreEqual(9, buttons.Length);
            Assert.AreEqual(
                3,
                buttons
                    .Select(button => ((RectTransform)button.transform).anchoredPosition.x)
                    .Distinct()
                    .Count()
            );
            Assert.AreEqual(
                3,
                buttons
                    .Select(button => ((RectTransform)button.transform).anchoredPosition.y)
                    .Distinct()
                    .Count()
            );
            foreach (TacticalCommandButton button in buttons)
            {
                RectTransform rect = (RectTransform)button.transform;
                Assert.IsTrue(button.interactable);
                Assert.That(rect.anchoredPosition.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(rect.anchoredPosition.y, Is.GreaterThanOrEqualTo(0f));
                Assert.That(
                    rect.anchoredPosition.x + rect.rect.width,
                    Is.LessThanOrEqualTo(panel.rect.width)
                );
                Assert.That(
                    rect.anchoredPosition.y + rect.rect.height,
                    Is.LessThanOrEqualTo(panel.rect.height)
                );
            }
        }

        [Test]
        public void RenderControlGroups_AssignedGroup_ShowsActiveNumberAboveRoster()
        {
            int[] counts = new int[TacticalSelectionController.ControlGroupCount];
            counts[0] = 2;

            _view.RenderControlGroups(counts, 0);

            Transform row = _view.transform.Find("BattleDock/RosterHousing/ControlGroups");
            Button[] buttons = row.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(TacticalSelectionController.ControlGroupCount, buttons.Length);
            Assert.IsTrue(buttons[0].interactable);
            Assert.IsTrue(buttons[1].interactable);
            Assert.AreEqual("1", buttons[0].GetComponentInChildren<TMP_Text>().text);
            Assert.That(
                ((RectTransform)row).anchoredPosition.y,
                Is.GreaterThan(((RectTransform)row.parent).rect.height)
            );
        }

        [Test]
        public void BindBattle_FriendlyOfficerAboardShip_ShowsPortraitAboveHull()
        {
            CapitalShip source = new CapitalShip
            {
                OwnerInstanceID = "PLAYER",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ModelSize = new ModelDimensions
                {
                    Width = 10f,
                    Height = 6f,
                    Depth = 14f,
                },
            };
            Officer officer = new Officer
            {
                InstanceID = "OFFICER",
                OwnerInstanceID = "PLAYER",
                SmallDisplayImagePath = "Pack/Officer/small-display",
            };
            source.AddChild(officer);
            officer.SetParent(source);
            CombatUnit combatant = CombatUnit.Create(source);
            _texture = new Texture2D(4, 2);
            Dictionary<CombatUnit, Transform> presentations = new Dictionary<CombatUnit, Transform>
            {
                [combatant] = _presentation,
            };
            _view.BindBattle(
                presentations,
                new BattleMapBounds
                {
                    MinimumX = -100f,
                    MaximumX = 100f,
                    MinimumY = -100f,
                    MaximumY = 100f,
                    MinimumZ = -100f,
                    MaximumZ = 100f,
                },
                _camera,
                "PLAYER",
                Array.Empty<CombatUnit>()
            );
            UnityEngine.Object.DestroyImmediate(
                _view.transform.Find("OfficerMarkerTemplate").GetComponent<Button>()
            );
            CombatUnit clicked = null;
            _view.OfficerMarkerClicked += combatant => clicked = combatant;
            _view.ApplyTheme(
                new BattleHudTheme { ContactColorHex = "#FF00FF" },
                new FixedTextureAssetSource(_texture)
            );

            _view.RenderSelection(Array.Empty<CombatUnit>(), presentations, _camera);

            Transform markerRoot = _view.transform.Find("OfficerMarker OFFICER");
            RawImage marker = markerRoot
                .Find("PortraitViewport/PortraitImage")
                .GetComponent<RawImage>();
            RawImage background = markerRoot
                .Find("PortraitViewport/PortraitBackground")
                .GetComponent<RawImage>();
            Assert.AreSame(_texture, marker.texture);
            Assert.AreEqual(new Rect(0.25f, 0f, 0.5f, 1f), marker.uvRect);
            Assert.AreEqual(new Vector2(40f, 40f), ((RectTransform)markerRoot).sizeDelta);
            Assert.AreEqual(new Color(0.08f, 0.32f, 0.52f), background.color);
            Assert.IsNull(markerRoot.GetComponentInChildren<Mask>(includeInactive: true));
            Assert.AreEqual(Color.magenta, markerRoot.GetComponent<Image>().color);
            Assert.IsTrue(markerRoot.gameObject.activeSelf);
            markerRoot.GetComponent<Button>().onClick.Invoke();
            Assert.AreSame(combatant, clicked);
        }

        /// <summary>
        /// Verifies carrier commands occupy the remaining three authored command slots.
        /// </summary>
        [Test]
        public void RenderSelection_CarrierCommandSlots_ShowLaunchRecallAndDock()
        {
            Transform panel = _view.transform.Find("BattleDock/CommandsHousing");

            foreach (string command in new[] { "Launch", "Recall", "Dock" })
            {
                Transform button = panel.Find(command);
                Assert.AreEqual(command, button.GetComponentInChildren<TMP_Text>().text);
                Assert.IsNull(button.Find("KeyFace/Symbol"));
            }
        }

        /// <summary>
        /// Verifies scrolling uses authored card widths rather than an obsolete fixed stride.
        /// </summary>
        [Test]
        public void RenderSelection_LargeSelection_ScrollContentEnclosesLastCard()
        {
            CombatUnit[] selected = Enumerable
                .Range(0, 40)
                .Select(_ => CopyCombatant(_combatant))
                .ToArray();

            _view.RenderSelection(selected, null, null);
            Canvas.ForceUpdateCanvases();

            ScrollRect scroll = _view.GetComponentInChildren<ScrollRect>();
            TacticalUnitCardView last = scroll
                .content.GetComponentsInChildren<TacticalUnitCardView>()
                .Last();
            RectTransform card = (RectTransform)last.transform;
            Vector3[] corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
            Assert.That(
                scroll.content.InverseTransformPoint(corners[2]).x,
                Is.EqualTo(scroll.content.rect.xMax).Within(0.1f)
            );
        }

        /// <summary>
        /// Verifies equal ship definitions share one roster card with averaged durability.
        /// </summary>
        [Test]
        public void RenderSelection_SameShipType_GroupsMembersAndAveragesBars()
        {
            _combatant.Data.TypeID = "CRUISER";
            CombatUnit second = CopyCombatant(_combatant);
            second.Condition.CurrentHullStrength = 25;
            second.Condition.CurrentShieldStrength = 0;
            _combatant.Condition.CurrentShieldStrength = 50;

            _view.RenderSelection(new[] { _combatant, second }, null, null);

            TacticalUnitCardView card = FindCard();
            CollectionAssert.AreEqual(new[] { _combatant, second }, card.Combatants);
            Assert.AreEqual("x02", FindLabel("UnitCount").text);
            Assert.AreEqual("Hull: 100 / 200", FindLabel("HullLabel").text);
            Assert.That(FindFill("HullBar").anchorMax.x, Is.EqualTo(0.5f));
            Assert.That(FindFill("ShieldBar").anchorMax.x, Is.EqualTo(0.5f));
        }

        /// <summary>
        /// Verifies a new selection cannot reuse stale grouped membership.
        /// </summary>
        [Test]
        public void RenderSelection_ChangedGroupMembers_RebindsCardAndUpdatesCount()
        {
            _combatant.Data.TypeID = "CRUISER";
            CombatUnit second = CopyCombatant(_combatant);
            _view.RenderSelection(new[] { _combatant, second }, null, null);
            TacticalUnitCardView card = FindCard();

            _view.RenderSelection(new[] { second }, null, null);

            Assert.AreSame(card, FindCard());
            CollectionAssert.AreEqual(new[] { second }, card.Combatants);
            Assert.AreEqual("x01", FindLabel("UnitCount").text);
        }

        /// <summary>
        /// Verifies equal definitions owned by opponents stay in separate cards.
        /// </summary>
        [Test]
        public void RenderSelection_OpposingOwners_DoesNotMergeCards()
        {
            _combatant.Data.TypeID = "CRUISER";
            _combatant.Data.OwnerInstanceID = "FNEMP1";
            CombatUnit second = CopyCombatant(_combatant);
            second.Data.OwnerInstanceID = "FNALL1";

            _view.RenderSelection(new[] { _combatant, second }, null, null);

            Assert.AreEqual(2, _view.GetComponentsInChildren<TacticalUnitCardView>().Length);
        }

        /// <summary>
        /// Verifies separate squadrons of one fighter definition collapse into one command card.
        /// </summary>
        [Test]
        public void RenderSelection_SameFighterTypeAcrossSquadrons_GroupsIntoOneCard()
        {
            Starfighter definition = new Starfighter
            {
                TypeID = "FIGHTER",
                InstanceID = "SQUADRON1",
            };
            CombatUnit first = CombatUnit.Create(definition);
            definition.InstanceID = "SQUADRON2";
            CombatUnit second = CombatUnit.Create(definition);

            _view.RenderSelection(new[] { first, second }, null, null);

            TacticalUnitCardView card = FindCard();
            CollectionAssert.AreEqual(new[] { first, second }, card.Combatants);
            Assert.AreEqual("x02", FindLabel("UnitCount").text);
        }

        /// <summary>
        /// Verifies a fleet-scale selection produces one card per unit definition without
        /// collapsing the represented command members.
        /// </summary>
        [Test]
        public void RenderSelection_MassiveTypedFleet_GroupsIntoOrderedTypeCards()
        {
            Starfighter fighter = new Starfighter
            {
                TypeID = "FIGHTER",
                DisplayName = "Fighter",
                OwnerInstanceID = "PLAYER",
            };
            CapitalShip capital = new CapitalShip
            {
                TypeID = "CAPITAL",
                DisplayName = "Capital",
                OwnerInstanceID = "PLAYER",
            };
            CombatUnit[] fighters = Enumerable
                .Range(0, 40)
                .Select(index =>
                {
                    fighter.InstanceID = $"FIGHTER-{index}";
                    return CombatUnit.Create(fighter);
                })
                .ToArray();
            CombatUnit[] capitals = Enumerable
                .Range(0, 20)
                .Select(index =>
                {
                    capital.InstanceID = $"CAPITAL-{index}";
                    return CombatUnit.Create(capital);
                })
                .ToArray();

            _view.RenderSelection(capitals.Concat(fighters).ToArray(), null, null);

            TacticalUnitCardView[] cards = _view.GetComponentsInChildren<TacticalUnitCardView>();
            Assert.AreEqual(2, cards.Length);
            Assert.That(cards[0].Combatant.Data.Kind, Is.EqualTo(CombatUnitKind.Starfighter));
            Assert.AreEqual(40, cards[0].Combatants.Count);
            Assert.That(cards[1].Combatant.Data.Kind, Is.EqualTo(CombatUnitKind.CapitalShip));
            Assert.AreEqual(20, cards[1].Combatants.Count);
        }

        /// <summary>
        /// Verifies sorting the roster does not make an unchanged controller-order selection look
        /// changed again on every rendered frame.
        /// </summary>
        [Test]
        public void RenderSelection_UnsortedUnchangedSelection_DoesNotRebindTypeCards()
        {
            _combatant.Data.TypeID = "CAPITAL";
            CombatUnit fighter = CombatUnit.Create(
                new Starfighter { TypeID = "FIGHTER", DisplayName = "Fighter" }
            );
            CombatUnit[] selected = { _combatant, fighter };
            _view.RenderSelection(selected, null, null);
            TMP_Text[] stableLabels = _view
                .GetComponentsInChildren<TacticalUnitCardView>()
                .SelectMany(card => card.GetComponentsInChildren<TMP_Text>(true))
                .Where(label => label.name == "UnitCount")
                .ToArray();
            foreach (TMP_Text label in stableLabels)
                label.havePropertiesChanged = false;

            _view.RenderSelection(selected, null, null);

            Assert.That(
                stableLabels,
                Has.All.Matches<TMP_Text>(label => !label.havePropertiesChanged)
            );
        }

        /// <summary>
        /// Verifies the roster matches the command-panel height without blocking the upper dock.
        /// </summary>
        [Test]
        public void RenderSelection_EmptyDock_MatchesCommandHeight()
        {
            RectTransform dock = (RectTransform)_view.transform.Find("BattleDock");
            RectTransform commands = (RectTransform)dock.Find("CommandsHousing");
            RectTransform roster = (RectTransform)dock.Find("RosterHousing");
            Assert.AreEqual(new Vector2(260f, 0f), commands.anchoredPosition);
            Assert.AreEqual(226f, commands.rect.height);
            Assert.AreEqual(commands.rect.height, roster.rect.height);
            Assert.AreEqual(488f, roster.offsetMin.x);
            Assert.IsFalse(dock.GetComponent<Graphic>().enabled);
        }

        /// <summary>
        /// Checks the roster fills two rows before starting the next column.
        /// </summary>
        [Test]
        public void RenderSelection_FourCards_UsesTwoRows()
        {
            CombatUnit[] selected = Enumerable
                .Range(0, 4)
                .Select(_ => CopyCombatant(_combatant))
                .ToArray();

            _view.RenderSelection(selected, null, null);
            Canvas.ForceUpdateCanvases();

            RectTransform[] cards = _view
                .GetComponentsInChildren<TacticalUnitCardView>()
                .Select(card => (RectTransform)card.transform)
                .ToArray();
            Assert.AreEqual(4, cards.Length);
            Assert.AreEqual(cards[0].anchoredPosition.x, cards[1].anchoredPosition.x);
            Assert.AreEqual(cards[0].anchoredPosition.y, cards[2].anchoredPosition.y);
            Assert.AreEqual(cards[1].anchoredPosition.y, cards[3].anchoredPosition.y);
            Assert.That(cards[0].anchoredPosition.y, Is.GreaterThan(cards[1].anchoredPosition.y));
            Assert.That(cards[2].anchoredPosition.x, Is.GreaterThan(cards[0].anchoredPosition.x));
        }

        /// <summary>
        /// Checks drag selection retains an opaque white outline.
        /// </summary>
        [Test]
        public void RenderMarquee_VisibleDrag_UsesWhiteRectangle()
        {
            _view.RenderMarquee(new Vector2(100f, 100f), new Vector2(300f, 250f), true);
            Canvas.ForceUpdateCanvases();

            TacticalHudGraphic marquee = _view
                .transform.Find("Marquee")
                .GetComponent<TacticalHudGraphic>();
            Assert.IsTrue(marquee.gameObject.activeSelf);
            Assert.AreEqual(Color.white, marquee.color);
            Mesh mesh = marquee.canvasRenderer.GetMesh();
            try
            {
                Assert.IsNotNull(mesh);
                Assert.AreEqual(16, mesh.vertexCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// Checks selecting a friendly carrier exposes its hidden squadron through the existing roster.
        /// </summary>
        [Test]
        public void RenderSelection_FriendlyCarrier_ShowsDockedCargoAndEnablesLaunch()
        {
            ActiveBattle battle = new ActiveBattle { Kind = BattleKind.Space };
            CapitalShip source = new CapitalShip
            {
                OwnerInstanceID = "PLAYER",
                StarfighterCapacity = 1,
            };
            Starfighter squadron = new Starfighter
            {
                OwnerInstanceID = "PLAYER",
                CurrentSquadronSize = 3,
                SublightSpeed = 10,
                TypeID = "FIGHTER",
            };
            source.AddChild(squadron);
            squadron.SetParent(source);
            CombatUnit carrier = battle.AddCombatants(source)[0];
            battle.AddCombatants(squadron);
            TacticalCarrierOperations operations = new TacticalCarrierOperations(
                battle,
                new Dictionary<CombatUnit, System.Numerics.Vector3[][]>
                {
                    [carrier] = new[]
                    {
                        new[] { System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitZ },
                    },
                }
            );
            _view.BindCarrierOperations(operations, "PLAYER");
            _view.RenderSelection(new[] { carrier }, null, null);
            TacticalUnitCardView[] cards = _view.GetComponentsInChildren<TacticalUnitCardView>();
            Assert.That(cards.Length, Is.EqualTo(2));
            Assert.That(cards[0].Combatant.Data.Kind, Is.EqualTo(CombatUnitKind.Starfighter));
            Assert.That(cards[1].Combatant.Data.Kind, Is.EqualTo(CombatUnitKind.CapitalShip));
            Assert.That(
                cards
                    .Single(card => card.Combatant.Data.Kind == CombatUnitKind.Starfighter)
                    .Combatants.Count,
                Is.EqualTo(1)
            );
            Assert.IsTrue(_view.LaunchButton.interactable);
            Assert.IsFalse(_view.RecallButton.interactable);
        }

        /// <summary>
        /// Checks camera following lives outside the unchanged nine-command panel.
        /// </summary>
        [Test]
        public void FollowButton_AuthoredDock_IsSeparateFromNineCommandPanel()
        {
            Transform dock = _view.transform.Find("BattleDock");
            Assert.AreSame(dock, _view.FollowButton.transform.parent);
            Assert.That(
                dock.Find("CommandsHousing")
                    .GetComponentsInChildren<TacticalCommandButton>()
                    .Length,
                Is.EqualTo(9)
            );
            Assert.That(
                ((RectTransform)_view.FollowButton.transform).anchoredPosition.y,
                Is.GreaterThan(226f)
            );
            Assert.IsFalse(_view.FollowButton.interactable);
        }

        /// <summary>
        /// Returns the active selected-unit card.
        /// </summary>
        /// <returns>The visible roster card.</returns>
        private TacticalUnitCardView FindCard() =>
            _view.GetComponentsInChildren<TacticalUnitCardView>().Single();

        /// <summary>
        /// Returns one authored status label without accessing private view state.
        /// </summary>
        /// <param name="name">The label name.</param>
        /// <returns>The authored text component.</returns>
        private TMP_Text FindLabel(string name) =>
            FindCard().GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == name);

        /// <summary>
        /// Creates an independent roster member while restoring its fixture-owned geometry.
        /// </summary>
        /// <param name="source">The combatant supplying the fixture state.</param>
        /// <returns>The independently identified combatant.</returns>
        private static CombatUnit CopyCombatant(CombatUnit source)
        {
            CombatUnit copy = SerializationHelper.Deserialize<CombatUnit>(
                SerializationHelper.Serialize(source)
            );
            copy.InstanceID = Guid.NewGuid().ToString("N");
            copy.Data.ModelSize = source.Data.ModelSize?.CreateCopy();
            return copy;
        }

        /// <summary>
        /// Returns one authored durability fill without accessing private view state.
        /// </summary>
        /// <param name="name">The bar name.</param>
        /// <returns>The fill rectangle.</returns>
        private RectTransform FindFill(string name) =>
            (RectTransform)FindCard().transform.Find($"{name}/Fill");

        private sealed class FixedTextureAssetSource : IContentAssetSource
        {
            private readonly Texture2D _texture;

            /// <summary>
            /// Creates an asset source returning one known portrait texture.
            /// </summary>
            /// <param name="texture">The texture returned for every texture address.</param>
            public FixedTextureAssetSource(Texture2D texture) => _texture = texture;

            /// <summary>
            /// Returns the fixture portrait texture.
            /// </summary>
            /// <param name="address">The ignored content address.</param>
            /// <returns>The fixture texture.</returns>
            public Texture2D GetTexture(string address) => _texture;

            /// <summary>
            /// Returns no sprite because this fixture resolves portraits only.
            /// </summary>
            /// <param name="address">The ignored content address.</param>
            /// <returns>Null.</returns>
            public Sprite GetSprite(string address) => null;

            /// <summary>
            /// Returns no bordered sprite because this fixture resolves portraits only.
            /// </summary>
            /// <param name="address">The ignored content address.</param>
            /// <param name="border">The ignored border.</param>
            /// <returns>Null.</returns>
            public Sprite GetSprite(string address, Vector4 border) => null;
        }
    }
}
