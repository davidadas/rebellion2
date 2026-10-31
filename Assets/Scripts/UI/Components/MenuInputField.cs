using System;
using TMPro;

/// <summary>
/// Exposes complete native text insertions while preserving TMP input behavior.
/// </summary>
public sealed class MenuInputField : TMP_InputField
{
    public event Action TextInsertionStarted;
    public event Action TextInsertionCompleted;

    /// <summary>
    /// Bounds a paste or other string insertion that TMP reports one character at a time.
    /// </summary>
    /// <param name="text">The complete text supplied by the input operation.</param>
    protected override void Append(string text)
    {
        try
        {
            TextInsertionStarted?.Invoke();
            base.Append(text);
        }
        finally
        {
            TextInsertionCompleted?.Invoke();
        }
    }
}
