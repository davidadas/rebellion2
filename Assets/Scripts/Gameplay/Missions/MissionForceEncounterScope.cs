using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Filters Force encounter candidates using the physical scope of one mission checkpoint.
    /// Fleet presence and approach protection are supplied by the checkpoint's detector assessment.
    /// </summary>
    internal readonly struct MissionForceEncounterScope
    {
        private readonly Mission _mission;
        private readonly Planet _planet;
        private readonly MissionEncounterPhase _phase;
        private readonly bool _groundDefenders;
        private readonly bool _fleetDefenders;

        /// <summary>
        /// Captures the location facts shared by Force selection and ordinary detection.
        /// </summary>
        /// <param name="mission">The mission reaching the checkpoint.</param>
        /// <param name="planet">The planet at which the checkpoint occurs.</param>
        /// <param name="phase">The current mission checkpoint.</param>
        /// <param name="planetaryObjective">Whether the objective lies directly on the planet.</param>
        /// <param name="hasHostileFleet">Whether a stationary hostile fleet is present.</param>
        /// <param name="blocksFleetDetection">Whether a completed friendly building protects the approach.</param>
        internal MissionForceEncounterScope(
            Mission mission,
            Planet planet,
            MissionEncounterPhase phase,
            bool planetaryObjective,
            bool hasHostileFleet,
            bool blocksFleetDetection
        )
        {
            _mission = mission;
            _planet = planet;
            _phase = phase;
            bool ground =
                phase == MissionEncounterPhase.DepartureStart
                || phase == MissionEncounterPhase.PreObjective && planetaryObjective;
            _groundDefenders =
                ground
                && !string.IsNullOrEmpty(planet.OwnerInstanceID)
                && planet.OwnerInstanceID != mission.OwnerInstanceID;
            _fleetDefenders =
                !ground
                && hasHostileFleet
                && (phase == MissionEncounterPhase.PreObjective || !blocksFleetDetection);
        }

        /// <summary>
        /// Selects the participating primary and decoy officers within the checkpoint's physical scope.
        /// </summary>
        /// <param name="primary">The primary team present at this checkpoint.</param>
        /// <param name="decoys">The decoy team present at this checkpoint.</param>
        /// <param name="config">The participant Force threshold.</param>
        /// <returns>The eligible primary officers followed by the eligible decoys.</returns>
        internal List<Officer> GetParticipants(
            IReadOnlyList<IMissionParticipant> primary,
            IReadOnlyList<IMissionParticipant> decoys,
            GameConfig.JediConfig config
        )
        {
            List<Officer> selected = new(primary.Count + decoys.Count);
            AddParticipants(primary, config.MissionParticipantEncounterMinimum, selected);
            AddParticipants(decoys, config.MissionParticipantEncounterMinimum, selected);
            return selected;
        }

        /// <summary>
        /// Selects opposing officers without enumerating the scene graph for each mission.
        /// Officers working on another mission retain their planet as their physical location.
        /// </summary>
        /// <param name="roster">The maintained officer roster for the game.</param>
        /// <param name="config">The defender Force threshold.</param>
        /// <returns>The opposing officers within the checkpoint's defender scope.</returns>
        internal List<Officer> GetDefenders(
            IReadOnlyList<Officer> roster,
            GameConfig.JediConfig config
        )
        {
            List<Officer> selected = new();
            if (!_groundDefenders && !_fleetDefenders)
                return selected;
            foreach (Officer officer in roster)
            {
                if (
                    officer.OwnerInstanceID == _mission.OwnerInstanceID
                    || officer.ForceRank < config.MissionDefenderEncounterMinimum
                    || !IsAvailable(officer)
                )
                    continue;

                ISceneNode parent = GetPhysicalParent(officer);
                if (_groundDefenders && parent == _planet)
                    selected.Add(officer);
                else if (
                    _fleetDefenders
                    && parent is CapitalShip ship
                    && ship.GetParent() is Fleet fleet
                    && fleet.GetParent() == _planet
                    && fleet.Movement == null
                    && ship.ManufacturingStatus == ManufacturingStatus.Complete
                )
                    selected.Add(officer);
            }
            return selected;
        }

        /// <summary>
        /// Appends eligible officers from one participant role without querying scene collections.
        /// </summary>
        /// <param name="participants">The primary or decoy participant list.</param>
        /// <param name="minimumRank">The inclusive minimum participant Force rank.</param>
        /// <param name="selected">The collection receiving eligible officers.</param>
        private void AddParticipants(
            IReadOnlyList<IMissionParticipant> participants,
            int minimumRank,
            List<Officer> selected
        )
        {
            foreach (IMissionParticipant participant in participants)
            {
                if (
                    participant is not Officer officer
                    || !IsAvailable(officer)
                    || officer.ForceRank < minimumRank
                )
                    continue;
                ISceneNode parent = GetPhysicalParent(officer);
                bool ground = parent == _planet;
                if (
                    !ground
                    && (
                        parent is not CapitalShip ship
                        || ship.GetParent() is not Fleet fleet
                        || fleet.GetParent() != _planet
                    )
                )
                    continue;

                bool groundAllowed = _phase switch
                {
                    MissionEncounterPhase.DepartureStart => true,
                    MissionEncounterPhase.DepartureComplete => _mission.LocationInstanceID
                        != _planet.InstanceID,
                    MissionEncounterPhase.Arrival => officer.MissionReturnLocationInstanceID
                        != _planet.InstanceID,
                    MissionEncounterPhase.PreObjective => true,
                    _ => false,
                };
                if (ground ? groundAllowed : _phase != MissionEncounterPhase.DepartureStart)
                    selected.Add(officer);
            }
        }

        /// <summary>
        /// Resolves the physical parent while treating the mission node as a logical assignment.
        /// </summary>
        /// <param name="officer">The officer whose physical container is requested.</param>
        /// <returns>The planet or capital ship physically containing the officer.</returns>
        private static ISceneNode GetPhysicalParent(Officer officer)
        {
            ISceneNode parent = officer.GetParent();
            return parent is Mission mission ? mission.GetParent() : parent;
        }

        /// <summary>
        /// Returns whether an officer can take part in a stationary encounter.
        /// </summary>
        /// <param name="officer">The candidate officer.</param>
        /// <returns>True for an active, free, living officer who is not travelling.</returns>
        private static bool IsAvailable(Officer officer) =>
            !officer.IsCaptured
            && !officer.IsKilled
            && ((IMovable)officer).GetTransitMovement() == null
            && officer.IsActive();
    }
}
