using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages victory condition checking during each game tick.
    /// </summary>
    public class VictorySystem : IGameResultHandler<HeadquartersDestroyedResult>
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a new VictoryManager.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public VictorySystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Checks victory conditions for the current tick and returns any triggered results.
        /// </summary>
        /// <returns>Any victory results triggered this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            foreach (Faction faction in _game.Factions)
            {
                VictoryResult outcome = CheckHQCapture(faction);
                if (outcome != null)
                {
                    GameLogger.Log(
                        $"Victory condition met: {outcome.Winner.GetDisplayName()} defeated {outcome.Loser.GetDisplayName()}."
                    );
                    return new List<GameResult> { outcome };
                }
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Applies the configured victory condition after a mobile headquarters is destroyed.
        /// </summary>
        /// <param name="results">The headquarters destruction results.</param>
        /// <returns>Any victories caused by the headquarters losses.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<HeadquartersDestroyedResult> results)
        {
            return (results ?? Array.Empty<HeadquartersDestroyedResult>())
                .Where(result => result?.Attacker != null && result.Defender != null)
                .Select(result => BuildHQVictory(result.Attacker, result.Defender))
                .Where(result => result != null)
                .Cast<GameResult>()
                .ToList();
        }

        /// <summary>
        /// Checks if a faction's HQ has been captured.
        /// </summary>
        /// <param name="defender">The faction to check for HQ capture.</param>
        /// <returns>A victory result if the HQ was captured, or null.</returns>
        private VictoryResult CheckHQCapture(Faction defender)
        {
            if (defender.Settings?.Headquarters?.IsMobile == true)
                return CheckMobileHQCapture(defender);

            string hqInstanceId = defender.GetHQInstanceID();
            if (string.IsNullOrEmpty(hqInstanceId))
                return null;

            Planet hqPlanet = _game.GetSceneNodeByInstanceID<Planet>(hqInstanceId);
            if (hqPlanet == null)
                return null;

            string currentOwner = hqPlanet.GetOwnerInstanceID();

            if (currentOwner == null || currentOwner == defender.InstanceID)
            {
                return null;
            }

            Faction attacker = _game.Factions.FirstOrDefault(f => f.InstanceID == currentOwner);
            if (attacker == null)
                return null;

            return BuildHQVictory(attacker, defender);
        }

        /// <summary>
        /// Checks ownership of a faction's mobile headquarters building.
        /// </summary>
        /// <param name="defender">The faction whose mobile headquarters is checked.</param>
        /// <returns>A victory when an opposing faction owns the headquarters; otherwise null.</returns>
        private VictoryResult CheckMobileHQCapture(Faction defender)
        {
            Planet headquartersPlanet = _game.GetSceneNodeByInstanceID<Planet>(
                defender.HQInstanceID
            );
            Building headquarters = headquartersPlanet
                ?.GetChildren<Building>(_ => true, recurse: false)
                .FirstOrDefault(building =>
                    building.BuildingType == BuildingType.Headquarters && building.Movement == null
                );
            if (
                headquarters == null
                || string.IsNullOrEmpty(headquarters.OwnerInstanceID)
                || headquarters.OwnerInstanceID == defender.InstanceID
            )
                return null;

            Faction attacker = _game.Factions.FirstOrDefault(faction =>
                faction.InstanceID == headquarters.OwnerInstanceID
            );
            return attacker == null ? null : BuildHQVictory(attacker, defender);
        }

        /// <summary>
        /// Builds an HQ victory after applying the selected victory-mode requirements.
        /// </summary>
        /// <param name="attacker">The faction that defeated the headquarters owner.</param>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <returns>A victory when all mode requirements are met; otherwise null.</returns>
        private VictoryResult BuildHQVictory(Faction attacker, Faction defender)
        {
            GameVictoryCondition victoryMode = _game.Summary.VictoryCondition;
            if (
                victoryMode == GameVictoryCondition.Conquest
                && !CheckAllMainCharactersCaptured(defender)
            )
                return null;

            return new VictoryResult
            {
                Winner = attacker,
                Loser = defender,
                GameMode = victoryMode,
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Checks if all main characters (IsMain == true) of a faction are captured.
        /// </summary>
        /// <param name="faction">The faction whose main characters to check.</param>
        /// <returns>True if all main characters are captured or none exist.</returns>
        private bool CheckAllMainCharactersCaptured(Faction faction)
        {
            List<Officer> mainCharacters = _game
                .GetSceneNodesByType<Officer>()
                .Where(o => o.GetOwnerInstanceID() == faction.InstanceID && o.IsMain)
                .ToList();

            if (mainCharacters.Count == 0)
                return true;

            return mainCharacters.All(o => o.IsCaptured);
        }
    }
}
