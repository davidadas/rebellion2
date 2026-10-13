using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using UnityEngine.InputSystem;

/// <summary>
/// Owns strategy shortcut subscriptions and evaluates the screen's current execution boundary.
/// </summary>
public sealed class StrategyShortcutBindings : IDisposable
{
    private readonly InputActionAsset _asset;
    private readonly Func<bool> _canExecute;
    private readonly List<(
        InputAction Action,
        Action<InputAction.CallbackContext> Callback
    )> _bindings = new();

    /// <summary>
    /// Enables the strategy action map and prepares to own its command listeners.
    /// </summary>
    /// <param name="asset">The application's shared input asset.</param>
    /// <param name="canExecute">Checks whether the screen currently accepts strategy shortcuts.</param>
    public StrategyShortcutBindings(InputActionAsset asset, Func<bool> canExecute)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        asset.FindActionMap("Strategy")?.Enable();
    }

    /// <summary>
    /// Maps the configured strategy actions to their feature commands.
    /// </summary>
    /// <param name="requestBookmark">Opens the selected zero-based bookmark slot.</param>
    /// <param name="openFinder">Opens a finder mode.</param>
    /// <param name="openEncyclopedia">Opens the encyclopedia.</param>
    /// <param name="openMessages">Opens a message tab.</param>
    /// <param name="beginConstruction">Starts advisor construction for a manufacturing category.</param>
    /// <param name="executeContextShortcut">Executes the selected targeting or movement shortcut.</param>
    /// <param name="stepGameSpeed">Changes speed in the requested direction.</param>
    /// <param name="selectFilter">Selects a galactic information filter.</param>
    public void BindCommands(
        Action<int> requestBookmark,
        Action<FinderMode> openFinder,
        Action openEncyclopedia,
        Action<MessagesTab> openMessages,
        Action<ManufacturingType> beginConstruction,
        Action<StrategyMenuAction> executeContextShortcut,
        Action<bool> stepGameSpeed,
        Action<GalacticInformationFilterMode> selectFilter
    )
    {
        for (int slot = 0; slot < 12; slot++)
        {
            int index = slot;
            Bind($"Strategy/BookmarkSlot{slot + 1}", () => requestBookmark(index));
        }

        Bind("Strategy/OpenSystemFinder", () => openFinder(FinderMode.Systems));
        Bind("Strategy/OpenFleetFinder", () => openFinder(FinderMode.Fleets));
        Bind("Strategy/OpenPersonnelFinder", () => openFinder(FinderMode.Personnel));
        Bind("Strategy/OpenPlanetFinder", () => openFinder(FinderMode.Troops));
        Bind("Strategy/OpenEncyclopedia", () => openEncyclopedia());
        Bind("Strategy/OpenAllMessages", () => openMessages(MessagesTab.All));
        Bind("Strategy/AdvisorBuildShips", () => beginConstruction(ManufacturingType.Ship));
        Bind("Strategy/AdvisorBuildTroops", () => beginConstruction(ManufacturingType.Troop));
        Bind(
            "Strategy/AdvisorBuildFacilities",
            () => beginConstruction(ManufacturingType.Building)
        );
        Bind(
            "Strategy/OpenMissionSetup",
            () => executeContextShortcut(StrategyMenuAction.CreateMission)
        );
        Bind("Strategy/IssueMoveOrder", () => executeContextShortcut(StrategyMenuAction.Move));
        Bind(
            "Strategy/ConfirmMoveOrder",
            () => executeContextShortcut(StrategyMenuAction.MoveConfirm)
        );
        Bind("Strategy/DecreaseGameSpeed", () => stepGameSpeed(false));
        Bind("Strategy/IncreaseGameSpeed", () => stepGameSpeed(true));

        Bind(
            "Strategy/ShowPopularSupport",
            () => selectFilter(GalacticInformationFilterMode.PopularSupport)
        );
        Bind("Strategy/ShowUprisings", () => selectFilter(GalacticInformationFilterMode.Uprisings));
        Bind(
            "Strategy/ShowIdleFleets",
            () => selectFilter(GalacticInformationFilterMode.IdleFleets)
        );
        Bind(
            "Strategy/ShowFleetsEnroute",
            () => selectFilter(GalacticInformationFilterMode.FleetsEnroute)
        );
        Bind(
            "Strategy/ShowIdlePersonnel",
            () => selectFilter(GalacticInformationFilterMode.IdlePersonnel)
        );
        Bind(
            "Strategy/ShowActivePersonnel",
            () => selectFilter(GalacticInformationFilterMode.ActivePersonnel)
        );
        Bind(
            "Strategy/ShowAvailableEnergy",
            () => selectFilter(GalacticInformationFilterMode.AvailableEnergy)
        );
        Bind(
            "Strategy/ShowAvailableRawMaterial",
            () => selectFilter(GalacticInformationFilterMode.AvailableRawMaterial)
        );
        Bind("Strategy/ShowMines", () => selectFilter(GalacticInformationFilterMode.Mines));
        Bind(
            "Strategy/ShowRefineries",
            () => selectFilter(GalacticInformationFilterMode.Refineries)
        );
        Bind("Strategy/ShowShipyards", () => selectFilter(GalacticInformationFilterMode.Shipyards));
        Bind(
            "Strategy/ShowTrainingFacilities",
            () => selectFilter(GalacticInformationFilterMode.TrainingFacilities)
        );
        Bind(
            "Strategy/ShowConstructionYards",
            () => selectFilter(GalacticInformationFilterMode.ConstructionYards)
        );
        Bind(
            "Strategy/ShowIdleShipyards",
            () => selectFilter(GalacticInformationFilterMode.IdleShipyards)
        );
        Bind(
            "Strategy/ShowIdleTrainingFacilities",
            () => selectFilter(GalacticInformationFilterMode.IdleTrainingFacilities)
        );
        Bind(
            "Strategy/ShowIdleConstructionYards",
            () => selectFilter(GalacticInformationFilterMode.IdleConstructionYards)
        );
        Bind("Strategy/ShowTroopers", () => selectFilter(GalacticInformationFilterMode.Troopers));
        Bind(
            "Strategy/ShowFighterSquadrons",
            () => selectFilter(GalacticInformationFilterMode.FighterSquadrons)
        );
        Bind(
            "Strategy/ShowUnitShieldGenerators",
            () => selectFilter(GalacticInformationFilterMode.UnitShieldGenerators)
        );
        Bind(
            "Strategy/ShowPlanetaryShieldGenerators",
            () => selectFilter(GalacticInformationFilterMode.PlanetaryShieldGenerators)
        );
        Bind(
            "Strategy/ShowPlanetaryDefenseBatteries",
            () => selectFilter(GalacticInformationFilterMode.PlanetaryDefenseBatteries)
        );
    }

    /// <summary>
    /// Subscribes a command when its action exists in the active input asset.
    /// </summary>
    /// <param name="actionPath">The map/action path to resolve.</param>
    /// <param name="command">The command invoked for permitted performed input.</param>
    public void Bind(string actionPath, Action command)
    {
        InputAction action = _asset.FindAction(actionPath, false);
        if (action == null)
            return;

        Action<InputAction.CallbackContext> callback = context =>
        {
            if (context.performed && _canExecute())
                command();
        };

        action.performed += callback;
        _bindings.Add((action, callback));
    }

    /// <summary>
    /// Detaches owned listeners while leaving the shared action map and other subscribers intact.
    /// Repeated disposal has no further effect.
    /// </summary>
    public void Dispose()
    {
        foreach ((InputAction action, Action<InputAction.CallbackContext> callback) in _bindings)
            action.performed -= callback;
        _bindings.Clear();
    }
}
