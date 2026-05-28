using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
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

        /// <summary>
        /// Captures a planet snapshot for every faction.
        /// </summary>
        /// <param name="planet">The planet being observed.</param>
        /// <param name="currentTick">The tick when the snapshot is captured.</param>
        public void CapturePlanetSnapshotForAllFactions(Planet planet, int currentTick)
        {
            if (planet == null)
                return;

            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (system == null)
                return;

            foreach (Faction faction in _game.Factions)
                CaptureSnapshot(faction, planet, system, currentTick);
        }

        /// <summary>
        /// Determines if a faction currently has real-time visibility of a planet.
        /// </summary>
        /// <param name="planet">The planet to check visibility for.</param>
        /// <param name="faction">The faction whose visibility to evaluate.</param>
        /// <returns>True if the faction owns the planet or has a fleet present.</returns>
        public bool IsPlanetVisible(Planet planet, Faction faction)
        {
            if (planet.OwnerInstanceID == faction.InstanceID)
                return true;

            if (planet.Fleets.Any(f => f.OwnerInstanceID == faction.InstanceID))
                return true;

            return false;
        }

        /// <summary>
        /// Builds a faction-specific galaxy view.
        /// Creates new structure (systems/planets) with shallow-copied entities.
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
                    Planet viewPlanet = BlankPlanetView(masterPlanet);

                    PlanetSnapshot planetSnapshot = null;
                    systemSnapshot?.Planets.TryGetValue(
                        masterPlanet.InstanceID,
                        out planetSnapshot
                    );

                    if (IsPlanetVisible(masterPlanet, faction))
                        ApplyRealTimeView(viewPlanet, masterPlanet, faction, planetSnapshot);
                    else if (planetSnapshot != null)
                        ApplySnapshotView(
                            viewPlanet,
                            masterPlanet,
                            masterSystem,
                            faction,
                            planetSnapshot
                        );
                    else
                        ApplyUnexploredView(viewPlanet, masterPlanet, faction);

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

        private static Officer CopyOfficerForSnapshot(Officer officer)
        {
            Officer copy = officer.GetShallowCopy(CloneMode.Full);
            copy.Skills = new Dictionary<MissionParticipantSkill, int>(officer.Skills);
            return copy;
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
            viewPlanet.CapitalShips = new List<CapitalShip>();
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

            viewPlanet.Officers.AddRange(masterPlanet.Officers.Select(CopyOfficerForSnapshot));
            viewPlanet.Fleets.AddRange(
                masterPlanet
                    .Fleets.Where(f =>
                        f.CapitalShips.Count > 0
                        && (f.Movement == null || f.OwnerInstanceID == faction.InstanceID)
                    )
                    .Select(f => f.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.CapitalShips.AddRange(
                masterPlanet.CapitalShips.Select(c => c.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.Regiments.AddRange(
                masterPlanet.Regiments.Select(r => r.GetShallowCopy(CloneMode.Full))
            );
            viewPlanet.Starfighters.AddRange(
                masterPlanet.Starfighters.Select(s => s.GetShallowCopy(CloneMode.Full))
            );

            viewPlanet.Buildings.AddRange(
                masterPlanet.Buildings.Select(b => b.GetShallowCopy(CloneMode.Full))
            );

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
            viewPlanet.CapitalShips.AddRange(
                planetSnapshot.CapitalShips.Select(c => c.GetShallowCopy(CloneMode.Full))
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
        /// Populates a view planet for a completely unexplored location. No ownership or
        /// entity data is surfaced. Captured friendly officers are always live data regardless.
        /// </summary>
        /// <param name="viewPlanet">The view planet to populate.</param>
        /// <param name="masterPlanet">The authoritative planet data source.</param>
        /// <param name="faction">The faction whose view is being built.</param>
        private void ApplyUnexploredView(Planet viewPlanet, Planet masterPlanet, Faction faction)
        {
            viewPlanet.Officers.AddRange(
                masterPlanet
                    .Officers.Where(o => o.IsCaptured && o.OwnerInstanceID == faction.InstanceID)
                    .Select(CopyOfficerForSnapshot)
            );
        }
    }
}
