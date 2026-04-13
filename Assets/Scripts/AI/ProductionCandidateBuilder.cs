using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Side identifiers matching the original binary's encoding.
/// Used by the production candidate builder to select the correct faction.
/// Validated at entry: values outside [1,2] cause immediate return.
/// </summary>
public enum AISide
{
    Alliance = 1,
    Empire = 2,
}

/// <summary>
/// Facility type codes from the original binary.
/// Valid range: [0x28, 0x2c) per the production candidate builder validation gate.
/// </summary>
public static class AIFacilityType
{
    public const int OrbitalShipyard = 0x28;
    public const int TrainingFacility = 0x29;
    public const int ConstructionYard = 0x2a;
}

/// <summary>
/// A unit type that passed all production candidate filters and is eligible
/// for the AI to queue at a facility this tick.
/// </summary>
public class ProductionCandidate
{
    /// <summary>The manufacturable entity template that passed all filters.</summary>
    public IManufacturable Unit { get; set; }
}

/// <summary>
/// Priority tier enable flags for the production candidate builder.
/// Each flag controls whether candidates at that priority tier relative to
/// the current production count are included in the output.
/// The caller (FUN_0052cd70) passes (1, 1, 0): include below and at threshold,
/// exclude above.
/// </summary>
public class PriorityTierFlags
{
    /// <summary>Non-zero = include candidates with ProductionPriority below productionCount.</summary>
    public int BelowThreshold { get; set; }

    /// <summary>Non-zero = include candidates with ProductionPriority equal to productionCount.</summary>
    public int AtThreshold { get; set; }

    /// <summary>Non-zero = include candidates with ProductionPriority above productionCount.</summary>
    public int AboveThreshold { get; set; }
}

/// <summary>
/// Production candidate list builder. For a given faction side and facility type,
/// walks all available unit templates and emits those that pass the production count
/// range check, construction cost gate, faction availability check, and priority
/// tier filter.
///
/// Corresponds to FUN_00565fb0 (Section 133). The original walked a start-data
/// registry linked list; here we query the game's entity catalog directly.
/// </summary>
public class ProductionCandidateBuilder
{
    private readonly GameRoot _game;

    /// <summary>
    /// Initializes the builder with the game root used to query the entity catalog.
    /// </summary>
    /// <param name="game">The game root.</param>
    public ProductionCandidateBuilder(GameRoot game)
    {
        _game = game;
    }

    /// <summary>
    /// Populates <paramref name="output"/> with all unit templates eligible for
    /// production given the current facility state.
    /// </summary>
    /// <param name="side">Faction side: 1 = Alliance, 2 = Empire. Outside [1,2] returns 0 immediately.</param>
    /// <param name="facilityType">Facility type code. Must be in [0x28, 0x2c). Outside range returns 0 immediately.</param>
    /// <param name="productionCount">Current production queue count. Used for range and priority threshold checks.</param>
    /// <param name="tierFlags">Enable flags controlling which priority tiers produce candidates.</param>
    /// <param name="output">Receives all candidates that pass all filters.</param>
    /// <returns>Always 0. Results are delivered via <paramref name="output"/>.</returns>
    public int Build(
        AISide side,
        int facilityType,
        int productionCount,
        PriorityTierFlags tierFlags,
        List<ProductionCandidate> output
    )
    {
        int sideValue = (int)side;
        if (sideValue < 1 || sideValue > 2)
            return 0;

        if (facilityType < 0x28 || facilityType >= 0x2c)
            return 0;

        // Determine the owning faction's instance ID for availability checks.
        // Side 1 = Alliance (index 0), side 2 = Empire (index 1).
        Faction faction = _game.Factions.ElementAtOrDefault(sideValue - 1);
        if (faction == null)
            return 0;

        string factionId = faction.InstanceID;

        // Retrieve all unit templates available in the game catalog.
        // Templates are IManufacturable nodes whose AllowedOwnerInstanceIDs includes
        // this faction and whose ConstructionCost > 0 (producible at this facility).
        List<IManufacturable> templates = _game
            .GetSceneNodesByType<IManufacturable>()
            .Where(u =>
                u.AllowedOwnerInstanceIDs != null
                && u.AllowedOwnerInstanceIDs.Contains(factionId)
                && u.ConstructionCost > 0
                && productionCount >= u.MinProductionCount
                && productionCount < u.MaxProductionCount
            )
            .ToList();

        foreach (IManufacturable unit in templates)
        {
            int priority = unit.ProductionPriority;

            int tierEnabled;
            if (priority < productionCount)
                tierEnabled = tierFlags.BelowThreshold;
            else if (priority == productionCount)
                tierEnabled = tierFlags.AtThreshold;
            else
                tierEnabled = tierFlags.AboveThreshold;

            if (tierEnabled == 0)
                continue;

            if (string.IsNullOrEmpty(unit.DisplayName))
                return 0;

            output.Add(new ProductionCandidate { Unit = unit });
        }

        return 0;
    }
}
