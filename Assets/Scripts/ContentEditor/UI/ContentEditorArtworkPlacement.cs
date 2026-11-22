using System;

/// <summary>
/// Identifies the authored horizontal and vertical fields that place one interface image.
/// </summary>
internal sealed class ContentEditorArtworkPlacement
{
    internal ContentPackEditorField XField { get; }

    internal ContentPackEditorField YField { get; }

    /// <summary>
    /// Creates one source-coordinate placement binding.
    /// </summary>
    /// <param name="xField">The authored horizontal source coordinate.</param>
    /// <param name="yField">The authored vertical source coordinate.</param>
    internal ContentEditorArtworkPlacement(
        ContentPackEditorField xField,
        ContentPackEditorField yField
    )
    {
        XField = xField ?? throw new ArgumentNullException(nameof(xField));
        YField = yField ?? throw new ArgumentNullException(nameof(yField));
    }
}

/// <summary>
/// Describes one authored coordinate produced by moving artwork in a composed preview.
/// </summary>
public readonly struct ContentEditorArtworkPlacementChange
{
    public ContentPackEditorField Field { get; }

    public int Value { get; }

    /// <summary>
    /// Captures one validated integer-coordinate replacement.
    /// </summary>
    /// <param name="field">The authored coordinate field.</param>
    /// <param name="value">The replacement source coordinate.</param>
    public ContentEditorArtworkPlacementChange(ContentPackEditorField field, int value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value;
    }
}
