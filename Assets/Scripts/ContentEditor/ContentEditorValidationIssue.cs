using System;

/// <summary>
/// Describes one authored field rejected during a pack-wide validation pass.
/// </summary>
internal sealed class ContentEditorValidationIssue
{
    public ArgumentException Exception { get; }

    public ContentPackEditorField Field { get; }

    public string FieldError { get; }

    public string Message => Exception.Message;

    /// <summary>
    /// Creates a navigable validation issue from a contextual field failure.
    /// </summary>
    /// <param name="exception">The contextual validation failure.</param>
    /// <param name="field">The authored field responsible for the failure.</param>
    /// <param name="fieldError">The concise message displayed beside the field.</param>
    public ContentEditorValidationIssue(
        ArgumentException exception,
        ContentPackEditorField field,
        string fieldError
    )
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Field = field ?? throw new ArgumentNullException(nameof(field));
        FieldError = fieldError ?? throw new ArgumentNullException(nameof(fieldError));
    }
}
