using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies global officer-loyalty reactions to completed battle losses.
    /// </summary>
    public sealed class BattleOfficerLoyaltyObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly OfficerLoyaltyCommands _commands;
        private IDisposable[] _subscriptions;

        /// <summary>Creates the battle-loss loyalty observer.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="commands">The generic officer-loyalty operations.</param>
        public BattleOfficerLoyaltyObserver(GameRoot game, OfficerLoyaltyCommands commands)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers completed battle callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers completed battle results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException(
                    "Battle officer-loyalty observer is already connected."
                );
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<SpaceCombatResult>(HandleResults),
                results.Subscribe<PlanetaryAssaultResult>(HandleResults),
                results.Subscribe<BombardmentResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving battle results.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Applies space-battle losses once exactly one combat side remains active at the system.
        /// </summary>
        /// <param name="results">The completed space combats.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<SpaceCombatResult> results)
        {
            foreach (SpaceCombatResult result in results ?? Array.Empty<SpaceCombatResult>())
            {
                if (!TryGetExclusiveSpaceController(result, out string controllerId))
                    continue;

                bool controllerIsAttacker = controllerId == result.AttackerOwnerInstanceID;
                int controllerLoss = GetDestroyedUnitValue(
                    controllerIsAttacker ? result.AttackingUnits : result.DefendingUnits
                );
                int opponentLoss = GetDestroyedUnitValue(
                    controllerIsAttacker ? result.DefendingUnits : result.AttackingUnits
                );
                ApplyBattleLossShift(controllerId, controllerLoss, opponentLoss);
            }

            return new List<GameResult>();
        }

        /// <summary>Applies destroyed-regiment values after planetary assaults.</summary>
        /// <param name="results">The completed planetary assaults.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetaryAssaultResult> results)
        {
            foreach (
                PlanetaryAssaultResult result in results ?? Array.Empty<PlanetaryAssaultResult>()
            )
            {
                string controllerId = result?.Planet?.GetOwnerInstanceID();
                if (!IsCombatant(controllerId, result))
                    continue;

                bool controllerIsAttacker = controllerId == result.AttackerOwnerInstanceID;
                int controllerLoss = GetDestroyedUnitValue(
                    controllerIsAttacker
                        ? result.DestroyedAttackerRegiments
                        : result.DestroyedDefenderRegiments
                );
                int opponentLoss = GetDestroyedUnitValue(
                    controllerIsAttacker
                        ? result.DestroyedDefenderRegiments
                        : result.DestroyedAttackerRegiments
                );
                ApplyBattleLossShift(controllerId, controllerLoss, opponentLoss);
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Applies destroyed-regiment and attacking-capital-ship values after bombardments.
        /// Destroyed facilities are excluded from battle-loss values.
        /// </summary>
        /// <param name="results">The completed bombardments.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BombardmentResult> results)
        {
            foreach (BombardmentResult result in results ?? Array.Empty<BombardmentResult>())
            {
                string controllerId =
                    result?.PlanetDestroyed == true
                        ? result.AttackerOwnerInstanceID
                        : result?.Planet?.GetOwnerInstanceID();
                if (!IsCombatant(controllerId, result))
                    continue;

                bool controllerIsAttacker = controllerId == result.AttackerOwnerInstanceID;
                int attackerLoss = GetDestroyedUnitValue(result.DestroyedCapitalShips);
                int defenderLoss = GetDestroyedUnitValue(result.DestroyedRegiments);
                ApplyBattleLossShift(
                    controllerId,
                    controllerIsAttacker ? attackerLoss : defenderLoss,
                    controllerIsAttacker ? defenderLoss : attackerLoss
                );
            }

            return new List<GameResult>();
        }

        /// <summary>Determines whether one and only one space-combat side remains active.</summary>
        /// <param name="result">The completed space-combat result.</param>
        /// <param name="controllerId">Receives the surviving faction identifier.</param>
        /// <returns>True when exactly one combat side remains active; otherwise false.</returns>
        private static bool TryGetExclusiveSpaceController(
            SpaceCombatResult result,
            out string controllerId
        )
        {
            controllerId = null;
            if (result == null)
                return false;

            bool attackerActive = result.AttackerOutcome == SpaceCombatSideOutcome.Active;
            bool defenderActive = result.DefenderOutcome == SpaceCombatSideOutcome.Active;
            if (attackerActive == defenderActive)
                return false;

            controllerId = attackerActive
                ? result.AttackerOwnerInstanceID
                : result.DefenderOwnerInstanceID;
            return !string.IsNullOrEmpty(controllerId);
        }

        /// <summary>Returns whether the supplied owner is one of an assault's combatants.</summary>
        /// <param name="ownerInstanceId">The faction identifier to inspect.</param>
        /// <param name="result">The planetary-assault result.</param>
        /// <returns>True when the owner participated in the assault; otherwise false.</returns>
        private static bool IsCombatant(string ownerInstanceId, PlanetaryAssaultResult result)
        {
            return result != null
                && !string.IsNullOrEmpty(ownerInstanceId)
                && (
                    ownerInstanceId == result.AttackerOwnerInstanceID
                    || ownerInstanceId == result.DefenderOwnerInstanceID
                );
        }

        /// <summary>Returns whether the supplied owner is one of a bombardment's combatants.</summary>
        /// <param name="ownerInstanceId">The faction identifier to inspect.</param>
        /// <param name="result">The bombardment result.</param>
        /// <returns>True when the owner participated in the bombardment; otherwise false.</returns>
        private static bool IsCombatant(string ownerInstanceId, BombardmentResult result)
        {
            return result != null
                && !string.IsNullOrEmpty(ownerInstanceId)
                && (
                    ownerInstanceId == result.AttackerOwnerInstanceID
                    || ownerInstanceId == result.DefenderOwnerInstanceID
                );
        }

        /// <summary>Totals loyalty weights for destroyed combat snapshots.</summary>
        /// <param name="snapshots">The combat snapshots to inspect.</param>
        /// <returns>The eligible destroyed-unit value.</returns>
        private static int GetDestroyedUnitValue(IEnumerable<CombatUnitSnapshot> snapshots)
        {
            return (snapshots ?? Enumerable.Empty<CombatUnitSnapshot>())
                .Where(snapshot => snapshot?.Destroyed == true)
                .Sum(snapshot => GetUnitValue(snapshot.Unit));
        }

        /// <summary>Totals loyalty weights for destroyed live-unit records.</summary>
        /// <typeparam name="T">The scene-node type represented by the records.</typeparam>
        /// <param name="units">The destroyed live-unit records.</param>
        /// <returns>The eligible destroyed-unit value.</returns>
        private static int GetDestroyedUnitValue<T>(IEnumerable<T> units)
            where T : class, ISceneNode
        {
            return (units ?? Enumerable.Empty<T>()).Sum(GetUnitValue);
        }

        /// <summary>
        /// Returns the battle-loss value for eligible combat-unit families.
        /// </summary>
        /// <param name="unit">The unit to evaluate.</param>
        /// <returns>The unit's battle-loss value, or zero for an ineligible family.</returns>
        private static int GetUnitValue(ISceneNode unit)
        {
            return unit switch
            {
                Regiment regiment => regiment.UprisingDefense,
                CapitalShip capitalShip => capitalShip.UprisingDefense,
                Starfighter starfighter => starfighter.UprisingDefense,
                _ => 0,
            };
        }

        /// <summary>Calculates and applies the configured two-division battle-loss formula.</summary>
        /// <param name="controllerInstanceId">The final controller faction identifier.</param>
        /// <param name="controllerLoss">The controller's destroyed eligible-unit value.</param>
        /// <param name="opponentLoss">The opponent's destroyed eligible-unit value.</param>
        private void ApplyBattleLossShift(
            string controllerInstanceId,
            int controllerLoss,
            int opponentLoss
        )
        {
            if (controllerLoss == 0 && opponentLoss == 0)
                return;

            Faction controller = _game
                .GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == controllerInstanceId);
            if (controller == null)
                return;

            int divisor = controller.Settings.BattleLossLoyaltyDivisor;
            if (divisor <= 0)
                throw new InvalidOperationException(
                    $"Faction '{controller.InstanceID}' BattleLossLoyaltyDivisor must be greater than zero."
                );

            int shift = (opponentLoss - controllerLoss) / divisor + opponentLoss / divisor;
            if (shift == 0)
                return;

            _commands.ApplyGlobalShift(controller, shift);
        }
    }
}
