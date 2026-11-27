using Rebellion.Tactical.Rendering;
using Rebellion.UI.SceneUI.TacticalCombat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    /// <summary>
    /// Supplies the scene references needed by tactical controller and window lifecycle tests.
    /// </summary>
    internal static class TacticalCombatViewFixture
    {
        /// <summary>
        /// Creates a battle view with its runtime lifecycle and no authored artwork or controls.
        /// </summary>
        /// <returns>The active test view.</returns>
        internal static TacticalCombatView Create()
        {
            GameObject root = new("Test tactical view");
            root.SetActive(false);
            TacticalCombatView view = root.AddComponent<TacticalCombatView>();
            Canvas canvas = Add<Canvas>(root.transform, "Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform frame = Add<RectTransform>(canvas.transform, "Frame");
            SerializedObject bindings = new(view);
            bindings.FindProperty("_frame").objectReferenceValue = frame;
            bindings.FindProperty("_battleRenderer").objectReferenceValue =
                root.AddComponent<TacticalCombatRenderer>();
            bindings.FindProperty("_controls").arraySize = 0;
            bindings.FindProperty("_panels").arraySize = 0;
            bindings.FindProperty("_systemIcons").arraySize = 0;
            Bind<RawImage>(bindings, frame, "_viewport");
            Bind<RawImage>(bindings, frame, "_panelImage");
            Bind<RawImage>(bindings, frame, "_objectPreview");
            Bind<RawImage>(bindings, frame, "_fighterPreview");
            Bind<RawImage>(bindings, frame, "_stationCharge");
            Bind<TextMeshProUGUI>(bindings, frame, "_status");
            Bind<TextMeshProUGUI>(bindings, frame, "_selection");
            Bind<TextMeshProUGUI>(bindings, frame, "_assignment");
            Bind<TextMeshProUGUI>(bindings, frame, "_orders");
            Bind<TextMeshProUGUI>(bindings, frame, "_tactics");
            Bind<TextMeshProUGUI>(bindings, frame, "_fighterCounts");
            Bind<Image>(bindings, frame, "_objectPreviewBackdrop");
            Bind<Image>(bindings, Add<RectTransform>(frame, "Hull gauge"), "_hullBar");
            Bind<Image>(bindings, Add<RectTransform>(frame, "Shield gauge"), "_shieldBar");
            bindings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return view;
        }

        /// <summary>
        /// Connects a new child component to a serialized view field.
        /// </summary>
        /// <typeparam name="T">The bound component type.</typeparam>
        /// <param name="bindings">The serialized view.</param>
        /// <param name="parent">The child object's parent.</param>
        /// <param name="field">The serialized field to bind.</param>
        private static void Bind<T>(SerializedObject bindings, Transform parent, string field)
            where T : Component =>
            bindings.FindProperty(field).objectReferenceValue = Add<T>(parent, field);

        /// <summary>
        /// Creates a child object with the requested component.
        /// </summary>
        /// <typeparam name="T">The added component type.</typeparam>
        /// <param name="parent">The child object's parent.</param>
        /// <param name="name">The child object's name.</param>
        /// <returns>The new child component.</returns>
        private static T Add<T>(Transform parent, string name)
            where T : Component
        {
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return typeof(T) == typeof(RectTransform)
                ? child.GetComponent<T>()
                : child.AddComponent<T>();
        }
    }
}
