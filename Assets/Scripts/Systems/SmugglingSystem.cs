using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Systems
{
    /// <summary>
    /// Maintains system smuggling and redirects stolen resource output to its beneficiary.
    /// </summary>
    public sealed class SmugglingSystem
    {
        private const int _percentScale = 100;

        private readonly GameRoot _game;

        /// <summary>
        /// Creates a smuggling system for the supplied game.
        /// </summary>
        /// <param name="game">The current game state.</param>
        public SmugglingSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Recomputes the support- and garrison-driven smuggling percentage.
        /// </summary>
        /// <returns>Results for percentages that changed during this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                Planet planet in _game
                    .GetGalaxyMap()
                    .PlanetSystems.SelectMany(system => system.Planets)
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                RefreshPlanet(planet, results);
            }

            return results;
        }

        /// <summary>
        /// Applies the per-resource smuggling roll to completed output.
        /// </summary>
        /// <param name="controller">The faction that ordinarily receives production.</param>
        /// <param name="facility">The facility producing the resource.</param>
        /// <returns>The controlling faction or the faction benefiting from smuggling.</returns>
        public Faction GetResourceRecipient(Faction controller, Building facility)
        {
            Planet planet = facility.GetParentOfType<Planet>();
            if (
                planet == null
                || planet.SmugglingPercent <= 0
                || _game.Random.NextInt(0, _percentScale) >= planet.SmugglingPercent
            )
                return controller;

            return FindBeneficiary(planet, controller) ?? controller;
        }

        private void RefreshPlanet(Planet planet, List<GameResult> results)
        {
            Faction controller = FindFaction(planet.OwnerInstanceID);
            int oldPercent = planet.SmugglingPercent;
            Faction oldController = FindFaction(planet.SmugglingControllerInstanceID);
            if (oldController == null && oldPercent > 0)
                oldController = controller;

            if (oldPercent > 0 && oldController != controller)
            {
                AddChange(
                    results,
                    planet,
                    oldController,
                    FindBeneficiary(planet, oldController),
                    oldPercent,
                    0
                );
                oldPercent = 0;
                planet.SmugglingPercent = 0;
            }

            Faction beneficiary = FindBeneficiary(planet, controller);
            int newPercent = CalculatePercent(planet, controller, beneficiary);
            if (oldPercent != newPercent)
            {
                AddChange(results, planet, controller, beneficiary, oldPercent, newPercent);
                planet.SmugglingPercent = newPercent;
            }

            planet.SmugglingControllerInstanceID = newPercent > 0 ? controller?.InstanceID : null;
        }

        private void AddChange(
            List<GameResult> results,
            Planet planet,
            Faction controller,
            Faction beneficiary,
            int oldPercent,
            int newPercent
        )
        {
            results.Add(
                new PlanetStatChangedResult
                {
                    Planet = planet,
                    Faction = controller,
                    Stat = PlanetStatType.Smuggling,
                    OldValue = oldPercent,
                    NewValue = newPercent,
                    Tick = _game.CurrentTick,
                }
            );
            if ((oldPercent == 0) == (newPercent == 0))
                return;

            results.Add(
                new SmugglingChangedResult
                {
                    Planet = planet,
                    Controller = controller,
                    Beneficiary = beneficiary,
                    OldPercent = oldPercent,
                    NewPercent = newPercent,
                    Tick = _game.CurrentTick,
                }
            );
        }

        private int CalculatePercent(Planet planet, Faction controller, Faction beneficiary)
        {
            if (
                controller == null
                || beneficiary == null
                || !planet.IsColonized
                || planet.IsDestroyed
            )
                return 0;

            GameConfig.SmugglingConfig config = _game.Config.Smuggling;
            int support = planet.GetPopularSupport(controller.InstanceID);
            if (support > config.MaximumSupport)
                return 0;

            Fleet[] fleets = planet
                .Fleets.Where(fleet =>
                    fleet.OwnerInstanceID == controller.InstanceID && fleet.Movement == null
                )
                .ToArray();
            if (
                fleets
                    .SelectMany(fleet => fleet.CapitalShips)
                    .Any(ship =>
                        IsOperational(ship)
                        && config.FullySuppressingCapitalShipTypeIDs.Contains(ship.TypeID)
                    )
            )
                return 0;

            int percent =
                support <= config.SevereSupportMaximum ? config.SevereLossPercent
                : support <= config.MajorSupportMaximum ? config.MajorLossPercent
                : config.MinorLossPercent;
            int capitalShips = fleets.Sum(fleet => fleet.GetOperationalCapitalShipCount());
            int starfighters =
                planet.Starfighters.Count(starfighter =>
                    starfighter.OwnerInstanceID == controller.InstanceID
                    && starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && starfighter.Movement == null
                )
                + fleets.Sum(fleet =>
                    fleet
                        .GetStarfighters()
                        .Count(starfighter =>
                            starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                );
            int regiments =
                planet.Regiments.Count(regiment =>
                    regiment.OwnerInstanceID == controller.InstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                + fleets.Sum(fleet =>
                    fleet
                        .GetRegiments()
                        .Count(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                );

            return Math.Clamp(
                percent
                    - capitalShips * config.CapitalShipSuppression
                    - starfighters * config.StarfighterSuppression
                    - regiments * config.RegimentSuppression,
                0,
                _percentScale
            );
        }

        private Faction FindBeneficiary(Planet planet, Faction controller)
        {
            if (controller == null)
                return null;

            return _game
                .GetFactions()
                .Where(faction => faction.InstanceID != controller.InstanceID)
                .OrderByDescending(faction => planet.GetPopularSupport(faction.InstanceID))
                .ThenBy(faction => faction.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private Faction FindFaction(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            return _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceId);
        }

        private static bool IsOperational(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null;
        }
    }
}
