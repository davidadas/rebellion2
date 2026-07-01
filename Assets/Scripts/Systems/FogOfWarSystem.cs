using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages fog of war operations: capturing snapshots, invalidating moved entities, building faction views.
    /// Operates on faction state, does not hold its own state.
    /// </summary>
    public class FogOfWarSystem
    {
        private readonly GameRoot _game;
        private readonly FogOfWarRecorder _recorder;

        /// <summary>
        /// Creates a FogOfWarSystem for the given game instance.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public FogOfWarSystem(GameRoot game)
        {
            _game = game;
            _recorder = new FogOfWarRecorder();
        }

        /// <summary>
        /// Captures a snapshot of a planet for a faction.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The planet being observed.</param>
        /// <param name="system">The system containing the planet.</param>
        /// <param name="currentTick">The tick when the snapshot is captured.</param>
        public void CaptureSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            _recorder.RecordPlanetSnapshot(faction, planet, system, currentTick);
        }

        public void RemoveEntityFromSnapshots(Faction faction, string entityId)
        {
            _recorder.RemoveEntityFromSnapshots(faction, entityId);
        }

        /// <summary>
        /// Determines if a faction currently has real-time visibility of a planet.
        /// </summary>
        /// <param name="planet">The planet to check visibility for.</param>
        /// <param name="faction">The faction whose visibility to evaluate.</param>
        /// <returns>True if the faction owns the planet or has an arrived fleet with ships present.</returns>
        public bool IsPlanetVisible(Planet planet, Faction faction)
        {
            if (planet.OwnerInstanceID == faction.InstanceID)
                return true;

            if (
                planet.Fleets.Any(f =>
                    f.OwnerInstanceID == faction.InstanceID
                    && f.Movement == null
                    && f.CapitalShips.Count > 0
                )
            )
                return true;

            return false;
        }

        /// <summary>
        /// Builds a faction-specific galaxy view.
        /// Creates new systems and planets. Owned visible entities remain live references;
        /// hidden and snapshotted entities are copied for display.
        /// </summary>
        /// <param name="faction">The faction to build a view for.</param>
        /// <returns>A galaxy map filtered by the faction's fog of war state.</returns>
        public GalaxyMap BuildFactionView(Faction faction)
        {
            GalaxyMap factionView = new GalaxyMap();

            foreach (PlanetSystem masterSystem in _game.Galaxy.PlanetSystems)
            {
                PlanetSystem viewSystem = masterSystem.GetShallowCopy(CloneMode.Full);
                viewSystem.Planets = new List<Planet>();

                faction.Fog.Snapshots.TryGetValue(
                    masterSystem.InstanceID,
                    out SystemSnapshot systemSnapshot
                );

                foreach (Planet masterPlanet in masterSystem.Planets)
                {
                    PlanetSnapshot planetSnapshot = null;
                    systemSnapshot?.Planets.TryGetValue(
                        masterPlanet.InstanceID,
                        out planetSnapshot
                    );

                    Planet viewPlanet;
                    if (IsPlanetVisible(masterPlanet, faction))
                    {
                        viewPlanet = BlankPlanetView(masterPlanet);
                        ApplyRealTimeView(viewPlanet, masterPlanet, faction, planetSnapshot);
                    }
                    else if (planetSnapshot != null)
                    {
                        viewPlanet = BlankPlanetView(masterPlanet);
                        ApplySnapshotView(
                            viewPlanet,
                            masterPlanet,
                            masterSystem,
                            faction,
                            planetSnapshot
                        );
                    }
                    else
                    {
                        viewPlanet = UnexploredPlanetView(masterPlanet, faction);
                    }

                    MergeOwnLiveUnits(viewPlanet, masterPlanet, faction);

                    viewPlanet.Missions.AddRange(
                        masterPlanet
                            .Missions.Where(m => m.GetOwnerInstanceID() == faction.InstanceID)
                            .Select(m => m.GetShallowCopy(CloneMode.Full))
                    );

                    viewSystem.Planets.Add(viewPlanet);
                }

                factionView.PlanetSystems.Add(viewSystem);
            }

            return factionView;
        }

        /// <summary>
        /// Copies an officer for use in a faction view or snapshot.
        /// </summary>
        /// <param name="officer">The officer to copy.</param>
        /// <returns>The copied officer.</returns>
        private static Officer CopyOfficerForSnapshot(Officer officer)
        {
            Officer copy = officer.GetShallowCopy(CloneMode.Full);
            copy.Ratings = new Dictionary<OfficerRating, int>(officer.Ratings);
            return copy;
        }

        /// <summary>
        /// Returns whether a scene node is owned by a faction.
        /// </summary>
        /// <param name="unit">The scene node to inspect.</param>
        /// <param name="faction">The faction to compare against.</param>
        /// <returns>True if the scene node owner matches the faction.</returns>
        private static bool IsOwnedBy(ISceneNode unit, Faction faction)
        {
            return unit.GetOwnerInstanceID() == faction.InstanceID;
        }

        /// <summary>
        /// Adds live friendly units to a planet view without duplicating existing entries.
        /// </summary>
        /// <param name="viewPlanet">The planet view being populated.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        private static void MergeOwnLiveUnits(
            Planet viewPlanet,
            Planet masterPlanet,
            Faction faction
        )
        {
            string factionId = faction.InstanceID;

            foreach (Fleet fleet in masterPlanet.Fleets)
                if (
                    fleet.OwnerInstanceID == factionId
                    && fleet.CapitalShips.Count > 0
                    && viewPlanet.Fleets.All(f => f.InstanceID != fleet.InstanceID)
                )
                    viewPlanet.Fleets.Add(fleet);

            foreach (Regiment regiment in masterPlanet.Regiments)
                if (
                    regiment.OwnerInstanceID == factionId
                    && viewPlanet.Regiments.All(r => r.InstanceID != regiment.InstanceID)
                )
                    viewPlanet.Regiments.Add(regiment);

            foreach (Starfighter starfighter in masterPlanet.Starfighters)
                if (
                    starfighter.OwnerInstanceID == factionId
                    && viewPlanet.Starfighters.All(s => s.InstanceID != starfighter.InstanceID)
                )
                    viewPlanet.Starfighters.Add(starfighter);

            foreach (Officer officer in masterPlanet.Officers)
                if (
                    officer.OwnerInstanceID == factionId
                    && !officer.IsCaptured
                    && viewPlanet.Officers.All(o => o.InstanceID != officer.InstanceID)
                )
                    viewPlanet.Officers.Add(officer);
        }

        /// <summary>
        /// Selects the visible representation for a unit in a faction view.
        /// </summary>
        /// <typeparam name="T">The scene node type being viewed.</typeparam>
        /// <param name="unit">The source unit.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <returns>The live unit for owned nodes, otherwise a copied view.</returns>
        private static T ViewUnit<T>(T unit, Faction faction)
            where T : class, ISceneNode
        {
            return IsOwnedBy(unit, faction) ? unit : unit.GetShallowCopy(CloneMode.Full);
        }

        /// <summary>
        /// Creates a planet view shell with all entity lists cleared, ready to be populated
        /// by one of the three visibility branches.
        /// </summary>
        /// <param name="masterPlanet">The source planet to copy structure from.</param>
        /// <returns>A blank planet view with empty entity lists.</returns>
        private Planet BlankPlanetView(Planet masterPlanet)
        {
            Planet viewPlanet = masterPlanet.GetShallowCopy(CloneMode.Full);
            viewPlanet.Officers = new List<Officer>();
            viewPlanet.Fleets = new List<Fleet>();
            viewPlanet.Regiments = new List<Regiment>();
            viewPlanet.Buildings = new List<Building>();
            viewPlanet.Starfighters = new List<Starfighter>();
            viewPlanet.Missions = new List<Mission>();
            viewPlanet.PopularSupport = new Dictionary<string, int>();
            return viewPlanet;
        }

        /// <summary>
        /// Populates a view planet from live master state. The faction has direct visibility
        /// (owns the planet or has a fleet present). Also merges any previously-snapshotted
        /// enemy fleets that are not currently present in the live data.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <param name="planetSnapshot">The prior snapshot for the planet, if any.</param>
        private void ApplyRealTimeView(
            Planet viewPlanet,
            Planet masterPlanet,
            Faction faction,
            PlanetSnapshot planetSnapshot
        )
        {
            viewPlanet.OwnerInstanceID = masterPlanet.OwnerInstanceID;
            viewPlanet.PopularSupport = new Dictionary<string, int>(masterPlanet.PopularSupport);
            viewPlanet.NumRawResourceNodes = masterPlanet.NumRawResourceNodes;

            viewPlanet.Officers.AddRange(
                masterPlanet.Officers.Select(officer =>
                    IsOwnedBy(officer, faction) && !officer.IsCaptured
                        ? officer
                        : CopyOfficerForSnapshot(officer)
                )
            );
            viewPlanet.Fleets.AddRange(
                masterPlanet
                    .Fleets.Where(f =>
                        f.CapitalShips.Count > 0
                        && (f.Movement == null || f.OwnerInstanceID == faction.InstanceID)
                    )
                    .Select(f => ViewUnit(f, faction))
            );
            viewPlanet.Regiments.AddRange(masterPlanet.Regiments.Select(r => ViewUnit(r, faction)));
            viewPlanet.Starfighters.AddRange(
                masterPlanet.Starfighters.Select(s => ViewUnit(s, faction))
            );

            viewPlanet.Buildings.AddRange(masterPlanet.Buildings.Select(b => ViewUnit(b, faction)));

            if (planetSnapshot != null)
            {
                HashSet<string> liveFleetIDs = new HashSet<string>(
                    viewPlanet.Fleets.Select(f => f.InstanceID)
                );
                viewPlanet.Fleets.AddRange(
                    planetSnapshot
                        .Fleets.Where(f =>
                            f.GetOwnerInstanceID() != faction.InstanceID
                            && !liveFleetIDs.Contains(f.InstanceID)
                        )
                        .Select(f => f.GetShallowCopy(CloneMode.Full))
                );
            }
        }

        /// <summary>
        /// Populates a view planet from the last known snapshot. The faction has no current
        /// visibility but has previously observed this planet. Core system popular support
        /// is always shown regardless of fog state. Captured friendly officers are always live.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="masterSystem">The authoritative system data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <param name="planetSnapshot">The prior snapshot for the planet.</param>
        private void ApplySnapshotView(
            Planet viewPlanet,
            Planet masterPlanet,
            PlanetSystem masterSystem,
            Faction faction,
            PlanetSnapshot planetSnapshot
        )
        {
            viewPlanet.OwnerInstanceID = planetSnapshot.OwnerInstanceID;
            viewPlanet.NumRawResourceNodes = 0;

            viewPlanet.PopularSupport =
                masterSystem.SystemType == PlanetSystemType.CoreSystem
                    ? new Dictionary<string, int>(masterPlanet.PopularSupport)
                    : new Dictionary<string, int>();

            viewPlanet.Officers.AddRange(planetSnapshot.Officers.Select(CopyOfficerForSnapshot));
            viewPlanet.Officers.AddRange(
                masterPlanet
                    .Officers.Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(CopyOfficerForSnapshot)
            );
            viewPlanet.Fleets.AddRange(
                planetSnapshot.Fleets.Select(f => f.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.Regiments.AddRange(
                planetSnapshot.Regiments.Select(r => r.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.Starfighters.AddRange(
                planetSnapshot.Starfighters.Select(s => s.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.Buildings.AddRange(
                planetSnapshot.Buildings.Select(b => b.GetShallowCopy(CloneMode.Full))
            );
        }

        /// <summary>
        /// Creates a view planet for a completely unexplored location. No ownership or
        /// entity data is surfaced. Captured friendly officers remain visible.
        /// </summary>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        /// <returns>A planet view containing only the data visible for an unexplored planet.</returns>
        private Planet UnexploredPlanetView(Planet masterPlanet, Faction faction)
        {
            Planet viewPlanet = new Planet
            {
                InstanceID = masterPlanet.InstanceID,
                DisplayName = masterPlanet.DisplayName,
                SystemDataId = masterPlanet.SystemDataId,
                PositionX = masterPlanet.PositionX,
                PositionY = masterPlanet.PositionY,
                PlanetIconPath = masterPlanet.PlanetIconPath,
                IsUnexploredView = true,
            };

            viewPlanet.Officers.AddRange(
                masterPlanet
                    .Officers.Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(CopyOfficerForSnapshot)
            );

            return viewPlanet;
        }
    }
}
