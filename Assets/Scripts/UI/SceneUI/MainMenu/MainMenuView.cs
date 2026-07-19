using System;
using System.Collections.Generic;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns main-menu control bindings and local presentation while emitting semantic user requests.
/// </summary>
public sealed class MainMenuView : MonoBehaviour
{
    /// <summary>
    /// Associates a galaxy-size toggle with its launch value.
    /// </summary>
    [Serializable]
    private sealed class GalaxySizeBinding
    {
        [SerializeField]
        private Toggle toggle;

        [SerializeField]
        private GameSize value;

        public Toggle Toggle => toggle;

        public GameSize Value => value;

        public bool IsConfigured => toggle != null;
    }

    /// <summary>
    /// Associates a difficulty toggle with its launch value.
    /// </summary>
    [Serializable]
    private sealed class DifficultyBinding
    {
        [SerializeField]
        private Toggle toggle;

        [SerializeField]
        private GameDifficulty value;

        public Toggle Toggle => toggle;

        public GameDifficulty Value => value;

        public bool IsConfigured => toggle != null;
    }

    /// <summary>
    /// Associates a faction launch button with its configured faction identifier.
    /// </summary>
    [Serializable]
    private sealed class FactionLaunchBinding
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private string factionId;

        public Button Button => button;

        public string FactionId => factionId;

