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

        public Image Image => button?.targetGraphic as Image;

        public Sprite[] Frames { get; private set; } = Array.Empty<Sprite>();

        public float FrameIntervalSeconds { get; private set; }

        public int FrameIndex { get; set; }

        public float FrameElapsedSeconds { get; set; }

        /// <summary>
        /// Gets whether the binding has a button and faction identifier.
        /// </summary>
        public bool IsConfigured => button != null && !string.IsNullOrWhiteSpace(factionId);

        /// <summary>
        /// Applies a faction identifier and loaded launch-animation frames to this binding.
        /// </summary>
        /// <param name="id">The configured faction identifier.</param>
        /// <param name="frames">The loaded launch-animation sprites.</param>
        /// <param name="frameIntervalSeconds">The elapsed time between animation frames.</param>
        public void Configure(string id, Sprite[] frames, float frameIntervalSeconds)
        {
            factionId = id;
            Frames = Image == null ? Array.Empty<Sprite>() : frames ?? Array.Empty<Sprite>();
            FrameIntervalSeconds = frameIntervalSeconds;
            FrameIndex = 0;
            FrameElapsedSeconds = 0f;
            if (Image != null && Frames.Length > 0)
                Image.sprite = Frames[0];
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
    private SaveMenuConfirmDialogView exitConfirmationDialog;

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
    private AudioCueBinding[] audioCueBindings = Array.Empty<AudioCueBinding>();

    private readonly List<Action> removeControlListeners = new List<Action>();
    private readonly List<Sprite> ownedFactionSprites = new List<Sprite>();
    private Sprite[] exitAnimationFrames = Array.Empty<Sprite>();
    private Sprite[] loadAnimationFrames = Array.Empty<Sprite>();
    private int exitAnimationFrameIndex;
    private int loadAnimationFrameIndex;
    private float exitAnimationElapsedSeconds;
    private float loadAnimationElapsedSeconds;
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
    /// Advances every active faction launch animation.
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
        foreach (FactionLaunchBinding binding in factionLaunchBindings)
            AdvanceFactionAnimation(binding, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Releases sprites created from external content textures.
    /// </summary>
    private void OnDestroy()
    {
        foreach (Sprite sprite in ownedFactionSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        ownedFactionSprites.Clear();
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
        standardVictoryConditionSprite =
            contentAssets.GetSprite(_standardVictorySpriteAddress)
            ?? throw new InvalidOperationException(
                $"Main-menu sprite is missing: {_standardVictorySpriteAddress}"
            );
        headquartersVictoryConditionSprite =
            contentAssets.GetSprite(_headquartersVictorySpriteAddress)
            ?? throw new InvalidOperationException(
                $"Main-menu sprite is missing: {_headquartersVictorySpriteAddress}"
            );
        SpriteState creditsSpriteState = creditsButton.spriteState;
        creditsSpriteState.pressedSprite =
            contentAssets.GetSprite(_creditsPressedSpriteAddress)
            ?? throw new InvalidOperationException(
                $"Main-menu sprite is missing: {_creditsPressedSpriteAddress}"
            );
        creditsButton.spriteState = creditsSpriteState;
        exitConfirmationDialog.InitializeContent(contentAssets);
        exitAnimationFrames = LoadAnimationFrames(contentAssets, _exitAnimationRoot);
        loadAnimationFrames = LoadAnimationFrames(contentAssets, _loadAnimationRoot);
        ResetAnimation(exitButton?.targetGraphic as Image, exitAnimationFrames);
        ResetAnimation(loadGameButton?.targetGraphic as Image, loadAnimationFrames);
    }

    /// <summary>
    /// Populates the authored launch positions from the active scenario's playable factions.
    /// </summary>
    /// <param name="factionIDs">The playable faction identifiers in display order.</param>
    /// <param name="getTheme">The active faction-theme resolver.</param>
    /// <param name="getTexture">The active content texture resolver.</param>
    internal void RenderFactions(
        IReadOnlyList<string> factionIDs,
        Func<string, FactionTheme> getTheme,
        Func<string, Texture2D> getTexture
    )
    {
        if (factionIDs == null)
            throw new ArgumentNullException(nameof(factionIDs));
        if (getTheme == null)
            throw new ArgumentNullException(nameof(getTheme));
        if (getTexture == null)
            throw new ArgumentNullException(nameof(getTexture));
        if (factionIDs.Count > factionLaunchBindings.Length)
        {
            throw new InvalidOperationException(
                $"The active scenario has {factionIDs.Count} playable factions, "
                    + $"but the main menu has {factionLaunchBindings.Length} launch positions."
            );
        }

        ClearFactionSprites();
        for (int index = 0; index < factionLaunchBindings.Length; index++)
        {
            FactionLaunchBinding binding = factionLaunchBindings[index];
            bool active = index < factionIDs.Count;
            binding.Button.gameObject.SetActive(active);
            if (!active)
                continue;

            string factionID = factionIDs[index];
            MainMenuFactionTheme mainMenuTheme =
                getTheme(factionID)?.MainMenu
                ?? throw new InvalidOperationException(
                    $"Faction '{factionID}' has no main-menu theme."
                );
            Sprite[] frames =
                binding.Image == null
                    ? Array.Empty<Sprite>()
                    : LoadFactionAnimationFrames(mainMenuTheme, getTexture);
            binding.Configure(factionID, frames, mainMenuTheme.LaunchAnimationFrameIntervalSeconds);
        }
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
    /// Loads and converts every configured launch-animation texture to a sprite.
    /// </summary>
    /// <param name="theme">The faction's main-menu theme.</param>
    /// <param name="getTexture">The active content texture resolver.</param>
    /// <returns>The created launch-animation sprites.</returns>
    private Sprite[] LoadFactionAnimationFrames(
        MainMenuFactionTheme theme,
        Func<string, Texture2D> getTexture
    )
    {
        if (
            string.IsNullOrWhiteSpace(theme.LaunchAnimationRoot)
            || theme.LaunchAnimationFrameCount <= 0
            || theme.LaunchAnimationFrameIntervalSeconds <= 0f
        )
            throw new InvalidOperationException("A main-menu faction animation is incomplete.");

        Sprite[] frames = new Sprite[theme.LaunchAnimationFrameCount];
        for (int index = 0; index < frames.Length; index++)
        {
            string path = theme.GetLaunchAnimationFramePath(index + 1);
            Texture2D texture =
                getTexture(path)
                ?? throw new InvalidOperationException(
                    $"Main-menu faction animation frame is missing: {path}"
                );
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sprite.name = texture.name;
            frames[index] = sprite;
            ownedFactionSprites.Add(sprite);
        }

        return frames;
    }

    /// <summary>
    /// Releases every faction launch sprite currently owned by this view.
    /// </summary>
    private void ClearFactionSprites()
    {
        foreach (Sprite sprite in ownedFactionSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        ownedFactionSprites.Clear();
    }

    /// <summary>
    /// Advances one faction launch animation by elapsed unscaled time.
    /// </summary>
    /// <param name="binding">The configured faction launch binding.</param>
    /// <param name="elapsedSeconds">The elapsed unscaled frame time.</param>
    private static void AdvanceFactionAnimation(FactionLaunchBinding binding, float elapsedSeconds)
    {
        if (
            binding?.Image == null
            || binding.Frames?.Length < 2
            || binding.FrameIntervalSeconds <= 0f
        )
            return;

        binding.FrameElapsedSeconds += elapsedSeconds;
        while (binding.FrameElapsedSeconds >= binding.FrameIntervalSeconds)
        {
            binding.FrameElapsedSeconds -= binding.FrameIntervalSeconds;
            binding.FrameIndex = (binding.FrameIndex + 1) % binding.Frames.Length;
        }

        binding.Image.sprite = binding.Frames[binding.FrameIndex];
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
