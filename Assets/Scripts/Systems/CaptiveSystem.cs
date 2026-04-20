using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Processes escape attempts for captured officers each tick.
    /// Escape probability is based on the officer's skills vs the planet's
    /// garrison strength, looked up in the escape table.
    /// </summary>
    public class CaptiveSystem : IGameSystem
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

                Planet planet = officer.GetParentOfType<Planet>();
                if (planet == null)
                    continue;

                if (RollEscapeAttempt(officer, planet))
                    results.Add(ReleaseOfficer(officer, planet));
            }

            return results;
        }

        /// <summary>
        /// Rolls the escape probability based on the officer's skills vs the planet's defenses.
        /// </summary>
        /// <param name="officer">The officer attempting escape.</param>
        /// <param name="planet">The planet the officer is held on.</param>
        /// <returns>True if the escape roll succeeds.</returns>
        private bool RollEscapeAttempt(Officer officer, Planet planet)
        {
            int delta = ComputeEscapeDelta(officer, planet);
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
        /// <param name="planet">The planet the officer is held on.</param>
        /// <returns>The escape delta for table lookup.</returns>
        private int ComputeEscapeDelta(Officer officer, Planet planet)
        {
            // Officer's own skills push toward escape.
            int officerSkills =
                officer.GetSkillValue(MissionParticipantSkill.Espionage)
                + officer.GetEffectiveCombat();

            // Average combat of the planet owner's free officers resists escape.
            string planetOwner = planet.OwnerInstanceID;
            List<Officer> guards = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == planetOwner && !o.IsCaptured && !o.IsKilled)
                .ToList();
            int avgGuardCombat = 0;
            if (guards.Count > 0)
                avgGuardCombat =
                    guards.Sum(g => g.GetSkillValue(MissionParticipantSkill.Combat)) / guards.Count;

            // Number of stationed enemy regiments also resists escape.
            int troopCount = planet
                .GetChildren()
                .OfType<Regiment>()
                .Count(r => r.OwnerInstanceID == planetOwner);

            return officerSkills - avgGuardCombat - troopCount;
        }
    }
}
