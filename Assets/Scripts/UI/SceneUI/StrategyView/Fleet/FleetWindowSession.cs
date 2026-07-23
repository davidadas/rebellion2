using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Owns the local interaction state for one fleet window.
/// </summary>
internal sealed class FleetWindowSession
{
    private readonly List<ISceneNode> detailItems = new List<ISceneNode>();
    private readonly List<Fleet> fleets = new List<Fleet>();
    private readonly HashSet<ISceneNode> selectedDetailNodes = new HashSet<ISceneNode>();
    private readonly HashSet<int> selectedDetailItems = new HashSet<int>();
    private readonly HashSet<ISceneNode> selectedFleetNodes = new HashSet<ISceneNode>();
    private readonly HashSet<int> selectedFleetItems = new HashSet<int>();
    private ISceneNode contextDetailNode;
    private Fleet contextFleet;
    private ISceneNode renameTarget;
    private Fleet selectedFleet;
    private int selectedFleetIndexHint = -1;

    /// <summary>
    /// Creates a fleet-window session for one planet and owning window.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="window">The owning window shell.</param>
    public FleetWindowSession(GalaxyMapPlanet planet, UIWindow window)
    {
        Planet = planet ?? throw new ArgumentNullException(nameof(planet));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Reconcile();
    }

    public FleetWindowTab ActiveTab { get; private set; } = FleetWindowTab.CapitalShips;

    public int ContextDetailItemIndex { get; private set; } = -1;

    public int ContextFleetIndex { get; private set; } = -1;

    public IReadOnlyList<ISceneNode> DetailItems => detailItems;

    public IReadOnlyList<Fleet> Fleets => fleets;

    public GalaxyMapPlanet Planet { get; private set; }

    public ISceneNode RenameTarget => renameTarget;

    public int RenameDetailItemIndex { get; private set; } = -1;

    public int RenameFleetRowIndex { get; private set; } = -1;

    public IReadOnlyCollection<int> SelectedDetailItems => selectedDetailItems;

    public int SelectedFleetIndex => selectedFleetIndexHint;

    public IReadOnlyCollection<int> SelectedFleetItems => selectedFleetItems;

    public Fleet SelectedFleet => selectedFleet;

    public UIWindow Window { get; }

    /// <summary>
    /// Rebinds the session to a refreshed projection of its planet.
    /// </summary>
    /// <param name="planet">The refreshed strategy planet.</param>
    public void RebindPlanet(GalaxyMapPlanet planet)
    {
        if (planet?.Planet == null)
            throw new ArgumentException(
                "A fleet session requires a projected planet.",
                nameof(planet)
            );

        Planet = planet;
        Reconcile();
    }

    /// <summary>
    /// Reconciles identity-backed interaction state with current domain collections.
    /// </summary>
    public void Reconcile()
    {
        RefreshFleets();
        ReconcileSelectedFleet();
        ReconcileSelection(selectedFleetNodes, selectedFleetItems, fleets);

        RefreshDetailItems();
        ReconcileSelection(selectedDetailNodes, selectedDetailItems, detailItems);
        SelectRequiredItems();

        contextFleet = ResolveNode(fleets, contextFleet);
        ContextFleetIndex = FindNodeIndex(fleets, contextFleet);
        contextDetailNode = ResolveNode(detailItems, contextDetailNode);
        ContextDetailItemIndex = FindNodeIndex(detailItems, contextDetailNode);
        ReconcileRenameTarget();
    }

    /// <summary>
    /// Selects the fleet and detail tab containing one scene node.
    /// </summary>
    /// <param name="target">The fleet or contained scene node.</param>
    /// <param name="tab">The detail tab containing the target.</param>
    /// <returns>True when the target belongs to the represented planet.</returns>
    public bool SelectTarget(ISceneNode target, FleetWindowTab tab)
    {
        if (target == null)
            return false;

        RefreshFleets();
        Fleet targetFleet = target as Fleet ?? target.GetParentOfType<Fleet>();
        int fleetIndex = FindNodeIndex(fleets, targetFleet);
        if (fleetIndex < 0)
            return false;

        ActiveTab = tab;
        SetSelectedFleet(fleets[fleetIndex], fleetIndex);
        selectedFleetNodes.Clear();
        selectedFleetItems.Clear();
        selectedDetailNodes.Clear();
        selectedDetailItems.Clear();

        RefreshDetailItems();
        ISceneNode selectedTarget = ResolveNode(detailItems, target);
        if (selectedTarget != null)
        {
            selectedDetailNodes.Add(selectedTarget);
            selectedDetailItems.Add(FindNodeIndex(detailItems, selectedTarget));
        }

        ClearContext();
        Reconcile();
        return true;
    }

