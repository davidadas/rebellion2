using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Records and invalidates faction intelligence at the time an observation is made.
    /// </summary>
    public class FogOfWarCommands
    {
        private readonly GameRoot _game;
        private readonly FogOfWarRecorder _recorder;

        /// <summary>
        /// Creates a FogOfWarCommands for the given game instance.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public FogOfWarCommands(GameRoot game)
            : this(game, new FogOfWarRecorder()) { }

        /// <summary>Creates fog-of-war operations using the supplied intelligence recorder.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="recorder">The recorder shared with intelligence lifecycle handling.</param>
        internal FogOfWarCommands(GameRoot game, FogOfWarRecorder recorder)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        /// <summary>
        /// Records the current state of a registered planet for a registered faction.
        /// </summary>
        /// <param name="faction">The faction receiving the observation.</param>
        /// <param name="planet">The planet being observed.</param>
        /// <param name="observedAtTick">The tick represented by the observation, or the current tick.</param>
        /// <returns>True when the observation was recorded.</returns>
        public bool ObservePlanet(Faction faction, Planet planet, int? observedAtTick = null)
        {
            Faction liveFaction = FindFaction(faction?.InstanceID);
            Planet livePlanet = string.IsNullOrEmpty(planet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
            PlanetSector sector = livePlanet?.GetParentOfType<PlanetSector>();
            if (liveFaction == null || livePlanet == null || sector == null)
                return false;

            CaptureSnapshot(liveFaction, livePlanet, sector, observedAtTick ?? _game.CurrentTick);
            return true;
        }

        /// <summary>Records selected registered entities for a registered faction.</summary>
        /// <param name="faction">The faction receiving the observations.</param>
        /// <param name="entities">The entities being observed.</param>
        /// <param name="observedAtTick">The tick represented by the observations, or the current tick.</param>
        /// <returns>True when the faction and at least one entity were valid.</returns>
        public bool ObserveEntities(
            Faction faction,
            IEnumerable<ISceneNode> entities,
            int? observedAtTick = null
        )
        {
            Faction liveFaction = FindFaction(faction?.InstanceID);
            List<ISceneNode> observations = (entities ?? Enumerable.Empty<ISceneNode>())
                .Where(entity => entity != null && !string.IsNullOrEmpty(entity.InstanceID))
                .ToList();
            if (liveFaction == null || observations.Count == 0)
                return false;

            _recorder.RecordSelectedObservations(
                _game,
                liveFaction,
                observations,
                observedAtTick ?? _game.CurrentTick
            );
            return true;
        }

        /// <summary>Records a planet's current ownership for the factions observing it.</summary>
        /// <param name="planet">The planet whose ownership is observed.</param>
        /// <param name="observers">The factions receiving a current ownership observation.</param>
        /// <param name="observedAtTick">The tick represented by the observation, or the current tick.</param>
        /// <returns>True when the planet belongs to a registered sector.</returns>
        public bool ObservePlanetOwnership(
            Planet planet,
            IEnumerable<Faction> observers,
            int? observedAtTick = null
        )
        {
            Planet livePlanet = string.IsNullOrEmpty(planet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
            PlanetSector sector = livePlanet?.GetParentOfType<PlanetSector>();
            if (livePlanet == null || sector == null)
                return false;

            HashSet<string> observerIds = new HashSet<string>(
                (observers ?? Enumerable.Empty<Faction>())
                    .Where(faction => faction != null)
                    .Select(faction => faction.InstanceID)
            );
            foreach (Faction faction in _game.GetFactions())
            {
                if (observerIds.Contains(faction.InstanceID))
                {
                    _recorder.RecordPlanetOwnershipSnapshot(
                        faction,
                        livePlanet,
                        sector,
                        observedAtTick ?? _game.CurrentTick
                    );
                }
                else
                {
                    _recorder.UpdateKnownPlanetOwnershipSnapshot(faction, livePlanet, sector);
                }
            }

            return true;
        }

        /// <summary>
        /// Removes a registered entity from one faction's remembered observations.
        /// </summary>
        /// <param name="faction">The faction whose observations are updated.</param>
        /// <param name="entityId">The registered entity identifier to forget.</param>
        /// <returns>True when the request identified a registered faction and entity.</returns>
        public bool ForgetEntity(Faction faction, string entityId)
        {
            Faction liveFaction = FindFaction(faction?.InstanceID);
            if (liveFaction == null || string.IsNullOrEmpty(entityId))
                return false;

            RemoveEntityFromSnapshots(liveFaction, entityId);
            return true;
        }

        /// <summary>
        /// Captures a snapshot of a planet for a faction.
        /// </summary>
        /// <param name="faction">The faction receiving the snapshot.</param>
        /// <param name="planet">The planet being observed.</param>
        /// <param name="sector">The sector containing the planet.</param>
        /// <param name="currentTick">The tick when the snapshot is captured.</param>
        private void CaptureSnapshot(
            Faction faction,
            Planet planet,
            PlanetSector sector,
            int currentTick
        )
        {
            _recorder.RecordPlanetSnapshot(faction, planet, sector, currentTick);
        }

        /// <summary>
        /// Removes an entity from all saved planet snapshots for a faction.
        /// </summary>
        /// <param name="faction">The faction whose snapshots are updated.</param>
        /// <param name="entityId">The entity instance ID to remove.</param>
        private void RemoveEntityFromSnapshots(Faction faction, string entityId)
        {
            _recorder.RemoveEntityFromSnapshots(faction, entityId);
        }

        /// <summary>
        /// Resolves a faction without throwing when an optional owner identifier is absent.
        /// </summary>
        /// <param name="factionId">The faction identifier.</param>
        /// <returns>The matching faction, or null.</returns>
        private Faction FindFaction(string factionId)
        {
            return string.IsNullOrEmpty(factionId)
                ? null
                : _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == factionId);
        }
    }
}
