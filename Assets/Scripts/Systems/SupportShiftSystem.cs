using System;
using System.Linq;
using Rebellion.Game;

namespace Rebellion.Systems
{
    /// <summary>
    /// Computes and applies periodic popular support recovery for controlled systems.
    /// Support recovers toward the controller when it's low, but hostile forces
    /// (fleets, fighters, troops) reduce the recovery rate.
    /// </summary>
    public class SupportShiftSystem
    {
        private readonly GameRoot game;

        public SupportShiftSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Processes support shifts for all owned planets.
        /// </summary>
        public void ProcessTick()
        {
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
            {
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                    continue;

                if (!planet.IsColonized)
                    continue;

                Faction faction = game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
                if (faction == null)
                    continue;

                int shift = CalculateSupportShift(planet, faction);
                if (shift == 0)
                    continue;

                // Apply core weak support halving
                shift = ApplyCoreWeakSupport(planet, faction, shift);

                // Apply the shift
                int currentSupport = planet.GetPopularSupport(faction.InstanceID);
                int newSupport = Math.Max(
                    0,
                    Math.Min(game.Config.Planet.MaxPopularSupport, currentSupport + shift)
                );

                if (newSupport != currentSupport)
                {
                    planet.SetPopularSupport(
                        faction.InstanceID,
                        newSupport,
                        game.Config.Planet.MaxPopularSupport
                    );
                }
            }
        }

        /// <summary>
        /// Calculates the periodic support shift for a planet.
        /// Only fires when support is low (&lt;= threshold) and no friendly fleets are present.
        /// Uses bracket system: support 0-20 -> base 75, 21-30 -> base 50, 31-40 -> base 25.
        /// Hostile forces subtract from the recovery: fleets*10, fighters*5, troops*2.
        /// Empire-side shifts are negated.
        /// </summary>
        private int CalculateSupportShift(Planet planet, Faction faction)
        {
            GameConfig.SupportShiftConfig config = game.Config.SupportShift;
            string factionId = faction.InstanceID;
            int support = planet.GetPopularSupport(factionId);

            // Support shift only applies when support is low and no friendly fleets present
            int friendlyFleetCount = planet
                .GetFleets()
                .Count(f => f.GetOwnerInstanceID() == factionId);

            if (support > config.ShiftThreshold || friendlyFleetCount > 0)
                return 0;

            // Determine base shift by support bracket
            int baseShift;
            if (support <= config.LowBracketCeiling)
            {
                baseShift = config.LowBracketShift; // 75
            }
            else if (support <= config.MidBracketCeiling)
            {
                baseShift = config.MidBracketShift; // 50
            }
            else
            {
                baseShift = config.HighBracketShift; // 25
            }

            // Count hostile forces
            int hostileFleetCount = planet
                .GetFleets()
                .Count(f => f.GetOwnerInstanceID() != null && f.GetOwnerInstanceID() != factionId);
            int hostileFighterCount = planet
                .GetAllStarfighters()
                .Count(s => s.GetOwnerInstanceID() != null && s.GetOwnerInstanceID() != factionId);
            int hostileTroopCount = planet
                .GetAllRegiments()
                .Count(r => r.GetOwnerInstanceID() != null && r.GetOwnerInstanceID() != factionId);

            // Apply faction troop effectiveness multiplier — only on core systems
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.TroopEffectiveness > 1
            )
            {
                hostileTroopCount *= faction.Modifiers.TroopEffectiveness;
            }

            // Subtract hostile force penalties
            int shift =
                baseShift
                - hostileFleetCount * config.FleetPenalty
                - hostileFighterCount * config.FighterPenalty
                - hostileTroopCount * config.TroopPenalty;

            // Clamp to [0, 100]
            shift = Math.Max(0, Math.Min(100, shift));

            // Invert for factions with inverted support shift
            if (faction.Modifiers.InvertSupportShift)
            {
                shift = -shift;
            }

            return shift;
        }

        /// <summary>
        /// Halves the support shift when it moves in the controller's favor
        /// on a core system. The trigger condition is faction-specific:
        /// Alliance is penalized when shift > 0, Empire when shift &lt; 0.
        /// </summary>
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
    }
}
