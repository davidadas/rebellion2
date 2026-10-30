using System;
using System.IO;
using System.Xml;

/// <summary>
/// Represents one editable XML document in a content-pack inheritance chain.
/// </summary>
public sealed class ContentPackEditorDocument
{
    private string savedText;
    private string text;

    public string Address { get; }

    public string DisplayName { get; }

    public string Group { get; }

    public string SourcePath { get; }

    public string TargetPath { get; }

    public bool IsInherited { get; private set; }

    public bool IsDirty { get; private set; }

    public string Text => text;
    internal bool IsRemoved => text == null;

    /// <summary>
    /// Reports a working-text change with its previous contents for the project edit history.
    /// </summary>
    public event Action<ContentPackEditorDocument, string> Changed;

    /// <summary>
    /// Creates an editable document over a resolved source and local target.
    /// </summary>
    /// <param name="address">The pack-scoped content address.</param>
    /// <param name="sourcePath">The resolved source path.</param>
    /// <param name="targetPath">The selected pack's local override path.</param>
    public ContentPackEditorDocument(string address, string sourcePath, string targetPath)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        SourcePath = Path.GetFullPath(
            sourcePath ?? throw new ArgumentNullException(nameof(sourcePath))
        );
        TargetPath = Path.GetFullPath(
            targetPath ?? throw new ArgumentNullException(nameof(targetPath))
        );
        string relativePath = address.StartsWith("Pack/", StringComparison.Ordinal)
            ? address[5..]
            : address;
        DisplayName = relativePath;
        int separator = relativePath.IndexOf('/');
        Group = separator < 0 ? "Pack" : relativePath[..separator];
        IsInherited = !string.Equals(SourcePath, TargetPath, StringComparison.OrdinalIgnoreCase);
        text = File.ReadAllText(SourcePath);
        savedText = text;
    }

    /// <summary>
    /// Creates an unsaved document whose creation can participate in normal edit history.
    /// </summary>
    /// <param name="address">The pack-scoped address.</param>
    /// <param name="targetPath">The new local file path.</param>
    internal ContentPackEditorDocument(string address, string targetPath)
    {
        Address = address;
        SourcePath = TargetPath = Path.GetFullPath(targetPath);
        DisplayName = address[5..];
        int separator = DisplayName.IndexOf('/');
        Group = separator < 0 ? "Pack" : DisplayName[..separator];
    }

    /// <summary>
    /// Replaces the working text and records whether it differs from the source on disk.
    /// </summary>
    /// <param name="value">The edited XML text.</param>
    public void SetText(string value)
    {
        RestoreText(value ?? string.Empty);
    }

    /// <summary>
    /// Restores document contents or their absence during reversible structural edits.
    /// </summary>
    /// <param name="value">The restored XML, or null when the document is removed.</param>
    internal void RestoreText(string value)
    {
        if (value == text)
            return;
        string previous = text;
        text = value;
        IsDirty = !string.Equals(text, savedText, StringComparison.Ordinal);
        Changed?.Invoke(this, previous);
    }

    /// <summary>
    /// Verifies that the working text is well-formed XML without writing it.
    /// </summary>
    public void Validate()
    {
        if (IsRemoved)
            return;
        XmlDocument document = new XmlDocument();
        document.LoadXml(text);
    }

    /// <summary>
    /// Validates and writes the working text to the selected pack's local layer.
    /// </summary>
    public void Save()
    {
        Validate();
        VerifyUnchangedTarget();

        if (IsRemoved)
        {
            if (File.Exists(TargetPath))
                File.Delete(TargetPath);
            savedText = null;
            IsDirty = false;
            return;
        }

        string directory = Path.GetDirectoryName(TargetPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException($"Content document path has no directory: {TargetPath}");
        Directory.CreateDirectory(directory);

        string temporaryPath = TargetPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, text);
            ReplaceFile(temporaryPath, TargetPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        IsInherited = false;
        IsDirty = false;
        savedText = text;
    }

    /// <summary>
    /// Refuses to overwrite a file that another process changed after this document was opened.
    /// </summary>
    internal void VerifyUnchangedTarget()
    {
        bool exists = File.Exists(TargetPath);
        bool changed;
        if (savedText == null)
            changed = exists;
        else if (exists)
            changed =
                IsInherited
                || !string.Equals(
                    File.ReadAllText(TargetPath),
                    savedText,
                    StringComparison.Ordinal
                );
        else
            changed = !IsInherited;
        if (!changed && IsInherited)
            changed =
                !File.Exists(SourcePath)
                || !string.Equals(
                    File.ReadAllText(SourcePath),
                    savedText,
                    StringComparison.Ordinal
                );
        if (changed)
            throw new IOException(
                "Content changed outside the editor. Reopen the pack before saving: " + TargetPath
            );
    }

    /// <summary>
    /// Moves a completed temporary file into place without exposing partial XML.
    /// </summary>
    /// <param name="temporaryPath">The complete temporary file.</param>
    /// <param name="destinationPath">The final document path.</param>
    private static void ReplaceFile(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, null);
        else
            File.Move(temporaryPath, destinationPath);
    }
}
