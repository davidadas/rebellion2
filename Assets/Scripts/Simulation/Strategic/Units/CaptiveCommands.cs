using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Establishes custody, releases officers, and evaluates scheduled escape attempts.
    /// Escape probability is based on the officer's skills and the forces guarding
    /// the planet, fleet, or ship where the officer is held.
    /// </summary>
    public class CaptiveCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementCommands _movementCommands;
        private readonly ProbabilityTable _escapeTable;
        private readonly GameConfig.TickRangeConfig _escapeAttemptInterval;

        /// <summary>Raised when an immediate capture or release produces a result.</summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates the custody and escape operations for the active game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">RNG provider for escape rolls.</param>
        /// <param name="movementCommands">Moves officers into and out of custody.</param>
        public CaptiveCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movementCommands
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _movementCommands =
                movementCommands ?? throw new ArgumentNullException(nameof(movementCommands));
            _escapeAttemptInterval = game.Config.Captive.EscapeAttemptInterval;
            _escapeTable = new ProbabilityTable(game.Config.Captive.EscapeTable);
        }

        /// <summary>
        /// Attempts to capture a registered officer and publishes the resulting custody change.
        /// </summary>
        /// <param name="officer">The officer to capture.</param>
        /// <param name="captor">The faction taking custody.</param>
        /// <param name="capturingUnit">The unit responsible for the capture, when present.</param>
        /// <param name="canEscape">Whether the officer may attempt to escape.</param>
        /// <returns>True when the officer was captured.</returns>
        public bool TryCaptureOfficer(
            Officer officer,
            Faction captor,
            ISceneNode capturingUnit = null,
            bool canEscape = true
        )
        {
            Officer liveOfficer = ResolveOfficer(officer);
            Faction liveCaptor = ResolveFaction(captor);
            if (liveOfficer == null || liveCaptor == null)
                return false;

            Planet context =
                capturingUnit?.GetParentOfType<Planet>() ?? liveOfficer.GetParentOfType<Planet>();
            OfficerCaptureStateResult result = CaptiveStateTransition.Capture(
                liveOfficer,
                liveCaptor,
                capturingUnit,
                context,
                _game.CurrentTick,
                canEscape
            );
            if (result == null)
                return false;

            ResultsProduced?.Invoke(new GameResult[] { result });
            return true;
        }

        /// <summary>
        /// Releases a registered captive officer and publishes the custody change.
        /// </summary>
        /// <param name="officer">The officer to release.</param>
        /// <returns>True when the officer was released.</returns>
        public bool TryReleaseOfficer(Officer officer)
        {
            Officer liveOfficer = ResolveOfficer(officer);
            if (liveOfficer?.IsCaptured != true)
                return false;

            OfficerCaptureStateResult result = CaptiveStateTransition.Release(
                liveOfficer,
                liveOfficer.GetParentOfType<Planet>(),
                _game.CurrentTick,
                liveOfficer.CaptorInstanceID
            );
            ResultsProduced?.Invoke(new GameResult[] { result });
            return true;
        }

        /// <summary>
        /// Processes one officer's escape attempt when it is due.
        /// </summary>
        /// <param name="officer">The officer whose captive state should advance.</param>
        /// <param name="results">The collection receiving a successful escape.</param>
        internal void ProcessEscapeAttempt(Officer officer, List<GameResult> results)
        {
            if (!officer.IsCaptured || !officer.CanEscape || officer.IsKilled)
            {
                officer.NextEscapeAttemptTick = 0;
                return;
            }

            if (officer.NextEscapeAttemptTick <= 0)
            {
                ScheduleEscapeAttempt(officer);
                return;
            }

            if (_game.CurrentTick < officer.NextEscapeAttemptTick)
                return;

            ScheduleEscapeAttempt(officer);

            ContainerNode custodyContext = GetCustodyContext(officer);
            Planet planet = officer.GetParentOfType<Planet>();
            if (
                custodyContext == null
                || planet == null
                || ((IMovable)officer).GetTransitMovement() != null
            )
                return;

            if (RollEscapeAttempt(officer, custodyContext))
            {
                OfficerCaptureStateResult result = TryEscapeCustody(officer, planet);
                if (result != null)
                    results.Add(result);
            }
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
        private OfficerCaptureStateResult TryEscapeCustody(Officer officer, Planet planet)
        {
            string captorInstanceID = officer.CaptorInstanceID;
            officer.IsCaptured = false;
            officer.CaptorInstanceID = null;

            Faction faction = _game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            ContainerNode destination = null;
            foreach (ContainerNode candidate in GetEscapeDestinations(faction, officer, planet))
            {
                if (!_movementCommands.TryRequestMove(officer, candidate))
                    continue;

                destination = candidate;
                break;
            }
            if (destination == null)
            {
                officer.IsCaptured = true;
                officer.CaptorInstanceID = captorInstanceID;
                return null;
            }

            return CaptiveStateTransition.Release(
                officer,
                planet,
                _game.CurrentTick,
                captorInstanceID
            );
        }

        /// <summary>
        /// Resolves an officer argument to the officer registered with the active game.
        /// </summary>
        /// <param name="officer">The officer or snapshot to resolve.</param>
        /// <returns>The registered officer, or null when it cannot be resolved.</returns>
        private Officer ResolveOfficer(Officer officer)
        {
            return string.IsNullOrEmpty(officer?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Officer>(
                    officer.InstanceID,
                    includeDisabled: true
                );
        }

        /// <summary>
        /// Resolves a faction argument to the faction registered with the active game.
        /// </summary>
        /// <param name="faction">The faction to resolve.</param>
        /// <returns>The registered faction, or null when it cannot be resolved.</returns>
        private Faction ResolveFaction(Faction faction)
        {
            return string.IsNullOrEmpty(faction?.InstanceID)
                ? null
                : _game
                    .GetFactions()
                    .FirstOrDefault(candidate => candidate.InstanceID == faction.InstanceID);
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
            return officer.GetEffectiveRating(SkillRating.Espionage)
                + officer.GetEffectiveRating(SkillRating.Combat);
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

            return guards.Sum(g => g.GetEffectiveRating(SkillRating.Combat)) / guards.Count;
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
