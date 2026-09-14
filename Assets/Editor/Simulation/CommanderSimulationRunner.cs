using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

public static partial class HeadlessSimulationRunner
{
    private const string _commanderFactionFlag = "-commanderFaction";
    private const string _commanderAdvanceFlag = "-commanderAdvance";
    private const string _commanderCommandsFlag = "-commanderCommands";
    private const string _commanderOutputFlag = "-commanderOut";
    private const string _commanderSaveFlag = "-commanderSave";
    private const string _commanderSeedFlag = "-commanderSeed";
    private const string _commanderPlayerId = "COMMANDER1";

    /// <summary>
    /// Opens or resumes a commander-controlled simulation, applies commands, and advances it.
    /// </summary>
    public static void RunCommanderSession()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            string factionId = GetArgument(args, _commanderFactionFlag, "FNEMP1");
            string saveName = GetArgument(args, _commanderSaveFlag, "commander-session");
            string outputPath = Path.GetFullPath(
                GetArgument(args, _commanderOutputFlag, "SimulationResults/commander-state.json")
            );
            string commandPath = GetArgument(args, _commanderCommandsFlag, null);
            int advanceTicks = GetIntegerArgument(args, _commanderAdvanceFlag, 0);
            int? seed = GetNullableIntegerArgument(args, _commanderSeedFlag);

            ContentPack contentPack = ContentPackLoader.OpenActive();
            SaveGameManager saveManager = SaveGameManager.Instance;
            GameRoot game = File.Exists(saveManager.GetSaveFilePath(saveName))
                ? saveManager.LoadGameData(saveName)
                : CreateCommanderGame(contentPack, seed);
            Faction commander = game.GetFactionByOwnerInstanceID(factionId);
            if (commander == null)
                throw new InvalidOperationException($"Unknown commander faction '{factionId}'.");

            foreach (Faction faction in game.GetFactions())
                faction.PlayerID = faction == commander ? _commanderPlayerId : null;
            game.Summary.PlayerFactionID = commander.InstanceID;

            GameManager manager = new GameManager(game, contentPack.GameData);
            manager.ReconcileLoadedState();
            List<CommanderCommandResult> commandResults = ApplyCommanderCommands(
                manager,
                commander,
                contentPack.GameData,
                commandPath
            );
            List<long> ignoredStepSamples = new List<long>();
            for (int tick = 0; tick < advanceTicks; tick++)
                ProcessTickIncrementally(manager, ignoredStepSamples);

