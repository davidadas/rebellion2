using System;
using Rebellion.Game.Tactical;
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
    private GameObject fighterOrderPanel;

    [SerializeField]
    private Button[] fighterOrderButtons = Array.Empty<Button>();

    [SerializeField]
    private Button assignFighterOrderButton;

    [SerializeField]
    private Button cancelFighterOrderButton;

    private readonly string[] fighterOrderAddressStems = new string[4];

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
    /// Raised when the player chooses a pending fighter-group order.
    /// </summary>
    public event Action<TacticalBehavior> FighterOrderSelected;

    /// <summary>
    /// Raised when the player assigns the pending fighter-group order.
    /// </summary>
    public event Action FighterOrderAssigned;

    /// <summary>
    /// Raised when the player dismisses the fighter-order panel without assigning its pending order.
    /// </summary>
    public event Action FighterOrderCancelled;

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
    /// <param name="fighterOrders">The four fighter-order controls.</param>
    /// <param name="fighterOrdersPanel">The fighter-order panel.</param>
    /// <param name="assignFighterOrder">The pending-order assignment control.</param>
    /// <param name="cancelFighterOrder">The pending-order cancellation control.</param>
    /// <param name="pause">The pause control.</param>
    /// <param name="pauseVisual">The pause control image.</param>
    public void Configure(
        Button[] taskForces,
        Button[] fighterGroups,
        Button[] navigationSets,
        GameObject fighterOrdersPanel,
        Button[] fighterOrders,
        Button assignFighterOrder,
        Button cancelFighterOrder,
        Button pause,
        RawImage pauseVisual
    )
    {
        taskForceButtons = taskForces ?? throw new ArgumentNullException(nameof(taskForces));
        fighterGroupButtons =
            fighterGroups ?? throw new ArgumentNullException(nameof(fighterGroups));
        navigationSetButtons =
            navigationSets ?? throw new ArgumentNullException(nameof(navigationSets));
        fighterOrderPanel =
            fighterOrdersPanel ?? throw new ArgumentNullException(nameof(fighterOrdersPanel));
        fighterOrderButtons =
            fighterOrders ?? throw new ArgumentNullException(nameof(fighterOrders));
        assignFighterOrderButton =
            assignFighterOrder ?? throw new ArgumentNullException(nameof(assignFighterOrder));
        cancelFighterOrderButton =
            cancelFighterOrder ?? throw new ArgumentNullException(nameof(cancelFighterOrder));
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
        ApplyFighterOrderTextures(theme.FighterOrderVariant);
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
    /// Opens the fighter-order panel and exposes only orders valid for the selected group.
    /// </summary>
    /// <param name="canRecover">Whether the selected fighters can return to their carrier.</param>
    /// <param name="canAttackDeathStar">Whether an opposing Death Star can be attacked.</param>
    public void ShowFighterOrders(bool canRecover, bool canAttackDeathStar)
    {
        SetFighterOrderAvailability(1, canRecover);
        SetFighterOrderAvailability(2, canAttackDeathStar);
        assignFighterOrderButton.interactable = true;
        fighterOrderPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the fighter-order panel.
    /// </summary>
    public void HideFighterOrders()
    {
        fighterOrderPanel.SetActive(false);
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
        TacticalBehavior[] fighterOrders =
        {
            TacticalBehavior.AttackCapitalShips,
            TacticalBehavior.Recover,
            TacticalBehavior.AttackDeathStar,
            TacticalBehavior.AttackFighters,
        };
        BindIndexedButtons(
            fighterOrderButtons,
            index =>
            {
                assignFighterOrderButton.interactable = true;
                FighterOrderSelected?.Invoke(fighterOrders[index]);
            }
        );
        assignFighterOrderButton.onClick.AddListener(() => FighterOrderAssigned?.Invoke());
        cancelFighterOrderButton.onClick.AddListener(() => FighterOrderCancelled?.Invoke());
        pauseButton.onClick.AddListener(() => PauseToggled?.Invoke());
        HideFighterOrders();
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
        if (fighterOrderPanel == null || fighterOrderButtons?.Length != 4)
            throw new MissingReferenceException(
                "Tactical HUD fighter-order references are incomplete."
            );
        if (assignFighterOrderButton == null || cancelFighterOrderButton == null)
            throw new MissingReferenceException(
                "Tactical HUD fighter-order action references are incomplete."
            );
        if (pauseButton == null || pauseImage == null)
            throw new MissingReferenceException("Tactical HUD pause references are incomplete.");
    }

    /// <summary>
    /// Resolves the faction-specific fighter-order artwork from external content.
    /// </summary>
    /// <param name="variant">The configured fighter-order artwork variant.</param>
    private void ApplyFighterOrderTextures(string variant)
    {
        if (variant != "alliance" && variant != "empire")
            throw new InvalidOperationException(
                $"Unsupported tactical fighter-order variant: '{variant}'."
            );

        string root = $"{sharedUIRoot}/FighterOrders";
        fighterOrderAddressStems[0] = $"{root}/{variant}-attack-capital-ships";
        fighterOrderAddressStems[1] = $"{root}/{variant}-recover";
        fighterOrderAddressStems[2] = $"{root}/attack-death-star";
        fighterOrderAddressStems[3] = $"{root}/{variant}-attack-fighters";
        for (int index = 0; index < fighterOrderAddressStems.Length; index++)
            SetFighterOrderTextures(index, fighterOrderAddressStems[index]);
    }

    /// <summary>
    /// Applies one released and pressed fighter-order texture pair.
    /// </summary>
    /// <param name="index">The fixed fighter-order slot.</param>
    /// <param name="addressStem">The shared address without its state suffix.</param>
    private void SetFighterOrderTextures(int index, string addressStem)
    {
        fighterOrderButtons[index]
            .GetComponent<RawImagePressVisual>()
            .SetInteractiveTextures(
                ContentBindings.RequireTexture(contentAssets, $"{addressStem}-up"),
                ContentBindings.RequireTexture(contentAssets, $"{addressStem}-down")
            );
    }

    /// <summary>
    /// Applies the normal pair or authored disabled artwork to one conditional fighter order.
    /// </summary>
    /// <param name="index">The fixed fighter-order slot.</param>
    /// <param name="available">Whether the selected group can receive the order.</param>
    private void SetFighterOrderAvailability(int index, bool available)
    {
        Button button = fighterOrderButtons[index];
        RawImagePressVisual visual = button.GetComponent<RawImagePressVisual>();
        string addressStem = fighterOrderAddressStems[index];
        if (contentAssets != null && !string.IsNullOrWhiteSpace(addressStem))
        {
            if (available)
                SetFighterOrderTextures(index, addressStem);
            else
                visual.SetInteractiveTextures(
                    ContentBindings.RequireTexture(contentAssets, $"{addressStem}-disabled"),
                    null
                );
        }

        button.interactable = available;
    }
}
