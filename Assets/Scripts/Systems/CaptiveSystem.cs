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
    /// Processes escape attempts for captured officers each tick.
    /// Escape probability is based on the officer's skills and the forces guarding
    /// the planet or fleet where the officer is held.
    /// </summary>
    public class CaptiveSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movementManager;
        private readonly ProbabilityTable _escapeTable;
        private readonly int _loyaltyShift;

        /// <summary>
        /// Creates a new CaptiveSystem.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">RNG provider for escape rolls.</param>
        /// <param name="movementManager">Used to move escaped officers to friendly planets.</param>
        public CaptiveSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movementManager
        )
        {
            _game = game;
            _provider = provider;
            _movementManager = movementManager;
            _escapeTable = new ProbabilityTable(game.Config.Captive.EscapeTable);
            _loyaltyShift = game.Config.Captive.EscapeLoyaltyShift;
        }

        /// <summary>
        /// Processes one tick of escape attempts for all captured officers.
        /// </summary>
        /// <returns>Results for any officers that escaped.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (!officer.IsCaptured || !officer.CanEscape || officer.IsKilled)
                    continue;

                ContainerNode custodyContext = GetCustodyContext(officer);
                Planet planet = officer.GetParentOfType<Planet>();
                if (
                    custodyContext == null
                    || planet == null
                    || officer.GetTransitMovement() != null
                )
                    continue;

                if (RollEscapeAttempt(officer, custodyContext))
                    results.Add(ReleaseOfficer(officer, planet));
            }

            return results;
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
        /// Frees a captured officer: clears capture state, shifts loyalty,
        /// and moves them to the nearest friendly planet.
        /// </summary>
        /// <param name="officer">The officer to release.</param>
        /// <param name="planet">The planet the officer escaped from.</param>
        /// <returns>A capture state result indicating the officer is free.</returns>
        private OfficerCaptureStateResult ReleaseOfficer(Officer officer, Planet planet)
        {
            officer.IsCaptured = false;
            officer.CaptorInstanceID = null;
            officer.CanEscape = false;
            officer.Loyalty = Math.Max(0, Math.Min(100, officer.Loyalty + _loyaltyShift));

            Faction faction = _game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            Planet destination = faction?.GetNearestFriendlyPlanetTo(officer);
            if (destination != null)
                _movementManager.RequestMove(officer, destination);

            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = false,
                Context = planet,
                Tick = _game.CurrentTick,
            };
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
