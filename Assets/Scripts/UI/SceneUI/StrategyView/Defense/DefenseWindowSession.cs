using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Owns the ordered domain snapshot and mutable interaction state for one Defense window.
/// </summary>
internal sealed class DefenseWindowSession
{
    private readonly Dictionary<DefenseWindowTab, List<ISceneNode>> itemsByTab =
        new Dictionary<DefenseWindowTab, List<ISceneNode>>();
    private readonly HashSet<ISceneNode> selectedNodes = new HashSet<ISceneNode>();
    private readonly HashSet<int> selectedIndexes = new HashSet<int>();
    private ISceneNode contextItem;
    private ISceneNode dragItem;

    /// <summary>
    /// Creates a Defense-window session for one planet and owning window.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="window">The owning window shell.</param>
    public DefenseWindowSession(GalaxyMapPlanet planet, UIWindow window)
    {
        Planet = planet ?? throw new ArgumentNullException(nameof(planet));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Reconcile();
    }

    public DefenseWindowTab ActiveTab { get; private set; } = DefenseWindowTab.Personnel;

    public int ContextItemIndex { get; private set; } = -1;

    public int DragItemIndex { get; private set; } = -1;

    public GalaxyMapPlanet Planet { get; private set; }

    public IReadOnlyCollection<int> SelectedItemIndexes => selectedIndexes;

    public UIWindow Window { get; }

    /// <summary>
    /// Rebinds the session to the refreshed projection of its represented planet.
    /// </summary>
    /// <param name="planet">The refreshed represented planet.</param>
    public void RebindPlanet(GalaxyMapPlanet planet)
    {
        Planet = planet ?? throw new ArgumentNullException(nameof(planet));
        Reconcile();
    }

    /// <summary>
    /// Reconciles the ordered tab snapshot and interaction targets by scene-node identity.
    /// </summary>
    public void Reconcile()
    {
        foreach (DefenseWindowTab tab in DefenseWindowRenderData.OrderedTabs)
            itemsByTab[tab] = GetPlanetItems(Planet?.Planet, tab);

        IReadOnlyList<ISceneNode> activeItems = GetItems(ActiveTab);
        ReconcileSelection(activeItems);
        contextItem = ResolveNode(activeItems, contextItem);
        ContextItemIndex = FindNodeIndex(activeItems, contextItem);
        dragItem = ResolveNode(activeItems, dragItem);
        DragItemIndex = FindNodeIndex(activeItems, dragItem);
    }

    /// <summary>
    /// Gets the ordered current items for one Defense tab.
    /// </summary>
    /// <param name="tab">The requested Defense tab.</param>
    /// <returns>The session-owned ordered items.</returns>
    public IReadOnlyList<ISceneNode> GetItems(DefenseWindowTab tab)
    {
        return itemsByTab.TryGetValue(tab, out List<ISceneNode> items)
            ? items
            : Array.Empty<ISceneNode>();
    }

    /// <summary>
    /// Gets the selected scene nodes in current visual order.
    /// </summary>
    /// <returns>The selected scene nodes.</returns>
    public List<ISceneNode> GetSelectedItems()
    {
        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        return selectedIndexes
            .Where(index => IsValidIndex(index, items.Count))
            .OrderBy(index => index)
            .Select(index => items[index])
            .ToList();
    }

    /// <summary>
    /// Gets one active-tab item by its current visual index.
    /// </summary>
    /// <param name="itemIndex">The requested visual index.</param>
    /// <param name="item">Receives the current scene node.</param>
    /// <returns>True when the index belongs to the active snapshot.</returns>
    public bool TryGetItem(int itemIndex, out ISceneNode item)
    {
        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        if (IsValidIndex(itemIndex, items.Count))
        {
            item = items[itemIndex];
            return true;
        }

        item = null;
        return false;
    }

    /// <summary>
    /// Selects the Defense tab requested by the user.
    /// </summary>
    /// <param name="tab">The requested Defense tab.</param>
    /// <returns>True when the active tab changed.</returns>
    public bool SelectTab(DefenseWindowTab tab)
    {
        if (!DefenseWindowRenderData.OrderedTabs.Contains(tab) || tab == ActiveTab)
            return false;

        ActiveTab = tab;
        ClearSelection();
        return true;
    }