    /// <summary>
    /// Clears item and context selections while preserving the displayed fleet.
    /// </summary>
    public void ClearItemSelection()
    {
        selectedFleetNodes.Clear();
        selectedFleetItems.Clear();
        selectedDetailNodes.Clear();
        selectedDetailItems.Clear();
        SelectRequiredItems();
        ClearContext();
    }

    /// <summary>
    /// Gets one fleet from the current ordered collection.
    /// </summary>
    /// <param name="fleetIndex">The current fleet-row index.</param>
    /// <param name="fleet">Receives the represented fleet.</param>
    /// <returns>True when the row exists.</returns>
    public bool TryGetFleet(int fleetIndex, out Fleet fleet)
    {
        if (IsValidIndex(fleetIndex, fleets.Count))
        {
            fleet = fleets[fleetIndex];
            return true;
        }

        fleet = null;
        return false;
    }

    /// <summary>
    /// Gets one detail item from the current ordered collection.
    /// </summary>
    /// <param name="itemIndex">The current detail-card index.</param>
    /// <param name="item">Receives the represented detail item.</param>
    /// <returns>True when the card exists.</returns>
    public bool TryGetDetailItem(int itemIndex, out ISceneNode item)
    {
        if (IsValidIndex(itemIndex, detailItems.Count))
        {
            item = detailItems[itemIndex];
            return true;
        }

        item = null;
        return false;
    }

    /// <summary>
    /// Captures one fleet or detail item as the current context target.
    /// </summary>
    /// <param name="target">The current fleet or detail item.</param>
    /// <returns>True when the target belongs to a current selectable collection.</returns>
    public bool CaptureContext(ISceneNode target)
    {
        if (target is Fleet)
        {
            int fleetIndex = FindNodeIndex(fleets, target);
            if (!TrySetFleetInteractionTarget(fleetIndex))
                return false;

            SelectContextItem(selectedFleetItems, fleetIndex);
            CaptureSelection(selectedFleetItems, fleets, selectedFleetNodes);
            selectedDetailNodes.Clear();
            selectedDetailItems.Clear();
            return true;
        }

        int itemIndex = FindNodeIndex(detailItems, target);
        if (!TrySetDetailInteractionTarget(itemIndex))
            return false;

        SelectContextItem(selectedDetailItems, itemIndex);
        CaptureSelection(selectedDetailItems, detailItems, selectedDetailNodes);
        return true;
    }

    /// <summary>
    /// Applies selection rules for the start of a fleet or detail drag gesture.
    /// </summary>
    /// <param name="target">The pressed fleet or detail item.</param>
    /// <returns>True when the existing selection can start dragging immediately.</returns>
    public bool PrepareDragSelection(ISceneNode target)
    {
        if (target is Fleet)
        {
            int fleetIndex = FindNodeIndex(fleets, target);
            if (!TrySetFleetInteractionTarget(fleetIndex))
                return false;

            bool canStartDrag = PrepareDragSelection(
                selectedFleetItems,
                fleetIndex,
                fleets,
                selectedFleetNodes
            );
            selectedDetailNodes.Clear();
            selectedDetailItems.Clear();
            SelectRequiredItems();
            return canStartDrag;
        }

        int itemIndex = FindNodeIndex(detailItems, target);
        if (!TrySetDetailInteractionTarget(itemIndex))
            return false;

        bool detailCanStartDrag = PrepareDragSelection(
            selectedDetailItems,
            itemIndex,
            detailItems,
            selectedDetailNodes
        );
        SelectRequiredItems();
        return detailCanStartDrag;
    }

    /// <summary>
    /// Applies final selection rules after a fleet or detail click release.
    /// </summary>
    /// <param name="target">The released fleet or detail item.</param>
    /// <returns>True when the target belongs to a current selectable collection.</returns>
    public bool SelectItem(ISceneNode target)
    {
        if (target is Fleet)
        {
            int fleetIndex = FindNodeIndex(fleets, target);
            if (!TrySetFleetInteractionTarget(fleetIndex))
                return false;

            SelectableListSelection.SelectIndexedItem(selectedFleetItems, fleetIndex, fleets.Count);
            CaptureSelection(selectedFleetItems, fleets, selectedFleetNodes);
            selectedDetailNodes.Clear();
            selectedDetailItems.Clear();
            SelectRequiredItems();
            return true;
        }

        int itemIndex = FindNodeIndex(detailItems, target);
        if (!TrySetDetailInteractionTarget(itemIndex))
            return false;

        SelectableListSelection.SelectIndexedItem(
            selectedDetailItems,
            itemIndex,
            detailItems.Count
        );
        CaptureSelection(selectedDetailItems, detailItems, selectedDetailNodes);
        SelectRequiredItems();
        return true;
    }

