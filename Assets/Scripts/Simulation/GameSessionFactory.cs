using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Commands;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using Rebellion.Util.Reflection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Constructs the complete runtime graph for one simulation session.
    /// </summary>
    public static class GameSessionFactory
    {
        /// <summary>
        /// Creates a session for the supplied game and content data.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="gameData">The active content data.</param>
        /// <returns>The completed simulation session.</returns>
        public static GameSession Create(GameRoot game, GameDataCatalog gameData)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));

            if (game.Config == null)
                game.SetConfig(gameData.GameConfig);
            game.RebuildSceneState();

            SimulationFeatures features = CreateFeatures(game, gameData);
            RebuildDerivedState(game, gameData, features);
            return Compose(game, features);
        }

        /// <summary>
        /// Composes runtime services around an already-prepared game state.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="gameData">The active content data.</param>
        /// <returns>The completed simulation session without rebuilding derived state.</returns>
        internal static GameSession Compose(GameRoot game, GameDataCatalog gameData)
        {
            return Compose(game, CreateFeatures(game, gameData));
        }

        /// <summary>
        /// Composes public session services around a connected feature graph.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="features">The connected feature graph.</param>
        /// <returns>The completed simulation session.</returns>
        private static GameSession Compose(GameRoot game, SimulationFeatures features)
        {
            GameQueries queries = new GameQueries(game, features);
            GameResults results = new GameResults(features);
            GameTick tick = new GameTick(game, features, results);
            GameClock clock = new GameClock(game);
            return new GameSession(game, features, clock, queries, results, tick);
        }

        /// <summary>
        /// Rebuilds runtime indexes and catalogs that are not persisted.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="gameData">The active content data.</param>
        /// <param name="features">The composed feature graph.</param>
        private static void RebuildDerivedState(
            GameRoot game,
            GameDataCatalog gameData,
            SimulationFeatures features
        )
        {
            IManufacturable[] templates = CopyTemplates(gameData.Buildings)
                .Cast<IManufacturable>()
                .Concat(CopyTemplates(gameData.CapitalShips))
                .Concat(CopyTemplates(gameData.Starfighters))
                .Concat(CopyTemplates(gameData.Regiments))
                .Concat(CopyTemplates(gameData.SpecialForces))
                .ToArray();

            foreach (Faction faction in game.GetFactions())
                faction.RebuildResearchCatalog(templates);

            features.Manufacturing.RebuildQueues();
        }

        /// <summary>
        /// Creates detached template copies with fresh runtime identities.
        /// </summary>
        /// <typeparam name="T">The scene-node template type.</typeparam>
        /// <param name="templates">The templates to copy.</param>
        /// <returns>Detached copies suitable for derived catalogs.</returns>
        private static IEnumerable<T> CopyTemplates<T>(IEnumerable<T> templates)
            where T : class, ISceneNode
        {
            foreach (T template in templates)
            {
                T copy = (T)template.CreateCopy();
                copy.InstanceID = null;
                yield return copy;
            }
        }

        /// <summary>
        /// Builds feature implementations and connects their command handlers.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="gameData">The active content data.</param>
        /// <returns>The connected feature graph.</returns>
        internal static SimulationFeatures CreateFeatures(GameRoot game, GameDataCatalog gameData)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));

            IRandomNumberProvider random = game.Random;
            SimulationFeatures features = new SimulationFeatures
            {
                Commands = new GameCommands(),
                Messages = new Messages(game, gameData.MessageDefinitions.GetDeepCopy()),
                FogOfWar = new FogOfWar(game),
                Blockades = new Blockades(game, random),
                Fleets = new Fleets(game),
                Personnel = new Personnel(game),
                Duels = new Duels(game, random),
            };
            features.Movement = new Movement(
                game,
                features.FogOfWar,
                features.Fleets,
                features.Blockades
            );
            features.Headquarters = new Headquarters(game, features.Movement);
            features.Manufacturing = new Manufacturing(game, features.Fleets, features.Movement);
            features.Naming = new Naming(game);
            features.Recovery = new Recovery(game);
            features.Captives = new Captives(game, random, features.Movement, features.FogOfWar);
            features.Automation = new FactionAutomation(game, gameData, features.Manufacturing);
            features.Maintenance = new Maintenance(game, random, features.Fleets);
            features.Resources = new ResourceProduction(game);
            features.PlanetaryControl = new PlanetaryControl(
                game,
                features.Movement,
                features.Manufacturing,
                features.FogOfWar
            );
            features.Uprisings = new Uprisings(game, random, features.PlanetaryControl);
            features.Jedi = new Jedi(game, random);
            features.OfficerLoyalty = new OfficerLoyalty(game, random);
            features.Missions = new Missions(
                game,
                random,
                features.Movement,
                features.Uprisings,
                features.OfficerLoyalty,
                features.Personnel
            );
            features.SpaceCombat = new SpaceCombat(game, features.Movement);
            features.Bombardment = new Bombardment(
                game,
                random,
                features.Movement,
                features.PlanetaryControl,
                features.Personnel
            );
            features.PlanetaryAssault = new PlanetaryAssault(
                game,
                random,
                features.PlanetaryControl
            );
            features.Research = new Research(game, random);
            features.Victory = new Victory(game);

            features.Commands.Subscribe<MoveUnitsCommand>(features.Movement);
            features.Commands.Subscribe<PlaceUnitsCommand>(features.Movement);
            features.Commands.Subscribe<SetCaptureStatusCommand>(features.Captives);
            features.Commands.Subscribe<SetPopularSupportCommand>(features.PlanetaryControl);
            features.Commands.Subscribe<OwnershipChangeCommand>(features.PlanetaryControl);
            features.Commands.Subscribe<DuelCommand>(features.Duels);
            features.Commands.Subscribe<DeliverMessageCommand>(features.Messages);
            features.Commands.Subscribe<StartManufacturingCommand>(command =>
                Accepted(
                    features.Manufacturing.StartManufacturing(
                        command.Producer,
                        command.Template,
                        command.Destination,
                        command.Count,
                        command.OwnerInstanceID
                    )
                )
            );
            features.Commands.Subscribe<EnqueueManufacturingCommand>(command =>
                ExecuteEnqueue(features.Manufacturing, command)
            );
            features.Commands.Subscribe<RetargetManufacturingCommand>(command =>
                Accepted(
                    features.Manufacturing.RetargetManufacturingDestination(
                        command.Producer,
                        command.Type,
                        command.Destination,
                        command.OwnerInstanceID
                    )
                )
            );
            features.Commands.Subscribe<CancelManufacturingCommand>(command =>
                Accepted(
                    features.Manufacturing.CancelManufacturing(
                        command.Items,
                        command.OwnerInstanceID
                    )
                )
            );
            features.Commands.Subscribe<ScrapUnitsCommand>(command =>
                Accepted(features.Maintenance.TryScrap(command.Items, command.OwnerInstanceID))
            );
            features.Commands.Subscribe<RetirePersonnelCommand>(command =>
                Accepted(features.Personnel.Retire(command.Personnel, command.OwnerInstanceID))
            );
            features.Commands.Subscribe<MoveSelectionCommand>(command =>
                Accepted(
                    features.Movement.TryRequestMove(
                        command.Items,
                        command.Destination,
                        command.OwnerInstanceID
                    )
                )
            );
            features.Commands.Subscribe<EvacuateUnitCommand>(command =>
                ExecuteEvacuation(features.Movement, command)
            );
            features.Commands.Subscribe<RelocateHeadquartersCommand>(command =>
                Accepted(
                    features.Headquarters.TryRelocate(command.Headquarters, command.Destination)
                )
            );
            features.Commands.Subscribe<SetFleetWaypointsCommand>(command =>
                Accepted(
                    features.Movement.TrySetFleetWaypointRoute(
                        command.Items,
                        command.PlanetInstanceIDs,
                        command.OwnerInstanceID
                    )
                )
            );
            features.Commands.Subscribe<ClearFleetWaypointsCommand>(command =>
                Accepted(
                    features.Movement.ClearFleetWaypoints(command.Items, command.OwnerInstanceID)
                )
            );
            features.Commands.Subscribe<CreateFleetCommand>(command =>
                Accepted(
                    features.Fleets.CreateFromCapitalShips(command.Ships, command.OwnerInstanceID)
                        != null
                )
            );
            features.Commands.Subscribe<BombardCommand>(command =>
                ExecuteBombardment(features.Bombardment, command)
            );
            features.Commands.Subscribe<AssaultPlanetCommand>(command =>
                ExecuteAssault(features.PlanetaryAssault, command)
            );
            features.Commands.Subscribe<InitiateMissionCommand>(command =>
                Accepted(features.Missions.InitiateMission(command.Context))
            );
            features.Commands.Subscribe<AbortMissionCommand>(command =>
                Accepted(features.Missions.AbortMission(command.MissionInstanceID))
            );

            UnitFactory unitFactory = new UnitFactory(
                gameData.Buildings,
                gameData.CapitalShips,
                gameData.Starfighters,
                gameData.Regiments,
                gameData.SpecialForces
            );
            features.Events = new GameEvents(game, random, unitFactory, features.Commands);
            features.Events.ValidateEvents(game.GetEventPool());
            features.AI = new AI(
                game,
                features.Commands,
                new GameQueries(game, features),
                random,
                features.FogOfWar
            );
            return features;
        }

        /// <summary>
        /// Creates a result for a command whose feature publishes any factual results directly.
        /// </summary>
        /// <param name="accepted">Whether the command was accepted.</param>
        /// <returns>The command outcome.</returns>
        private static GameCommandResult Accepted(bool accepted)
        {
            return new GameCommandResult(accepted, Array.Empty<GameResult>());
        }

        /// <summary>
        /// Executes a validated bombardment and returns every fact it produced.
        /// </summary>
        /// <param name="bombardment">The bombardment feature.</param>
        /// <param name="command">The bombardment command.</param>
        /// <returns>The raw command outcome.</returns>
        private static GameCommandResult ExecuteBombardment(
            Bombardment bombardment,
            BombardCommand command
        )
        {
            if (!bombardment.CanExecute(command.Fleets, command.Planet, command.Type))
                return Accepted(false);

            BombardmentResult result = bombardment.Execute(
                command.Fleets,
                command.Planet,
                command.Type
            );
            List<GameResult> results = new List<GameResult> { result };
            results.AddRange(result.Events);
            if (result.OwnershipChange != null)
                results.Add(result.OwnershipChange);
            return new GameCommandResult(true, results);
        }

        /// <summary>
        /// Executes a validated planetary assault and returns every fact it produced.
        /// </summary>
        /// <param name="assault">The planetary-assault feature.</param>
        /// <param name="command">The assault command.</param>
        /// <returns>The raw command outcome.</returns>
        private static GameCommandResult ExecuteAssault(
            PlanetaryAssault assault,
            AssaultPlanetCommand command
        )
        {
            if (!assault.CanExecute(command.Fleets, command.Planet))
                return Accepted(false);

            PlanetaryAssaultResult result = assault.Execute(command.Fleets, command.Planet);
            List<GameResult> results = new List<GameResult> { result };
            results.AddRange(result.Events);
            if (result.OwnershipChange != null)
                results.Add(result.OwnershipChange);
            return new GameCommandResult(true, results);
        }

        /// <summary>
        /// Executes a validated evacuation command.
        /// </summary>
        /// <param name="movement">The movement feature.</param>
        /// <param name="command">The evacuation command.</param>
        /// <returns>The command outcome.</returns>
        private static GameCommandResult ExecuteEvacuation(
            Movement movement,
            EvacuateUnitCommand command
        )
        {
            if (!movement.CanEvacuateToNearestFriendlyPlanet(command.Unit))
                return Accepted(false);

            movement.EvacuateToNearestFriendlyPlanet(command.Unit);
            return Accepted(true);
        }

        /// <summary>
        /// Enqueues a created unit for the concrete destination kind.
        /// </summary>
        /// <param name="manufacturing">The manufacturing feature.</param>
        /// <param name="command">The enqueue command.</param>
        /// <returns>The command outcome.</returns>
        private static GameCommandResult ExecuteEnqueue(
            Manufacturing manufacturing,
            EnqueueManufacturingCommand command
        )
        {
            bool accepted = command.Destination switch
            {
                Planet planet => manufacturing.Enqueue(
                    command.Producer,
                    command.Unit,
                    planet,
                    command.IgnoreCost
                ),
                Fleet fleet => manufacturing.Enqueue(
                    command.Producer,
                    command.Unit,
                    fleet,
                    command.IgnoreCost
                ),
                CapitalShip ship => manufacturing.Enqueue(
                    command.Producer,
                    command.Unit,
                    ship,
                    command.IgnoreCost
                ),
                _ => false,
            };
            return Accepted(accepted);
        }
    }

    /// <summary>
    /// Holds the private feature graph owned by a simulation session.
    /// </summary>
    internal sealed class SimulationFeatures
    {
        internal GameCommands Commands { get; set; }
        internal Messages Messages { get; set; }
        internal GameEvents Events { get; set; }
        internal FogOfWar FogOfWar { get; set; }
        internal Blockades Blockades { get; set; }
        internal Fleets Fleets { get; set; }
        internal Personnel Personnel { get; set; }
        internal Duels Duels { get; set; }
        internal Movement Movement { get; set; }
        internal Headquarters Headquarters { get; set; }
        internal Naming Naming { get; set; }
        internal Recovery Recovery { get; set; }
        internal Captives Captives { get; set; }
        internal Manufacturing Manufacturing { get; set; }
        internal Maintenance Maintenance { get; set; }
        internal ResourceProduction Resources { get; set; }
        internal FactionAutomation Automation { get; set; }
        internal PlanetaryControl PlanetaryControl { get; set; }
        internal Uprisings Uprisings { get; set; }
        internal Jedi Jedi { get; set; }
        internal Missions Missions { get; set; }
        internal SpaceCombat SpaceCombat { get; set; }
        internal Bombardment Bombardment { get; set; }
        internal PlanetaryAssault PlanetaryAssault { get; set; }
        internal Research Research { get; set; }
        internal OfficerLoyalty OfficerLoyalty { get; set; }
        internal Victory Victory { get; set; }
        internal AI AI { get; set; }
    }
}
