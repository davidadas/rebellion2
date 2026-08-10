using System;
using System.Linq;
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
    private GameObject missionOrderPanel;

    [SerializeField]
    private Button[] missionOrderButtons = Array.Empty<Button>();

    [SerializeField]
    private Button assignMissionOrderButton;

    [SerializeField]
    private Button cancelMissionOrderButton;

    private readonly string[] missionOrderAddressStems = new string[4];

    [SerializeField]
    private GameObject maneuverPanel;

    [SerializeField]
    private Button[] maneuverButtons = Array.Empty<Button>();

    [SerializeField]
    private Button formationButton;

    [SerializeField]
    private Button assignManeuverButton;

    [SerializeField]
    private Button cancelManeuverButton;

    [SerializeField]
    private GameObject capitalShipStatusPanel;

    [SerializeField]
    private Button previousCapitalShipButton;

    [SerializeField]
    private Button nextCapitalShipButton;

    [SerializeField]
    private Button capitalShipMissionsButton;

    [SerializeField]
    private Button capitalShipManeuversButton;

    [SerializeField]
    private Image hullStatusBar;

    [SerializeField]
    private Image shieldStatusBar;

    [SerializeField]
    private RawImage[] systemStatusImages = Array.Empty<RawImage>();

    private TacticalFormation pendingFormation;

    [SerializeField]
    private Button pauseButton;

    [SerializeField]
    private RawImage pauseImage;

    [SerializeField]
    private Button gameOptionsButton;

    [SerializeField]
    private GameObject gameOptionsPanel;

    [SerializeField]
    private Button withdrawalButton;

    [SerializeField]
    private Button immediateResultButton;

    [SerializeField]
    private Button commandModeButton;

    [SerializeField]
    private RawImage commandModeImage;

    [SerializeField]
    private Button settingsButton;

    [SerializeField]
    private Button closeGameOptionsButton;

    [SerializeField]
    private GameObject withdrawalPanel;

    [SerializeField]
    private Button confirmWithdrawalButton;

    [SerializeField]
    private Button cancelWithdrawalButton;

    private IContentAssetSource contentAssets;
    private string sharedUIRoot;
    private int availableTaskForceCount;
    private int availableFighterGroupCount;
    private bool observing;

    /// <summary>
    /// Raised when the player selects one of the eight capital-ship task forces.
    /// </summary>
    public event Action<int> TaskForceSelected;

    /// <summary>
    /// Raised when the player selects one of the four fighter groups.
    /// </summary>
    public event Action<int> FighterGroupSelected;

    /// <summary>
    /// Raised when the player toggles one of the four navigation-point sets.
    /// </summary>
    public event Action<int> NavigationSetVisibilityToggled;

    /// <summary>
    /// Raised when the player chooses a pending mission order for the selected tactical group.
    /// </summary>
    public event Action<TacticalBehavior> MissionOrderSelected;

    /// <summary>
    /// Raised when the player assigns the pending mission order.
    /// </summary>
    public event Action MissionOrderAssigned;

    /// <summary>
    /// Raised when the player dismisses the mission-order panel without assigning its pending order.
    /// </summary>
    public event Action MissionOrderCancelled;

    /// <summary>
    /// Raised when the player chooses a pending capital-ship maneuver.
    /// </summary>
    public event Action<TacticalBehavior> ManeuverSelected;

    /// <summary>
    /// Raised when the player changes the pending capital-ship formation.
    /// </summary>
    public event Action<TacticalFormation> FormationSelected;

    /// <summary>
    /// Raised when the player assigns the pending maneuver and formation.
    /// </summary>
    public event Action ManeuverAssigned;

    /// <summary>
    /// Raised when the player dismisses the maneuver panel without assigning its pending values.
    /// </summary>
    public event Action ManeuverCancelled;

    /// <summary>
    /// Raised when the player requests the previous capital ship in the selected task force.
    /// </summary>
    public event Action PreviousCapitalShipRequested;

    /// <summary>
    /// Raised when the player requests the next capital ship in the selected task force.
    /// </summary>
    public event Action NextCapitalShipRequested;

    /// <summary>
    /// Raised when the player opens mission orders for the selected capital ship's task force.
    /// </summary>
    public event Action CapitalShipMissionsRequested;

    /// <summary>
    /// Raised when the player opens maneuvers for the selected capital ship's task force.
    /// </summary>
    public event Action CapitalShipManeuversRequested;

    /// <summary>
    /// Raised when the player toggles tactical simulation pause.
    /// </summary>
    public event Action PauseToggled;

    /// <summary>
    /// Raised when the player opens the tactical game-options panel.
    /// </summary>
    public event Action GameOptionsRequested;

    /// <summary>
    /// Raised when the player requests withdrawal from the battle.
    /// </summary>
    public event Action WithdrawalRequested;

    /// <summary>
    /// Raised when the player confirms withdrawal from the battle.
    /// </summary>
    public event Action WithdrawalConfirmed;

    /// <summary>
    /// Raised when the player cancels withdrawal from the battle.
    /// </summary>
    public event Action WithdrawalCancelled;

    /// <summary>
    /// Raised when the player requests an immediate tactical result.
    /// </summary>
    public event Action ImmediateResultRequested;

    /// <summary>
    /// Raised when the player toggles between commanding and observing the battle.
    /// </summary>
    public event Action CommandModeToggled;

    /// <summary>
    /// Raised when the player requests the general game settings.
    /// </summary>
    public event Action SettingsRequested;

    /// <summary>
    /// Raised when the player closes the tactical game-options panel.
    /// </summary>
    public event Action GameOptionsClosed;

    /// <summary>
    /// Supplies the generated tactical HUD references.
    /// </summary>
    /// <param name="taskForces">The eight task-force controls.</param>
    /// <param name="fighterGroups">The four fighter-group controls.</param>
    /// <param name="navigationSets">The four navigation-set controls.</param>
    /// <param name="missionOrders">The four mission-order controls.</param>
    /// <param name="missionOrdersPanel">The mission-order panel.</param>
    /// <param name="assignMissionOrder">The pending-order assignment control.</param>
    /// <param name="cancelMissionOrder">The pending-order cancellation control.</param>
    /// <param name="maneuversPanel">The capital-ship maneuver panel.</param>
    /// <param name="maneuvers">The five capital-ship maneuver controls.</param>
    /// <param name="formation">The Stand Off/Surround formation control.</param>
    /// <param name="assignManeuver">The pending-maneuver assignment control.</param>
    /// <param name="cancelManeuver">The pending-maneuver cancellation control.</param>
    /// <param name="pause">The pause control.</param>
    /// <param name="pauseVisual">The pause control image.</param>
    public void Configure(
        Button[] taskForces,
        Button[] fighterGroups,
        Button[] navigationSets,
        GameObject missionOrdersPanel,
        Button[] missionOrders,
        Button assignMissionOrder,
        Button cancelMissionOrder,
        GameObject maneuversPanel,
        Button[] maneuvers,
        Button formation,
        Button assignManeuver,
        Button cancelManeuver,
        Button pause,
        RawImage pauseVisual
    )
    {
        taskForceButtons = taskForces ?? throw new ArgumentNullException(nameof(taskForces));
        fighterGroupButtons =
            fighterGroups ?? throw new ArgumentNullException(nameof(fighterGroups));
        navigationSetButtons =
            navigationSets ?? throw new ArgumentNullException(nameof(navigationSets));
        missionOrderPanel =
            missionOrdersPanel ?? throw new ArgumentNullException(nameof(missionOrdersPanel));
        missionOrderButtons =
            missionOrders ?? throw new ArgumentNullException(nameof(missionOrders));
        assignMissionOrderButton =
            assignMissionOrder ?? throw new ArgumentNullException(nameof(assignMissionOrder));
        cancelMissionOrderButton =
            cancelMissionOrder ?? throw new ArgumentNullException(nameof(cancelMissionOrder));
        maneuverPanel = maneuversPanel ?? throw new ArgumentNullException(nameof(maneuversPanel));
        maneuverButtons = maneuvers ?? throw new ArgumentNullException(nameof(maneuvers));
        formationButton = formation ?? throw new ArgumentNullException(nameof(formation));
        assignManeuverButton =
            assignManeuver ?? throw new ArgumentNullException(nameof(assignManeuver));
        cancelManeuverButton =
            cancelManeuver ?? throw new ArgumentNullException(nameof(cancelManeuver));
        pauseButton = pause ?? throw new ArgumentNullException(nameof(pause));
        pauseImage = pauseVisual ?? throw new ArgumentNullException(nameof(pauseVisual));
    }

    /// <summary>
    /// Supplies the generated withdrawal controls.
    /// </summary>
    /// <param name="withdraw">The control that requests withdrawal.</param>
    /// <param name="panel">The tactical withdrawal confirmation panel.</param>
    /// <param name="confirm">The control that confirms withdrawal.</param>
    /// <param name="cancel">The control that cancels withdrawal.</param>
    public void ConfigureWithdrawal(
        Button withdraw,
        GameObject panel,
        Button confirm,
        Button cancel
    )
    {
        withdrawalButton = withdraw ?? throw new ArgumentNullException(nameof(withdraw));
        withdrawalPanel = panel ?? throw new ArgumentNullException(nameof(panel));
        confirmWithdrawalButton = confirm ?? throw new ArgumentNullException(nameof(confirm));
        cancelWithdrawalButton = cancel ?? throw new ArgumentNullException(nameof(cancel));
    }

    /// <summary>
    /// Supplies the generated tactical game-options controls.
    /// </summary>
    /// <param name="open">The control that opens the panel.</param>
    /// <param name="panel">The tactical game-options panel.</param>
    /// <param name="immediateResult">The immediate-result control.</param>
    /// <param name="commandMode">The command/observe toggle.</param>
    /// <param name="commandModeVisual">The command/observe toggle image.</param>
    /// <param name="settings">The general settings control.</param>
    /// <param name="close">The control that closes the panel.</param>
    public void ConfigureGameOptions(
        Button open,
        GameObject panel,
        Button immediateResult,
        Button commandMode,
        RawImage commandModeVisual,
        Button settings,
        Button close
    )
    {
        gameOptionsButton = open ?? throw new ArgumentNullException(nameof(open));
        gameOptionsPanel = panel ?? throw new ArgumentNullException(nameof(panel));
        immediateResultButton =
            immediateResult ?? throw new ArgumentNullException(nameof(immediateResult));
        commandModeButton = commandMode ?? throw new ArgumentNullException(nameof(commandMode));
        commandModeImage =
            commandModeVisual ?? throw new ArgumentNullException(nameof(commandModeVisual));
        settingsButton = settings ?? throw new ArgumentNullException(nameof(settings));
        closeGameOptionsButton = close ?? throw new ArgumentNullException(nameof(close));
    }

    /// <summary>
    /// Supplies the generated capital-ship status controls.
    /// </summary>
    /// <param name="panel">The selected-capital-ship status panel.</param>
    /// <param name="previous">The previous-ship control.</param>
    /// <param name="next">The next-ship control.</param>
    /// <param name="missions">The control that opens task-force mission orders.</param>
    /// <param name="maneuvers">The control that opens task-force maneuvers.</param>
    /// <param name="hull">The hull-integrity status bar.</param>
    /// <param name="shields">The shield-strength status bar.</param>
    /// <param name="systems">The five subsystem status images in source order.</param>
    public void ConfigureCapitalShipStatus(
        GameObject panel,
        Button previous,
        Button next,
        Button missions,
        Button maneuvers,
        Image hull,
        Image shields,
        RawImage[] systems
    )
    {
        capitalShipStatusPanel = panel ?? throw new ArgumentNullException(nameof(panel));
        previousCapitalShipButton = previous ?? throw new ArgumentNullException(nameof(previous));
        nextCapitalShipButton = next ?? throw new ArgumentNullException(nameof(next));
        capitalShipMissionsButton = missions ?? throw new ArgumentNullException(nameof(missions));
        capitalShipManeuversButton =
            maneuvers ?? throw new ArgumentNullException(nameof(maneuvers));
        hullStatusBar = hull ?? throw new ArgumentNullException(nameof(hull));
        shieldStatusBar = shields ?? throw new ArgumentNullException(nameof(shields));
        systemStatusImages = systems ?? throw new ArgumentNullException(nameof(systems));
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
        ApplyMissionOrderTextures(theme.FighterOrderVariant);
        SetObserving(false);
        SetPaused(false);
    }

    /// <summary>
    /// Displays the tactical game-options panel in the original right-panel region.
    /// </summary>
    public void ShowGameOptions()
    {
        HideMissionOrders();
        HideManeuvers();
        HideCapitalShipStatus();
        HideWithdrawalConfirmation();
        gameOptionsPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the tactical game-options panel.
    /// </summary>
    public void HideGameOptions()
    {
        gameOptionsPanel.SetActive(false);
    }

    /// <summary>
    /// Switches the played side between direct command and autonomous observation.
    /// </summary>
    /// <param name="observing">Whether the player is observing instead of commanding.</param>
    public void SetObserving(bool observing)
    {
        this.observing = observing;
        SetAvailableButtons(taskForceButtons, observing ? 0 : availableTaskForceCount);
        SetAvailableButtons(fighterGroupButtons, observing ? 0 : availableFighterGroupCount);
        bool commandsEnabled = !observing;
        foreach (Button button in navigationSetButtons)
            button.interactable = commandsEnabled;
        capitalShipMissionsButton.interactable = commandsEnabled;
        capitalShipManeuversButton.interactable = commandsEnabled;
        if (observing)
        {
            HideMissionOrders();
            HideManeuvers();
            HideCapitalShipStatus();
        }

        if (contentAssets != null)
        {
            string address =
                $"{sharedUIRoot}/{(observing ? "1153-1033-tactical-ui-take-command" : "1154-1033-tactical-ui-observe-battle")}";
            commandModeImage.texture = ContentBindings.RequireTexture(contentAssets, address);
        }
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
        availableTaskForceCount = taskForceCount;
        availableFighterGroupCount = fighterGroupCount;
        SetAvailableButtons(taskForceButtons, observing ? 0 : taskForceCount);
        SetAvailableButtons(fighterGroupButtons, observing ? 0 : fighterGroupCount);
    }

    /// <summary>
    /// Opens the mission-order panel and exposes only orders valid for the selected group.
    /// </summary>
    /// <param name="canRecover">Whether the selected fighters can return to their carrier.</param>
    /// <param name="canAttackDeathStar">Whether an opposing Death Star can be attacked.</param>
    public void ShowMissionOrders(bool canRecover, bool canAttackDeathStar)
    {
        SetMissionOrderAvailability(1, canRecover);
        SetMissionOrderAvailability(2, canAttackDeathStar);
        assignMissionOrderButton.interactable = true;
        missionOrderPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the mission-order panel.
    /// </summary>
    public void HideMissionOrders()
    {
        missionOrderPanel.SetActive(false);
    }

    /// <summary>
    /// Opens the capital-ship maneuver panel with the group's current formation.
    /// </summary>
    /// <param name="formation">The formation currently assigned to the group.</param>
    public void ShowManeuvers(TacticalFormation formation)
    {
        SetFormation(formation);
        maneuverPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the capital-ship maneuver panel.
    /// </summary>
    public void HideManeuvers()
    {
        maneuverPanel.SetActive(false);
    }

    /// <summary>
    /// Displays the selected capital ship's hull, shields, and subsystem condition.
    /// </summary>
    /// <param name="unit">The selected capital ship.</param>
    /// <param name="canCycle">Whether its task force contains another active capital ship.</param>
    public void ShowCapitalShipStatus(TacticalUnitState unit, bool canCycle)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));
        if (unit.Kind != TacticalUnitKind.CapitalShip)
            throw new ArgumentException(
                "Capital-ship status requires a capital ship.",
                nameof(unit)
            );

        hullStatusBar.fillAmount = GetRatio(unit.Hull, unit.InitialHull);
        shieldStatusBar.fillAmount = GetRatio(unit.Shields, unit.InitialShields);
        previousCapitalShipButton.interactable = canCycle;
        nextCapitalShipButton.interactable = canCycle;
        ApplySystemStatus(unit);
        capitalShipStatusPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the selected capital-ship status panel.
    /// </summary>
    public void HideCapitalShipStatus()
    {
        capitalShipStatusPanel.SetActive(false);
    }

    /// <summary>
    /// Replaces the tactical command panel with the withdrawal confirmation panel.
    /// </summary>
    public void ShowWithdrawalConfirmation()
    {
        HideMissionOrders();
        HideManeuvers();
        HideCapitalShipStatus();
        HideGameOptions();
        withdrawalPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the withdrawal confirmation panel.
    /// </summary>
    public void HideWithdrawalConfirmation()
    {
        withdrawalPanel.SetActive(false);
    }

    /// <summary>
    /// Connects generated buttons after Unity has restored their serialized references.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindIndexedButtons(taskForceButtons, index => TaskForceSelected?.Invoke(index));
        BindIndexedButtons(fighterGroupButtons, index => FighterGroupSelected?.Invoke(index));
        BindIndexedButtons(
            navigationSetButtons,
            index => NavigationSetVisibilityToggled?.Invoke(index)
        );
        TacticalBehavior[] missionOrders =
        {
            TacticalBehavior.AttackCapitalShips,
            TacticalBehavior.Recover,
            TacticalBehavior.AttackDeathStar,
            TacticalBehavior.AttackFighters,
        };
        BindIndexedButtons(
            missionOrderButtons,
            index =>
            {
                assignMissionOrderButton.interactable = true;
                MissionOrderSelected?.Invoke(missionOrders[index]);
            }
        );
        assignMissionOrderButton.onClick.AddListener(() => MissionOrderAssigned?.Invoke());
        cancelMissionOrderButton.onClick.AddListener(() => MissionOrderCancelled?.Invoke());
        TacticalBehavior[] maneuvers =
        {
            TacticalBehavior.LeftHook,
            TacticalBehavior.RightHook,
            TacticalBehavior.Hammer,
            TacticalBehavior.Anvil,
            TacticalBehavior.Hold,
        };
        BindIndexedButtons(maneuverButtons, index => ManeuverSelected?.Invoke(maneuvers[index]));
        formationButton.onClick.AddListener(ToggleFormation);
        assignManeuverButton.onClick.AddListener(() => ManeuverAssigned?.Invoke());
        cancelManeuverButton.onClick.AddListener(() => ManeuverCancelled?.Invoke());
        previousCapitalShipButton.onClick.AddListener(() => PreviousCapitalShipRequested?.Invoke());
        nextCapitalShipButton.onClick.AddListener(() => NextCapitalShipRequested?.Invoke());
        capitalShipMissionsButton.onClick.AddListener(() => CapitalShipMissionsRequested?.Invoke());
        capitalShipManeuversButton.onClick.AddListener(() =>
            CapitalShipManeuversRequested?.Invoke()
        );
        pauseButton.onClick.AddListener(() => PauseToggled?.Invoke());
        gameOptionsButton.onClick.AddListener(() => GameOptionsRequested?.Invoke());
        withdrawalButton.onClick.AddListener(() => WithdrawalRequested?.Invoke());
        immediateResultButton.onClick.AddListener(() => ImmediateResultRequested?.Invoke());
        commandModeButton.onClick.AddListener(() => CommandModeToggled?.Invoke());
        settingsButton.onClick.AddListener(() => SettingsRequested?.Invoke());
        closeGameOptionsButton.onClick.AddListener(() => GameOptionsClosed?.Invoke());
        confirmWithdrawalButton.onClick.AddListener(() => WithdrawalConfirmed?.Invoke());
        cancelWithdrawalButton.onClick.AddListener(() => WithdrawalCancelled?.Invoke());
        HideMissionOrders();
        HideManeuvers();
        HideCapitalShipStatus();
        HideGameOptions();
        HideWithdrawalConfirmation();
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
        if (missionOrderPanel == null || missionOrderButtons?.Length != 4)
            throw new MissingReferenceException(
                "Tactical HUD mission-order references are incomplete."
            );
        if (assignMissionOrderButton == null || cancelMissionOrderButton == null)
            throw new MissingReferenceException(
                "Tactical HUD mission-order action references are incomplete."
            );
        if (maneuverPanel == null || maneuverButtons?.Length != 5)
            throw new MissingReferenceException("Tactical HUD maneuver references are incomplete.");
        if (formationButton == null || assignManeuverButton == null || cancelManeuverButton == null)
            throw new MissingReferenceException(
                "Tactical HUD maneuver action references are incomplete."
            );
        if (
            capitalShipStatusPanel == null
            || previousCapitalShipButton == null
            || nextCapitalShipButton == null
            || capitalShipMissionsButton == null
            || capitalShipManeuversButton == null
            || hullStatusBar == null
            || shieldStatusBar == null
            || systemStatusImages?.Length != 5
            || systemStatusImages.Any(image => image == null)
        )
        {
            throw new MissingReferenceException(
                "Tactical HUD capital-ship status references are incomplete."
            );
        }
        if (pauseButton == null || pauseImage == null)
            throw new MissingReferenceException("Tactical HUD pause references are incomplete.");
        if (
            gameOptionsButton == null
            || gameOptionsPanel == null
            || immediateResultButton == null
            || commandModeButton == null
            || commandModeImage == null
            || settingsButton == null
            || closeGameOptionsButton == null
        )
        {
            throw new MissingReferenceException(
                "Tactical HUD game-options references are incomplete."
            );
        }
        if (
            withdrawalButton == null
            || withdrawalPanel == null
            || confirmWithdrawalButton == null
            || cancelWithdrawalButton == null
        )
        {
            throw new MissingReferenceException(
                "Tactical HUD withdrawal references are incomplete."
            );
        }
    }

    /// <summary>
    /// Resolves the faction-specific mission-order artwork from external content.
    /// </summary>
    /// <param name="variant">The configured mission-order artwork variant.</param>
    private void ApplyMissionOrderTextures(string variant)
    {
        if (variant != "alliance" && variant != "empire")
            throw new InvalidOperationException(
                $"Unsupported tactical mission-order variant: '{variant}'."
            );

        string root = $"{sharedUIRoot}/FighterOrders";
        missionOrderAddressStems[0] = $"{root}/{variant}-attack-capital-ships";
        missionOrderAddressStems[1] = $"{root}/{variant}-recover";
        missionOrderAddressStems[2] = $"{root}/attack-death-star";
        missionOrderAddressStems[3] = $"{root}/{variant}-attack-fighters";
        for (int index = 0; index < missionOrderAddressStems.Length; index++)
            SetMissionOrderTextures(index, missionOrderAddressStems[index]);
    }

    /// <summary>
    /// Applies one released and pressed mission-order texture pair.
    /// </summary>
    /// <param name="index">The fixed mission-order slot.</param>
    /// <param name="addressStem">The shared address without its state suffix.</param>
    private void SetMissionOrderTextures(int index, string addressStem)
    {
        missionOrderButtons[index]
            .GetComponent<RawImagePressVisual>()
            .SetInteractiveTextures(
                ContentBindings.RequireTexture(contentAssets, $"{addressStem}-up"),
                ContentBindings.RequireTexture(contentAssets, $"{addressStem}-down")
            );
    }

    /// <summary>
    /// Applies the normal pair or authored disabled artwork to one conditional mission order.
    /// </summary>
    /// <param name="index">The fixed mission-order slot.</param>
    /// <param name="available">Whether the selected group can receive the order.</param>
    private void SetMissionOrderAvailability(int index, bool available)
    {
        Button button = missionOrderButtons[index];
        RawImagePressVisual visual = button.GetComponent<RawImagePressVisual>();
        string addressStem = missionOrderAddressStems[index];
        if (contentAssets != null && !string.IsNullOrWhiteSpace(addressStem))
        {
            if (available)
                SetMissionOrderTextures(index, addressStem);
            else
                visual.SetInteractiveTextures(
                    ContentBindings.RequireTexture(contentAssets, $"{addressStem}-disabled"),
                    null
                );
        }

        button.interactable = available;
    }

    /// <summary>
    /// Changes the pending formation and updates its authored toggle artwork.
    /// </summary>
    /// <param name="formation">The pending formation.</param>
    private void SetFormation(TacticalFormation formation)
    {
        pendingFormation = formation;
        if (contentAssets == null)
            return;

        string root = $"{sharedUIRoot}/Maneuvers";
        string current = formation == TacticalFormation.StandOff ? "stand-off" : "surround";
        string alternate = formation == TacticalFormation.StandOff ? "surround" : "stand-off";
        formationButton
            .GetComponent<RawImagePressVisual>()
            .SetInteractiveTextures(
                ContentBindings.RequireTexture(contentAssets, $"{root}/{current}"),
                ContentBindings.RequireTexture(contentAssets, $"{root}/{alternate}")
            );
    }

    /// <summary>
    /// Toggles the pending formation without publishing it before assignment.
    /// </summary>
    private void ToggleFormation()
    {
        TacticalFormation formation =
            pendingFormation == TacticalFormation.StandOff
                ? TacticalFormation.Surround
                : TacticalFormation.StandOff;
        SetFormation(formation);
        FormationSelected?.Invoke(formation);
    }

    /// <summary>
    /// Resolves the five source-ordered subsystem condition images for one capital ship.
    /// </summary>
    /// <param name="unit">The selected capital ship.</param>
    private void ApplySystemStatus(TacticalUnitState unit)
    {
        if (contentAssets == null)
            return;

        string[] names =
        {
            "shield-recharge",
            "weapon-recharge",
            "tractor-beam",
            "engines",
            "hyperspace-engines",
        };
        TacticalDamageSystem[] systems =
        {
            TacticalDamageSystem.ShieldGenerator,
            TacticalDamageSystem.WeaponSystems,
            TacticalDamageSystem.TractorBeam,
            TacticalDamageSystem.SublightDrive,
            TacticalDamageSystem.Hyperdrive,
        };
        int[] resourceBases = { 1201, 1206, 1211, 1216, 1221 };
        for (int index = 0; index < systemStatusImages.Length; index++)
        {
            int condition = Math.Max(0, 4 - unit.GetSystemDamage(systems[index]));
            int percentage = condition * 25;
            int resource = resourceBases[index] + condition;
            string address =
                $"{sharedUIRoot}/{resource}-1033-tactical-ui-{names[index]}-{percentage}%";
            systemStatusImages[index].texture = ContentBindings.RequireTexture(
                contentAssets,
                address
            );
        }
    }

    /// <summary>
    /// Converts a current and maximum value to a clamped fill ratio.
    /// </summary>
    /// <param name="current">The current value.</param>
    /// <param name="maximum">The maximum value.</param>
    /// <returns>The value from zero through one.</returns>
    private static float GetRatio(int current, int maximum)
    {
        return maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
    }
}