    /// <summary>
    /// Changes the detail tab and clears state owned by the previous tab.
    /// </summary>
    /// <param name="tab">The requested detail tab.</param>
    /// <returns>True when the active tab changed.</returns>
    public bool SelectTab(FleetWindowTab tab)
    {
        if (!FleetWindowRenderData.OrderedTabs.Contains(tab) || tab == ActiveTab)
            return false;

        ActiveTab = tab;
        selectedDetailNodes.Clear();
        selectedDetailItems.Clear();
        contextDetailNode = null;
        ContextDetailItemIndex = -1;
        renameTarget = null;
        RenameFleetRowIndex = -1;
        RenameDetailItemIndex = -1;
        RefreshDetailItems();
        SelectRequiredItems();
        return true;
    }

    /// <summary>
    /// Begins rename state for one current fleet or capital ship.
    /// </summary>
    /// <param name="target">The requested rename target.</param>
    /// <returns>True when the target can be represented in the current window state.</returns>
    public bool BeginRename(ISceneNode target)
    {
        if (target is not Fleet and not CapitalShip)
            return false;

        renameTarget = target;
        Reconcile();
        return renameTarget != null;
    }

    /// <summary>
    /// Clears the current rename target.
    /// </summary>
    public void EndRename()
    {
        renameTarget = null;
        RenameFleetRowIndex = -1;
        RenameDetailItemIndex = -1;
    }

    /// <summary>
    /// Clears current context targets without changing item selection.
    /// </summary>
    public void ClearContext()
    {
        contextFleet = null;
        ContextFleetIndex = -1;
        contextDetailNode = null;
        ContextDetailItemIndex = -1;
    }

    /// <summary>
    /// Resolves the current context selection to scene nodes in visual order.
    /// </summary>
    /// <returns>The current context items.</returns>
    public List<ISceneNode> GetContextItems()
    {
        Reconcile();
        if (ContextFleetIndex >= 0)
        {
            if (selectedFleetItems.Contains(ContextFleetIndex))
            {
                List<ISceneNode> selectedFleets = selectedFleetItems
                    .OrderBy(index => index)
                    .Select(index => (ISceneNode)fleets[index])
                    .ToList();
                if (selectedFleets.Count > 0)
                    return selectedFleets;
            }

            return new List<ISceneNode> { fleets[ContextFleetIndex] };
        }

        if (ContextDetailItemIndex < 0)
            return new List<ISceneNode>();

        if (selectedDetailItems.Contains(ContextDetailItemIndex))
        {
            List<ISceneNode> selectedItems = selectedDetailItems
                .OrderBy(index => index)
                .Select(index => detailItems[index])
                .ToList();
            if (selectedItems.Count > 0)
                return selectedItems;
        }

        return new List<ISceneNode> { detailItems[ContextDetailItemIndex] };
    }

    /// <summary>
    /// Reports whether the selected fleet has items for one detail tab.
    /// </summary>
    /// <param name="tab">The detail tab to inspect.</param>
    /// <returns>True when the selected fleet has matching items.</returns>
    public bool HasDetailItems(FleetWindowTab tab)
    {
        return tab switch
        {
            FleetWindowTab.CapitalShips => selectedFleet?.CapitalShips.Count > 0,
            FleetWindowTab.Starfighters => selectedFleet?.GetStarfighters().Any() == true,
            FleetWindowTab.Regiments => selectedFleet?.GetRegiments().Any() == true,
            FleetWindowTab.Personnel => selectedFleet?.GetOfficers().Any() == true
                || selectedFleet?.GetSpecialForces().Any() == true,
            _ => false,
        };
    }

    /// <summary>
    /// Reconciles the displayed fleet by stable scene-node identity.
    /// </summary>
    private void ReconcileSelectedFleet()
    {
        Fleet currentFleet = ResolveNode(fleets, selectedFleet);
        if (currentFleet == null && fleets.Count > 0)
        {
            int fallbackIndex =
                selectedFleetIndexHint < 0 ? 0 : Math.Min(selectedFleetIndexHint, fleets.Count - 1);
            currentFleet = fleets[fallbackIndex];
        }

        selectedFleet = currentFleet;
        selectedFleetIndexHint = FindNodeIndex(fleets, currentFleet);
    }