    /// <summary>
    /// Selects one current item on a requested Defense tab.
    /// </summary>
    /// <param name="tab">The tab containing the item.</param>
    /// <param name="itemIndex">The item's current visual index.</param>
    /// <returns>True when the item exists.</returns>
    public bool SelectSingleItem(DefenseWindowTab tab, int itemIndex)
    {
        SelectTab(tab);
        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        if (!IsValidIndex(itemIndex, items.Count))
            return false;

        selectedIndexes.Clear();
        selectedIndexes.Add(itemIndex);
        CaptureSelection(items);
        ClearTransientTargets();
        return true;
    }

    /// <summary>
    /// Clears item selection and transient pointer targets.
    /// </summary>
    public void ClearSelection()
    {
        selectedNodes.Clear();
        selectedIndexes.Clear();
        ClearTransientTargets();
    }

    /// <summary>
    /// Captures a current context item and selects it when needed.
    /// </summary>
    /// <param name="itemIndex">The context-targeted visual index.</param>
    /// <returns>True when the item exists.</returns>
    public bool CaptureContextItem(int itemIndex)
    {
        if (!TryGetItem(itemIndex, out contextItem))
        {
            ClearContextItem();
            return false;
        }

        ContextItemIndex = itemIndex;
        SelectContextItem(itemIndex);
        return true;
    }

    /// <summary>
    /// Clears the current context target.
    /// </summary>
    public void ClearContextItem()
    {
        contextItem = null;
        ContextItemIndex = -1;
    }

    /// <summary>
    /// Prepares pointer selection for one current item and clears the previous drag target.
    /// </summary>
    /// <param name="itemIndex">The pressed visual index.</param>
    /// <returns>True when the item exists.</returns>
    public bool PrepareItemSelection(int itemIndex)
    {
        if (!TryGetItem(itemIndex, out contextItem))
            return false;

        ContextItemIndex = itemIndex;
        dragItem = null;
        DragItemIndex = -1;
        return true;
    }

    /// <summary>
    /// Selects a context item without disturbing a selection that already contains it.
    /// </summary>
    /// <param name="itemIndex">The context-targeted visual index.</param>
    public void SelectContextItem(int itemIndex)
    {
        if (selectedIndexes.Contains(itemIndex))
            return;

        selectedIndexes.Clear();
        selectedIndexes.Add(itemIndex);
        CaptureSelection(GetItems(ActiveTab));
    }

    /// <summary>
    /// Applies the drag-selection gesture for one current item.
    /// </summary>
    /// <param name="itemIndex">The pressed visual index.</param>
    /// <param name="columnCount">The number of authored item columns.</param>
    public void SelectItemForDrag(int itemIndex, int columnCount)
    {
        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        SelectableListSelection.SelectIndexedItemForDrag(
            selectedIndexes,
            itemIndex,
            items.Count,
            columnCount
        );
        CaptureSelection(items);
    }

    /// <summary>
    /// Records the current item supplying the active drag preview.
    /// </summary>
    /// <param name="itemIndex">The drag-source visual index.</param>
    public void BeginDrag(int itemIndex)
    {
        if (!TryGetItem(itemIndex, out dragItem))
            return;

        DragItemIndex = itemIndex;
    }

    /// <summary>
    /// Applies the release-selection gesture for one current item.
    /// </summary>
    /// <param name="itemIndex">The released visual index.</param>
    /// <param name="columnCount">The number of authored item columns.</param>
    public void SelectItem(int itemIndex, int columnCount)
    {
        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        SelectableListSelection.SelectIndexedItem(
            selectedIndexes,
            itemIndex,
            items.Count,
            columnCount
        );
        CaptureSelection(items);
    }

    /// <summary>
    /// Reports whether every selected current item can participate in a move drag.
    /// </summary>
    /// <returns>True when the selection is non-empty and movable.</returns>
    public bool CanDragSelectedItems()
    {
        if (selectedIndexes.Count == 0)
            return false;

        IReadOnlyList<ISceneNode> items = GetItems(ActiveTab);
        return selectedIndexes.All(index =>
            IsValidIndex(index, items.Count) && CanDragItem(items[index])
        );
    }

