using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Processes scheduled escape attempts for captured officers.
    /// Escape probability is based on the officer's skills and the forces guarding
    /// the planet, fleet, or ship where the officer is held.
    /// </summary>
    public class CaptiveSystem
        : IGameResultHandler<OfficerCaptureStateResult>,
            IGameResultHandler<PlanetOwnershipChangedResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movementSystem;
        private readonly FogOfWarSystem _fogOfWarSystem;
        private readonly ProbabilityTable _escapeTable;
        private readonly GameConfig.TickRangeConfig _escapeAttemptInterval;
        private readonly int _loyaltyShift;

        /// <summary>
        /// Creates a new CaptiveSystem.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">RNG provider for escape rolls.</param>
        /// <param name="movementSystem">Moves officers into and out of custody.</param>
        /// <param name="fogOfWarSystem">Records the custody destination known at capture time.</param>
        public CaptiveSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movementSystem,
            FogOfWarSystem fogOfWarSystem
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _movementSystem =
                movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
            _fogOfWarSystem =
                fogOfWarSystem ?? throw new ArgumentNullException(nameof(fogOfWarSystem));
            _escapeAttemptInterval = game.Config.Captive.EscapeAttemptInterval;
            _escapeTable = new ProbabilityTable(game.Config.Captive.EscapeTable);
            _loyaltyShift = game.Config.Captive.EscapeLoyaltyShift;
        }

        /// <summary>
        /// Establishes custody for newly captured officers and records the location revealed to
        /// their original factions.
        /// </summary>
        /// <param name="results">The capture-state changes to process.</param>
        /// <returns>Movement results produced while transferring captives.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledOfficerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficerCaptureStateResult result in results)
            {
                Officer officer = result?.TargetOfficer;
                if (officer == null)
                    continue;

                Faction originalFaction = _game.GetFactionByOwnerInstanceID(
                    officer.OwnerInstanceID
                );
                if (result.IsCaptured == false)
                {
                    officer.NextEscapeAttemptTick = 0;
                    if (!officer.IsCaptured)
                        _fogOfWarSystem.RemoveEntityFromSnapshots(
                            originalFaction,
                            officer.InstanceID
                        );
                    continue;
                }

                if (
                    officer.IsCaptured != true
                    || string.IsNullOrEmpty(officer.CaptorInstanceID)
                    || !handledOfficerIds.Add(officer.InstanceID)
                )
                    continue;

                ContainerNode destination = ResolveCustodyDestination(result, officer);
                if (
                    destination == null
                    || !_movementSystem.TryEstablishCapturedOfficerCustody(
                        officer,
                        destination,
                        GetCustodyEscort(result.CapturingUnit),
                        reactions
                    )
                )
                {
                    GameLogger.Log(
                        $"Captured officer {officer.GetDisplayName()} has no valid custody destination for {officer.CaptorInstanceID}.",
                        GameLogger.LogLevel.Error
                    );
                    ApplyPostCaptureState(result, officer);
                    continue;
                }

                ApplyPostCaptureState(result, officer);
                _fogOfWarSystem.RecordObservations(originalFaction, new[] { officer }, result.Tick);
                if (officer.CanEscape && officer.NextEscapeAttemptTick <= 0)
                    ScheduleEscapeAttempt(officer);
            }

            return reactions;
        }

        /// <summary>
        /// Releases captured officers when their faction takes control of the planet holding them.
        /// </summary>
        /// <param name="results">The planet ownership changes to process.</param>
        /// <returns>Capture-state changes for the officers released by the ownership changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                Planet planet = result?.Planet;
                string newOwnerInstanceID = result?.NewOwner?.InstanceID;
                if (planet == null || string.IsNullOrEmpty(newOwnerInstanceID))
                    continue;

                foreach (
                    Officer officer in planet
                        .GetAllOfficers()
                        .Where(officer =>
                            officer.IsCaptured
                            && !officer.IsKilled
                            && officer.GetOwnerInstanceID() == newOwnerInstanceID
                        )
                )
                {
                    reactions.Add(
                        ReleaseOfficer(officer, planet, result.Tick, officer.CaptorInstanceID)
                    );
                }
            }

            return reactions;
        }

        /// <summary>
        /// Processes escape attempts that are due on the current tick.
        /// </summary>
        /// <returns>Results for any officers that escaped.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsCaptured || !officer.CanEscape || officer.IsKilled)
                {
                    officer.NextEscapeAttemptTick = 0;
                    continue;
                }

                if (officer.NextEscapeAttemptTick <= 0)
                {
                    ScheduleEscapeAttempt(officer);
                    continue;
                }

                if (_game.CurrentTick < officer.NextEscapeAttemptTick)
                    continue;

                ScheduleEscapeAttempt(officer);

                ContainerNode custodyContext = GetCustodyContext(officer);
                Planet planet = officer.GetParentOfType<Planet>();
                if (
                    custodyContext == null
                    || planet == null
                    || officer.GetTransitMovement() != null
                )
                    continue;

                if (RollEscapeAttempt(officer, custodyContext))
                {
                    OfficerCaptureStateResult result = TryReleaseOfficer(officer, planet);
                    if (result != null)
                        results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Applies authored officer state only after mission interruption and custody processing.
        /// </summary>
        /// <param name="result">The completed capture-state change.</param>
        /// <param name="officer">The captured officer.</param>
        private static void ApplyPostCaptureState(OfficerCaptureStateResult result, Officer officer)
        {
            if (result.DeactivateAfterCapture)
                officer.IsEnabled = false;
        }

        /// <summary>
        /// Schedules an officer's next escape attempt within the configured inclusive range.
        /// </summary>
        /// <param name="officer">The captive officer whose next attempt is scheduled.</param>
        private void ScheduleEscapeAttempt(Officer officer)
        {
            int minimum = Math.Max(0, _escapeAttemptInterval?.Minimum ?? 0);
            int maximum = Math.Max(minimum, _escapeAttemptInterval?.Maximum ?? minimum);
            officer.NextEscapeAttemptTick = checked(
                _game.CurrentTick + _provider.NextInt(minimum, maximum + 1)
            );
        }

        /// <summary>
        /// Resolves an established custody container, the capturing unit's container, or a
        /// captor-controlled fallback planet.
        /// </summary>
        /// <param name="result">The capture result that identifies the capturing unit and location.</param>
        /// <param name="officer">The captured officer requiring custody.</param>
        /// <returns>The selected custody container, or null when none can hold the officer.</returns>
        private ContainerNode ResolveCustodyDestination(
            OfficerCaptureStateResult result,
            Officer officer
        )
        {
            string captorInstanceId = officer.CaptorInstanceID;
            ContainerNode establishedCustody = GetCaptorControlledContainer(
                officer.GetParent() as ContainerNode,
                captorInstanceId,
                officer
            );
            if (establishedCustody != null)
                return establishedCustody;

            Planet capturePlanet = GetResultPlanet(result);
            ContainerNode capturePlanetCustody = GetCaptorControlledContainer(
                capturePlanet,
                captorInstanceId,
                officer
            );
            if (capturePlanetCustody != null)
                return capturePlanetCustody;

            ContainerNode capturingUnitCustody = GetCapturingUnitCustody(
                result.CapturingUnit,
                captorInstanceId,
                officer
            );
            if (capturingUnitCustody != null)
                return capturingUnitCustody;

            Faction captor = _game.GetFactionByOwnerInstanceID(captorInstanceId);
            return captor
                ?.GetOwnedColonizedPlanets()
                .Where(planet =>
                    !planet.IsDestroyed
                    && string.Equals(
                        planet.GetOwnerInstanceID(),
                        captorInstanceId,
                        StringComparison.Ordinal
                    )
                    && planet.CanAcceptChild(officer)
                )
                .OrderBy(planet => planet.GetRawDistanceTo(officer.GetPosition()))
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the planet where a capture occurred.
        /// </summary>
        /// <param name="result">The capture result to inspect.</param>
        /// <returns>The capture planet, or null when the result has no planetary context.</returns>
        private static Planet GetResultPlanet(OfficerCaptureStateResult result)
        {
            return result?.Context as Planet
                ?? (result?.Context as ISceneNode)?.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Returns the captor-controlled ship or planet currently containing a capturing unit.
        /// </summary>
        /// <param name="capturingUnit">The unit responsible for the capture.</param>
        /// <param name="captorInstanceId">The capturing faction identifier.</param>
        /// <param name="officer">The captured officer requiring custody.</param>
        /// <returns>The capturing unit's custody container, or null when it has none.</returns>
        private static ContainerNode GetCapturingUnitCustody(
            ISceneNode capturingUnit,
            string captorInstanceId,
            Officer officer
        )
        {
            if (capturingUnit == null)
                return null;

            CapitalShip ship =
                capturingUnit as CapitalShip ?? capturingUnit.GetParentOfType<CapitalShip>();
            ContainerNode shipCustody = GetCaptorControlledContainer(
                ship,
                captorInstanceId,
                officer
            );
            if (shipCustody != null)
                return shipCustody;

            return GetCaptorControlledContainer(
                capturingUnit.GetParent() as ContainerNode,
                captorInstanceId,
                officer
            );
        }

        /// <summary>
        /// Returns a capturing officer or special-forces unit that can escort the captive.
        /// </summary>
        /// <param name="capturingUnit">The unit responsible for the capture.</param>
        /// <returns>The movable escort, or null when the capture has no physical escort.</returns>
        private static IMovable GetCustodyEscort(ISceneNode capturingUnit)
        {
            return capturingUnit switch
            {
                Officer officer => officer,
                SpecialForces specialForces => specialForces,
                _ => null,
            };
        }

        /// <summary>
        /// Returns a ship or planet when the supplied container belongs to the capturing faction.
        /// </summary>
        /// <param name="container">The possible custody container.</param>
        /// <param name="captorInstanceId">The capturing faction identifier.</param>
        /// <param name="officer">The captured officer requiring custody.</param>
        /// <returns>The controlled custody container, or null when ownership does not match.</returns>
        private static ContainerNode GetCaptorControlledContainer(
            ContainerNode container,
            string captorInstanceId,
            Officer officer
        )
        {
            return
                container is Planet or CapitalShip
                && IsControlledBy(container, captorInstanceId)
                && container.CanAcceptChild(officer)
                ? container
                : null;
        }

        /// <summary>
        /// Returns whether a scene node belongs to the capturing faction and remains usable.
        /// </summary>
        /// <param name="node">The node to inspect.</param>
        /// <param name="captorInstanceId">The capturing faction identifier.</param>
        /// <returns>True when the node is a valid captor-controlled custody location.</returns>
        private static bool IsControlledBy(ISceneNode node, string captorInstanceId)
        {
            if (node == null || string.IsNullOrEmpty(captorInstanceId))
                return false;

            if (node is Planet { IsDestroyed: true })
                return false;
            if (
                node is CapitalShip capitalShip
                && capitalShip.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            return string.Equals(
                node.GetOwnerInstanceID(),
                captorInstanceId,
                StringComparison.Ordinal
            );
        }

        /// <summary>
        /// Returns the local container whose forces guard a captive.
        /// </summary>
        /// <param name="officer">The captive officer.</param>
        /// <returns>The fleet, ship, or planet holding the officer.</returns>
        private static ContainerNode GetCustodyContext(Officer officer)
        {
            ContainerNode fleet = officer.GetParentOfType<Fleet>();
            ContainerNode ship = officer.GetParentOfType<CapitalShip>();
            return fleet ?? ship ?? officer.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Rolls the escape probability against the forces in the officer's custody context.
        /// </summary>
        /// <param name="officer">The officer attempting escape.</param>
        /// <param name="custodyContext">The planet, fleet, or ship holding the officer.</param>
        /// <returns>True if the escape roll succeeds.</returns>
        private bool RollEscapeAttempt(Officer officer, ContainerNode custodyContext)
        {
            int delta = ComputeEscapeDelta(officer, custodyContext);
            double probability = _escapeTable.Lookup(delta);
            return _provider.NextDouble() * 100 <= probability;
        }

        /// <summary>
        /// Frees a captured officer when a friendly fleet or planet accepts their movement.
        /// </summary>
        /// <param name="officer">The officer to release.</param>
        /// <param name="planet">The planet the officer escaped from.</param>
        /// <returns>A release result when movement succeeds; otherwise null.</returns>
        private OfficerCaptureStateResult TryReleaseOfficer(Officer officer, Planet planet)
        {
            string captorInstanceID = officer.CaptorInstanceID;
            officer.IsCaptured = false;
            officer.CaptorInstanceID = null;

            Faction faction = _game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            ContainerNode destination = GetEscapeDestinations(faction, officer, planet)
                .FirstOrDefault(candidate => _movementSystem.TryRequestMove(officer, candidate));
            if (destination == null)
            {
                officer.IsCaptured = true;
                officer.CaptorInstanceID = captorInstanceID;
                return null;
            }

            officer.Loyalty = Math.Max(0, Math.Min(100, officer.Loyalty + _loyaltyShift));

            return ReleaseOfficer(officer, planet, _game.CurrentTick, captorInstanceID);
        }

        /// <summary>
        /// Clears an officer's custody state and describes the release.
        /// </summary>
        /// <param name="officer">The officer being released.</param>
        /// <param name="context">The planet where the release occurred.</param>
        /// <param name="tick">The tick when the release occurred.</param>
        /// <param name="captorInstanceID">The faction that held the officer.</param>
        /// <returns>The resulting capture-state change.</returns>
        private static OfficerCaptureStateResult ReleaseOfficer(
            Officer officer,
            Planet context,
            int tick,
            string captorInstanceID
        )
        {
            officer.IsCaptured = false;
            officer.CaptorInstanceID = null;
            officer.CanEscape = false;
            officer.NextEscapeAttemptTick = 0;

            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = false,
                CaptorInstanceID = captorInstanceID,
                Context = context,
                Tick = tick,
            };
        }

        /// <summary>
        /// Returns friendly fleets and planets that could receive an escaping officer.
        /// </summary>
        /// <param name="faction">The escaping officer's faction.</param>
        /// <param name="officer">The officer attempting to escape.</param>
        /// <param name="origin">The planet where the officer is held.</param>
        /// <returns>Candidate destinations ordered by distance, with local fleets preferred.</returns>
        private IEnumerable<ContainerNode> GetEscapeDestinations(
            Faction faction,
            Officer officer,
            Planet origin
        )
        {
            if (faction == null || officer == null || origin == null)
                return Enumerable.Empty<ContainerNode>();

            IEnumerable<ContainerNode> fleets = _game
                .GetSceneNodesByType<Fleet>()
                .Where(fleet =>
                    fleet.GetOwnerInstanceID() == faction.InstanceID
                    && fleet.Movement == null
                    && fleet.HasOperationalCapitalShips()
                    && fleet.GetParentOfType<Planet>() != null
                );
            IEnumerable<ContainerNode> planets = faction
                .GetOwnedColonizedPlanets()
                .Where(candidate => !candidate.IsDestroyed)
                .Cast<ContainerNode>();

            return fleets
                .Concat(planets)
                .OrderBy(destination =>
                    origin.GetRawDistanceTo(
                        destination.GetParentOfType<Planet>() ?? destination as Planet
                    )
                )
                .ThenBy(destination => destination is Fleet ? 0 : 1)
                .ThenBy(destination => destination.InstanceID, StringComparer.Ordinal);
        }

        /// <summary>
        /// Computes the escape delta for the probability table lookup.
        /// Higher values favour escape.
        /// </summary>
        /// <param name="officer">The officer attempting escape.</param>
        /// <param name="custodyContext">The planet, fleet, or ship holding the officer.</param>
        /// <returns>The escape delta for table lookup.</returns>
        private int ComputeEscapeDelta(Officer officer, ContainerNode custodyContext)
        {
            int officerSkills = GetEscapeSkillScore(officer);
            int guardCombat = GetAverageGuardCombat(custodyContext, officer.CaptorInstanceID);
            int guardRegiments = CountGuardRegiments(custodyContext, officer.CaptorInstanceID);

            return officerSkills - guardCombat - guardRegiments;
        }

        /// <summary>
        /// Gets the officer skill value used for escape attempts.
        /// </summary>
        /// <param name="officer">The officer attempting escape.</param>
        /// <returns>The officer escape score.</returns>
        private static int GetEscapeSkillScore(Officer officer)
        {
            return officer.GetEffectiveRating(OfficerRating.Espionage)
                + officer.GetEffectiveRating(OfficerRating.Combat);
        }

        /// <summary>
        /// Gets the average combat value of free captor-aligned guards in the custody context.
        /// </summary>
        /// <param name="custodyContext">The planet, fleet, or ship holding the officer.</param>
        /// <param name="captorInstanceId">The faction holding the officer captive.</param>
        /// <returns>The average guard combat value, or 0 if no guards are present.</returns>
        private static int GetAverageGuardCombat(
            ContainerNode custodyContext,
            string captorInstanceId
        )
        {
            List<Officer> guards = GetCustodyUnits<Officer>(custodyContext)
                .Where(officer =>
                    officer.GetOwnerInstanceID() == captorInstanceId
                    && !officer.IsCaptured
                    && !officer.IsKilled
                )
                .ToList();

            if (guards.Count == 0)
                return 0;

            return guards.Sum(g => g.GetEffectiveRating(OfficerRating.Combat)) / guards.Count;
        }

        /// <summary>
        /// Counts captor-aligned guard regiments in the custody context.
        /// </summary>
        /// <param name="custodyContext">The planet, fleet, or ship holding the officer.</param>
        /// <param name="captorInstanceId">The faction holding the officer captive.</param>
        /// <returns>The number of guard regiments.</returns>
        private static int CountGuardRegiments(
            ContainerNode custodyContext,
            string captorInstanceId
        )
        {
            return GetCustodyUnits<Regiment>(custodyContext)
                .Count(regiment => regiment.OwnerInstanceID == captorInstanceId);
        }

        /// <summary>
        /// Returns units directly on a planet or recursively carried by a fleet or ship.
        /// </summary>
        /// <typeparam name="T">The unit type to return.</typeparam>
        /// <param name="custodyContext">The planet, fleet, or ship holding the captive.</param>
        /// <returns>The units guarding the captive.</returns>
        private static IReadOnlyList<T> GetCustodyUnits<T>(ContainerNode custodyContext)
            where T : class, ISceneNode
        {
            bool recursive = custodyContext is Fleet or CapitalShip;
            return custodyContext.GetChildren<T>(recursive);
        }
    }
}
