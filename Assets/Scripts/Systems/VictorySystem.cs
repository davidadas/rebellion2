using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Systems.Results;

/// <summary>
/// Manages victory condition checking during each game tick.
/// Ported from open-rebellion/crates/rebellion-core/src/victory.rs
/// </summary>
namespace Rebellion.Systems
{
    public class VictorySystem
    {
        private readonly GameRoot game;
        private const int MIN_VICTORY_TICK = 200;

        /// <summary>
        /// Creates a new VictoryManager.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public VictorySystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Checks victory conditions for the current tick.
        /// Ported from open-rebellion victory.rs (fleet presence logic),
        /// modified to check planet ownership (not verified against original game).
        /// </summary>
        /// <returns>VictoryResult if a victory condition is met, null otherwise.</returns>
        public VictoryResult? CheckVictory()
        {
            // Grace period: don't check victory until the game has had time to develop
            if (game.CurrentTick < MIN_VICTORY_TICK)
            {
                return null;
            }

            // Check HQ capture for each faction
            foreach (Faction faction in game.Factions)
            {
                VictoryResult? outcome = CheckHQCapture(faction);
                if (outcome != null)
                {
                    return outcome;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a faction's HQ has been captured.
        /// HQ capture = planet ownership changed to enemy faction.
        /// For Conquest mode, also requires all main characters to be captured.
        /// </summary>
        private VictoryResult? CheckHQCapture(Faction defender)
        {
            // Get the defender's HQ planet via Faction.HQInstanceID
            string hqInstanceId = defender.GetHQInstanceID();
            if (string.IsNullOrEmpty(hqInstanceId))
                return null;

            Planet hqPlanet = game.GetSceneNodeByInstanceID<Planet>(hqInstanceId);
            if (hqPlanet == null)
                return null;

            // Check if HQ is now owned by a different faction
            string currentOwner = hqPlanet.GetOwnerInstanceID();

            // No capture if still owned by defender or unowned
            if (currentOwner == null || currentOwner == defender.InstanceID)
            {
                return null;
            }

            // HQ has been captured - find the capturing faction
            Faction attacker = game.Factions.FirstOrDefault(f => f.InstanceID == currentOwner);
            if (attacker == null)
                return null;

            // Check victory mode
            GameVictoryCondition victoryMode = game.Summary.VictoryCondition;

            if (victoryMode == GameVictoryCondition.Conquest)
            {
                // Conquest mode: also check if all main characters are captured
                if (!CheckAllMainCharactersCaptured(defender))
                {
                    return null; // HQ captured but leaders still free
                }
            }

            // Victory condition met
            return new VictoryResult
            {
                Winner = attacker,
                Loser = defender,
                GameMode = victoryMode,
                Tick = game.CurrentTick,
            };
        }

        /// <summary>
        /// Checks if all main characters (IsMain == true) of a faction are captured.
        /// </summary>
        private bool CheckAllMainCharactersCaptured(Faction faction)
        {
            List<Officer> mainCharacters = game.GetSceneNodesByType<Officer>()
                .Where(o => o.GetOwnerInstanceID() == faction.InstanceID && o.IsMain)
                .ToList();

            // If no main characters exist, treat as captured (prevents softlock)
            if (mainCharacters.Count == 0)
                return true;

            // All main characters must be captured
            return mainCharacters.All(o => o.IsCaptured);
        }
    }
}
