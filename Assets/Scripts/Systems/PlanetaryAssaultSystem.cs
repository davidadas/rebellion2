using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Coordinates planetary-assault commands and applies their resolved outcomes.
    /// </summary>
    public class PlanetaryAssaultSystem
    {
        private readonly GameRoot _game;
        private readonly PlanetaryControlSystem _ownership;
        private readonly PlanetaryAssaultResolver _resolver;

        /// <summary>
        /// Raised after an immediate planetary-assault command produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates the planetary-assault system.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random-number provider used by assault resolution.</param>
        /// <param name="ownership">The planetary control system used to capture planets.</param>
        public PlanetaryAssaultSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem ownership
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            _resolver = new PlanetaryAssaultResolver(game.Config.Combat.PlanetaryAssault, provider);
        }

        /// <summary>
        /// Executes a validated planetary-assault command and publishes its results.
        /// </summary>
        /// <param name="attackingFleets">The attacking fleets.</param>
        /// <param name="targetPlanet">The assault target planet.</param>
        /// <returns>The assault result, or null when the assault cannot execute.</returns>
        public PlanetaryAssaultResult TryExecute(
            IReadOnlyList<Fleet> attackingFleets,
            Planet targetPlanet
        )
        {
            if (targetPlanet == null)
                return null;

            List<Fleet> fleets =
                attackingFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
            if (!CanExecute(fleets, targetPlanet))
                return null;

            PlanetaryAssaultResult result = Execute(fleets, targetPlanet);
            List<GameResult> results = new List<GameResult> { result };
            results.AddRange(result.Events);
            if (result.OwnershipChange != null)
                results.Add(result.OwnershipChange);

            ResultsProduced?.Invoke(results);
            return result;
        }

        /// <summary>
        /// Runs the planetary-assault pipeline against a defending planet.
        /// </summary>
        /// <param name="attackingFleets">The fleets performing the assault.</param>
        /// <param name="defendingPlanet">The planet being assaulted.</param>
        /// <returns>The assault outcome and applied game-state changes.</returns>
        public PlanetaryAssaultResult Execute(List<Fleet> attackingFleets, Planet defendingPlanet)
        {
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = defendingPlanet,
                Tick = _game.CurrentTick,
            };

            if (!CanAssault(attackingFleets, defendingPlanet))
                return result;

            string attackerId = attackingFleets[0].GetOwnerInstanceID();
            string defenderId = defendingPlanet.GetOwnerInstanceID();
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerId);
            result.AttackerOwnerInstanceID = attackerId;
            result.DefenderOwnerInstanceID = defenderId;
            result.AttackingUnits.AddRange(CombatUnitSnapshot.CaptureFleetUnits(attackingFleets));
            result.DefendingUnits.AddRange(
                CombatUnitSnapshot.CapturePlanetUnits(defendingPlanet, defenderId)
            );

            if (
                PlanetaryAssaultResolver.IsBlockedByShields(
                    defendingPlanet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
            )
            {
                result.BlockedByShields = true;
                return result;
            }

            if (!PlanetaryAssaultResolver.HasReadyAttackers(attackingFleets))
                return result;

            SetAssaultCombatState(attackingFleets, defendingPlanet, true);
            try
            {
                PlanetaryAssaultResult resolvedResult = _resolver.Resolve(
                    attackingFleets,
                    defendingPlanet
                );
                resolvedResult.Planet = result.Planet;
                resolvedResult.Tick = result.Tick;
                resolvedResult.AttackingFaction = result.AttackingFaction;
                resolvedResult.AttackerOwnerInstanceID = result.AttackerOwnerInstanceID;
                resolvedResult.DefenderOwnerInstanceID = result.DefenderOwnerInstanceID;
                resolvedResult.AttackingUnits = result.AttackingUnits;
                resolvedResult.DefendingUnits = result.DefendingUnits;
                result = resolvedResult;

                ApplyResult(result, defendingPlanet, result.AttackingFaction);

                if (result.DestroyedDefenderRegiments.Count > 0 || result.LandedRegiments.Count > 0)
                {
                    result.Events.Add(
                        new PlanetGarrisonChangedResult
                        {
                            Planet = defendingPlanet,
                            Tick = _game.CurrentTick,
                        }
                    );
                }

                return result;
            }
            finally
            {
                RecordUnitOutcomes(result);
                SetAssaultCombatState(attackingFleets, defendingPlanet, false);
            }
        }

        /// <summary>
        /// Determines whether the supplied fleets can execute a planetary assault.
        /// </summary>
        /// <param name="fleets">The fleets attempting the assault.</param>
        /// <param name="planet">The planet being assaulted.</param>
        /// <returns>True when the fleets contain ready troops and shields do not block them.</returns>
        public bool CanExecute(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            return CanAssault(fleets, planet)
                && !PlanetaryAssaultResolver.IsBlockedByShields(
                    planet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
                && PlanetaryAssaultResolver.HasReadyAttackers(fleets);
        }

        /// <summary>
        /// Determines whether the supplied fleets can begin an assault at the planet.
        /// </summary>
        /// <param name="fleets">The fleets attempting the assault.</param>
        /// <param name="planet">The planet being assaulted.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        private static bool CanAssault(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            if (
                planet?.IsDestroyed != false
                || fleets?.Any() != true
                || fleets.Any(fleet => fleet == null)
            )
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && planet.GetOwnerInstanceID() != ownerId
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.GetParent() == planet
                );
        }

        /// <summary>
        /// Applies a resolved assault result to the scene graph.
        /// </summary>
        /// <param name="result">The resolved assault result.</param>
        /// <param name="planet">The assaulted planet.</param>
        /// <param name="attacker">The faction performing the assault.</param>
        private void ApplyResult(PlanetaryAssaultResult result, Planet planet, Faction attacker)
        {
            foreach (Regiment regiment in result.DestroyedAttackerRegiments)
                _game.DeleteNode(regiment);
            foreach (Regiment regiment in result.DestroyedDefenderRegiments)
                _game.DeleteNode(regiment);
            foreach (Building building in result.CollateralDestroyedBuildings)
                _game.DeleteNode(building);

            planet.EnergyCapacity -= result.EnergyCapacityDamage;
            planet.AllocatedEnergy -= result.AllocatedEnergyDamage;

            if (!result.Success)
                return;

            result.OwnershipChange = _ownership.TransferPlanet(planet, attacker);
            foreach (Regiment regiment in result.LandedRegiments)
                _game.MoveNode(regiment, planet);
        }

        /// <summary>
        /// Records which captured units were destroyed during the assault.
        /// </summary>
        /// <param name="result">The completed planetary-assault result.</param>
        private static void RecordUnitOutcomes(PlanetaryAssaultResult result)
        {
            CombatUnitSnapshot.RecordOutcomes(
                result.AttackingUnits,
                null,
                result.DestroyedAttackerRegiments
            );
            CombatUnitSnapshot.RecordOutcomes(
                result.DefendingUnits,
                null,
                result
                    .DestroyedDefenderRegiments.Cast<ISceneNode>()
                    .Concat(result.CollateralDestroyedBuildings)
            );
        }

        /// <summary>
        /// Sets the combat state for the attacking fleets and fleets stationed at the planet.
        /// </summary>
        /// <param name="attackers">The fleets performing the assault.</param>
        /// <param name="planet">The planet where the assault is occurring.</param>
        /// <param name="isInCombat">Whether the affected fleets are in combat.</param>
        private static void SetAssaultCombatState(
            IEnumerable<Fleet> attackers,
            Planet planet,
            bool isInCombat
        )
        {
            foreach (Fleet fleet in attackers)
                fleet.SetCombatState(isInCombat);

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
                fleet.SetCombatState(isInCombat);
        }
    }
}
