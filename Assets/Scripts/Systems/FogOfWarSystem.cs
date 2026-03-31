using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages fog of war operations: capturing snapshots, invalidating moved entities, building faction views.
    /// Operates on faction state, does not hold its own state.
    /// </summary>
    public class FogOfWarSystem
    {
        private readonly GameRoot game;

        public FogOfWarSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Captures a snapshot of a planet for a faction.
        /// Called when: espionage succeeds, fleet arrives, or at game initialization.
        /// </summary>
        public void CaptureSnapshot(
            Faction faction,
            Planet planet,
            PlanetSystem system,
            int currentTick
        )
        {
            if (
                !faction.Fog.Snapshots.TryGetValue(
                    system.InstanceID,
                    out SystemSnapshot systemSnapshot
                )
            )
            {
                systemSnapshot = new SystemSnapshot();
                faction.Fog.Snapshots[system.InstanceID] = systemSnapshot;
            }

            faction.Fog.PlanetToSystem[planet.InstanceID] = system.InstanceID;

            PlanetSnapshot planetSnapshot = new PlanetSnapshot
            {
                TickCaptured = currentTick,
                OwnerInstanceID = planet.OwnerInstanceID,
                PopularSupport = new Dictionary<string, int>(planet.PopularSupport),
            };

            foreach (Officer officer in planet.Officers)
            {
                planetSnapshot.Officers.Add(CopyOfficer(officer));
                InvalidateEntityFromOtherSnapshots(faction, officer.InstanceID, planet.InstanceID);
            }

            foreach (Fleet fleet in planet.Fleets)
            {
                if (fleet.OwnerInstanceID != faction.InstanceID && fleet.Movement != null)
                    continue;

                planetSnapshot.Fleets.Add(fleet.GetShallowCopy(CloneMode.Full));
                InvalidateEntityFromOtherSnapshots(faction, fleet.InstanceID, planet.InstanceID);
            }

            foreach (Regiment regiment in planet.Regiments)
            {
                planetSnapshot.Regiments.Add(regiment.GetShallowCopy(CloneMode.Full));
                InvalidateEntityFromOtherSnapshots(faction, regiment.InstanceID, planet.InstanceID);
            }

            foreach (List<Building> buildingList in planet.Buildings.Values)
            {
                foreach (Building building in buildingList)
                {
                    planetSnapshot.Buildings.Add(building.GetShallowCopy(CloneMode.Full));
                    InvalidateEntityFromOtherSnapshots(
                        faction,
                        building.InstanceID,
                        planet.InstanceID
                    );
                }
            }

            foreach (Starfighter starfighter in planet.Starfighters)
            {
                planetSnapshot.Starfighters.Add(starfighter.GetShallowCopy(CloneMode.Full));
                InvalidateEntityFromOtherSnapshots(
                    faction,
                    starfighter.InstanceID,
                    planet.InstanceID
                );
            }

            systemSnapshot.Planets[planet.InstanceID] = planetSnapshot;
        }

        /// <summary>
        /// Copies an officer with a fresh Skills dictionary to prevent shared references.
        /// </summary>
        private Officer CopyOfficer(Officer officer)
        {
            Officer copy = officer.GetShallowCopy(CloneMode.Full);
            copy.Skills = new Dictionary<MissionParticipantSkill, int>(officer.Skills);
            return copy;
        }

        /// <summary>
        /// Removes an entity from its old snapshot location when it's discovered elsewhere.
        /// O(1) lookup via PlanetToSystem index.
        /// </summary>
        private void InvalidateEntityFromOtherSnapshots(
            Faction faction,
            string entityID,
            string currentPlanetID
        )
        {
            if (faction.Fog.EntityLastSeenAt.TryGetValue(entityID, out string oldPlanetID))
            {
                if (oldPlanetID != currentPlanetID)
                {
                    if (
                        faction.Fog.PlanetToSystem.TryGetValue(oldPlanetID, out string oldSystemID)
                        && faction.Fog.Snapshots.TryGetValue(
                            oldSystemID,
                            out SystemSnapshot systemSnapshot
                        )
                        && systemSnapshot.Planets.TryGetValue(
                            oldPlanetID,
                            out PlanetSnapshot oldPlanetSnapshot
                        )
                    )
                    {
                        oldPlanetSnapshot.Officers.RemoveAll(o => o.InstanceID == entityID);
                        oldPlanetSnapshot.Fleets.RemoveAll(f => f.InstanceID == entityID);
                        oldPlanetSnapshot.Regiments.RemoveAll(r => r.InstanceID == entityID);
                        oldPlanetSnapshot.Buildings.RemoveAll(b => b.InstanceID == entityID);
                        oldPlanetSnapshot.Starfighters.RemoveAll(s => s.InstanceID == entityID);
                    }
                }
            }

            faction.Fog.EntityLastSeenAt[entityID] = currentPlanetID;
        }

        /// <summary>
        /// Determines if a faction currently has real-time visibility of a planet.
        /// </summary>
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
        public GalaxyMap BuildFactionView(Faction faction)
        {
            GalaxyMap factionView = new GalaxyMap();

            foreach (PlanetSystem masterSystem in game.Galaxy.PlanetSystems)
            {
                PlanetSystem viewSystem = masterSystem.GetShallowCopy(CloneMode.Full);
                viewSystem.Planets = new List<Planet>();

                bool hasSystemSnapshot = faction.Fog.Snapshots.TryGetValue(
                    masterSystem.InstanceID,
                    out SystemSnapshot systemSnapshot
                );

                foreach (Planet masterPlanet in masterSystem.Planets)
                {
                    Planet viewPlanet = masterPlanet.GetShallowCopy(CloneMode.Full);
                    viewPlanet.Officers = new List<Officer>();
                    viewPlanet.Fleets = new List<Fleet>();
                    viewPlanet.Regiments = new List<Regiment>();
                    viewPlanet.Buildings = new Dictionary<BuildingSlot, List<Building>>();
                    viewPlanet.Starfighters = new List<Starfighter>();
                    viewPlanet.Missions = new List<Mission>();
                    viewPlanet.PopularSupport = new Dictionary<string, int>();

                    bool isVisible = IsPlanetVisible(masterPlanet, faction);

                    PlanetSnapshot planetSnapshot = null;
                    if (hasSystemSnapshot)
                    {
                        systemSnapshot.Planets.TryGetValue(
                            masterPlanet.InstanceID,
                            out planetSnapshot
                        );
                    }

                    if (isVisible)
                    {
                        // Real-time.
                        viewPlanet.OwnerInstanceID = masterPlanet.OwnerInstanceID;
                        viewPlanet.PopularSupport = new Dictionary<string, int>(
                            masterPlanet.PopularSupport
                        );
                        viewPlanet.Officers.AddRange(masterPlanet.Officers.Select(CopyOfficer));
                        viewPlanet.Fleets.AddRange(
                            masterPlanet
                                .Fleets.Where(f =>
                                    f.Movement == null || f.OwnerInstanceID == faction.InstanceID
                                )
                                .Select(f => f.GetShallowCopy(CloneMode.Full))
                        );
                        viewPlanet.Regiments.AddRange(
                            masterPlanet.Regiments.Select(r => r.GetShallowCopy(CloneMode.Full))
                        );

                        foreach (BuildingSlot slot in masterPlanet.Buildings.Keys)
                        {
                            viewPlanet.Buildings[slot] = masterPlanet
                                .Buildings[slot]
                                .Select(b => b.GetShallowCopy(CloneMode.Full))
                                .ToList();
                        }

                        viewPlanet.Starfighters.AddRange(
                            masterPlanet.Starfighters.Select(s => s.GetShallowCopy(CloneMode.Full))
                        );
                        viewPlanet.NumRawResourceNodes = masterPlanet.NumRawResourceNodes;
                        // Also surface enemy fleets captured in a prior snapshot
                        // (e.g. via prior occupation) — persists alongside live data.
                        // Enemy missions are never surfaced regardless of snapshot state.
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
                    else if (planetSnapshot != null)
                    {
                        // Snapshot.
                        viewPlanet.OwnerInstanceID = planetSnapshot.OwnerInstanceID;
                        // Core system popular support is always visible regardless of fog.
                        viewPlanet.PopularSupport =
                            masterSystem.SystemType == PlanetSystemType.CoreSystem
                                ? new Dictionary<string, int>(masterPlanet.PopularSupport)
                                : new Dictionary<string, int>();
                        viewPlanet.Officers.AddRange(planetSnapshot.Officers.Select(CopyOfficer));
                        viewPlanet.Fleets.AddRange(
                            planetSnapshot.Fleets.Select(f => f.GetShallowCopy(CloneMode.Full))
                        );
                        viewPlanet.Regiments.AddRange(
                            planetSnapshot.Regiments.Select(r => r.GetShallowCopy(CloneMode.Full))
                        );

                        viewPlanet.Buildings[BuildingSlot.Ground] = planetSnapshot
                            .Buildings.Select(b => b.GetShallowCopy(CloneMode.Full))
                            .ToList();

                        viewPlanet.Starfighters.AddRange(
                            planetSnapshot.Starfighters.Select(s =>
                                s.GetShallowCopy(CloneMode.Full)
                            )
                        );
                        viewPlanet.NumRawResourceNodes = 0;
                    }
                    else
                    {
                        // Unexplored — no visibility, no snapshot.
                    }

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
    }
}
