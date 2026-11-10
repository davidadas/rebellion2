using System;
using System.Globalization;
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
