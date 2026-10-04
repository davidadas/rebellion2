using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates faction victory conditions during a game tick.
    /// </summary>
    internal sealed class VictoryTickProcessor : ITickProcessor
    {
        private readonly VictoryCommands _commands;

        /// <summary>
        /// Creates victory tick processing.
        /// </summary>
        /// <param name="commands">The victory operations and declaration state.</param>
        public VictoryTickProcessor(VictoryCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Evaluates all currently eligible victory conditions.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>No queued results; declarations publish through the victory command.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            if (_commands.IsDeclared)
                return Array.Empty<GameResult>();

            foreach (Faction faction in game.GetFactions())
            {
                if (!TryGetHeadquartersCaptor(game, faction, out Faction attacker))
                    continue;

                VictoryResult outcome = _commands.TryDeclareVictory(attacker, faction);
                if (outcome == null)
                    continue;
                GameLogger.Log(
                    $"Victory condition met: {outcome.Winner.GetDisplayName()} defeated {outcome.Loser.GetDisplayName()}."
                );
                return Array.Empty<GameResult>();
            }

            return Array.Empty<GameResult>();
        }

        /// <summary>Finds the opposing faction currently controlling a faction's headquarters.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="defender">The faction whose headquarters is inspected.</param>
        /// <param name="attacker">Receives the faction controlling the headquarters.</param>
        /// <returns>True when an opposing faction controls the headquarters.</returns>
        private static bool TryGetHeadquartersCaptor(
            GameRoot game,
            Faction defender,
            out Faction attacker
        )
        {
            attacker = null;
            string ownerInstanceId = GetHeadquartersOwner(game, defender);
            if (string.IsNullOrEmpty(ownerInstanceId) || ownerInstanceId == defender.InstanceID)
                return false;

            attacker = game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == ownerInstanceId);
            return attacker != null;
        }

        /// <summary>Gets the current owner of a faction's configured headquarters.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="defender">The faction whose headquarters is inspected.</param>
        /// <returns>The current headquarters owner identifier, or null.</returns>
        private static string GetHeadquartersOwner(GameRoot game, Faction defender)
        {
            Planet planet = game.GetSceneNodeByInstanceID<Planet>(defender.GetHQInstanceID());
            if (defender.Settings?.Headquarters?.IsMobile != true)
                return planet?.GetOwnerInstanceID();

            Building headquarters = planet
                ?.GetChildren<Building>()
                .FirstOrDefault(building =>
                    building.BuildingType == BuildingType.Headquarters && building.Movement == null
                );
            return headquarters?.OwnerInstanceID;
        }
    }
}