    /// <summary>
    /// Sets the displayed fleet and its current visual index.
    /// </summary>
    /// <param name="fleet">The displayed fleet.</param>
    /// <param name="fleetIndex">The fleet's current visual index.</param>
    private void SetSelectedFleet(Fleet fleet, int fleetIndex)
    {
        selectedFleet = fleet;
        selectedFleetIndexHint = fleetIndex;
    }

    /// <summary>
    /// Sets a fleet and context target from the current fleet collection.
    /// </summary>
    /// <param name="fleetIndex">The requested fleet-row index.</param>
    /// <returns>True when the row exists.</returns>
    private bool TrySetFleetInteractionTarget(int fleetIndex)
    {
        if (!IsValidIndex(fleetIndex, fleets.Count))
            return false;

        SetSelectedFleet(fleets[fleetIndex], fleetIndex);
        RefreshDetailItems();
        contextFleet = fleets[fleetIndex];
        ContextFleetIndex = fleetIndex;
        contextDetailNode = null;
        ContextDetailItemIndex = -1;
        return true;
    }

    /// <summary>
    /// Sets a context target from the current detail collection.
    /// </summary>
    /// <param name="itemIndex">The requested detail-card index.</param>
    /// <returns>True when the card exists.</returns>
    private bool TrySetDetailInteractionTarget(int itemIndex)
    {
        if (!IsValidIndex(itemIndex, detailItems.Count))
            return false;

        contextFleet = null;
        ContextFleetIndex = -1;
        contextDetailNode = detailItems[itemIndex];
        ContextDetailItemIndex = itemIndex;
        return true;
    }

    /// <summary>
    /// Reconciles the current rename target and its visual index.
    /// </summary>
    private void ReconcileRenameTarget()
    {
        renameTarget = renameTarget switch
        {
            Fleet fleet => ResolveNode(fleets, fleet),
            CapitalShip ship when ActiveTab == FleetWindowTab.CapitalShips => ResolveNode(
                detailItems,
                ship
            ),
            _ => null,
        };
        RenameFleetRowIndex = renameTarget is Fleet ? FindNodeIndex(fleets, renameTarget) : -1;
        RenameDetailItemIndex =
            renameTarget is CapitalShip ? FindNodeIndex(detailItems, renameTarget) : -1;
    }

    /// <summary>
    /// Refreshes the current ordered detail collection for the displayed fleet and tab.
    /// </summary>
    private void RefreshDetailItems()
    {
        detailItems.Clear();
        if (selectedFleet == null)
            return;

        switch (ActiveTab)
        {
            case FleetWindowTab.CapitalShips:
                detailItems.AddRange(selectedFleet.CapitalShips);
                break;
            case FleetWindowTab.Starfighters:
                detailItems.AddRange(selectedFleet.GetStarfighters());
                break;
            case FleetWindowTab.Regiments:
                detailItems.AddRange(selectedFleet.GetRegiments());
                break;
            case FleetWindowTab.Personnel:
                detailItems.AddRange(selectedFleet.GetOfficers());
                detailItems.AddRange(selectedFleet.GetSpecialForces());
                break;
        }
    }

    /// <summary>
    /// Refreshes the current ordered fleet collection from the represented planet.
    /// </summary>
    private void RefreshFleets()
    {
        fleets.Clear();
        if (Planet?.Planet?.Fleets != null)
            fleets.AddRange(Planet.Planet.Fleets);
    }

    /// <summary>
    /// Restores the required fleet and detail selections after an interaction or refresh.
    /// </summary>
    private void SelectRequiredItems()
    {
        if (selectedFleet != null && selectedFleetItems.Count == 0)
        {
            selectedFleetItems.Add(selectedFleetIndexHint);
            selectedFleetNodes.Add(selectedFleet);
        }

        if (detailItems.Count == 0 || selectedDetailItems.Count != 0)
            return;

        selectedDetailItems.Add(0);
        selectedDetailNodes.Add(detailItems[0]);
    }

    /// <summary>
    /// Replaces one visual selection with the matching nodes from a current collection.
    /// </summary>
    /// <typeparam name="T">The scene-node type.</typeparam>
    /// <param name="selectedNodes">The identity-backed selection.</param>
    /// <param name="selectedIndexes">The current visual indices.</param>
    /// <param name="items">The current ordered items.</param>
    private static void ReconcileSelection<T>(
        HashSet<ISceneNode> selectedNodes,
        HashSet<int> selectedIndexes,
        IReadOnlyList<T> items
    )
        where T : class, ISceneNode
    {
        List<ISceneNode> currentNodes = new List<ISceneNode>();
        selectedIndexes.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            if (!selectedNodes.Any(selected => HasSameIdentity(selected, items[i])))
                continue;

            currentNodes.Add(items[i]);
            selectedIndexes.Add(i);
        }

