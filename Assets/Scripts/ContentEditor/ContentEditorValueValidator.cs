using System;
using System.Globalization;
using Rebellion.Game;
using Rebellion.Game.Units;

/// <summary>
/// Checks authored integer values against the constraints used by their runtime consumers.
/// </summary>
internal static class ContentEditorValueValidator
{
    /// <summary>
    /// Rejects invalid numeric values before either form edits or saved XML reach the runtime.
    /// </summary>
    /// <param name="field">The authored field, including its containing object.</param>
    /// <param name="value">The proposed serialized value.</param>
    internal static void Validate(ContentPackEditorField field, string value)
    {
        Type owner = ContentPackEditorFieldSchema.GetValueType(
            field.Record.Element,
            field.Container ?? field.Record.Element
        );
        if (
            owner == null
            || !int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int number
            )
        )
            return;

        string member = field.MemberName;
        if (
            (
                typeof(IManufacturable).IsAssignableFrom(owner)
                && member is "ConstructionCost" or "MaintenanceCost"
            )
            || (owner == typeof(Building) && member == "ProcessRate")
            || (
                owner == typeof(CapitalShip)
                && member is "StarfighterCapacity" or "RegimentCapacity"
            )
        )
            RequireRange(field, number, 0, int.MaxValue);
        if (owner == typeof(Officer))
        {
            if (member == "JediProbability")
                RequireRange(field, number, 0, 100);
            if (member is "ForceValue" or "ForceTrainingAdjustment")
                RequireRange(field, number, short.MinValue, short.MaxValue);
        }
        if (
            (owner == typeof(GameConfig.ProductionConfig) && member == "ScrapRefundDivisor")
            || (owner == typeof(GameConfig.AIConfig) && member == "TickInterval")
            || (
                owner == typeof(GameConfig.GarrisonConfig)
                && member == "ResourceFacilitiesPerGarrison"
            )
            || (
                owner == typeof(GameConfig.PlanetaryAssaultConfig)
                && member
                    is "PersonnelDivisor"
                        or "GeneralLeadershipDivisor"
                        or "DefenseFireDivisor"
            )
            || (
                owner == typeof(GameConfig.BombardmentConfig)
                && member is "AttackerLeadershipDivisor" or "DefenderLeadershipDivisor"
            )
            || (owner == typeof(GameConfig.UprisingConfig) && member == "MissionLeadershipDivisor")
        )
            RequireRange(field, number, 1, int.MaxValue);
        if (owner == typeof(GameConfig.UprisingConfig) && member == "DiceRange")
            RequireRange(field, number, 1, int.MaxValue - 1);
        if (owner == typeof(GameConfig.ResearchConfig) && member == "RefreshIntervalSpread")
            RequireRange(field, number, 0, int.MaxValue - 1);
        if (
            owner == typeof(GameConfig.BombardmentConfig)
            && member is "StrikeRollMinimum" or "StrikeRollMaximum"
        )
        {
            RequireRange(field, number, 0, int.MaxValue - 1);
            string otherMember =
                member == "StrikeRollMinimum" ? "StrikeRollMaximum" : "StrikeRollMinimum";
            if (
                int.TryParse(
                    field.Container.Element(otherMember)?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int other
                ) && (member == "StrikeRollMinimum" ? number > other : number < other)
            )
                throw new ArgumentException(
                    $"{field.Document.Address}: {field.Record.ID} / {field.Name}: strike roll minimum must not exceed strike roll maximum."
                );
        }
    }

    /// <summary>
    /// Reports a field's document and record when its value cannot satisfy its runtime contract.
    /// </summary>
    /// <param name="field">The invalid field.</param>
    /// <param name="value">The parsed integer.</param>
    /// <param name="minimum">The inclusive minimum.</param>
    /// <param name="maximum">The inclusive maximum.</param>
    private static void RequireRange(
        ContentPackEditorField field,
        int value,
        int minimum,
        int maximum
    )
    {
        if (value < minimum || value > maximum)
            throw new ArgumentException(
                $"{field.Document.Address}: {field.Record.ID} / {field.Name} must be from {minimum} through {maximum}."
            );
    }
}
