using System;
using System.Collections.Generic;
using Rebellion.Content;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Applies manual battle loyalty and sector support after campaign loss reactions settle.
    /// </summary>
    public sealed class SpaceCombatAftermath
    {
        private readonly GameRoot _game;
        private readonly TacticalCombatDefinition _definition;
        private readonly PersonnelCommands _personnel;

        /// <summary>
        /// Creates the campaign aftermath owner for manually played space battles.
        /// </summary>
        /// <param name="game">The campaign whose retained losses and current military values are read.</param>
        /// <param name="definition">The authored faction-index mapping.</param>
        /// <param name="personnel">The commands applying loyalty changes.</param>
        public SpaceCombatAftermath(
            GameRoot game,
            TacticalCombatDefinition definition,
            PersonnelCommands personnel
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _personnel = personnel ?? throw new ArgumentNullException(nameof(personnel));
        }

        /// <summary>
        /// Requires complete authored inputs before the campaign admits a manual encounter.
        /// </summary>
        /// <param name="planet">The encounter's campaign location.</param>
        internal void ValidateBattle(Planet planet)
        {
            if (
                _game.Config.SupportShift.BattleSupport?.TryResolveProfile(
                    _game,
                    _definition,
                    out _,
                    out var factions
                ) != true
            )
                throw new InvalidOperationException(
                    "The campaign has no complete battle support profile."
                );
            PlanetSector sector = planet?.GetParentOfType<PlanetSector>();
            if (sector == null)
                throw new InvalidOperationException("The combat planet has no sector.");
            if (
                factions.First.Settings.BattleLossLoyaltyDivisor == 0
                || factions.Second.Settings.BattleLossLoyaltyDivisor == 0
            )
                throw new InvalidOperationException(
                    "Battle loyalty requires a nonzero configured divisor."
                );
        }

        /// <summary>
        /// Applies a single loyalty change and posts sector support for each settled manual encounter.
        /// </summary>
        /// <param name="results">The settled encounter results.</param>
        /// <returns>The support requests admitted before their ownership reactions execute.</returns>
        internal List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            List<GameResult> reactions = null;
            foreach (GameResult item in results)
            {
                if (item is not SpaceCombatResult { IsTacticalCombat: true } result)
                    continue;
                if (result.RemainingSide is not CombatSide remaining)
                    continue;

                if (
                    _game.Config.SupportShift.BattleSupport?.TryResolveProfile(
                        _game,
                        _definition,
                        out SpaceCombatSupportProfile profile,
                        out var factions
                    ) != true
                )
                    throw new InvalidOperationException(
                        "The campaign has no complete battle support profile."
                    );

                (Faction first, Faction second) = factions;
                string firstID = first.InstanceID;
                string secondID = second.InstanceID;
                string controllerID = result.GetOwner(remaining);
                int survivingSide =
                    controllerID == firstID ? 1
                    : controllerID == secondID ? 2
                    : 0;
                if (survivingSide == 0)
                    throw new InvalidOperationException(
                        "The surviving combat faction has no authored battle index."
                    );

                (int firstLoss, int secondLoss) = CaptureLostMilitaryValues(
                    result.Events,
                    firstID,
                    secondID
                );
                Faction controller = survivingSide == 1 ? first : second;
                int loyalty = PersonnelCommands.CalculateBattleLossLoyalty(
                    survivingSide == 1 ? firstLoss : secondLoss,
                    survivingSide == 1 ? secondLoss : firstLoss,
                    controller.Settings.BattleLossLoyaltyDivisor
                );

                (int firstTotal, int secondTotal) = CaptureMilitaryValues(_game, firstID, secondID);
                SpaceCombatSupport.TryCalculate(
                    firstTotal,
                    secondTotal,
                    firstLoss,
                    secondLoss,
                    survivingSide,
                    profile.FirstSide,
                    profile.SecondSide,
                    profile.Divisor.Value,
                    out SpaceCombatSupport.Adjustment adjustment
                );

                List<GameResult> support = CreateSupportRequests(result, first, second, adjustment);
                _personnel.ApplyGlobalLoyaltyShift(
                    controller,
                    loyalty,
                    survivingSide == 1 ? second : first
                );
                if (support.Count > 0)
                {
                    reactions ??= new List<GameResult>();
                    reactions.AddRange(support);
                }
            }
            return reactions;
        }

        /// <summary>
        /// Captures both requested owners' current full military values without scene traversal or allocation.
        /// </summary>
        /// <param name="game">The campaign supplying its live scene index.</param>
        /// <param name="firstOwnerInstanceId">The first owner identity, or null for neutral ownership.</param>
        /// <param name="secondOwnerInstanceId">The second owner identity, or null for neutral ownership.</param>
        /// <returns>The current completed and delivering military values in the requested owner order.</returns>
        internal static (int FirstSide, int SecondSide) CaptureMilitaryValues(
            GameRoot game,
            string firstOwnerInstanceId,
            string secondOwnerInstanceId
        )
        {
            int first = 0;
            int second = 0;
            foreach (ISceneNode unit in game.NodesByInstanceID.Values)
            {
                if (
                    unit
                    is not IManufacturable
                    {
                        ManufacturingStatus: ManufacturingStatus.Complete
                            or ManufacturingStatus.Delivering
                    }
                )
                    continue;
                int value = CombatUnitSnapshot.GetMilitaryValue(unit);
                string owner = unit.OwnerInstanceID;
                if (owner == firstOwnerInstanceId)
                    first = unchecked(first + value);
                if (owner == secondOwnerInstanceId)
                    second = unchecked(second + value);
            }
            return (first, second);
        }

        internal static (int FirstSide, int SecondSide) CaptureLostMilitaryValues(
            IEnumerable<GameResult> events,
            string firstOwner,
            string secondOwner
        )
        {
            int firstLoss = 0;
            int secondLoss = 0;
            foreach (GameResult item in events)
            {
                if (
                    item
                    is not GameObjectDestroyedResult
                    {
                        CountsAsMilitaryLoss: true,
                        DestroyedObject: ISceneNode unit,
                    }
                )
                    continue;
                int value = CombatUnitSnapshot.GetMilitaryValue(unit);
                string owner = unit.OwnerInstanceID;
                if (owner == firstOwner)
                    firstLoss = unchecked(firstLoss + value);
                if (owner == secondOwner)
                    secondLoss = unchecked(secondLoss + value);
            }
            return (firstLoss, secondLoss);
        }

        /// <summary>
        /// Selects populated sector planets whose counterpart support falls below the threshold.
        /// </summary>
        /// <param name="result">The completed encounter selecting the sector and tick.</param>
        /// <param name="first">The first authored faction, relative to which support shifts are signed.</param>
        /// <param name="second">The second authored faction.</param>
        /// <param name="adjustment">The calculated support rule.</param>
        /// <returns>The admitted support requests.</returns>
        private static List<GameResult> CreateSupportRequests(
            SpaceCombatResult result,
            Faction first,
            Faction second,
            SpaceCombatSupport.Adjustment adjustment
        )
        {
            List<GameResult> requests = new();
            if (adjustment.Magnitude == 0)
                return requests;
            PlanetSector sector = result.Planet?.GetParentOfType<PlanetSector>();
            if (sector == null)
                throw new InvalidOperationException("The combat planet has no sector.");
            Faction comparison = adjustment.ComparisonSide == 1 ? first : second;
            int shift =
                adjustment.BeneficiarySide == 1
                    ? unchecked((int)adjustment.Magnitude)
                    : unchecked(-(int)adjustment.Magnitude);
            foreach (Planet planet in sector.GetChildren<Planet>())
            {
                if (
                    !planet.IsPopulated()
                    || planet.GetPopularSupport(comparison.InstanceID)
                        >= unchecked((int)adjustment.SupportThreshold)
                )
                    continue;
                requests.Add(
                    new PopularSupportShiftResult
                    {
                        Planet = planet,
                        Faction = first,
                        Shift = shift,
                        Tick = result.Tick,
                    }
                );
            }
            return requests;
        }
    }
}
