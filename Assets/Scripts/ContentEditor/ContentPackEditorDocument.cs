using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Keeps authored XML separate from the effective values displayed by the editor.
/// </summary>
public sealed class ContentPackEditorDocument
{
    private readonly string inheritedText;
    private readonly Dictionary<string, string> observedFiles = new(StringComparer.Ordinal);
    private string savedText;
    private string text;
    private string authoredText;
    private string savedAuthoredText;
    private string lastValidAuthoredText;
    private bool usesSettingOverrides;
    private XElement inheritedRoot;
    private XElement authoredRoot;

    public string Address { get; }
    public string DisplayName { get; }
    public string Group { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }
    public bool IsInherited { get; private set; }
    public bool IsDirty { get; private set; }
    public string Text => text;
    internal string AuthoredText => authoredText;
    internal bool IsRemoved => authoredText == null;
    internal string SavedText => savedText;
    internal bool InheritsAll =>
        authoredText != null && UsesSettingOverrides && ContentXmlOverrides.IsEmpty(authoredText);
    private bool UsesSettingOverrides => usesSettingOverrides;
    private XElement InheritedRoot => inheritedRoot ??= XElement.Parse(inheritedText);
    private XElement AuthoredRoot => authoredRoot ??= XElement.Parse(lastValidAuthoredText);

    /// <summary>
    /// Reports authored state changes for reversible project history.
    /// </summary>
    public event Action<ContentPackEditorDocument, string> Changed;
    internal event Action<ContentPackEditorDocument, ContentPackEditorDocumentState> StateChanged;

    /// <summary>
    /// Creates a legacy document without setting inheritance.
    /// </summary>
    /// <param name="address">The logical document address.</param>
    /// <param name="sourcePath">The resolved source file.</param>
    /// <param name="targetPath">The local destination file.</param>
    /// <param name="sourceText">The observed source contents.</param>
    public ContentPackEditorDocument(
        string address,
        string sourcePath,
        string targetPath,
        string sourceText
    )
        : this(address, sourcePath, targetPath, sourceText, null, null) { }

