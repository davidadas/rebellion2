using System;
using System.Collections.Generic;
using System.Linq;
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
    private const float _optionsSurfaceWidth = 853.33f;
    private const float _optionsSurfaceHeight = 480f;
    private const string _headquartersVictorySpriteAddress =
        "Application/MainMenu/UI/ui_mainmenu_hqonly_icon";
    private const string _standardVictorySpriteAddress =
        "Application/MainMenu/UI/ui_mainmenu_hq_icon";
    private const string _exitAnimationRoot = "Application/MainMenu/UI/ui_mainmenu_exit_";
    private const string _loadAnimationRoot = "Application/MainMenu/UI/ui_mainmenu_load_";
    private const string _creditsPressedSpriteAddress =
        "Application/MainMenu/UI/ui_mainmenu_credits_icon_pressed";
    private const int _commandAnimationFrameCount = 30;
    private const float _exitAnimationFrameIntervalSeconds = 1f / 60f;
    private const float _loadAnimationFrameIntervalSeconds = 1.7666667f / 30f;

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
        public bool IsConfigured => button != null && !string.IsNullOrWhiteSpace(factionId);

        /// <summary>
        /// Applies a faction identifier to this binding.
        /// </summary>
        /// <param name="id">The configured faction identifier.</param>
        public void Configure(string id)
        {
            factionId = id;
        }
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

        public string ResourcePath => resourcePath?.Trim();

        /// <summary>
        /// Gets whether the binding has a trigger and audio resource path.
        /// </summary>
        public bool IsConfigured => trigger != null && !string.IsNullOrWhiteSpace(resourcePath);
    }

    [Header("Commands")]
    [SerializeField]
    private Button loadGameButton;

    [SerializeField]
    private Button exitButton;

    [SerializeField]
    private GameObject exitPressedImage;

    [SerializeField]
    private ConfirmationDialogView exitConfirmationDialog;

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

    [SerializeField]
    private AutoRotate victoryConditionSpinner;

    [SerializeField]
    private GameObject victoryConditionSelectionOverlay;

    [Header("Pointer Presentation")]
    [SerializeField]
    private AudioCueBinding[] audioCueBindings = Array.Empty<AudioCueBinding>();

    [Header("Options overlay")]
    [SerializeField]
    private GameObject optionsOverlay;

    [SerializeField]
    private RectTransform optionsWindowLayer;

    [SerializeField]
    private UIWindowManager optionsWindowManager;

    private readonly List<Action> removeControlListeners = new List<Action>();
    private Sprite[] exitAnimationFrames = Array.Empty<Sprite>();
    private Sprite[] loadAnimationFrames = Array.Empty<Sprite>();
    private int exitAnimationFrameIndex;
    private int loadAnimationFrameIndex;
    private float exitAnimationElapsedSeconds;
    private float loadAnimationElapsedSeconds;
    private bool controlsBound;

    /// <summary>
    /// Returns the authored layer that receives the Options window.
    /// </summary>
    internal Transform OptionsWindowLayer => optionsWindowLayer;

    /// <summary>
    /// Returns the authored manager for Main Menu modal windows.
    /// </summary>
    internal UIWindowManager OptionsWindowManager => optionsWindowManager;

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
    public event Action SaveLoadMenuRequested;

    /// <summary>
    /// Occurs when the player requests exiting the application.
    /// </summary>
    public event Action ExitRequested;

    /// <summary>
    /// Occurs when the player requests the credits sequence.
    /// </summary>
    public event Action CreditsRequested;

    /// <summary>
    /// Occurs when a configured pointer interaction requests an audio cue.
    /// </summary>
    public event Action<string> AudioCueRequested;

    /// <summary>
    /// Advances the animated command buttons.
    /// </summary>
    private void Update()
    {
        AdvanceAnimation(
            exitButton?.targetGraphic as Image,
            exitAnimationFrames,
            _exitAnimationFrameIntervalSeconds,
            ref exitAnimationFrameIndex,
            ref exitAnimationElapsedSeconds,
            Time.unscaledDeltaTime
        );
        AdvanceAnimation(
            loadGameButton?.targetGraphic as Image,
            loadAnimationFrames,
            _loadAnimationFrameIntervalSeconds,
            ref loadAnimationFrameIndex,
            ref loadAnimationElapsedSeconds,
            Time.unscaledDeltaTime
        );
    }

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

        // HQ-only state shows a crosshair over the citadel and pauses its spin so it reads as a still
        // HQ symbol.
        victoryConditionSelectionOverlay.SetActive(headquarters);
        victoryConditionSpinner.enabled = !headquarters;
    }

    /// <summary>
    /// Replaces editor-only preview sprites with assets loaded from the installation content.
    /// </summary>
    /// <param name="contentAssets">The active external content source.</param>
    internal void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        ContentBindings.Apply(transform.root.gameObject, contentAssets);
        standardVictoryConditionSprite = ContentBindings.RequireSprite(
            contentAssets,
            _standardVictorySpriteAddress
        );
        headquartersVictoryConditionSprite = ContentBindings.RequireSprite(
            contentAssets,
            _headquartersVictorySpriteAddress
        );
        SpriteState creditsSpriteState = creditsButton.spriteState;
        creditsSpriteState.pressedSprite = ContentBindings.RequireSprite(
            contentAssets,
            _creditsPressedSpriteAddress
        );
        creditsButton.spriteState = creditsSpriteState;
        exitConfirmationDialog.InitializeContent(contentAssets);
        exitAnimationFrames = LoadAnimationFrames(contentAssets, _exitAnimationRoot);
        loadAnimationFrames = LoadAnimationFrames(contentAssets, _loadAnimationRoot);
        ResetAnimation(exitButton?.targetGraphic as Image, exitAnimationFrames);
        ResetAnimation(loadGameButton?.targetGraphic as Image, loadAnimationFrames);
    }

    /// <summary>
    /// Shows or hides the authored Options overlay and its full-screen dimmer.
    /// </summary>
    /// <param name="visible">Whether the overlay should receive input and render.</param>
    internal void RenderOptionsOverlay(bool visible)
    {
        if (optionsOverlay != null && optionsOverlay.activeSelf != visible)
            optionsOverlay.SetActive(visible);
    }

    /// <summary>
    /// Centers an Options window in the overlay's source-coordinate surface.
    /// </summary>
    /// <param name="prefab">The Options window prefab being positioned.</param>
    /// <returns>The centered source-coordinate position.</returns>
    internal Vector2Int GetOptionsWindowPosition(OptionsMenuView prefab)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));

        RectTransform rect = (RectTransform)prefab.transform;
        return new Vector2Int(
            Mathf.RoundToInt(_optionsSurfaceWidth / 2f - rect.sizeDelta.x / 2f),
            Mathf.RoundToInt(_optionsSurfaceHeight / 2f - rect.sizeDelta.y / 2f)
        );
    }

    /// <summary>
    /// Populates the authored launch positions from the active scenario's playable factions.
    /// </summary>
    /// <param name="factionIDs">The playable faction identifiers in display order.</param>
    internal void RenderFactions(IReadOnlyList<string> factionIDs)
    {
        if (factionIDs == null)
            throw new ArgumentNullException(nameof(factionIDs));
        if (factionIDs.Count > factionLaunchBindings.Length)
        {
            throw new InvalidOperationException(
                $"The active scenario has {factionIDs.Count} playable factions, "
                    + $"but the main menu has {factionLaunchBindings.Length} launch positions."
            );
        }

        for (int index = 0; index < factionLaunchBindings.Length; index++)
        {
            FactionLaunchBinding binding = factionLaunchBindings[index];
            bool active = index < factionIDs.Count;
            binding.Button.gameObject.SetActive(active);
            if (!active)
                continue;

            binding.Configure(factionIDs[index]);
        }
    }

    /// <summary>
    /// Renders the selected galaxy size without emitting a selection request.
    /// </summary>
    /// <param name="size">The galaxy size selected in launch state.</param>
    internal void RenderGalaxySize(GameSize size)
    {
        foreach (GalaxySizeBinding binding in galaxySizeBindings)
        {
            if (binding?.Toggle != null)
                binding.Toggle.SetIsOnWithoutNotify(binding.Value == size);
        }
    }

    /// <summary>
    /// Renders the selected difficulty without emitting a selection request.
    /// </summary>
    /// <param name="difficulty">The difficulty selected in launch state.</param>
    internal void RenderDifficulty(GameDifficulty difficulty)
    {
        foreach (DifficultyBinding binding in difficultyBindings)
        {
            if (binding?.Toggle != null)
                binding.Toggle.SetIsOnWithoutNotify(binding.Value == difficulty);
        }
    }

    /// <summary>
    /// Returns the distinct sound-effect resource paths configured for main-menu interactions.
    /// </summary>
    /// <returns>The configured main-menu sound-effect resource paths.</returns>
    internal IReadOnlyList<string> GetAudioCuePaths()
    {
        return (audioCueBindings ?? Array.Empty<AudioCueBinding>())
            .Where(binding => binding != null && !string.IsNullOrWhiteSpace(binding.ResourcePath))
            .Select(binding => binding.ResourcePath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Verifies that all required authored controls and presentation references are assigned.
    /// </summary>
    private void VerifyReferences()
    {
        if (
            loadGameButton == null
            || exitButton == null
            || exitPressedImage == null
            || exitConfirmationDialog == null
            || creditsButton == null
            || victoryConditionButton == null
            || victoryConditionIcon == null
            || victoryConditionText == null
            || victoryConditionSpinner == null
            || victoryConditionSelectionOverlay == null
            || optionsOverlay == null
            || optionsWindowLayer == null
            || optionsWindowManager == null
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
        VerifyBindings(audioCueBindings, binding => binding?.IsConfigured == true, "audio cue");
    }

    /// <summary>
    /// Loads one complete command-button animation from installation content.
    /// </summary>
    /// <param name="contentAssets">The active external content source.</param>
    /// <param name="addressRoot">The address prefix before the two-digit frame number.</param>
    /// <returns>The loaded animation frames.</returns>
    private Sprite[] LoadAnimationFrames(IContentAssetSource contentAssets, string addressRoot)
    {
        Sprite[] frames = new Sprite[_commandAnimationFrameCount];
        for (int index = 0; index < frames.Length; index++)
        {
            string address = addressRoot + (index + 1).ToString("00");
            Sprite sprite =
                contentAssets.GetSprite(address)
                ?? throw new InvalidOperationException(
                    $"Main-menu animation frame is missing: {address}"
                );
            frames[index] = sprite;
        }

        return frames;
    }

    /// <summary>
    /// Restarts one command-button animation from its first frame.
    /// </summary>
    /// <param name="image">The image animated by the frame sequence.</param>
    /// <param name="frames">The loaded animation frames.</param>
    private static void ResetAnimation(Image image, Sprite[] frames)
    {
        if (image != null && frames?.Length > 0)
            image.sprite = frames[0];
    }

    /// <summary>
    /// Advances one command-button animation by elapsed unscaled time.
    /// </summary>
    private static void AdvanceAnimation(
        Image image,
        Sprite[] frames,
        float frameIntervalSeconds,
        ref int frameIndex,
        ref float elapsedSeconds,
        float deltaTime
    )
    {
        if (image == null || frames?.Length < 2 || frameIntervalSeconds <= 0f)
            return;

        elapsedSeconds += deltaTime;
        while (elapsedSeconds >= frameIntervalSeconds)
        {
            elapsedSeconds -= frameIntervalSeconds;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        image.sprite = frames[frameIndex];
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

        BindButton(loadGameButton, () => SaveLoadMenuRequested?.Invoke());
        BindButton(exitButton, ShowExitConfirmation);
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

        BindAudioCues();
        BindExitPresentation();
        exitConfirmationDialog.Confirmed += ConfirmExit;
        exitConfirmationDialog.Canceled += CancelExit;
        removeControlListeners.Add(() => exitConfirmationDialog.Confirmed -= ConfirmExit);
        removeControlListeners.Add(() => exitConfirmationDialog.Canceled -= CancelExit);
        controlsBound = true;
    }

    /// <summary>
    /// Binds the exit lever's pressed-state presentation.
    /// </summary>
    private void BindExitPresentation()
    {
        EventTrigger trigger = exitButton.GetComponent<EventTrigger>();
        BindTrigger(trigger, EventTriggerType.PointerDown, _ => SetExitPressed(true));
        BindTrigger(trigger, EventTriggerType.PointerUp, _ => SetExitPressed(false));
        BindTrigger(trigger, EventTriggerType.PointerExit, _ => SetExitPressed(false));
    }

    /// <summary>
    /// Switches the exit lever between its default and pressed visuals.
    /// </summary>
    /// <param name="pressed">Whether the lever is currently pressed.</param>
    private void SetExitPressed(bool pressed)
    {
        Image defaultImage = exitButton.targetGraphic as Image;
        if (defaultImage != null)
            defaultImage.enabled = !pressed;
        exitPressedImage.SetActive(pressed);
    }

    /// <summary>
    /// Opens the exit confirmation dialog.
    /// </summary>
    private void ShowExitConfirmation()
    {
        SetExitPressed(false);
        exitConfirmationDialog.Show("Are you sure you want to quit?");
    }

    /// <summary>
    /// Forwards a confirmed exit request.
    /// </summary>
    private void ConfirmExit()
    {
        ExitRequested?.Invoke();
    }

    /// <summary>
    /// Restores the exit lever after cancellation.
    /// </summary>
    private void CancelExit()
    {
        SetExitPressed(false);
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
