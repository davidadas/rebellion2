using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    /// <summary>
    /// Builds a minimal tactical battle view hierarchy mirroring the authored prefab layout.
    /// </summary>
    internal static class TacticalBattleViewTestFactory
    {
        internal const float SurfaceWidth = 853.3333f;
        internal const float SurfaceHeight = 480f;
        internal const int PanelWidth = 213;
        internal const int IndicatorCount = 4;

        /// <summary>
        /// Creates a tactical battle view centered on a canvas at the world origin.
        /// </summary>
        /// <param name="root">Receives the canvas root to destroy after the test.</param>
        /// <returns>The configured view.</returns>
        internal static TacticalBattleView Create(out GameObject root)
        {
            root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(SurfaceWidth, SurfaceHeight);
            GameObject layer = CreateRect("TacticalBattle", root.transform);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = new Vector2(0.5f, 0.5f);
            layerRect.anchorMax = new Vector2(0.5f, 0.5f);
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.anchoredPosition = Vector2.zero;
            layerRect.sizeDelta = new Vector2(SurfaceWidth, SurfaceHeight);
            TacticalBattleView view = layer.AddComponent<TacticalBattleView>();

            int arenaWidth = Mathf.RoundToInt(SurfaceWidth) - PanelWidth;
            RawImage background = CreateImage("BackgroundImage", layer.transform);
            UILayout.SetStretch(background.rectTransform);
            RectTransform arena = CreateRect("Arena", layer.transform)
                .GetComponent<RectTransform>();
            UILayout.SetSourceRect(arena, 0, 0, arenaWidth, Mathf.RoundToInt(SurfaceHeight));
            RawImage starfield = CreateImage("StarfieldImage", arena);
            RawImage planet = CreateImage("PlanetImage", arena);
            RawImage sceneImage = CreateImage("SceneImage", arena);
            TacticalBattleScene scene = CreateScene(root.transform, sceneImage);
            RectTransform shots = CreateRect("Shots", arena).GetComponent<RectTransform>();
            RectTransform units = CreateRect("Units", arena).GetComponent<RectTransform>();
            RawImage shotTemplate = CreateImage("ShotTemplate", shots);
            shotTemplate.gameObject.SetActive(false);
            TacticalUnitView unitTemplate = CreateUnitTemplate(units);
            RawImage selectionBox = CreateImage("SelectionBoxImage", arena);
            selectionBox.gameObject.SetActive(false);

            RectTransform panel = CreateRect("InfoPanel", layer.transform)
                .GetComponent<RectTransform>();
            UILayout.SetSourceRect(
                panel,
                arenaWidth,
                0,
                PanelWidth,
                Mathf.RoundToInt(SurfaceHeight)
            );

            Set(view, "backgroundImage", background);
            Set(view, "arenaLayer", arena);
            Set(view, "starfieldImage", starfield);
            Set(view, "planetImage", planet);
            Set(view, "sceneImage", sceneImage);
            Set(view, "sceneView", scene);
            Set(view, "unitLayer", units);
            Set(view, "shotLayer", shots);
            Set(view, "unitTemplate", unitTemplate);
            Set(view, "shotTemplate", shotTemplate);
            Set(view, "selectionBoxImage", selectionBox);
            Set(view, "titleTextField", CreateText("TitleTextField", layer.transform));
            RectTransform roster = CreateRect("Roster", layer.transform)
                .GetComponent<RectTransform>();
            UILayout.SetSourceRect(roster, 8, 24, arenaWidth - 16, 30);
            Set(view, "rosterLayer", roster);
            Set(
                view,
                "rosterSideButton",
                CreateButton("RosterSideButton", roster, out TextMeshProUGUI rosterSideLabel)
            );
            Set(view, "rosterSideLabel", rosterSideLabel);
            Set(view, "rosterSlotTemplate", CreateRosterSlotTemplate(roster));
            Set(
                view,
                "targetPriorityButton",
                CreateButton("TargetPriorityButton", panel, out TextMeshProUGUI targetPriorityLabel)
            );
            Set(view, "targetPriorityLabel", targetPriorityLabel);
            Button superlaser = CreateButton(
                "SuperlaserButton",
                panel,
                out TextMeshProUGUI superlaserLabel
            );
            Set(view, "superlaserButton", superlaser);
            Set(
                view,
                "superlaserBarImage",
                CreateImage("SuperlaserBarImage", superlaser.transform)
            );
            Set(view, "superlaserLabel", superlaserLabel);
            Set(view, "portraitFrameImage", CreateImage("PortraitFrameImage", panel));
            Set(view, "portraitImage", CreateImage("PortraitImage", panel));
            Set(view, "nameTextField", CreateText("NameTextField", panel));
            Set(view, "subtitleTextField", CreateText("SubtitleTextField", panel));
            Set(
                view,
                "shieldBarBackgroundImage",
                CreateBar("ShieldBarBackgroundImage", panel, 100)
            );
            Set(view, "shieldBarImage", CreateBar("ShieldBarImage", panel, 100));
            Set(view, "shieldTextField", CreateText("ShieldTextField", panel));
            Set(view, "hullBarBackgroundImage", CreateBar("HullBarBackgroundImage", panel, 100));
            Set(view, "hullBarImage", CreateBar("HullBarImage", panel, 100));
            Set(view, "hullTextField", CreateText("HullTextField", panel));
            Set(view, "systemImages", CreateImages("System", "Image", panel));
            Set(view, "systemLabels", CreateTexts("System", "Label", panel));
            Set(view, "arcBarBackgroundImages", CreateBars("Arc", "BarBackgroundImage", panel));
            Set(view, "arcBarImages", CreateBars("Arc", "BarImage", panel));
            Set(view, "personnelImages", CreateImages("Personnel", "Image", panel));
            Set(view, "personnelTextField", CreateText("PersonnelTextField", panel));
            Set(view, "ordersTextField", CreateText("OrdersTextField", panel));
            Set(view, "tacticsTextField", CreateText("TacticsTextField", panel));
            Set(view, "attackerStatusTextField", CreateText("AttackerStatusTextField", panel));
            Set(view, "defenderStatusTextField", CreateText("DefenderStatusTextField", panel));
            Set(view, "statusTextField", CreateText("StatusTextField", panel));
            Set(view, "selectionTextField", CreateText("SelectionTextField", panel));
            Set(
                view,
                "retreatButton",
                CreateButton("RetreatButton", panel, out TextMeshProUGUI retreatLabel)
            );
            Set(view, "retreatLabel", retreatLabel);
            Set(
                view,
                "autoButton",
                CreateButton("AutoButton", panel, out TextMeshProUGUI autoLabel)
            );
            Set(view, "autoLabel", autoLabel);
            Set(
                view,
                "pauseButton",
                CreateButton("PauseButton", panel, out TextMeshProUGUI pauseLabel)
            );
            Set(view, "pauseLabel", pauseLabel);
            Set(
                view,
                "speedButton",
                CreateButton("SpeedButton", panel, out TextMeshProUGUI speedLabel)
            );
            Set(view, "speedLabel", speedLabel);
            return view;
        }

        /// <summary>
        /// Finds a named button within a view.
        /// </summary>
        /// <param name="view">The view to search.</param>
        /// <param name="objectName">The button object name.</param>
        /// <returns>The matching button.</returns>
        internal static Button FindButton(TacticalBattleView view, string objectName)
        {
            foreach (Button button in view.GetComponentsInChildren<Button>(true))
            {
                if (button.name == objectName)
                    return button;
            }

            return null;
        }

        /// <summary>
        /// Converts a source-space point on the view into the screen point a pointer event uses.
        /// </summary>
        /// <param name="source">The source-space point.</param>
        /// <returns>The equivalent screen point for a view centered on the world origin.</returns>
        internal static Vector2 ToScreenPoint(Vector2 source)
        {
            return new Vector2(source.x - SurfaceWidth / 2f, SurfaceHeight / 2f - source.y);
        }

        /// <summary>
        /// Creates the off-canvas 3D rig with a bare camera and model root, without HDRP data.
        /// </summary>
        /// <param name="parent">The test root that owns the rig.</param>
        /// <param name="outputImage">The arena image that shows the scene.</param>
        /// <returns>The configured scene component.</returns>
        private static TacticalBattleScene CreateScene(Transform parent, RawImage outputImage)
        {
            GameObject rig = new GameObject("TacticalBattleRig");
            rig.transform.SetParent(parent, false);
            rig.transform.position = new Vector3(0f, 1000f, 0f);
            TacticalBattleScene scene = rig.AddComponent<TacticalBattleScene>();
            GameObject cameraObject = new GameObject("Camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.GetComponent<Camera>().enabled = false;
            GameObject modelRoot = new GameObject("Units");
            modelRoot.transform.SetParent(rig.transform, false);
            Set(scene, "sceneCamera", cameraObject.GetComponent<Camera>());
            Set(scene, "modelRoot", modelRoot.transform);
            Set(scene, "outputImage", outputImage);
            rig.SetActive(false);
            return scene;
        }

        /// <summary>
        /// Creates the unit template with every image the unit view requires.
        /// </summary>
        /// <param name="parent">The unit layer.</param>
        /// <returns>The inactive unit template.</returns>
        private static TacticalUnitView CreateUnitTemplate(Transform parent)
        {
            GameObject unit = CreateRect("UnitTemplate", parent);
            TacticalUnitView view = unit.AddComponent<TacticalUnitView>();
            Set(view, "spriteImage", CreateImage("SpriteImage", unit.transform));
            Set(view, "selectionImage", CreateImage("SelectionImage", unit.transform));
            Set(view, "barBackgroundImage", CreateImage("BarBackgroundImage", unit.transform));
            Set(view, "hullBarImage", CreateImage("HullBarImage", unit.transform));
            Set(view, "shieldBarImage", CreateImage("ShieldBarImage", unit.transform));
            unit.SetActive(false);
            return view;
        }

        /// <summary>
        /// Creates one flat button with a caption label.
        /// </summary>
        /// <param name="name">The button object name.</param>
        /// <param name="parent">The parent transform.</param>
        /// <param name="label">Receives the caption label.</param>
        /// <returns>The created button.</returns>
        /// <summary>
        /// Creates the inactive roster slot template with every reference the slot view requires.
        /// </summary>
        /// <param name="parent">The roster layer.</param>
        /// <returns>The configured slot view.</returns>
        private static TacticalRosterSlotView CreateRosterSlotTemplate(Transform parent)
        {
            RawImage frame = CreateImage("RosterSlotTemplate", parent);
            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            TacticalRosterSlotView slot = frame.gameObject.AddComponent<TacticalRosterSlotView>();
            Set(slot, "button", button);
            Set(slot, "highlightImage", CreateImage("SlotHighlightImage", frame.transform));
            Set(slot, "portraitImage", CreateImage("SlotPortraitImage", frame.transform));
            Set(slot, "label", CreateText("SlotLabel", frame.transform));
            frame.gameObject.SetActive(false);
            return slot;
        }

        private static Button CreateButton(string name, Transform parent, out TextMeshProUGUI label)
        {
            RawImage image = CreateImage(name, parent);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = CreateText(name + "Label", image.transform);
            return button;
        }

        /// <summary>
        /// Creates an indexed set of raw images.
        /// </summary>
        /// <param name="prefix">The object-name prefix.</param>
        /// <param name="suffix">The object-name suffix.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created images.</returns>
        private static RawImage[] CreateImages(string prefix, string suffix, Transform parent)
        {
            RawImage[] images = new RawImage[IndicatorCount];
            for (int index = 0; index < images.Length; index++)
                images[index] = CreateImage($"{prefix}{index}{suffix}", parent);
            return images;
        }

        /// <summary>
        /// Creates an indexed set of bars with source-space widths.
        /// </summary>
        /// <param name="prefix">The object-name prefix.</param>
        /// <param name="suffix">The object-name suffix.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created bars.</returns>
        private static RawImage[] CreateBars(string prefix, string suffix, Transform parent)
        {
            RawImage[] bars = new RawImage[IndicatorCount];
            for (int index = 0; index < bars.Length; index++)
                bars[index] = CreateBar($"{prefix}{index}{suffix}", parent, 80);
            return bars;
        }

        /// <summary>
        /// Creates an indexed set of text labels.
        /// </summary>
        /// <param name="prefix">The object-name prefix.</param>
        /// <param name="suffix">The object-name suffix.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created labels.</returns>
        private static TextMeshProUGUI[] CreateTexts(string prefix, string suffix, Transform parent)
        {
            TextMeshProUGUI[] labels = new TextMeshProUGUI[IndicatorCount];
            for (int index = 0; index < labels.Length; index++)
                labels[index] = CreateText($"{prefix}{index}{suffix}", parent);
            return labels;
        }

        /// <summary>
        /// Creates a bar image with an authored source-space rectangle.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <param name="parent">The parent transform.</param>
        /// <param name="width">The source-space width.</param>
        /// <returns>The created bar.</returns>
        private static RawImage CreateBar(string name, Transform parent, int width)
        {
            RawImage bar = CreateImage(name, parent);
            UILayout.SetSourceRect(bar.rectTransform, 10, 10, width, 8);
            return bar;
        }

        /// <summary>
        /// Creates an empty rect transform object.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created object.</returns>
        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        /// <summary>
        /// Creates a raw image object.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created image.</returns>
        private static RawImage CreateImage(string name, Transform parent)
        {
            GameObject created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            created.transform.SetParent(parent, false);
            return created.GetComponent<RawImage>();
        }

        /// <summary>
        /// Creates a text label object.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <param name="parent">The parent transform.</param>
        /// <returns>The created label.</returns>
        private static TextMeshProUGUI CreateText(string name, Transform parent)
        {
            GameObject created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            created.transform.SetParent(parent, false);
            return created.GetComponent<TextMeshProUGUI>();
        }

        /// <summary>
        /// Assigns a serialized private field.
        /// </summary>
        /// <param name="target">The component to modify.</param>
        /// <param name="fieldName">The private field name.</param>
        /// <param name="value">The value to assign.</param>
        private static void Set(Component target, string fieldName, object value)
        {
            FieldInfo field = target
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }
    }
}
