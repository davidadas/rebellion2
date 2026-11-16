using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Places unmodified authored combat units into an otherwise canonically generated campaign.
    /// </summary>
    internal static class EosTacticalScenario
    {
        internal const string PlanetID = "eos-tactical-arena";

        /// <summary>
        /// Builds the negotiated campaign with its complete authored officer and story prerequisites.
        /// </summary>
        /// <param name="content">The real verified active content pack.</param>
        /// <param name="summary">The agreed host launch settings.</param>
        /// <param name="options">The explicitly requested authored fleet counts.</param>
        /// <returns>The complete campaign containing one additional legal contested system.</returns>
        internal static GameRoot Create(
            ContentPack content,
            GameSummary summary,
            EosTacticalIntegrationOptions options
        )
        {
            GameRoot game = new GameBuilder(summary, content.GameData).Build();
            PlanetSector sector = game.GetGalaxyMap().GetChildren<PlanetSector>().First();
            Planet planet = new()
            {
                InstanceID = PlanetID,
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
                PositionX = 15,
                PositionY = 15,
            };
            planet.SetPopularSupport("FNALL1", 100);
            game.AttachNode(planet, sector);
            var data = content.GameData;
            UnitFactory factory = new(
                data.Buildings,
                data.CapitalShips,
                data.Starfighters,
                data.Regiments,
                data.SpecialForces,
                data.TacticalCombat
            );
            foreach (string faction in new[] { "FNEMP1", "FNALL1" })
            {
                Fleet fleet = new(faction, "EOS tactical carrier")
                {
                    InstanceID = "eos-fleet-" + faction,
                };
                game.AttachNode(fleet, planet);
                List<CapitalShip> carriers = new();
                for (int index = 0; index < options.CapitalsPerSide; index++)
                {
                    CapitalShip carrier = factory.Create<CapitalShip>(
                        faction == "FNEMP1" ? "CSEM006" : "ALCS008",
                        faction
                    );
                    carrier.InstanceID = "eos-carrier-" + faction + "-" + index;
                    game.AttachNode(carrier, fleet);
                    carriers.Add(carrier);
                }
                if (carriers.Sum(carrier => carrier.StarfighterCapacity) < options.SquadronsPerSide)
                    throw new System.InvalidOperationException(
                        "The requested squadrons exceed the authored carrier bay capacity."
                    );
                int carrierIndex = 0;
                int occupied = 0;
                for (int index = 0; index < options.SquadronsPerSide; index++)
                {
                    while (occupied >= carriers[carrierIndex].StarfighterCapacity)
                    {
                        carrierIndex++;
                        occupied = 0;
                    }
                    Starfighter fighter = factory.Create<Starfighter>(
                        faction == "FNEMP1" ? "SFEM01" : "SFAL02",
                        faction
                    );
                    fighter.InstanceID = "eos-fighter-" + faction + "-" + index;
                    game.AttachNode(fighter, carriers[carrierIndex]);
                    occupied++;
                }
            }
            return game;
        }
    }
}
