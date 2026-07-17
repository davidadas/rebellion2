using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Rebuilds MainMenuView bindings from the authored MainMenuRoot hierarchy.
/// </summary>
public static class MainMenuPrefabAuthoring
{
    private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
    private const string _exitButtonControllerPath =
        "Assets/Prefabs/UI/MainMenu/ExitButton.controller";
    private const string _standardVictorySpritePath =
        "Assets/Resources/Art/HD/UI/MainMenu/ui_mainmenu_hq_icon.png";

    /// <summary>
    /// Rebuilds the authored main-menu view bindings and removes controller-targeted UnityEvents.
    /// </summary>
    [MenuItem("Rebellion/Main Menu/Rebuild Main Menu View Bindings")]
    public static void RebuildMainMenuViewBindings()
    {
        UIAuthoringGuard.EnsureEditMode();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath) == null)
            throw new FileNotFoundException(_prefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(_prefabPath);
        try
        {
            RebuildMainMenuViewBindings(root);
            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Rebuilds the view bindings on one loaded main-menu prefab hierarchy.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    private static void RebuildMainMenuViewBindings(GameObject root)
    {
        MainMenuController controller = FindRequiredComponent<MainMenuController>(root);
        MainMenuView view = controller.GetComponent<MainMenuView>();
        if (view == null)
            view = controller.gameObject.AddComponent<MainMenuView>();

        List<(Toggle Toggle, int Value)> galaxySizes = DiscoverToggleBindings<GameSize>(
            root,
            "GalaxySizeGroup"
        );
        List<(Toggle Toggle, int Value)> difficulties = DiscoverToggleBindings<GameDifficulty>(
            root,
            "DifficultyGroup"
        );

        List<(Button Button, string FactionId)> factionLaunches = ReadFactionLaunchBindings(view);
        if (factionLaunches.Count == 0)
            factionLaunches = DiscoverFactionLaunchBindings(root, controller);

        List<(
            EventTrigger Trigger,
            Graphic[] HiddenGraphics,
            GameObject[] ShownObjects
        )> pressVisuals = ReadPressVisualBindings(view);
        if (pressVisuals.Count == 0)
            pressVisuals = DiscoverPressVisualBindings(root);

        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> audioCues =
            ReadAudioCueBindings(view);
        if (audioCues.Count == 0)
            audioCues = DiscoverAudioCueBindings(root, controller);

        Button loadGameButton =
            ReadReference<Button>(view, "loadGameButton")
            ?? FindButtonByControllerMethod(root, controller, "OpenLoadGameMenu");
        Button creditsButton =
            ReadReference<Button>(view, "creditsButton")
            ?? FindButtonByControllerMethod(root, controller, "ShowCredits");
        Button victoryConditionButton =
            ReadReference<Button>(view, "victoryConditionButton")
            ?? FindButtonByControllerMethod(root, controller, "ToggleVictoryCondition");
        Image victoryConditionIcon =
            ReadReference<Image>(view, "victoryConditionIcon")
            ?? FindRequiredComponent<Image>(root, "VictoryConditionIcon");
        TMP_Text victoryConditionText =
            ReadReference<TMP_Text>(view, "victoryConditionText")
            ?? FindRequiredComponent<TMP_Text>(root, "VictoryConditionText");
        Sprite standardVictoryConditionSprite =
            ReadReference<Sprite>(view, "standardVictoryConditionSprite")
            ?? LoadRequiredSprite(_standardVictorySpritePath);
        Sprite headquartersVictoryConditionSprite =
            ReadReference<Sprite>(view, "headquartersVictoryConditionSprite")
            ?? victoryConditionIcon.sprite;
        Animator exitButtonAnimator = FindRequiredComponent<Animator>(root, "ExitButton");
        exitButtonAnimator.runtimeAnimatorController = LoadRequiredAsset<RuntimeAnimatorController>(
            _exitButtonControllerPath
        );

        ConfigureView(
            view,
            loadGameButton,
            creditsButton,
            victoryConditionButton,
            galaxySizes,
            difficulties,
            factionLaunches,
            victoryConditionIcon,
            standardVictoryConditionSprite,
            headquartersVictoryConditionSprite,
            victoryConditionText,
            pressVisuals,
            audioCues
        );
        AssignReference(controller, "view", view);
        RemoveControllerPersistentCalls(root, controller);
        RebuildEventTriggers(pressVisuals, audioCues);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(exitButtonAnimator);
        EditorUtility.SetDirty(view);
    }

    /// <summary>
    /// Reads an object reference from a serialized view field.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="view">The main-menu view.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <returns>The referenced object, or <see langword="null"/>.</returns>
    private static T ReadReference<T>(MainMenuView view, string propertyName)
        where T : Object
    {
        SerializedProperty property = new SerializedObject(view).FindProperty(propertyName);
        return property?.objectReferenceValue as T;
    }

    /// <summary>
    /// Reads existing serialized faction-launch bindings from the view.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <returns>The configured launch buttons and faction identifiers.</returns>
    private static List<(Button Button, string FactionId)> ReadFactionLaunchBindings(
        MainMenuView view
    )
    {
        SerializedProperty bindings = new SerializedObject(view).FindProperty(
            "factionLaunchBindings"
        );
        List<(Button Button, string FactionId)> result =
            new List<(Button Button, string FactionId)>();
        if (bindings == null)
            return result;

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            Button button = binding.FindPropertyRelative("button").objectReferenceValue as Button;
            string factionId = binding.FindPropertyRelative("factionId").stringValue;
            if (button == null || string.IsNullOrEmpty(factionId))
            {
                throw new InvalidOperationException($"Faction launch binding {i} is incomplete.");
            }

            result.Add((button, factionId));
        }

        return result;
    }

    /// <summary>
    /// Reads existing serialized pressed-visual bindings from the view.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <returns>The configured pressed-visual bindings.</returns>
    private static List<(
        EventTrigger Trigger,
        Graphic[] HiddenGraphics,
        GameObject[] ShownObjects
    )> ReadPressVisualBindings(MainMenuView view)
    {
        SerializedProperty bindings = new SerializedObject(view).FindProperty(
            "pressVisualBindings"
        );
        List<(EventTrigger Trigger, Graphic[] HiddenGraphics, GameObject[] ShownObjects)> result =
            new List<(EventTrigger Trigger, Graphic[] HiddenGraphics, GameObject[] ShownObjects)>();
        if (bindings == null)
            return result;

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            EventTrigger trigger =
                binding.FindPropertyRelative("trigger").objectReferenceValue as EventTrigger;
            if (trigger == null)
                throw new InvalidOperationException($"Press visual binding {i} has no trigger.");

            Graphic[] hiddenGraphics = ReadReferenceArray<Graphic>(
                binding.FindPropertyRelative("graphicsHiddenWhilePressed")
            );
            GameObject[] shownObjects = ReadReferenceArray<GameObject>(
                binding.FindPropertyRelative("objectsShownWhilePressed")
            );
            if (
                !hiddenGraphics.Any(graphic => graphic != null)
                && !shownObjects.Any(activeObject => activeObject != null)
            )
            {
                throw new InvalidOperationException(
                    $"Press visual binding {i} has no visual targets."
                );
            }

            result.Add((trigger, hiddenGraphics, shownObjects));
        }

        return result;
    }

    /// <summary>
    /// Reads existing serialized audio-cue bindings from the view.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <returns>The configured event-trigger audio cues.</returns>
    private static List<(
        EventTrigger Trigger,
        EventTriggerType EventType,
        string ResourcePath
    )> ReadAudioCueBindings(MainMenuView view)
    {
        SerializedProperty bindings = new SerializedObject(view).FindProperty("audioCueBindings");
        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> result =
            new List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)>();
        if (bindings == null)
            return result;

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            EventTrigger trigger =
                binding.FindPropertyRelative("trigger").objectReferenceValue as EventTrigger;
            string resourcePath = binding.FindPropertyRelative("resourcePath").stringValue;
            if (trigger == null || string.IsNullOrEmpty(resourcePath))
                throw new InvalidOperationException($"Audio cue binding {i} is incomplete.");

            result.Add(
                (
                    trigger,
                    (EventTriggerType)binding.FindPropertyRelative("eventType").intValue,
                    resourcePath
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Reads a serialized Unity-object reference array.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="property">The serialized array property.</param>
    /// <returns>The referenced objects.</returns>
    private static T[] ReadReferenceArray<T>(SerializedProperty property)
        where T : Object
    {
        T[] values = new T[property.arraySize];
        for (int i = 0; i < property.arraySize; i++)
            values[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as T;
        return values;
    }

    /// <summary>
    /// Discovers enum-backed toggle values from one authored toggle group.
    /// </summary>
    /// <typeparam name="T">The enum represented by the toggle group.</typeparam>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="groupName">The authored toggle-group object name.</param>
    /// <returns>The discovered toggle bindings.</returns>
    private static List<(Toggle Toggle, int Value)> DiscoverToggleBindings<T>(
        GameObject root,
        string groupName
    )
        where T : struct, Enum
    {
        ToggleGroup group = FindRequiredComponent<ToggleGroup>(root, groupName);
        Toggle[] toggles = group.GetComponentsInChildren<Toggle>(true);
        T[] values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        if (toggles.Length != values.Length)
        {
            throw new InvalidOperationException(
                $"The authored {groupName} toggles do not match the supported {typeof(T).Name} values."
            );
        }

        List<(Toggle Toggle, int Value)> bindings = new List<(Toggle Toggle, int Value)>();
        for (int i = 0; i < toggles.Length; i++)
            bindings.Add((toggles[i], Convert.ToInt32(values[i])));
        return bindings;
    }

    /// <summary>
    /// Discovers faction launch identifiers from the currently authored UnityEvents.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <returns>The discovered faction launch bindings.</returns>
    private static List<(Button Button, string FactionId)> DiscoverFactionLaunchBindings(
        GameObject root,
        MainMenuController controller
    )
    {
        List<(Button Button, string FactionId)> bindings =
            new List<(Button Button, string FactionId)>();
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (
                TryReadPersistentStringArgument(
                    button,
                    "m_OnClick",
                    button.onClick,
                    controller,
                    "SelectFaction",
                    out string factionId
                )
            )
            {
                bindings.Add((button, factionId));
            }
        }

        if (bindings.Count == 0)
            throw new InvalidOperationException("No authored faction launch bindings were found.");
        return bindings;
    }

    /// <summary>
    /// Discovers pressed-visual targets from the authored pointer-down callbacks.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    /// <returns>The discovered pressed-visual bindings.</returns>
    private static List<(
        EventTrigger Trigger,
        Graphic[] HiddenGraphics,
        GameObject[] ShownObjects
    )> DiscoverPressVisualBindings(GameObject root)
    {
        List<(EventTrigger Trigger, Graphic[] HiddenGraphics, GameObject[] ShownObjects)> bindings =
            new List<(EventTrigger Trigger, Graphic[] HiddenGraphics, GameObject[] ShownObjects)>();
        foreach (EventTrigger trigger in root.GetComponentsInChildren<EventTrigger>(true))
        {
            List<Graphic> hiddenGraphics = new List<Graphic>();
            List<GameObject> shownObjects = new List<GameObject>();
            foreach (EventTrigger.Entry entry in trigger.triggers)
            {
                if (entry.eventID != EventTriggerType.PointerDown)
                    continue;

                for (int i = 0; i < entry.callback.GetPersistentEventCount(); i++)
                {
                    string method = entry.callback.GetPersistentMethodName(i);
                    Object target = entry.callback.GetPersistentTarget(i);
                    if (method == "set_enabled" && target is Graphic graphic)
                        hiddenGraphics.Add(graphic);
                    else if (method == "SetActive" && target is GameObject activeObject)
                        shownObjects.Add(activeObject);
                }
            }

            if (hiddenGraphics.Count > 0 || shownObjects.Count > 0)
                bindings.Add((trigger, hiddenGraphics.ToArray(), shownObjects.ToArray()));
        }

        if (bindings.Count == 0)
            throw new InvalidOperationException("No authored pressed-visual bindings were found.");
        return bindings;
    }

    /// <summary>
    /// Discovers UI audio cues from the currently authored event-trigger callbacks.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <returns>The discovered audio cue bindings.</returns>
    private static List<(
        EventTrigger Trigger,
        EventTriggerType EventType,
        string ResourcePath
    )> DiscoverAudioCueBindings(GameObject root, MainMenuController controller)
    {
        List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)> bindings =
            new List<(EventTrigger Trigger, EventTriggerType EventType, string ResourcePath)>();
        foreach (EventTrigger trigger in root.GetComponentsInChildren<EventTrigger>(true))
        {
            SerializedObject serializedTrigger = new SerializedObject(trigger);
            SerializedProperty delegates = serializedTrigger.FindProperty("m_Delegates");
            for (int entryIndex = 0; entryIndex < trigger.triggers.Count; entryIndex++)
            {
                EventTrigger.Entry entry = trigger.triggers[entryIndex];
                SerializedProperty serializedEntry = delegates.GetArrayElementAtIndex(entryIndex);
                SerializedProperty callback = serializedEntry.FindPropertyRelative("callback");
                SerializedProperty calls = GetPersistentCalls(callback);
                for (
                    int callIndex = 0;
                    callIndex < entry.callback.GetPersistentEventCount();
                    callIndex++
                )
                {
                    if (
                        entry.callback.GetPersistentTarget(callIndex) != controller
                        || entry.callback.GetPersistentMethodName(callIndex) != "PlaySfx"
                    )
                    {
                        continue;
                    }

                    SerializedProperty call = calls.GetArrayElementAtIndex(callIndex);
                    SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
                    string resourcePath = arguments
                        .FindPropertyRelative("m_StringArgument")
                        .stringValue;
                    bindings.Add((trigger, entry.eventID, resourcePath));
                }
            }
        }

        if (bindings.Count == 0)
            throw new InvalidOperationException("No authored main-menu audio cues were found.");
        return bindings;
    }

    /// <summary>
    /// Finds a command button by its persistent controller method.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    /// <param name="methodName">The persistent controller method name.</param>
    /// <returns>The unique matching button.</returns>
    private static Button FindButtonByControllerMethod(
        GameObject root,
        MainMenuController controller,
        string methodName
    )
    {
        Button[] matches = root.GetComponentsInChildren<Button>(true)
            .Where(button => HasPersistentCall(button.onClick, controller, methodName))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one button bound to {methodName}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Determines whether a UnityEvent contains one controller method binding.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <returns><see langword="true"/> when a matching call exists.</returns>
    private static bool HasPersistentCall(
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName
    )
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (
                unityEvent.GetPersistentTarget(i) == controller
                && unityEvent.GetPersistentMethodName(i) == methodName
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a string argument from one matching persistent UnityEvent call.
    /// </summary>
    /// <param name="owner">The serialized UnityEvent owner.</param>
    /// <param name="eventPropertyName">The serialized UnityEvent property name.</param>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <param name="value">Receives the serialized string argument.</param>
    /// <returns><see langword="true"/> when a matching call exists.</returns>
    private static bool TryReadPersistentStringArgument(
        Object owner,
        string eventPropertyName,
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName,
        out string value
    )
    {
        int callIndex = FindPersistentCallIndex(unityEvent, controller, methodName);
        if (callIndex < 0)
        {
            value = null;
            return false;
        }

        SerializedProperty call = GetPersistentCalls(owner, eventPropertyName)
            .GetArrayElementAtIndex(callIndex);
        value = call.FindPropertyRelative("m_Arguments")
            .FindPropertyRelative("m_StringArgument")
            .stringValue;
        return true;
    }

    /// <summary>
    /// Finds one persistent controller call in a UnityEvent.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to inspect.</param>
    /// <param name="controller">The expected target controller.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <returns>The matching call index, or negative one.</returns>
    private static int FindPersistentCallIndex(
        UnityEventBase unityEvent,
        MainMenuController controller,
        string methodName
    )
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (
                unityEvent.GetPersistentTarget(i) == controller
                && unityEvent.GetPersistentMethodName(i) == methodName
            )
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the serialized persistent-call array for a UnityEvent field.
    /// </summary>
    /// <param name="owner">The serialized UnityEvent owner.</param>
    /// <param name="eventPropertyName">The serialized UnityEvent property name.</param>
    /// <returns>The serialized persistent-call array.</returns>
    private static SerializedProperty GetPersistentCalls(Object owner, string eventPropertyName)
    {
        SerializedProperty unityEvent = new SerializedObject(owner).FindProperty(eventPropertyName);
        return GetPersistentCalls(unityEvent);
    }

    /// <summary>
    /// Gets the serialized persistent-call array below a UnityEvent property.
    /// </summary>
    /// <param name="unityEvent">The serialized UnityEvent property.</param>
    /// <returns>The serialized persistent-call array.</returns>
    private static SerializedProperty GetPersistentCalls(SerializedProperty unityEvent)
    {
        return unityEvent.FindPropertyRelative("m_PersistentCalls").FindPropertyRelative("m_Calls");
    }

    /// <summary>
    /// Writes all serialized MainMenuView references and binding collections.
    /// </summary>
    /// <param name="view">The main-menu view.</param>
    /// <param name="loadGameButton">The load-game button.</param>
    /// <param name="creditsButton">The credits button.</param>
    /// <param name="victoryConditionButton">The victory-condition button.</param>
    /// <param name="galaxySizes">The galaxy-size bindings.</param>
    /// <param name="difficulties">The difficulty bindings.</param>
    /// <param name="factionLaunches">The faction launch bindings.</param>
    /// <param name="victoryConditionIcon">The victory-condition icon.</param>
    /// <param name="standardVictoryConditionSprite">The standard victory sprite.</param>
    /// <param name="headquartersVictoryConditionSprite">The headquarters victory sprite.</param>
    /// <param name="victoryConditionText">The victory-condition label.</param>
    /// <param name="pressVisuals">The pressed-visual bindings.</param>
    /// <param name="audioCues">The audio-cue bindings.</param>
    private static void ConfigureView(
        MainMenuView view,
        Button loadGameButton,
        Button creditsButton,
        Button victoryConditionButton,
        IReadOnlyList<(Toggle Toggle, int Value)> galaxySizes,
        IReadOnlyList<(Toggle Toggle, int Value)> difficulties,
        IReadOnlyList<(Button Button, string FactionId)> factionLaunches,
        Image victoryConditionIcon,
        Sprite standardVictoryConditionSprite,
        Sprite headquartersVictoryConditionSprite,
        TMP_Text victoryConditionText,
        IReadOnlyList<(
            EventTrigger Trigger,
            Graphic[] HiddenGraphics,
            GameObject[] ShownObjects
        )> pressVisuals,
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> audioCues
    )
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("loadGameButton").objectReferenceValue = loadGameButton;
        serializedView.FindProperty("creditsButton").objectReferenceValue = creditsButton;
        serializedView.FindProperty("victoryConditionButton").objectReferenceValue =
            victoryConditionButton;
        serializedView.FindProperty("victoryConditionIcon").objectReferenceValue =
            victoryConditionIcon;
        serializedView.FindProperty("standardVictoryConditionSprite").objectReferenceValue =
            standardVictoryConditionSprite;
        serializedView.FindProperty("headquartersVictoryConditionSprite").objectReferenceValue =
            headquartersVictoryConditionSprite;
        serializedView.FindProperty("victoryConditionText").objectReferenceValue =
            victoryConditionText;

        WriteToggleBindings(serializedView.FindProperty("galaxySizeBindings"), galaxySizes);
        WriteToggleBindings(serializedView.FindProperty("difficultyBindings"), difficulties);
        WriteFactionLaunchBindings(
            serializedView.FindProperty("factionLaunchBindings"),
            factionLaunches
        );
        WritePressVisualBindings(serializedView.FindProperty("pressVisualBindings"), pressVisuals);
        WriteAudioCueBindings(serializedView.FindProperty("audioCueBindings"), audioCues);
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Writes one serialized toggle-binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The toggle bindings.</param>
    private static void WriteToggleBindings(
        SerializedProperty property,
        IReadOnlyList<(Toggle Toggle, int Value)> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("toggle").objectReferenceValue = bindings[i].Toggle;
            binding.FindPropertyRelative("value").intValue = bindings[i].Value;
        }
    }

    /// <summary>
    /// Writes the serialized faction-launch binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The faction launch bindings.</param>
    private static void WriteFactionLaunchBindings(
        SerializedProperty property,
        IReadOnlyList<(Button Button, string FactionId)> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("button").objectReferenceValue = bindings[i].Button;
            binding.FindPropertyRelative("factionId").stringValue = bindings[i].FactionId;
        }
    }

    /// <summary>
    /// Writes the serialized pressed-visual binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The pressed-visual bindings.</param>
    private static void WritePressVisualBindings(
        SerializedProperty property,
        IReadOnlyList<(
            EventTrigger Trigger,
            Graphic[] HiddenGraphics,
            GameObject[] ShownObjects
        )> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("trigger").objectReferenceValue = bindings[i].Trigger;
            WriteReferenceArray(
                binding.FindPropertyRelative("graphicsHiddenWhilePressed"),
                bindings[i].HiddenGraphics
            );
            WriteReferenceArray(
                binding.FindPropertyRelative("objectsShownWhilePressed"),
                bindings[i].ShownObjects
            );
        }
    }

    /// <summary>
    /// Writes the serialized audio-cue binding array.
    /// </summary>
    /// <param name="property">The serialized binding array.</param>
    /// <param name="bindings">The audio-cue bindings.</param>
    private static void WriteAudioCueBindings(
        SerializedProperty property,
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> bindings
    )
    {
        property.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            SerializedProperty binding = property.GetArrayElementAtIndex(i);
            binding.FindPropertyRelative("trigger").objectReferenceValue = bindings[i].Trigger;
            binding.FindPropertyRelative("eventType").intValue = (int)bindings[i].EventType;
            binding.FindPropertyRelative("resourcePath").stringValue = bindings[i].ResourcePath;
        }
    }

    /// <summary>
    /// Writes one serialized Unity-object reference array.
    /// </summary>
    /// <typeparam name="T">The referenced Unity object type.</typeparam>
    /// <param name="property">The serialized array property.</param>
    /// <param name="values">The referenced objects.</param>
    private static void WriteReferenceArray<T>(SerializedProperty property, IReadOnlyList<T> values)
        where T : Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    /// <summary>
    /// Assigns one serialized Unity-object reference.
    /// </summary>
    /// <param name="target">The serialized target.</param>
    /// <param name="propertyName">The serialized field name.</param>
    /// <param name="value">The referenced Unity object.</param>
    private static void AssignReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingMemberException(target.GetType().Name, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Removes persistent calls that target MainMenuController from authored controls.
    /// </summary>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="controller">The main-menu controller.</param>
    private static void RemoveControllerPersistentCalls(
        GameObject root,
        MainMenuController controller
    )
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            RemovePersistentCalls(button.onClick, controller);

        foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            RemovePersistentCalls(toggle.onValueChanged, controller);
    }

    /// <summary>
    /// Removes all calls to one target from a persistent UnityEvent.
    /// </summary>
    /// <param name="unityEvent">The UnityEvent to update.</param>
    /// <param name="target">The target whose calls should be removed.</param>
    private static void RemovePersistentCalls(UnityEventBase unityEvent, Object target)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (unityEvent.GetPersistentTarget(i) == target)
                UnityEventTools.RemovePersistentListener(unityEvent, i);
        }
    }

    /// <summary>
    /// Rebuilds event-trigger entries required by pressed presentation and audio cues.
    /// </summary>
    /// <param name="pressVisuals">The pressed-visual bindings.</param>
    /// <param name="audioCues">The audio-cue bindings.</param>
    private static void RebuildEventTriggers(
        IReadOnlyList<(
            EventTrigger Trigger,
            Graphic[] HiddenGraphics,
            GameObject[] ShownObjects
        )> pressVisuals,
        IReadOnlyList<(
            EventTrigger Trigger,
            EventTriggerType EventType,
            string ResourcePath
        )> audioCues
    )
    {
        Dictionary<EventTrigger, HashSet<EventTriggerType>> eventTypes =
            new Dictionary<EventTrigger, HashSet<EventTriggerType>>();
        foreach ((EventTrigger trigger, _, _) in pressVisuals)
        {
            AddEventType(eventTypes, trigger, EventTriggerType.PointerDown);
            AddEventType(eventTypes, trigger, EventTriggerType.PointerUp);
            AddEventType(eventTypes, trigger, EventTriggerType.PointerExit);
        }

        foreach ((EventTrigger trigger, EventTriggerType eventType, _) in audioCues)
            AddEventType(eventTypes, trigger, eventType);

        EventTriggerType[] authoredOrder =
        {
            EventTriggerType.PointerDown,
            EventTriggerType.PointerUp,
            EventTriggerType.PointerExit,
        };
        foreach (KeyValuePair<EventTrigger, HashSet<EventTriggerType>> pair in eventTypes)
        {
            List<EventTriggerType> orderedTypes = authoredOrder
                .Where(pair.Value.Contains)
                .Concat(pair.Value.Except(authoredOrder).OrderBy(value => value))
                .ToList();
            pair.Key.triggers = orderedTypes.ConvertAll(eventType => new EventTrigger.Entry
            {
                eventID = eventType,
                callback = new EventTrigger.TriggerEvent(),
            });
            EditorUtility.SetDirty(pair.Key);
        }
    }

    /// <summary>
    /// Adds one required event type for an event trigger.
    /// </summary>
    /// <param name="eventTypes">The trigger event-type map.</param>
    /// <param name="trigger">The event trigger.</param>
    /// <param name="eventType">The required event type.</param>
    private static void AddEventType(
        IDictionary<EventTrigger, HashSet<EventTriggerType>> eventTypes,
        EventTrigger trigger,
        EventTriggerType eventType
    )
    {
        if (!eventTypes.TryGetValue(trigger, out HashSet<EventTriggerType> types))
        {
            types = new HashSet<EventTriggerType>();
            eventTypes.Add(trigger, types);
        }

        types.Add(eventType);
    }

    /// <summary>
    /// Finds one uniquely named component in the prefab hierarchy.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="root">The loaded prefab root.</param>
    /// <param name="objectName">The required GameObject name.</param>
    /// <returns>The unique matching component.</returns>
    private static T FindRequiredComponent<T>(GameObject root, string objectName)
        where T : Component
    {
        T[] matches = root.GetComponentsInChildren<T>(true)
            .Where(component => component.gameObject.name == objectName)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name} named {objectName}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Finds the unique component of a type in the prefab hierarchy.
    /// </summary>
    /// <typeparam name="T">The required component type.</typeparam>
    /// <param name="root">The loaded prefab root.</param>
    /// <returns>The unique matching component.</returns>
    private static T FindRequiredComponent<T>(GameObject root)
        where T : Component
    {
        T[] matches = root.GetComponentsInChildren<T>(true);
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name}, found {matches.Length}."
            );
        }

        return matches[0];
    }

    /// <summary>
    /// Loads the required sprite at an authored asset path.
    /// </summary>
    /// <param name="path">The sprite asset path.</param>
    /// <returns>The loaded sprite.</returns>
    private static Sprite LoadRequiredSprite(string path)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
            throw new FileNotFoundException(path);
        if (sprites.Length > 1)
            throw new InvalidOperationException($"Expected one sprite at {path}.");

        return sprites[0];
    }

    /// <summary>
    /// Loads one required Unity asset at an authored project path.
    /// </summary>
    /// <typeparam name="T">The required asset type.</typeparam>
    /// <param name="path">The project-relative asset path.</param>
    /// <returns>The loaded asset.</returns>
    private static T LoadRequiredAsset<T>(string path)
        where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
    }
}
