using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the source-resolution tactical HUD controls and forwards semantic player input.
/// </summary>
public sealed class TacticalBattleView : MonoBehaviour
{
    [SerializeField]
    private Button[] taskForceButtons = Array.Empty<Button>();

    [SerializeField]
    private Button[] fighterGroupButtons = Array.Empty<Button>();

    [SerializeField]
    private Button[] navigationSetButtons = Array.Empty<Button>();

    [SerializeField]
    private Button pauseButton;

    [SerializeField]
    private RawImage pauseImage;

    private IContentAssetSource contentAssets;
    private string sharedUIRoot;

    /// <summary>
    /// Raised when the player selects one of the eight capital-ship task forces.
    /// </summary>
    public event Action<int> TaskForceSelected;

    /// <summary>
    /// Raised when the player selects one of the four fighter groups.
    /// </summary>
    public event Action<int> FighterGroupSelected;

    /// <summary>
    /// Raised when the player selects one of the four navigation-point sets.
    /// </summary>
    public event Action<int> NavigationSetSelected;

    /// <summary>
    /// Raised when the player toggles tactical simulation pause.
    /// </summary>
    public event Action PauseToggled;

    /// <summary>
    /// Supplies the generated tactical HUD references.
    /// </summary>
    /// <param name="taskForces">The eight task-force controls.</param>
    /// <param name="fighterGroups">The four fighter-group controls.</param>
    /// <param name="navigationSets">The four navigation-set controls.</param>
    /// <param name="pause">The pause control.</param>
    /// <param name="pauseVisual">The pause control image.</param>
    public void Configure(
        Button[] taskForces,
        Button[] fighterGroups,
        Button[] navigationSets,
        Button pause,
        RawImage pauseVisual
    )
    {
        taskForceButtons = taskForces ?? throw new ArgumentNullException(nameof(taskForces));
        fighterGroupButtons =
            fighterGroups ?? throw new ArgumentNullException(nameof(fighterGroups));
        navigationSetButtons =
            navigationSets ?? throw new ArgumentNullException(nameof(navigationSets));
        pauseButton = pause ?? throw new ArgumentNullException(nameof(pause));
        pauseImage = pauseVisual ?? throw new ArgumentNullException(nameof(pauseVisual));
    }

    /// <summary>
    /// Resolves every authored HUD texture from installation content.
    /// </summary>
    /// <param name="assets">The active content source.</param>
    /// <param name="theme">The player faction's tactical theme.</param>
    public void InitializeContent(IContentAssetSource assets, TacticalBattleTheme theme)
    {
        contentAssets = assets ?? throw new ArgumentNullException(nameof(assets));
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        if (string.IsNullOrWhiteSpace(theme.SharedUIRoot))
            throw new InvalidOperationException("The tactical shared UI root is missing.");

        sharedUIRoot = theme.SharedUIRoot;
        ContentBindings.Apply(gameObject, contentAssets);
        SetPaused(false);
    }

    /// <summary>
    /// Displays the control that performs the next valid pause transition.
    /// </summary>
    /// <param name="paused">Whether the tactical simulation is paused.</param>
    public void SetPaused(bool paused)
    {
        if (contentAssets == null || pauseImage == null)
            return;

        string address = $"{sharedUIRoot}/Hud/{(paused ? "resume" : "pause")}";
        pauseImage.texture = ContentBindings.RequireTexture(contentAssets, address);
    }

    /// <summary>
    /// Enables only command slots that contain units for the played side.
    /// </summary>
    /// <param name="taskForceCount">The number of populated capital task-force slots.</param>
    /// <param name="fighterGroupCount">The number of populated fighter-type slots.</param>
    public void SetGroupAvailability(int taskForceCount, int fighterGroupCount)
    {
        SetAvailableButtons(taskForceButtons, taskForceCount);
        SetAvailableButtons(fighterGroupButtons, fighterGroupCount);
    }

    /// <summary>
    /// Connects generated buttons after Unity has restored their serialized references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindIndexedButtons(taskForceButtons, index => TaskForceSelected?.Invoke(index));
        BindIndexedButtons(fighterGroupButtons, index => FighterGroupSelected?.Invoke(index));
        BindIndexedButtons(navigationSetButtons, index => NavigationSetSelected?.Invoke(index));
        pauseButton.onClick.AddListener(() => PauseToggled?.Invoke());
    }

    /// <summary>
    /// Connects one stable index to each button in source order.
    /// </summary>
    /// <param name="buttons">The ordered controls.</param>
    /// <param name="handler">The indexed callback.</param>
    private static void BindIndexedButtons(Button[] buttons, Action<int> handler)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => handler(index));
        }
    }

    /// <summary>
    /// Enables the populated prefix of a fixed tactical button bank.
    /// </summary>
    /// <param name="buttons">The fixed button bank.</param>
    /// <param name="availableCount">The number of populated slots.</param>
    private static void SetAvailableButtons(Button[] buttons, int availableCount)
    {
        if (availableCount < 0 || availableCount > buttons.Length)
            throw new ArgumentOutOfRangeException(nameof(availableCount));

        for (int index = 0; index < buttons.Length; index++)
            buttons[index].interactable = index < availableCount;
    }

    /// <summary>
    /// Rejects incomplete generated tactical HUD references.
    /// </summary>
    private void VerifyReferences()
    {
        if (taskForceButtons?.Length != 8)
            throw new MissingReferenceException("Tactical HUD requires eight task-force buttons.");
        if (fighterGroupButtons?.Length != 4)
            throw new MissingReferenceException(
                "Tactical HUD requires four fighter-group buttons."
            );
        if (navigationSetButtons?.Length != 4)
            throw new MissingReferenceException(
                "Tactical HUD requires four navigation-set buttons."
            );
        if (pauseButton == null || pauseImage == null)
            throw new MissingReferenceException("Tactical HUD pause references are incomplete.");
    }
}
