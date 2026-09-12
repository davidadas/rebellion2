using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    public abstract class CombatTestBase
    {
        protected SpaceCombatSystem MakeSpaceCombat(GameRoot game)
        {
            return new SpaceCombatSystem(game, CreateMovement(game));
        }

        protected BombardmentSystem MakeBombardment(GameRoot game, IRandomNumberProvider rng)
        {
            (MovementSystem movement, PlanetaryControlSystem planetaryControl) =
                CreatePlanetaryCombatSystems(game);
            return new BombardmentSystem(game, rng, movement, planetaryControl);
        }

        protected PlanetaryAssaultSystem MakePlanetaryAssault(
            GameRoot game,
            IRandomNumberProvider rng
        )
        {
            (_, PlanetaryControlSystem planetaryControl) = CreatePlanetaryCombatSystems(game);
            return new PlanetaryAssaultSystem(game, rng, planetaryControl);
        }

        private static MovementSystem CreateMovement(GameRoot game)
        {
            return new MovementSystem(game, new FogOfWarSystem(game), new FleetSystem(game));
        }

        private static (
            MovementSystem movement,
            PlanetaryControlSystem planetaryControl
        ) CreatePlanetaryCombatSystems(GameRoot game)
        {
            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar, new FleetSystem(game));
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movement,
                new ManufacturingSystem(game, new FleetSystem(game)),
                fogOfWar
            );
            return (movement, planetaryControl);
        }

        protected GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = "empire",
                        PlayerID = null,
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
                        PlayerID = null,
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