    /// <summary>
    /// Reports whether one scene node can initiate a Defense-window move drag.
    /// </summary>
    /// <param name="item">The scene node to inspect.</param>
    /// <returns>True when the node is a movable non-building unit.</returns>
    public static bool CanDragItem(ISceneNode item)
    {
        return item is IMovable movable && item is not Building && movable.IsMovable();
    }

    /// <summary>
    /// Returns the ordered source items for one Defense tab.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="tab">The requested Defense tab.</param>
    /// <returns>The ordered tab items.</returns>
    internal static List<ISceneNode> GetPlanetItems(Planet planet, DefenseWindowTab tab)
    {
        if (planet == null)
            return new List<ISceneNode>();

        return tab switch
        {
            DefenseWindowTab.Personnel => planet
                .Officers.Cast<ISceneNode>()
                .Concat(planet.SpecialForces.Cast<ISceneNode>())
                .ToList(),
            DefenseWindowTab.Regiments => planet.Regiments.Cast<ISceneNode>().ToList(),
            DefenseWindowTab.Starfighters => planet.Starfighters.Cast<ISceneNode>().ToList(),
            DefenseWindowTab.Shields => GetBuildings(planet, true),
            DefenseWindowTab.Batteries => GetBuildings(planet, false),
            _ => new List<ISceneNode>(),
        };
    }

    /// <summary>
    /// Captures the current index selection as identity-backed scene nodes.
    /// </summary>
    /// <param name="items">The current ordered items.</param>
    private void CaptureSelection(IReadOnlyList<ISceneNode> items)
    {
        selectedNodes.Clear();
        foreach (int index in selectedIndexes)
        {
            if (IsValidIndex(index, items.Count))
                selectedNodes.Add(items[index]);
        }
    }

    /// <summary>
    /// Reconciles identity-backed selection against the current ordered items.
    /// </summary>
    /// <param name="items">The current ordered items.</param>
    private void ReconcileSelection(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> currentNodes = new List<ISceneNode>();
        selectedIndexes.Clear();
        for (int index = 0; index < items.Count; index++)
        {
            if (!selectedNodes.Any(selected => HasSameIdentity(selected, items[index])))
                continue;

            currentNodes.Add(items[index]);
            selectedIndexes.Add(index);
        }

        selectedNodes.Clear();
        selectedNodes.UnionWith(currentNodes);
    }

    /// <summary>
    /// Clears context and drag targets without changing selection.
    /// </summary>
    private void ClearTransientTargets()
    {
        contextItem = null;
        ContextItemIndex = -1;
        dragItem = null;
        DragItemIndex = -1;
    }

    /// <summary>
    /// Gets shield or battery buildings represented by a Defense window.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="shields">Whether shield facilities are requested.</param>
    /// <returns>The ordered matching building scene nodes.</returns>
    private static List<ISceneNode> GetBuildings(Planet planet, bool shields)
    {
        return planet
            .Buildings.Where(building =>
                shields
                    ? building.DefenseFacilityClass
                        is DefenseFacilityClass.Shield
                            or DefenseFacilityClass.DeathStarShield
                    : building.DefenseFacilityClass
                        is DefenseFacilityClass.KDY
                            or DefenseFacilityClass.LNR
            )
            .Cast<ISceneNode>()
            .ToList();
    }

    /// <summary>
    /// Resolves a previously held node against a current ordered collection.
    /// </summary>
    /// <param name="items">The current ordered items.</param>
    /// <param name="target">The previously held target.</param>
    /// <returns>The matching current node, or null.</returns>
    private static ISceneNode ResolveNode(IReadOnlyList<ISceneNode> items, ISceneNode target)
    {
        int index = FindNodeIndex(items, target);
        return index >= 0 ? items[index] : null;
    }

    /// <summary>
    /// Finds one node's current visual index by stable identity.
    /// </summary>
    /// <param name="items">The current ordered items.</param>
    /// <param name="target">The target node.</param>
    /// <returns>The current visual index, or negative one.</returns>
    private static int FindNodeIndex(IReadOnlyList<ISceneNode> items, ISceneNode target)
    {
        if (target == null)
            return -1;

        for (int index = 0; index < items.Count; index++)
        {
            if (HasSameIdentity(items[index], target))
                return index;
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
                && string.Equals(left.InstanceID, right.InstanceID, StringComparison.Ordinal);
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