    /// <summary>
    /// Opens a document with its inherited layer and observed source files.
    /// </summary>
    /// <param name="address">The logical document address.</param>
    /// <param name="sourcePath">The highest resolved source file.</param>
    /// <param name="targetPath">The local destination file.</param>
    /// <param name="sourceText">The highest layer's authored XML.</param>
    /// <param name="inheritedText">The effective XML beneath the local pack.</param>
    /// <param name="sources">The observed source files, including inherited dependencies.</param>
    internal ContentPackEditorDocument(
        string address,
        string sourcePath,
        string targetPath,
        string sourceText,
        string inheritedText,
        IReadOnlyDictionary<string, string> sources
    )
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        SourcePath = Path.GetFullPath(
            sourcePath ?? throw new ArgumentNullException(nameof(sourcePath))
        );
        TargetPath = Path.GetFullPath(
            targetPath ?? throw new ArgumentNullException(nameof(targetPath))
        );
        this.inheritedText = inheritedText;
        string relative = address.StartsWith("Pack/", StringComparison.Ordinal)
            ? address[5..]
            : address;
        DisplayName = relative;
        int separator = relative.IndexOf('/');
        Group = separator < 0 ? "Pack" : relative[..separator];
        IsInherited = !string.Equals(SourcePath, TargetPath, StringComparison.OrdinalIgnoreCase);
        savedText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        authoredText =
            IsInherited && inheritedText != null
                ? ContentXmlOverrides.Create(inheritedText)
                : sourceText;
        savedAuthoredText = authoredText;
        lastValidAuthoredText = authoredText;
        usesSettingOverrides = ContentXmlOverrides.IsOverlay(authoredText);
        text = UsesSettingOverrides
            ? ContentXmlOverrides.Merge(inheritedText, authoredText)
            : sourceText;
        if (sources != null)
            foreach (var source in sources)
                observedFiles[source.Key] = source.Value;
        observedFiles[SourcePath] = sourceText;
        observedFiles[TargetPath] = IsInherited ? null : sourceText;
    }

    /// <summary>
    /// Creates an unsaved document tracked by the project history.
    /// </summary>
    /// <param name="address">The logical document address.</param>
    /// <param name="targetPath">The local destination file.</param>
    internal ContentPackEditorDocument(string address, string targetPath)
    {
        Address = address;
        SourcePath = TargetPath = Path.GetFullPath(targetPath);
        DisplayName = address[5..];
        int separator = DisplayName.IndexOf('/');
        Group = separator < 0 ? "Pack" : DisplayName[..separator];
        observedFiles[TargetPath] = null;
    }

    /// <summary>
    /// Checks whether one displayed field has an independently inherited value.
    /// </summary>
    /// <param name="element">The displayed member.</param>
    /// <param name="container">The owner of an absent optional member.</param>
    /// <returns>True for a setting outside an ordered collection with an inherited container.</returns>
    internal bool CanInherit(XElement element, XElement container = null) =>
        inheritedText != null
        && ContentXmlOverrides.CanResetSetting(
            element.Parent != null ? element : container ?? element
        )
        && ContentXmlOverrides.FindEquivalent(element.Parent ?? container ?? element, InheritedRoot)
            != null;

    /// <summary>
    /// Checks which layer owns one displayed field.
    /// </summary>
    /// <param name="element">The displayed member.</param>
    /// <param name="attribute">The edited attribute, when present.</param>
    /// <param name="container">The owner of an absent optional member.</param>
    /// <returns>True when the field has no authored override.</returns>
    internal bool IsSettingInherited(XElement element, XName attribute, XElement container = null)
    {
        if (!UsesSettingOverrides)
            return false;
        if (element.Parent != null || container == null)
            return !ContentXmlOverrides.IsOverridden(AuthoredRoot, element, attribute);
        return ContentXmlOverrides.FindEquivalent(container, AuthoredRoot)?.Element(element.Name)
            == null;
    }

    /// <summary>
    /// Removes a setting override without changing the other authored values.
    /// </summary>
    /// <param name="element">The displayed member.</param>
    /// <param name="attribute">The edited attribute, when present.</param>
    /// <param name="container">The owner of an absent optional member.</param>
    internal void ResetSetting(XElement element, XName attribute, XElement container = null)
    {
        if (!CanInherit(element, container))
            throw new InvalidOperationException(
                "This setting has no independently inherited value."
            );
        string overlay = UsesSettingOverrides
            ? authoredText
            : ContentXmlOverrides.Update(inheritedText, text, authoredText);
        if (element.Parent == null && container != null)
        {
            XElement copy = new XElement(container.AncestorsAndSelf().Last());
            XElement parent = ContentXmlOverrides.FindEquivalent(container, copy);
            element = new XElement(element);
            parent.Add(element);
        }
        RestoreText(ContentXmlOverrides.Reset(overlay, element, attribute));
    }

    /// <summary>
    /// Records changes made to the effective form without copying untouched defaults.
    /// </summary>
    /// <param name="value">The edited effective XML.</param>
    internal void SetText(string value)
    {
        value ??= string.Empty;
        try
        {
            XElement.Parse(value);
        }
        catch (XmlException)
        {
            RestoreText(value);
            return;
        }
        RestoreText(
            UsesSettingOverrides
                ? ContentXmlOverrides.Update(inheritedText, value, lastValidAuthoredText)
                : value,
            value
        );
    }

    /// <summary>
    /// Restores authored state during undo, redo, and structural edits.
    /// </summary>
    /// <param name="value">The authored XML, or null for a removed document.</param>
    /// <param name="effectiveText">The exact edited display text, when supplied by a form or XML edit.</param>
    internal void RestoreText(string value, string effectiveText = null)
    {
        if (value == authoredText && (effectiveText == null || effectiveText == text))
            return;
        ContentPackEditorDocumentState previous = CaptureState();
        string effective = value;
        try
        {
            usesSettingOverrides = ContentXmlOverrides.IsOverlay(value);
            if (usesSettingOverrides)
                effective = ContentXmlOverrides.Merge(inheritedText, value);
            lastValidAuthoredText = value;
        }
        catch (XmlException)
        {
            // Invalid drafts remain editable and are rejected by the save boundary.
        }
        authoredText = value;
        authoredRoot = null;
        text = effectiveText ?? effective;
        IsDirty = !string.Equals(authoredText, savedAuthoredText, StringComparison.Ordinal);
        Changed?.Invoke(this, previous.Text);
        StateChanged?.Invoke(this, previous);
    }

    /// <summary>
    /// Captures authored values and the exact displayed draft for project history.
    /// </summary>
    /// <returns>The immutable editing state.</returns>
    internal ContentPackEditorDocumentState CaptureState() =>
        new ContentPackEditorDocumentState(
            authoredText,
            text,
            lastValidAuthoredText,
            usesSettingOverrides
        );

    /// <summary>
    /// Restores a complete editing state without recomputing authored intent.
    /// </summary>
    /// <param name="state">The prior or subsequent editing state.</param>
    internal void RestoreState(ContentPackEditorDocumentState state)
    {
        ContentPackEditorDocumentState previous = CaptureState();
        authoredText = state.AuthoredText;
        authoredRoot = null;
        text = state.Text;
        lastValidAuthoredText = state.LastValidAuthoredText;
        usesSettingOverrides = state.UsesSettingOverrides;
        IsDirty = authoredText != savedAuthoredText;
        Changed?.Invoke(this, previous.Text);
        StateChanged?.Invoke(this, previous);
    }

    /// <summary>
    /// Validates both authored syntax and the effective displayed document.
    /// </summary>
    public void Validate()
    {
        if (IsRemoved)
            return;
        XmlDocument document = new XmlDocument();
        document.LoadXml(authoredText);
        document.LoadXml(text);
    }

    /// <summary>
    /// Rejects writes after any local or inherited source changes externally.
    /// </summary>
    internal void VerifyUnchangedSources()
    {
        if (
            observedFiles.Any(source =>
                (File.Exists(source.Key) ? File.ReadAllText(source.Key) : null) != source.Value
            )
        )
            throw new IOException(
                "Content changed outside the editor. Reopen the pack before saving: " + TargetPath
            );
    }

    /// <summary>
    /// Accepts persistence while keeping inherited dependencies observed.
    /// </summary>
    internal void AcceptSave()
    {
        IsInherited = InheritsAll;
        observedFiles[TargetPath] = IsRemoved || InheritsAll ? null : authoredText;
        savedText = authoredText;
        savedAuthoredText = authoredText;
        IsDirty = false;
    }
}