            saveManager.SaveGameData(
                game,
                saveName,
                $"Commander session at tick {game.CurrentTick}"
            );
            WriteCommanderState(
                outputPath,
                manager,
                commander,
                contentPack.GameData,
                commandResults
            );
            UnityEngine.Debug.Log(
                $"[CommanderSim] faction={factionId} tick={game.CurrentTick} output={outputPath}"
            );
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            UnityEditor.EditorApplication.Exit(1);
        }
    }

    private static GameRoot CreateCommanderGame(ContentPack contentPack, int? seed)
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            StartingFactionIDs = contentPack.Scenario.PlayableFactionIDs.ToArray(),
            PlayerFactionID = contentPack.Scenario.PlayableFactionIDs.FirstOrDefault(),
            PackID = contentPack.Definition.ID,
            PackVersion = contentPack.Definition.Version,
            ScenarioID = contentPack.Scenario.ID,
        };
        if (seed.HasValue)
            summary.Seed = seed.Value;

        return CreateGameBuilder(summary, contentPack.GameData, seed).BuildGame();
    }

    private static List<CommanderCommandResult> ApplyCommanderCommands(
        GameManager manager,
        Faction commander,
        GameDataCatalog gameData,
        string commandPath
    )
    {
        List<CommanderCommandResult> results = new List<CommanderCommandResult>();
        if (string.IsNullOrWhiteSpace(commandPath) || !File.Exists(commandPath))
            return results;

        CommanderCommandBatch batch = UnityEngine.JsonUtility.FromJson<CommanderCommandBatch>(
            File.ReadAllText(commandPath)
        );
        foreach (CommanderCommand command in batch?.Commands ?? Array.Empty<CommanderCommand>())
            results.Add(ApplyCommanderCommand(manager, commander, gameData, command));
        return results;
    }

    private static CommanderCommandResult ApplyCommanderCommand(
        GameManager manager,
        Faction commander,
        GameDataCatalog gameData,
        CommanderCommand command
    )
    {
        if (command == null)
            return CommanderCommandResult.Rejected(null, "Command is missing.");

        if (string.Equals(command.Type, "Manufacture", StringComparison.OrdinalIgnoreCase))
            return ApplyManufacturingCommand(manager, commander, gameData, command);
        if (string.Equals(command.Type, "Assault", StringComparison.OrdinalIgnoreCase))
            return ApplyAssaultCommand(manager, commander, command);
        if (string.Equals(command.Type, "Bombard", StringComparison.OrdinalIgnoreCase))
            return ApplyBombardmentCommand(manager, commander, command);
        if (string.Equals(command.Type, "ResolveCombat", StringComparison.OrdinalIgnoreCase))
        {
            return manager.TryResolveCombat(autoResolve: true)
                ? CommanderCommandResult.Accepted(command)
                : CommanderCommandResult.Rejected(
                    command,
                    "No player combat is awaiting resolution."
                );
        }
        if (string.Equals(command.Type, "LoadRegiments", StringComparison.OrdinalIgnoreCase))
            return ApplyLoadRegimentsCommand(manager, commander, command);
        if (string.Equals(command.Type, "UnloadRegiments", StringComparison.OrdinalIgnoreCase))
            return ApplyUnloadRegimentsCommand(manager, commander, command);
        if (!string.Equals(command.Type, "Move", StringComparison.OrdinalIgnoreCase))
            return CommanderCommandResult.Rejected(command, "Unsupported command type.");

        GameRoot game = manager.GetGame();
        ContainerNode destination = game.GetSceneNodeByInstanceID<ContainerNode>(
            command.DestinationId
        );
        string[] unitIds = command.UnitIds ?? Array.Empty<string>();
        List<ISceneNode> units = unitIds
            .Select(unitId => game.GetSceneNodeByInstanceID<ISceneNode>(unitId))
            .Where(unit => unit != null)
            .ToList();
        if (destination == null || units.Count != unitIds.Length)
            return CommanderCommandResult.Rejected(command, "Unknown unit or destination.");
        if (units.Any(unit => unit.GetOwnerInstanceID() != commander.InstanceID))
            return CommanderCommandResult.Rejected(command, "Unit is not commander-owned.");

        return manager.TryRequestMove(units, destination, commander.InstanceID)
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(command, "Movement system rejected the command.");
    }

    private static CommanderCommandResult ApplyUnloadRegimentsCommand(
        GameManager manager,
        Faction commander,
        CommanderCommand command
    )
    {
        GameRoot game = manager.GetGame();
        Fleet source = game.GetSceneNodeByInstanceID<Fleet>(command.ProducerId);
        Planet destination = game.GetSceneNodeByInstanceID<Planet>(command.DestinationId);
        if (
            source?.GetOwnerInstanceID() != commander.InstanceID
            || destination?.GetOwnerInstanceID() != commander.InstanceID
            || source.GetParentOfType<Planet>() != destination
        )
            return CommanderCommandResult.Rejected(
                command,
                "Fleet and destination planet are not co-located and owned."
            );

        List<ISceneNode> regiments = source
            .GetRegiments()
            .Where(regiment =>
                regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null
            )
            .OrderBy(regiment => regiment.AttackRating)
            .ThenBy(regiment => regiment.InstanceID, StringComparer.Ordinal)
            .Take(Math.Max(1, command.Count))
            .Cast<ISceneNode>()
            .ToList();
        return manager.TryRequestMove(regiments, destination, commander.InstanceID)
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(
                command,
                "Movement system rejected regiment unloading."
            );
    }

    private static CommanderCommandResult ApplyLoadRegimentsCommand(
        GameManager manager,
        Faction commander,
        CommanderCommand command
    )
    {
        GameRoot game = manager.GetGame();
        Planet source = game.GetSceneNodeByInstanceID<Planet>(command.ProducerId);
        Fleet destination = game.GetSceneNodeByInstanceID<Fleet>(command.DestinationId);
        if (
            source?.GetOwnerInstanceID() != commander.InstanceID
            || destination?.GetOwnerInstanceID() != commander.InstanceID
            || destination.GetParentOfType<Planet>() != source
        )
            return CommanderCommandResult.Rejected(
                command,
                "Fleet and source planet are not co-located and owned."
            );

        List<ISceneNode> regiments = source
            .GetChildren<Regiment>()
            .Where(regiment =>
                regiment.GetOwnerInstanceID() == commander.InstanceID
                && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null
            )
            .OrderByDescending(regiment => regiment.AttackRating)
            .ThenBy(regiment => regiment.InstanceID, StringComparer.Ordinal)
            .Take(Math.Max(1, command.Count))
            .Cast<ISceneNode>()
            .ToList();
        return manager.TryRequestMove(regiments, destination, commander.InstanceID)
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(
                command,
                "Movement system rejected regiment loading."
            );
    }

    private static CommanderCommandResult ApplyManufacturingCommand(
        GameManager manager,
        Faction commander,
        GameDataCatalog gameData,
        CommanderCommand command
    )
    {
        GameRoot game = manager.GetGame();
        Planet producer = game.GetSceneNodeByInstanceID<Planet>(command.ProducerId);
        ISceneNode destination = game.GetSceneNodeByInstanceID<ISceneNode>(command.DestinationId);
        IManufacturable template = GetManufacturingTemplates(gameData)
            .FirstOrDefault(candidate => candidate.TypeID == command.TemplateId);
        bool accepted = manager.TryStartManufacturing(
            producer,
            template,
            destination,
            Math.Max(1, command.Count),
            commander.InstanceID
        );
        return accepted
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(
                command,
                "Manufacturing system rejected the command."
            );
    }

    private static CommanderCommandResult ApplyAssaultCommand(
        GameManager manager,
        Faction commander,
        CommanderCommand command
    )
    {
        ResolveCombatCommand(
            manager.GetGame(),
            commander,
            command,
            out List<Fleet> fleets,
            out Planet target
        );
        return manager.TryPlanetaryAssault(fleets, target) != null
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(
                command,
                "Planetary assault system rejected the command."
            );
    }

    private static CommanderCommandResult ApplyBombardmentCommand(
        GameManager manager,
        Faction commander,
        CommanderCommand command
    )
    {
        ResolveCombatCommand(
            manager.GetGame(),
            commander,
            command,
            out List<Fleet> fleets,
            out Planet target
        );
        if (!Enum.TryParse(command.BombardmentType, true, out BombardmentType type))
            return CommanderCommandResult.Rejected(command, "Unknown bombardment type.");
        return manager.TryBombard(fleets, target, type) != null
            ? CommanderCommandResult.Accepted(command)
            : CommanderCommandResult.Rejected(command, "Bombardment system rejected the command.");
    }

    private static void ResolveCombatCommand(
        GameRoot game,
        Faction commander,
        CommanderCommand command,
        out List<Fleet> fleets,
        out Planet target
    )
    {
        fleets = (command.UnitIds ?? Array.Empty<string>())
            .Select(fleetId => game.GetSceneNodeByInstanceID<Fleet>(fleetId))
            .Where(fleet => fleet?.GetOwnerInstanceID() == commander.InstanceID)
            .ToList();
        target = game.GetSceneNodeByInstanceID<Planet>(command.DestinationId);
    }

    private static IEnumerable<IManufacturable> GetManufacturingTemplates(GameDataCatalog gameData)
    {
        return gameData
            .Buildings.Cast<IManufacturable>()
            .Concat(gameData.CapitalShips)
            .Concat(gameData.Starfighters)
            .Concat(gameData.Regiments)
            .Concat(gameData.SpecialForces);
    }

    private static void WriteCommanderState(
        string outputPath,
        GameManager manager,
        Faction commander,
        GameDataCatalog gameData,
        List<CommanderCommandResult> commandResults
    )
    {
        GalaxyMap view = manager.GetFogOfWarSystem().BuildFactionView(commander);
        CommanderState state = new CommanderState
        {
            Tick = manager.GetCurrentTick(),
            FactionId = commander.InstanceID,
            RawMaterials = commander.RawMaterialStockpile,
            RefinedMaterials = commander.RefinedMaterialStockpile,
            CommandResults = commandResults,
            Templates = GetManufacturingTemplates(gameData)
                .Where(template =>
                    IManufacturable.CanBeManufacturedBy(template, commander.InstanceID)
                )
                .Select(template => new CommanderTemplate
                {
                    Id = template.TypeID,
                    Name = template.GetDisplayName(),
                    Category = template.GetManufacturingType().ToString(),
                    Cost = template.GetConstructionCost(),
                    Maintenance = template.GetMaintenanceCost(),
                })
                .OrderBy(template => template.Category, StringComparer.Ordinal)
                .ThenBy(template => template.Cost)
                .ToList(),
            Planets = view.GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(planet => !planet.IsUnexploredView)
                .Select(planet => new CommanderPlanet
                {
                    Id = planet.InstanceID,
                    Name = planet.GetDisplayName(),
                    OwnerId = planet.GetOwnerInstanceID(),
                    Colonized = planet.IsColonized,
                    Headquarters = planet.IsHeadquarters,
                    Energy = planet.GetEnergyCapacity(),
                    RawResources = planet.GetRawResourceNodes(),
                    ConstructionCapacity = planet.GetAvailableManufacturingCapacity(
                        ManufacturingType.Building
                    ),
                    ShipCapacity = planet.GetAvailableManufacturingCapacity(ManufacturingType.Ship),
                    TroopCapacity = planet.GetAvailableManufacturingCapacity(
                        ManufacturingType.Troop
                    ),
                    CapitalShips = planet
                        .GetChildren<Fleet>()
                        .Where(fleet => fleet.GetOwnerInstanceID() == planet.GetOwnerInstanceID())
                        .Sum(fleet => fleet.GetChildren<CapitalShip>().Count),
                    Regiments = planet
                        .GetAllRegiments()
                        .Count(regiment =>
                            regiment.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                        ),
                    Starfighters = planet
                        .GetAllStarfighters()
                        .Count(starfighter =>
                            starfighter.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                        ),
                    Shields = planet
                        .GetChildren<Building>()
                        .Count(building => building.IsPlanetaryShieldGenerator()),
                })
                .OrderBy(planet => planet.Id, StringComparer.Ordinal)
                .ToList(),
            Fleets = manager
                .GetGame()
                .GetSceneNodesByOwnerInstanceID<Fleet>(commander.InstanceID)
                .Select(fleet => new CommanderFleet
                {
                    Id = fleet.InstanceID,
                    PlanetId = fleet.GetParentOfType<Planet>()?.InstanceID,
                    CapitalShips = fleet.GetChildren<CapitalShip>().Count,
                    Regiments = fleet.GetRegiments().Count(),
                    ReadyRegiments = fleet
                        .GetRegiments()
                        .Count(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                            && regiment.Movement == null
                        ),
                    Starfighters = fleet.GetStarfighters().Count(),
                    CombatValue = fleet.GetCombatValue(),
                    Bombardment = BombardmentSystem.GetBombardmentStrength(
                        new[] { fleet },
                        manager.GetGame().Config.Combat.Bombardment
                    ),
                    Moving = fleet.Movement != null,
                })
                .OrderBy(fleet => fleet.Id, StringComparer.Ordinal)
                .ToList(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, UnityEngine.JsonUtility.ToJson(state, true));
    }

    private static string GetArgument(IReadOnlyList<string> args, string flag, string defaultValue)
    {
        for (int index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return defaultValue;
    }

    private static int GetIntegerArgument(
        IReadOnlyList<string> args,
        string flag,
        int defaultValue
    ) => int.TryParse(GetArgument(args, flag, null), out int value) ? value : defaultValue;

    private static int? GetNullableIntegerArgument(IReadOnlyList<string> args, string flag) =>
        int.TryParse(GetArgument(args, flag, null), out int value) ? value : null;

    [Serializable]
    private sealed class CommanderCommandBatch
    {
        public CommanderCommand[] Commands = Array.Empty<CommanderCommand>();
    }

    [Serializable]
    private sealed class CommanderCommand
    {
        public string Type = string.Empty;
        public string[] UnitIds = Array.Empty<string>();
        public string DestinationId = string.Empty;
        public string ProducerId = string.Empty;
        public string TemplateId = string.Empty;
        public int Count = 1;
        public string BombardmentType = string.Empty;
    }

    [Serializable]
    private sealed class CommanderCommandResult
    {
        public string Type = string.Empty;
        public string DestinationId = string.Empty;
        public bool WasAccepted;
        public string Message = string.Empty;

        public static CommanderCommandResult Accepted(CommanderCommand command) =>
            Create(command, true, string.Empty);

        public static CommanderCommandResult Rejected(CommanderCommand command, string message) =>
            Create(command, false, message);

        private static CommanderCommandResult Create(
            CommanderCommand command,
            bool wasAccepted,
            string message
        ) =>
            new CommanderCommandResult
            {
                Type = command?.Type ?? string.Empty,
                DestinationId = command?.DestinationId ?? string.Empty,
                WasAccepted = wasAccepted,
                Message = message,
            };
    }

    [Serializable]
    private sealed class CommanderState
    {
        public int Tick;
        public string FactionId = string.Empty;
        public int RawMaterials;
        public int RefinedMaterials;
        public List<CommanderCommandResult> CommandResults = new List<CommanderCommandResult>();
        public List<CommanderTemplate> Templates = new List<CommanderTemplate>();
        public List<CommanderPlanet> Planets = new List<CommanderPlanet>();
        public List<CommanderFleet> Fleets = new List<CommanderFleet>();
    }

    [Serializable]
    private sealed class CommanderPlanet
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string OwnerId = string.Empty;
        public bool Colonized;
        public bool Headquarters;
        public int Energy;
        public int RawResources;
        public int ConstructionCapacity;
        public int ShipCapacity;
        public int TroopCapacity;
        public int CapitalShips;
        public int Regiments;
        public int Starfighters;
        public int Shields;
    }

    [Serializable]
    private sealed class CommanderFleet
    {
        public string Id = string.Empty;
        public string PlanetId = string.Empty;
        public int CapitalShips;
        public int Regiments;
        public int ReadyRegiments;
        public int Starfighters;
        public int CombatValue;
        public int Bombardment;
        public bool Moving;
    }

    [Serializable]
    private sealed class CommanderTemplate
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Category = string.Empty;
        public int Cost;
        public int Maintenance;
    }
}
