using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary ownership and popular support.
    /// Each tick: applies support recovery/decay, then claims any unowned planet whose
    /// support for a faction has crossed the ownership threshold. Owned planets do not
    /// flip via support alone — they change hands only through conquest (via TransferPlanet,
    /// invoked from CombatSystem).
    /// </summary>
    public class PlanetaryControlSystem
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movementSystem;
        private readonly ManufacturingSystem _manufacturingSystem;

        /// <summary>
        /// Creates a new PlanetaryControlSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movementSystem">Used to evacuate enemy units on ownership change.</param>
        /// <param name="manufacturingSystem">Used to clear queues on ownership change.</param>
        public PlanetaryControlSystem(
            GameRoot game,
            MovementSystem movementSystem,
            ManufacturingSystem manufacturingSystem
        )
        {
            _game = game;
            _movementSystem = movementSystem;
            _manufacturingSystem = manufacturingSystem;
        }

        /// <summary>
        /// Applies support shifts to all owned planets, then checks for ownership transfers.
        /// </summary>
        /// <returns>Any ownership change results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                    continue;

                if (!planet.IsColonized)
                    continue;

                Faction faction = _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
                if (faction == null)
                    continue;

                ApplySupportShift(planet, faction);
            }

            CheckOwnershipTransfers(results);

            return results;
        }

        /// <summary>
        /// Transfers a planet to a new owner.
        /// </summary>
        /// <param name="planet">The planet to transfer.</param>
        /// <param name="newOwner">The faction receiving ownership.</param>
        public void TransferPlanet(Planet planet, Faction newOwner)
        {
            CancelCompetingMissions(planet, newOwner.InstanceID);
            TransferBuildings(planet, newOwner);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            EvictEnemyUnits(planet, newOwner.InstanceID);
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
        }

        /// <summary>
        /// Applies periodic support shift for a single planet.
        /// </summary>
        /// <param name="planet">The planet to apply the shift to.</param>
        /// <param name="faction">The controlling faction.</param>
        private void ApplySupportShift(Planet planet, Faction faction)
        {
            int shift = CalculateSupportShift(planet, faction);
            if (shift == 0)
                return;

            shift = ApplyCoreWeakSupport(planet, faction, shift);

            int currentSupport = planet.GetPopularSupport(faction.InstanceID);
            int newSupport = Math.Max(
                0,
                Math.Min(_game.Config.Planet.MaxPopularSupport, currentSupport + shift)
            );

            if (newSupport != currentSupport)
            {
                planet.SetPopularSupport(
                    faction.InstanceID,
                    newSupport,
                    _game.Config.Planet.MaxPopularSupport
                );
            }
        }

        /// <summary>
        /// Checks all planets for support above the ownership threshold and transfers if needed.
        /// </summary>
        /// <param name="results">Collection to append any ownership change results to.</param>
        private void CheckOwnershipTransfers(List<GameResult> results)
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                if (!planet.IsColonized)
                    continue;

                // Only claim unowned planets — owned planets don't flip via support alone.
                if (!string.IsNullOrEmpty(planet.GetOwnerInstanceID()))
                    continue;

                foreach (Faction faction in _game.GetFactions())
                {
                    int support = planet.GetPopularSupport(faction.InstanceID);
                    if (support <= threshold)
                        continue;

                    TransferPlanet(planet, faction);

                    results.Add(
                        new PlanetOwnershipChangedResult
                        {
                            Planet = planet,
                            PreviousOwner = null,
                            NewOwner = faction,
                            Tick = _game.CurrentTick,
                        }
                    );

                    GameLogger.Log(
                        $"Planet {planet.GetDisplayName()} transferred to {faction.DisplayName} (support {support} > {threshold})"
                    );

                    break;
                }
            }
        }

        /// <summary>
        /// Calculates the periodic support shift for a planet.
        /// Only applies when support is at or below ShiftThreshold and no friendly fleets are present.
        /// Uses a bracket system for base recovery, reduced by hostile force penalties.
        /// Negated for factions with InvertSupportShift set.
        /// </summary>
        /// <param name="planet">The planet to calculate support shift for.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The net support shift to apply, or zero if none.</returns>
        private int CalculateSupportShift(Planet planet, Faction faction)
        {
            GameConfig.SupportShiftConfig config = _game.Config.SupportShift;
            string factionId = faction.InstanceID;
            int support = planet.GetPopularSupport(factionId);

            int friendlyFleetCount = planet
                .GetFleets()
                .Count(f => f.GetOwnerInstanceID() == factionId);

            if (support > config.ShiftThreshold || friendlyFleetCount > 0)
                return 0;

            int baseShift;
            if (support <= config.LowBracketCeiling)
            {
                baseShift = config.LowBracketShift;
            }
            else if (support <= config.MidBracketCeiling)
            {
                baseShift = config.MidBracketShift;
            }
            else
            {
                baseShift = config.HighBracketShift;
            }

            int hostileFleetCount = planet
                .GetFleets()
                .Count(f => f.GetOwnerInstanceID() != null && f.GetOwnerInstanceID() != factionId);
            int hostileFighterCount = planet
                .GetAllStarfighters()
                .Count(s => s.GetOwnerInstanceID() != null && s.GetOwnerInstanceID() != factionId);
            int hostileTroopCount = planet
                .GetAllRegiments()
                .Count(r => r.GetOwnerInstanceID() != null && r.GetOwnerInstanceID() != factionId);

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.TroopEffectiveness > 1
            )
            {
                hostileTroopCount *= faction.Modifiers.TroopEffectiveness;
            }

            int shift =
                baseShift
                - hostileFleetCount * config.FleetPenalty
                - hostileFighterCount * config.FighterPenalty
                - hostileTroopCount * config.TroopPenalty;

            shift = Math.Max(0, Math.Min(100, shift));

            if (faction.Modifiers.InvertSupportShift)
            {
                shift = -shift;
            }

            return shift;
        }

        /// <summary>
        /// Halves the support shift on core systems when the faction's
        /// WeakSupportPenaltyTrigger condition is met.
        /// </summary>
        /// <param name="planet">The planet being evaluated.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="shift">The current support shift value.</param>
        /// <returns>The adjusted shift, halved if the core penalty applies.</returns>
        private int ApplyCoreWeakSupport(Planet planet, Faction faction, int shift)
        {
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (parentSystem == null || parentSystem.SystemType != PlanetSystemType.CoreSystem)
                return shift;

            bool penaltyApplies = faction.Modifiers.WeakSupportPenaltyTrigger switch
            {
                SupportShiftCondition.Positive => shift > 0,
                SupportShiftCondition.Negative => shift < 0,
                _ => false,
            };

            if (penaltyApplies)
            {
                shift /= 2;
            }

            return shift;
        }

        /// <summary>
        /// Cancels missions targeting this planet that belong to factions other than the new owner.
        /// </summary>
        /// <param name="planet">The planet changing ownership.</param>
        /// <param name="newOwnerID">The instance ID of the new owning faction.</param>
        private void CancelCompetingMissions(Planet planet, string newOwnerID)
        {
            List<Mission> competing = _game
                .GetSceneNodesByType<Mission>()
                .Where(m =>
                    m.CanceledOnOwnershipChange
                    && m.OwnerInstanceID != newOwnerID
                    && m.GetParentOfType<Planet>() == planet
                )
                .ToList();

            foreach (Mission mission in competing)
            {
                foreach (IMissionParticipant participant in mission.GetAllParticipants())
                    _movementSystem.EvacuateToNearestFriendlyPlanet(participant);

                _game.DetachNode(mission);
            }
        }

        /// <summary>
        /// Transfers all buildings on the planet to the new owner.
        /// </summary>
        /// <param name="planet">The planet whose buildings are transferred.</param>
        /// <param name="newOwner">The faction receiving ownership of the buildings.</param>
        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(_ => true, recurse: false))
            {
                building.AllowedOwnerInstanceIDs = new List<string> { newOwner.InstanceID };
                _game.ChangeUnitOwnership(building, newOwner.InstanceID);
            }
        }

        /// <summary>
        /// Evacuates non-owner units from the planet to the nearest friendly planet.
        /// </summary>
        /// <param name="planet">The planet to evict enemy units from.</param>
        /// <param name="newOwnerID">The instance ID of the new owning faction.</param>
        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<IMovable> enemies = planet
                .GetChildren<IMovable>(
                    m =>
                        m.GetOwnerInstanceID() != newOwnerID && m is not Fleet && m is not Building,
                    recurse: false
                )
                .ToList();

            foreach (IMovable unit in enemies)
                _movementSystem.EvacuateToNearestFriendlyPlanet(unit);
        }
    }
}