        /// <summary>
        /// Gets whether the binding has a button and faction identifier.
        /// </summary>
        public bool IsConfigured => button != null && !string.IsNullOrEmpty(factionId);
    }

    /// <summary>
    /// Defines the visual changes applied while a main-menu control is pressed.
    /// </summary>
    [Serializable]
    private sealed class PressVisualBinding
    {
        [SerializeField]
        private EventTrigger trigger;

        [SerializeField]
        private Graphic[] graphicsHiddenWhilePressed = Array.Empty<Graphic>();

        [SerializeField]
        private GameObject[] objectsShownWhilePressed = Array.Empty<GameObject>();

        public EventTrigger Trigger => trigger;

        public IReadOnlyList<Graphic> GraphicsHiddenWhilePressed =>
            graphicsHiddenWhilePressed ?? Array.Empty<Graphic>();

        public IReadOnlyList<GameObject> ObjectsShownWhilePressed =>
            objectsShownWhilePressed ?? Array.Empty<GameObject>();

        public bool IsConfigured =>
            trigger != null
            && (
                Array.Exists(
                    graphicsHiddenWhilePressed ?? Array.Empty<Graphic>(),
                    graphic => graphic != null
                )
                || Array.Exists(
                    objectsShownWhilePressed ?? Array.Empty<GameObject>(),
                    activeObject => activeObject != null
                )
            );
    }

    /// <summary>
    /// Defines an audio cue emitted by a specific pointer event.
    /// </summary>
    [Serializable]
    private sealed class AudioCueBinding
    {
        [SerializeField]
        private EventTrigger trigger;

        [SerializeField]
        private EventTriggerType eventType;

        [SerializeField]
        private string resourcePath;

        public EventTrigger Trigger => trigger;

        public EventTriggerType EventType => eventType;

        public string ResourcePath => resourcePath;

        /// <summary>
        /// Gets whether the binding has a trigger and audio resource path.
        /// </summary>
        public bool IsConfigured => trigger != null && !string.IsNullOrEmpty(resourcePath);
    }

    [Header("Commands")]
    [SerializeField]
    private Button loadGameButton;

    [SerializeField]
    private Button creditsButton;

    [SerializeField]
    private Button victoryConditionButton;

    [Header("Launch Options")]
    [SerializeField]
    private GalaxySizeBinding[] galaxySizeBindings = Array.Empty<GalaxySizeBinding>();

    [SerializeField]
    private DifficultyBinding[] difficultyBindings = Array.Empty<DifficultyBinding>();

    [SerializeField]
    private FactionLaunchBinding[] factionLaunchBindings = Array.Empty<FactionLaunchBinding>();

    [Header("Victory Condition")]
    [SerializeField]
    private Image victoryConditionIcon;

    [SerializeField]
    private Sprite standardVictoryConditionSprite;

    [SerializeField]
    private Sprite headquartersVictoryConditionSprite;

    [SerializeField]
    private TMP_Text victoryConditionText;

    [Header("Pointer Presentation")]
    [SerializeField]
    private PressVisualBinding[] pressVisualBindings = Array.Empty<PressVisualBinding>();

    [SerializeField]
    private AudioCueBinding[] audioCueBindings = Array.Empty<AudioCueBinding>();

    private readonly List<Action> removeControlListeners = new List<Action>();
    private bool controlsBound;

    /// <summary>
    /// Occurs when the player selects a galaxy size.
    /// </summary>
    public event Action<GameSize> GalaxySizeSelected;

    /// <summary>
    /// Occurs when the player selects a difficulty.
    /// </summary>
    public event Action<GameDifficulty> DifficultySelected;

    /// <summary>
    /// Occurs when the player requests a new game for a faction.
    /// </summary>
    public event Action<string> StartGameRequested;

    /// <summary>
    /// Occurs when the player requests toggling the victory condition.
    /// </summary>
    public event Action VictoryConditionToggleRequested;

    /// <summary>
    /// Occurs when the player requests the load-game menu.
    /// </summary>
    public event Action LoadGameRequested;

    /// <summary>
    /// Occurs when the player requests the credits sequence.
    /// </summary>
    public event Action CreditsRequested;

    /// <summary>
    /// Occurs when a configured pointer interaction requests an audio cue.
    /// </summary>
    public event Action<string> AudioCueRequested;

    /// <summary>
    /// Validates the authored references before runtime interaction begins.
    /// </summary>
    private void Awake()
    {
        if (Application.isPlaying)
            VerifyReferences();
    }

    /// <summary>
    /// Binds authored controls when the view becomes active.
    /// </summary>
    private void OnEnable()
    {
        BindControls();
    }

    /// <summary>
    /// Removes control listeners and restores non-pressed presentation when the view is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnbindControls();
        foreach (PressVisualBinding binding in pressVisualBindings)
            SetPressed(binding, false);
    }

    /// <summary>
    /// Renders the selected victory condition without changing launch state.
    /// </summary>
    /// <param name="condition">The victory condition to present.</param>
    public void RenderVictoryCondition(GameVictoryCondition condition)
    {
        bool headquarters = condition == GameVictoryCondition.Headquarters;
        victoryConditionIcon.sprite = headquarters
            ? headquartersVictoryConditionSprite
            : standardVictoryConditionSprite;
        victoryConditionIcon.gameObject.SetActive(true);
        victoryConditionText.text = headquarters ? "Headquarters Victory" : "Standard Game";
    }

    /// <summary>
    /// Gets the difficulty selected by the authored toggle group.
    /// </summary>
    /// <param name="difficulty">Receives the selected difficulty when available.</param>
    /// <returns><see langword="true"/> when a configured difficulty toggle is selected.</returns>
    public bool TryGetSelectedDifficulty(out GameDifficulty difficulty)
    {
        foreach (DifficultyBinding binding in difficultyBindings)
        {
            if (binding?.Toggle != null && binding.Toggle.isOn)
            {
                difficulty = binding.Value;
                return true;
            }
        }

        difficulty = default;
        return false;
    }

    /// <summary>
    /// Verifies that all required authored controls and presentation references are assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (
            loadGameButton == null
            || creditsButton == null
            || victoryConditionButton == null
            || victoryConditionIcon == null
            || standardVictoryConditionSprite == null
            || headquartersVictoryConditionSprite == null
            || victoryConditionText == null
        )
        {
            throw new MissingReferenceException($"{name} has incomplete main-menu references.");
        }

        VerifyBindings(galaxySizeBindings, binding => binding?.IsConfigured == true, "galaxy size");
        VerifyBindings(difficultyBindings, binding => binding?.IsConfigured == true, "difficulty");
        VerifyBindings(
            factionLaunchBindings,
            binding => binding?.IsConfigured == true,
            "faction launch"
        );
        VerifyBindings(
            pressVisualBindings,
            binding => binding?.IsConfigured == true,
            "press visual"
        );
        VerifyBindings(audioCueBindings, binding => binding?.IsConfigured == true, "audio cue");
    }

    /// <summary>
    /// Verifies one serialized binding collection.
    /// </summary>
    /// <typeparam name="T">The serialized binding type.</typeparam>
    /// <param name="bindings">The bindings to validate.</param>
    /// <param name="isConfigured">Determines whether one binding is complete.</param>
    /// <param name="bindingName">The binding category used in errors.</param>
    private void VerifyBindings<T>(T[] bindings, Func<T, bool> isConfigured, string bindingName)
    {
        if (
            bindings == null
            || bindings.Length == 0
            || Array.Exists(bindings, item => !isConfigured(item))
        )
        {
            throw new MissingReferenceException($"{name} has incomplete {bindingName} bindings.");
        }
    }

    /// <summary>
    /// Binds command, option, pointer-presentation, and audio controls once.
    /// </summary>
    private void BindControls()
    {
        if (controlsBound)
            return;

        BindButton(loadGameButton, () => LoadGameRequested?.Invoke());
        BindButton(creditsButton, () => CreditsRequested?.Invoke());
        BindButton(victoryConditionButton, () => VictoryConditionToggleRequested?.Invoke());

        foreach (GalaxySizeBinding binding in galaxySizeBindings)
        {
            if (binding?.Toggle == null)
                continue;

            BindToggle(
                binding.Toggle,
                isOn =>
                {
                    if (isOn)
                        GalaxySizeSelected?.Invoke(binding.Value);
                }
            );
        }

        foreach (DifficultyBinding binding in difficultyBindings)
        {
            if (binding?.Toggle == null)
                continue;

            BindToggle(
                binding.Toggle,
                isOn =>
                {
                    if (isOn)
                        DifficultySelected?.Invoke(binding.Value);
                }
            );
        }

        foreach (FactionLaunchBinding binding in factionLaunchBindings)
        {
            if (binding?.Button == null)
                continue;

            BindButton(binding.Button, () => StartGameRequested?.Invoke(binding.FactionId));
        }

        BindPressVisuals();
        BindAudioCues();
        controlsBound = true;
    }

    /// <summary>
    /// Binds one button listener and retains the exact delegate for cleanup.
    /// </summary>
    /// <param name="button">The button to bind.</param>
    /// <param name="listener">The semantic listener.</param>
    private void BindButton(Button button, UnityAction listener)
    {
        if (button == null)
            return;

        button.onClick.AddListener(listener);
        removeControlListeners.Add(() => button.onClick.RemoveListener(listener));
    }

    /// <summary>
    /// Binds one toggle listener and retains the exact delegate for cleanup.
    /// </summary>
    /// <param name="toggle">The toggle to bind.</param>
    /// <param name="listener">The semantic listener.</param>
    private void BindToggle(Toggle toggle, UnityAction<bool> listener)
    {
        toggle.onValueChanged.AddListener(listener);
        removeControlListeners.Add(() => toggle.onValueChanged.RemoveListener(listener));
    }

    /// <summary>
    /// Binds pointer events that control local pressed presentation.
    /// </summary>
    private void BindPressVisuals()
    {
        foreach (PressVisualBinding binding in pressVisualBindings)
        {
            if (binding?.Trigger == null)
                continue;

            BindTrigger(
                binding.Trigger,
                EventTriggerType.PointerDown,
                _ => SetPressed(binding, true)
            );
            BindTrigger(
                binding.Trigger,
                EventTriggerType.PointerUp,
                _ => SetPressed(binding, false)
            );
            BindTrigger(
                binding.Trigger,
                EventTriggerType.PointerExit,
                _ => SetPressed(binding, false)
            );
        }
    }

    /// <summary>
    /// Binds pointer events that emit configured audio cues.
    /// </summary>
    private void BindAudioCues()
    {
        foreach (AudioCueBinding binding in audioCueBindings)
        {
            if (binding?.Trigger == null)
                continue;

            BindTrigger(
                binding.Trigger,
                binding.EventType,
                _ => AudioCueRequested?.Invoke(binding.ResourcePath)
            );
        }
    }

    /// <summary>
    /// Adds one runtime listener to an authored event-trigger entry.
    /// </summary>
    /// <param name="trigger">The authored event trigger.</param>
    /// <param name="eventType">The pointer event to bind.</param>
    /// <param name="listener">The runtime listener.</param>
    private void BindTrigger(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityAction<BaseEventData> listener
    )
    {
        EventTrigger.Entry entry = FindTriggerEntry(trigger, eventType);
        entry.callback.AddListener(listener);
        removeControlListeners.Add(() => entry.callback.RemoveListener(listener));
    }

    /// <summary>
    /// Finds a required event-trigger entry without changing the authored hierarchy.
    /// </summary>
    /// <param name="trigger">The event trigger to inspect.</param>
    /// <param name="eventType">The required pointer event.</param>
    /// <returns>The matching authored entry.</returns>
    private static EventTrigger.Entry FindTriggerEntry(
        EventTrigger trigger,
        EventTriggerType eventType
    )
    {
        if (trigger == null)
            throw new ArgumentNullException(nameof(trigger));
        if (trigger.triggers == null)
            throw new MissingReferenceException($"{trigger.name} has no event-trigger entries.");

        foreach (EventTrigger.Entry entry in trigger.triggers)
        {
            if (entry != null && entry.eventID == eventType)
                return entry;
        }

        throw new MissingReferenceException(
            $"{trigger.name} has no {eventType} event-trigger entry."
        );
    }

    /// <summary>
    /// Applies or clears one authored pressed-visual state.
    /// </summary>
    /// <param name="binding">The pressed-visual binding.</param>
    /// <param name="pressed">Whether the control is pressed.</param>
    private static void SetPressed(PressVisualBinding binding, bool pressed)
    {
        if (binding == null)
            return;

        foreach (Graphic graphic in binding.GraphicsHiddenWhilePressed)
        {
            if (graphic != null)
                graphic.enabled = !pressed;
        }

        foreach (GameObject activeObject in binding.ObjectsShownWhilePressed)
        {
            if (activeObject != null)
                activeObject.SetActive(pressed);
        }
    }

    /// <summary>
    /// Removes all listeners installed by this view.
    /// </summary>
    private void UnbindControls()
    {
        foreach (Action removeListener in removeControlListeners)
            removeListener();

        removeControlListeners.Clear();
        controlsBound = false;
    }
}