/// <summary>
/// Retains both authored intent and exact draft text across undo and redo.
/// </summary>
internal sealed class ContentPackEditorDocumentState
{
    internal string AuthoredText { get; }
    internal string Text { get; }
    internal string LastValidAuthoredText { get; }
    internal bool UsesSettingOverrides { get; }

    /// <summary>
    /// Captures a document editing state without reading its source files.
    /// </summary>
    /// <param name="authoredText">The authored layer or removed state.</param>
    /// <param name="text">The exact displayed draft.</param>
    /// <param name="lastValidAuthoredText">The preceding syntactically valid authored state.</param>
    /// <param name="usesSettingOverrides">Whether edits are authored as setting overrides.</param>
    internal ContentPackEditorDocumentState(
        string authoredText,
        string text,
        string lastValidAuthoredText,
        bool usesSettingOverrides
    )
    {
        AuthoredText = authoredText;
        Text = text;
        LastValidAuthoredText = lastValidAuthoredText;
        UsesSettingOverrides = usesSettingOverrides;
    }

    /// <summary>
    /// Checks whether two history states represent the same editing intent and draft.
    /// </summary>
    /// <param name="other">The state to compare.</param>
    /// <returns>True when the states match.</returns>
    internal bool Matches(ContentPackEditorDocumentState other) =>
        AuthoredText == other.AuthoredText
        && Text == other.Text
        && UsesSettingOverrides == other.UsesSettingOverrides;
}
