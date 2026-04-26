using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rebellion.Game;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using UnityEngine;

public static class HeadlessSimulationBootstrap
{
    private const string SimulationFlag = "-headlessSim";
    private const string TickCountFlag = "-simTicks";
    private const string OutputPathFlag = "-simOut";
    private const string PlayerFactionFlag = "-simPlayerFaction";
    private const string SeedFlag = "-simSeed";
    private const string LogDirectory = "/tmp/rebellion2-sim-logs";
    private static readonly MethodInfo ProcessTickMethod = typeof(GameManager).GetMethod(
        "ProcessTick",
        BindingFlags.Instance | BindingFlags.NonPublic
    );
    private static readonly FieldInfo PendingCombatField = typeof(GameManager).GetField(
        "_pendingCombatDecision",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void MaybeStartSimulation()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (!args.Contains(SimulationFlag))
            return;

        try
        {
            RunSimulation();
            ExitApplication(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ExitApplication(1);
        }
    }

    private static void RunSimulation()
    {
        if (ProcessTickMethod == null || PendingCombatField == null)
        {
            throw new InvalidOperationException(
                "Headless simulation could not access GameManager tick internals."
            );
        }

        SimulationOptions options = SimulationOptions.Parse(Environment.GetCommandLineArgs());
        GameSummary summary = CreateDefaultSummary(options.PlayerFactionId);
        GameBuilder builder = new GameBuilder(summary);
        GameRoot game = builder.BuildGame();
        GameManager manager = new GameManager(game);
        string logPath = GetLogPath(options.OutputPath);

        string startMessage =
            $"[HeadlessSim] starting ticks={options.TickCount} seed={options.Seed?.ToString() ?? "random"} playerFaction={options.PlayerFactionId} galaxySize={summary.GalaxySize}";
        Debug.Log(startMessage);
        LogToFile(logPath, startMessage);

        for (int i = 0; i < options.TickCount; i++)
        {
            AutoResolvePendingCombat(manager);
            ProcessTickMethod.Invoke(manager, null);
            AutoResolvePendingCombat(manager);
        }

        SimulationSummary report = BuildSummary(game, options);
        WriteSummary(report, options.OutputPath);

        string completeMessage =
            $"[HeadlessSim] complete ticks={report.TicksCompleted} output={options.OutputPath}";
        Debug.Log(completeMessage);
        LogToFile(logPath, completeMessage);
    }

    private static void AutoResolvePendingCombat(GameManager manager)
    {
        while (PendingCombatField.GetValue(manager) != null)
            manager.ResolveCombat(true);
    }

    private static GameSummary CreateDefaultSummary(string playerFactionId)
    {
        return new GameSummary
        {
            IsNewGame = true,
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            PlayerFactionID = playerFactionId,
            StartingFactionIDs = new[] { "FNALL1", "FNEMP1" },
        };
    }

    private static SimulationSummary BuildSummary(GameRoot game, SimulationOptions options)
    {
        return new SimulationSummary
        {
            TicksRequested = options.TickCount,
            TicksCompleted = game.CurrentTick,
            PlayerFactionId = options.PlayerFactionId,
            Seed = options.Seed,
            GalaxySize = game.Summary?.GalaxySize.ToString() ?? string.Empty,
            OutputPath = options.OutputPath,
            Factions = game
                .Factions.Select(faction => new FactionSimulationSummary
                {
                    FactionId = faction.InstanceID,
                    DisplayName = faction.GetDisplayName(),
                    PlanetCount = game.GetSceneNodesByOwnerInstanceID<Planet>(
                        faction.InstanceID
                    ).Count,
                    FleetCount = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                        faction.InstanceID
                    ).Count,
                    BuildingCount = game.GetSceneNodesByOwnerInstanceID<Building>(
                        faction.InstanceID
                    ).Count,
                    CapitalShipCount = game.GetSceneNodesByOwnerInstanceID<CapitalShip>(
                        faction.InstanceID
                    ).Count,
                    StarfighterCount = game.GetSceneNodesByOwnerInstanceID<Starfighter>(
                        faction.InstanceID
                    ).Count,
                    RegimentCount = game.GetSceneNodesByOwnerInstanceID<Regiment>(
                        faction.InstanceID
                    ).Count,
                    OfficerCount = game.GetSceneNodesByOwnerInstanceID<Officer>(
                        faction.InstanceID
                    ).Count,
                    RawMaterials = faction.GetTotalAvailableMaterialsRaw(),
                    RefinedMaterials = game.GetRefinedMaterials(faction),
                    Energy = faction.GetTotalAvailableEnergy(),
                    UnitCost = faction.GetTotalMaintenanceCost(),
                    Fleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(faction.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                        .Select(BuildFleetSummary)
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static void WriteSummary(SimulationSummary summary, string outputPath)
    {
        string resolvedPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(resolvedPath, JsonUtility.ToJson(summary, true));
    }

    private static string GetLogPath(string outputPath)
    {
        string resolvedOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(LogDirectory);
        return Path.Combine(
            LogDirectory,
            $"{Path.GetFileNameWithoutExtension(resolvedOutputPath)}.log"
        );
    }

    private static void LogToFile(string logPath, string message)
    {
        File.AppendAllText(logPath, message + Environment.NewLine);
    }

    private static void ExitApplication(int exitCode)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(exitCode);
#else
        Application.Quit(exitCode);
#endif
    }

    [Serializable]
    private sealed class SimulationSummary
    {
        public int TicksRequested;
        public int TicksCompleted;
        public string PlayerFactionId;
        public int? Seed;
        public string GalaxySize;
        public string OutputPath;
        public FactionSimulationSummary[] Factions;
    }

    [Serializable]
    private sealed class FactionSimulationSummary
    {
        public string FactionId;
        public string DisplayName;
        public int PlanetCount;
        public int FleetCount;
        public int BuildingCount;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public int RawMaterials;
        public int RefinedMaterials;
        public int Energy;
        public int UnitCost;
        public FleetSimulationSummary[] Fleets;
    }

    [Serializable]
    private sealed class FleetSimulationSummary
    {
        public string FleetId;
        public string DisplayName;
        public string RoleType;
        public string LocationPlanetId;
        public string LocationPlanetName;
        public bool InTransit;
        public int TransitTicksRemaining;
        public int CombatValue;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int OfficerCount;
        public string[] CapitalShips;
        public string[] Starfighters;
        public string[] Regiments;
        public string[] Officers;
    }

    private sealed class SimulationOptions
    {
        public int TickCount { get; private set; }
        public string OutputPath { get; private set; }
        public string PlayerFactionId { get; private set; }
        public int? Seed { get; private set; }

        public static SimulationOptions Parse(string[] args)
        {
            return new SimulationOptions
            {
                TickCount = ParseInt(args, TickCountFlag, 200),
                OutputPath = ParseString(
                    args,
                    OutputPathFlag,
                    "SimulationResults/headless-simulation-summary.json"
                ),
                PlayerFactionId = ParseString(args, PlayerFactionFlag, null),
                Seed = ParseNullableInt(args, SeedFlag),
            };
        }

        private static int ParseInt(string[] args, string flag, int defaultValue)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        private static int? ParseNullableInt(string[] args, string flag)
        {
            string value = ParseString(args, flag, null);
            return int.TryParse(value, out int parsed) ? parsed : null;
        }

        private static string ParseString(string[] args, string flag, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return defaultValue;
        }
    }

    private static FleetSimulationSummary BuildFleetSummary(Fleet fleet)
    {
        Planet location = fleet.GetParentOfType<Planet>();
        return new FleetSimulationSummary
        {
            FleetId = fleet.InstanceID,
            DisplayName = fleet.GetDisplayName(),
            RoleType = fleet.RoleType.ToString(),
            LocationPlanetId = location?.InstanceID,
            LocationPlanetName = location?.GetDisplayName(),
            InTransit = fleet.Movement != null,
            TransitTicksRemaining = fleet.Movement?.TicksRemaining() ?? 0,
            CombatValue = fleet.GetCombatValue(),
            CapitalShipCount = fleet.CapitalShips.Count,
            StarfighterCount = fleet.GetStarfighters().Count(),
            RegimentCount = fleet.GetRegiments().Count(),
            OfficerCount = fleet.GetOfficers().Count(),
            CapitalShips = SummarizeUnits(fleet.CapitalShips),
            Starfighters = SummarizeUnits(fleet.GetStarfighters()),
            Regiments = SummarizeUnits(fleet.GetRegiments()),
            Officers = SummarizeUnits(fleet.GetOfficers()),
        };
    }

    private static string[] SummarizeUnits<T>(IEnumerable<T> units)
        where T : class
    {
        return units
            .GroupBy(GetUnitLabel)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();
    }

    private static string GetUnitLabel<T>(T unit)
        where T : class
    {
        switch (unit)
        {
            case IGameEntity entity when !string.IsNullOrEmpty(entity.GetDisplayName()):
                return entity.GetDisplayName();
            case IGameEntity entity:
                return entity.GetTypeID();
            default:
                return unit?.ToString() ?? "Unknown";
        }
    }
}
