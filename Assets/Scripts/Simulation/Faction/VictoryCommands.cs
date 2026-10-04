using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using VictoryResult = Rebellion.Game.Results.VictoryResult;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates and records victory conditions.
    /// </summary>
    public class VictoryCommands
    {
        private readonly GameRoot _game;
        private bool _victoryDeclared;

        internal bool IsDeclared => _victoryDeclared;

        /// <summary>
        /// Creates victory operations for one game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public VictoryCommands(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Checks if a faction's HQ has been captured.
        /// </summary>
        /// <param name="defender">The faction to check for HQ capture.</param>
        /// <returns>A victory result if the HQ was captured, or null.</returns>
        internal VictoryResult CheckHQCapture(Faction defender)
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

            Faction attacker = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == currentOwner);
            if (attacker == null)
                return null;

            return ResolveHeadquartersLoss(attacker, defender);
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
                ?.GetChildren<Building>()
                .FirstOrDefault(building =>
                    building.BuildingType == BuildingType.Headquarters && building.Movement == null
                );
            if (
                headquarters == null
                || string.IsNullOrEmpty(headquarters.OwnerInstanceID)
                || headquarters.OwnerInstanceID == defender.InstanceID
            )
                return null;

            Faction attacker = _game
                .GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == headquarters.OwnerInstanceID);
            return attacker == null ? null : ResolveHeadquartersLoss(attacker, defender);
        }

        /// <summary>
        /// Builds an HQ victory after applying the selected victory-mode requirements.
        /// </summary>
        /// <param name="attacker">The faction that defeated the headquarters owner.</param>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <returns>A victory when all mode requirements are met; otherwise null.</returns>
        public VictoryResult ResolveHeadquartersLoss(Faction attacker, Faction defender)
        {
            if (_victoryDeclared)
                return null;

            GameVictoryCondition victoryMode = _game.Summary.VictoryCondition;
            if (
                victoryMode == GameVictoryCondition.Conquest
                && !CheckAllMainCharactersCaptured(defender)
            )
                return null;

            _victoryDeclared = true;
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
