using UnityEngine;

public interface IFinderWindowNavigationActions
{
    Vector2Int GetSystemSourcePosition(GalaxyMapSector sector);
    bool OpenSectorWindow(GalaxyMapSector sector, int x, int y);
    UIWindow OpenPlanetWindowAt(GalaxyMapPlanet planet, PlanetIcon icon, int x, int y);
    void CloseWindow(UIWindow window);
    bool TryGetWindowView<TView>(UIWindow window, out TView view)
        where TView : class;
}

public sealed class FinderWindowNavigationController
{
    private readonly IFinderWindowNavigationActions actions;

    public FinderWindowNavigationController(IFinderWindowNavigationActions actions)
    {
        this.actions = actions;
    }

    public void OpenSelectedFinderItem(UIWindow window, FinderWindowView view)
    {
        if (view == null)
            return;

        OpenFinderRow(window, view, view.GetSelectedSourceRow());
    }

    private void OpenFinderRow(
        UIWindow sourceWindow,
        FinderWindowView sourceView,
        FinderWindowRow row
    )
    {
        if (row?.Planet == null)
            return;

        Vector2Int sectorPosition = actions.GetSystemSourcePosition(row.Planet.Sector);
        int sectorX = sectorPosition.x;
        int sectorY = sectorPosition.y;
        actions.OpenSectorWindow(row.Planet.Sector, sectorX, sectorY);

        if (row.TargetIcon == PlanetIcon.Fleet)
        {
            UIWindow fleetWindow = actions.OpenPlanetWindowAt(
                row.Planet,
                PlanetIcon.Fleet,
                sectorX,
                sectorY
            );
            SelectFinderFleetTarget(fleetWindow, row);
        }
        else if (row.TargetIcon == PlanetIcon.Defense)
        {
            UIWindow defenseWindow = actions.OpenPlanetWindowAt(
                row.Planet,
                PlanetIcon.Defense,
                sectorX,
                sectorY
            );
            if (defenseWindow == null)
                return;

            if (actions.TryGetWindowView(defenseWindow, out DefenseWindowView defenseView))
            {
                if (sourceView.Mode == FinderMode.Troops)
                    defenseView.SelectFinderTab(1);
                else if (sourceView.Mode == FinderMode.Personnel)
                    defenseView.SelectFinderTab(0);
            }
        }

        actions.CloseWindow(sourceWindow);
    }

    private void SelectFinderFleetTarget(UIWindow fleetWindow, FinderWindowRow row)
    {
        if (!actions.TryGetWindowView(fleetWindow, out FleetWindowView fleetView))
            return;

        fleetView.SelectFinderTarget(row);
    }
}
