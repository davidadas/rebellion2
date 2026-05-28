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
    public class VictorySystem : IGameSystem
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
        /// Checks if a faction's HQ has been captured.
        /// </summary>
        /// <param name="defender">The faction to check for HQ capture.</param>
        /// <returns>A victory result if the HQ was captured, or null.</returns>
        private VictoryResult CheckHQCapture(Faction defender)
        {
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

            GameVictoryCondition victoryMode = _game.Summary.VictoryCondition;

            if (victoryMode == GameVictoryCondition.Conquest)
            {
                if (!CheckAllMainCharactersCaptured(defender))
                {
                    return null;
                }
            }

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
