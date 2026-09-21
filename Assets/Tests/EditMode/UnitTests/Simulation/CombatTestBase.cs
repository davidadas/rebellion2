using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    public abstract class CombatTestBase
    {
        /// <summary>
        /// Executes make space combat.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The result of make space combat.</returns>
        protected SpaceCombatCommands MakeSpaceCombat(GameRoot game)
        {
            MovementCommands movement = CreateMovement(game);
            return new SpaceCombatCommands(
                game,
                movement,
                new SpaceCombatQueries(game, new MovementQueries(game))
            );
        }

        /// <summary>
        /// Executes make bombardment.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="rng">The rng.</param>
        /// <returns>The result of make bombardment.</returns>
        protected BombardmentCommands MakeBombardment(GameRoot game, IRandomNumberProvider rng)
        {
            (MovementCommands movement, PlanetaryControlCommands planetaryControl) =
                CreatePlanetaryCombatSystems(game);
            return new BombardmentCommands(
                game,
                rng,
                movement,
                planetaryControl,
                new BombardmentQueries(game)
            );
        }

        /// <summary>
        /// Executes make planetary assault.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="rng">The rng.</param>
        /// <returns>The result of make planetary assault.</returns>
        protected PlanetaryAssaultCommands MakePlanetaryAssault(
            GameRoot game,
            IRandomNumberProvider rng
        )
        {
            (_, PlanetaryControlCommands planetaryControl) = CreatePlanetaryCombatSystems(game);
            return new PlanetaryAssaultCommands(
                game,
                rng,
                planetaryControl,
                new PlanetaryAssaultQueries(game)
            );
        }

        /// <summary>
        /// Creates movement.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The created movement.</returns>
        private static MovementCommands CreateMovement(GameRoot game)
        {
            return new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
        }

        /// <summary>
        /// Creates planetary combat systems.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The created planetary combat systems.</returns>
        private static (
            MovementCommands movement,
            PlanetaryControlCommands planetaryControl
        ) CreatePlanetaryCombatSystems(GameRoot game)
        {
            FogOfWarCommands fogOfWar = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fogOfWar,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                movement,
                new ManufacturingCommands(
                    game,
                    new FleetCommands(game),
                    new ManufacturingQueries(game)
                ),
                fogOfWar,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            return (movement, planetaryControl);
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        protected GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = "empire",
                        Settings = new FactionSettings
                        {
                            InvertSupportShift = true,
                            SupportResistance = SupportChange.Decrease,
                            CivilianBombardmentCoreSupportPenalty = -3,
                            CivilianBombardmentOuterRimSupportPenalty = -1,
                            Headquarters = new HeadquartersSettings { IsBombardable = false },
                        },
                    }
                );
            game.GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = "alliance",
                        Settings = new FactionSettings
                        {
                            InvertSupportShift = false,
                            SupportResistance = SupportChange.Increase,
                            CivilianBombardmentCoreSupportPenalty = -4,
                            CivilianBombardmentOuterRimSupportPenalty = -2,
                            Headquarters = new HeadquartersSettings { IsBombardable = true },
                        },
                    }
                );
            return game;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="id">The id.</param>
        /// <param name="owner">The owner.</param>
        /// <param name="energy">The energy.</param>
        /// <returns>The created planet.</returns>
        protected (Planet planet, PlanetSector planetSector) CreatePlanet(
            GameRoot game,
            string id,
            string owner = null,
            int energy = 5
        )
        {
            PlanetSector planetSector = new PlanetSector { InstanceID = $"sector_{id}" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
                EnergyCapacity = energy,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", 50 },
                    { "alliance", 50 },
                },
            };
            game.AttachNode(planet, planetSector);
            return (planet, planetSector);
        }

        /// <summary>
        /// Random-number provider that throws from every roll.
        /// </summary>
        protected class ThrowingRNG : IRandomNumberProvider
        {
            /// <summary>
            /// Throws when a double roll is requested.
            /// </summary>
            /// <returns>This method always throws.</returns>
            public double NextDouble()
            {
                throw new InvalidOperationException("RNG failure");
            }

            /// <summary>
            /// Throws when an integer roll is requested.
            /// </summary>
            /// <param name="min">Minimum roll value.</param>
            /// <param name="max">Maximum roll value.</param>
            /// <returns>This method always throws.</returns>
            public int NextInt(int min, int max)
            {
                throw new InvalidOperationException("RNG failure");
            }
        }
    }
}
