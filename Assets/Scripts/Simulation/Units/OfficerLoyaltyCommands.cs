using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies strategic events that change officer loyalty.
    /// </summary>
    public class OfficerLoyaltyCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates officer loyalty operations for the active game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        public OfficerLoyaltyCommands(GameRoot game, IRandomNumberProvider provider = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? game.Random;
        }

        /// <summary>
        /// Applies one signed global loyalty shift relative to the favored faction.
        /// Officers belonging to the favored faction receive the shift; officers belonging to
        /// every other faction receive its inverse. Officers whose loyalty cannot change ignore it.
        /// </summary>
        /// <param name="favoredFaction">The faction for which the shift is positive.</param>
        /// <param name="shift">The signed shift relative to the favored faction.</param>
        public void ApplyGlobalShift(Faction favoredFaction, int shift)
        {
            if (favoredFaction == null)
                throw new ArgumentNullException(nameof(favoredFaction));
            if (shift == 0)
                return;

            foreach (
                Officer officer in _game.GetRegisteredSceneNodesByType<Officer>(
                    includeDisabled: true
                )
            )
            {
                int officerShift =
                    officer.GetOwnerInstanceID() == favoredFaction.InstanceID ? shift : -shift;
                officer.TryAdjustLoyalty(officerShift);
            }
        }

        /// <summary>
        /// Applies the officer-loyalty reaction to a completed space combat when exactly one side
        /// remains active.
        /// </summary>
        /// <param name="result">The completed space-combat result.</param>
        public void ApplyBattleLoss(SpaceCombatResult result)
        {
            if (!TryGetExclusiveSpaceController(result, out string controllerId))
                return;

            bool controllerIsAttacker = controllerId == result.AttackerOwnerInstanceID;
            int controllerLoss = GetDestroyedUnitValue(
                controllerIsAttacker ? result.AttackingUnits : result.DefendingUnits
            );
            int opponentLoss = GetDestroyedUnitValue(
                controllerIsAttacker ? result.DefendingUnits : result.AttackingUnits
            );
            ApplyBattleLossShift(controllerId, controllerLoss, opponentLoss);
        }

        /// <summary>
        /// Applies the officer-loyalty reaction to the destroyed regiments from a completed
        /// planetary assault.
        /// </summary>
        /// <param name="result">The completed planetary-assault result.</param>
        public void ApplyBattleLoss(PlanetaryAssaultResult result)
        {
            string controllerId = result?.Planet?.GetOwnerInstanceID();
            if (!IsCombatant(controllerId, result))
                return;

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

        /// <summary>
        /// Applies the officer-loyalty reaction to eligible losses from a completed bombardment.
        /// Destroyed facilities do not contribute.
        /// </summary>
        /// <param name="result">The completed bombardment result.</param>
        public void ApplyBattleLoss(BombardmentResult result)
        {
            string controllerId =
                result?.PlanetDestroyed == true
                    ? result.AttackerOwnerInstanceID
                    : result?.Planet?.GetOwnerInstanceID();
            if (!IsCombatant(controllerId, result))
                return;

            bool controllerIsAttacker = controllerId == result.AttackerOwnerInstanceID;
            int attackerLoss = GetDestroyedUnitValue(result.DestroyedCapitalShips);
            int defenderLoss = GetDestroyedUnitValue(result.DestroyedRegiments);
            ApplyBattleLossShift(
                controllerId,
                controllerIsAttacker ? attackerLoss : defenderLoss,
                controllerIsAttacker ? defenderLoss : attackerLoss
            );
        }

        /// <summary>
        /// Resolves whether an eligible mission participant betrays the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <param name="results">Receives the results.</param>
        /// <returns>True when the officer betrays the mission; otherwise false.</returns>
        public bool TryResolveMissionBetrayal(Mission mission, out List<GameResult> results)
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));

            results = new List<GameResult>();
            Officer defector = FindBetrayingOfficer(mission);
            if (defector == null)
                return false;

            return true;
        }

        /// <summary>
        /// Returns the first eligible participant whose loyalty roll causes them to betray the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <returns>The matching betraying officer.</returns>
        private Officer FindBetrayingOfficer(Mission mission) =>
            mission.GetAllParticipants().OfType<Officer>().FirstOrDefault(BetraysMission);

        /// <summary>
        /// Determines whether an eligible officer betrays a mission using inverse loyalty as
        /// the percentage chance.
        /// </summary>
        /// <param name="officer">The officer.</param>
        /// <returns>True when the officer betrays the mission; otherwise false.</returns>
        private bool BetraysMission(Officer officer)
        {
            if (officer is not { IsCaptured: false, IsKilled: false })
                return false;

            int probability = 100 - Math.Clamp(officer.Loyalty, 0, 100);
            return _provider.NextInt(0, 100) < probability;
        }

        /// <summary>Determines whether exactly one space-combat side remains active.</summary>
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

        /// <summary>Returns whether an owner participated in a planetary assault.</summary>
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

        /// <summary>Returns whether an owner participated in a bombardment.</summary>
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

        /// <summary>Totals eligible values for destroyed combat snapshots.</summary>
        /// <param name="snapshots">The combat snapshots to inspect.</param>
        /// <returns>The eligible destroyed-unit value.</returns>
        private static int GetDestroyedUnitValue(IEnumerable<CombatUnitSnapshot> snapshots)
        {
            return (snapshots ?? Enumerable.Empty<CombatUnitSnapshot>())
                .Where(snapshot => snapshot?.Destroyed == true)
                .Sum(snapshot => GetUnitValue(snapshot.Unit));
        }

        /// <summary>Totals eligible values for destroyed live-unit records.</summary>
        /// <typeparam name="T">The scene-node type represented by the records.</typeparam>
        /// <param name="units">The destroyed live-unit records.</param>
        /// <returns>The eligible destroyed-unit value.</returns>
        private static int GetDestroyedUnitValue<T>(IEnumerable<T> units)
            where T : class, ISceneNode
        {
            return (units ?? Enumerable.Empty<T>()).Sum(GetUnitValue);
        }

        /// <summary>Returns the authored value for an eligible destroyed-unit family.</summary>
        /// <param name="unit">The unit to evaluate.</param>
        /// <returns>The unit's authored value, or zero for an ineligible family.</returns>
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

        /// <summary>Calculates and applies the configured battle-loss loyalty formula.</summary>
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
            ApplyGlobalShift(controller, shift);
        }
    }
}