        selectedNodes.Clear();
        selectedNodes.UnionWith(currentNodes);
    }

    /// <summary>
    /// Captures an index selection as current scene-node identities.
    /// </summary>
    /// <typeparam name="T">The scene-node type.</typeparam>
    /// <param name="selectedIndexes">The selected visual indices.</param>
    /// <param name="items">The current ordered items.</param>
    /// <param name="selectedNodes">The identity-backed destination selection.</param>
    private static void CaptureSelection<T>(
        IReadOnlyCollection<int> selectedIndexes,
        IReadOnlyList<T> items,
        HashSet<ISceneNode> selectedNodes
    )
        where T : class, ISceneNode
    {
        selectedNodes.Clear();
        foreach (int index in selectedIndexes)
        {
            if (IsValidIndex(index, items.Count))
                selectedNodes.Add(items[index]);
        }
    }

    /// <summary>
    /// Applies drag-selection rules to one indexed scene-node collection.
    /// </summary>
    /// <typeparam name="T">The scene-node type.</typeparam>
    /// <param name="selectedIndexes">The selected visual indices.</param>
    /// <param name="pressedIndex">The pressed visual index.</param>
    /// <param name="items">The current ordered items.</param>
    /// <param name="selectedNodes">The identity-backed selection.</param>
    /// <returns>True when the pressed item already belonged to the draggable selection.</returns>
    private static bool PrepareDragSelection<T>(
        HashSet<int> selectedIndexes,
        int pressedIndex,
        IReadOnlyList<T> items,
        HashSet<ISceneNode> selectedNodes
    )
        where T : class, ISceneNode
    {
        bool canStartDrag = SelectableListSelection.CanDragExistingSelection(
            selectedIndexes,
            pressedIndex
        );
        SelectableListSelection.SelectIndexedItemForDrag(
            selectedIndexes,
            pressedIndex,
            items.Count
        );
        CaptureSelection(selectedIndexes, items, selectedNodes);
        return canStartDrag;
    }

    /// <summary>
    /// Selects a context index unless it already belongs to the current selection.
    /// </summary>
    /// <param name="selection">The selected visual indices.</param>
    /// <param name="index">The context index.</param>
    private static void SelectContextItem(HashSet<int> selection, int index)
    {
        if (selection.Contains(index))
            return;

        selection.Clear();
        selection.Add(index);
    }

    /// <summary>
    /// Resolves a previously held node against a current ordered collection.
    /// </summary>
    /// <typeparam name="T">The scene-node type.</typeparam>
    /// <param name="items">The current ordered items.</param>
    /// <param name="target">The previously held target.</param>
    /// <returns>The matching current node, or null.</returns>
    private static T ResolveNode<T>(IReadOnlyList<T> items, ISceneNode target)
        where T : class, ISceneNode
    {
        int index = FindNodeIndex(items, target);
        return index >= 0 ? items[index] : null;
    }

    /// <summary>
    /// Finds one node's current visual index by stable identity.
    /// </summary>
    /// <typeparam name="T">The scene-node type.</typeparam>
    /// <param name="items">The current ordered items.</param>
    /// <param name="target">The target node.</param>
    /// <returns>The current visual index, or -1.</returns>
    private static int FindNodeIndex<T>(IReadOnlyList<T> items, ISceneNode target)
        where T : class, ISceneNode
    {
        if (target == null)
            return -1;

        for (int i = 0; i < items.Count; i++)
        {
            if (HasSameIdentity(items[i], target))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Reports whether two scene nodes represent the same stable entity.
    /// </summary>
    /// <param name="left">The first node.</param>
    /// <param name="right">The second node.</param>
    /// <returns>True when the references or non-empty instance identifiers match.</returns>
    private static bool HasSameIdentity(ISceneNode left, ISceneNode right)
    {
        return ReferenceEquals(left, right)
            || left != null
                && right != null
                && !string.IsNullOrEmpty(left.InstanceID)
                && left.InstanceID == right.InstanceID;
    }

    /// <summary>
    /// Reports whether one index belongs to a current collection.
    /// </summary>
    /// <param name="index">The candidate index.</param>
    /// <param name="count">The current item count.</param>
    /// <returns>True when the index is in range.</returns>
    private static bool IsValidIndex(int index, int count)
    {
        return index >= 0 && index < count;
    }
}
