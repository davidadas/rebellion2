using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Keeps release drafts and their icon inside the editable project, separate from game content.
/// </summary>
internal static class ContentEditorPublishingStorage
{
    internal const string DirectoryName = ".publishing";

    /// <summary>
    /// Reads a saved publishing draft or derives initial listing text from the pack identity.
    /// </summary>
    /// <param name="project">The editable pack.</param>
    /// <returns>The editable publishing draft.</returns>
    internal static ContentEditorPublishingSettings Load(ContentPackEditorProject project)
    {
        string path = Path.Combine(project.PackRootPath, DirectoryName, "settings.json");
        ContentEditorPublishingSettings settings = File.Exists(path)
            ? JsonUtility.FromJson<ContentEditorPublishingSettings>(File.ReadAllText(path))
            : new ContentEditorPublishingSettings
            {
                PackageName = Regex.Replace(project.Definition.ID, "[^A-Za-z0-9_]", "_"),
                Readme = "# " + project.Definition.DisplayName + "\n\n",
            };
        if (settings == null)
            throw new InvalidDataException("The publishing draft could not be read.");
        settings.IconPath = File.Exists(
            Path.Combine(project.PackRootPath, DirectoryName, "icon.png")
        )
            ? Path.Combine(project.PackRootPath, DirectoryName, "icon.png")
            : string.Empty;
        return settings;
    }

    /// <summary>
    /// Persists a publishing draft without changing the authored game definitions.
    /// </summary>
    /// <param name="project">The owning editable pack.</param>
    /// <param name="settings">The current publishing fields.</param>
    internal static void Save(
        ContentPackEditorProject project,
        ContentEditorPublishingSettings settings
    )
    {
        string folder = Path.Combine(project.PackRootPath, DirectoryName);
        string path = Path.Combine(folder, "settings.json");
        ContentEditorPaths.VerifyWritePath(project, path);
        Directory.CreateDirectory(folder);
        if (!string.IsNullOrWhiteSpace(settings.IconPath))
        {
            byte[] icon = ReadIcon(settings.IconPath);
            string destination = Path.Combine(folder, "icon.png");
            ContentEditorPaths.VerifyWritePath(project, destination);
            ContentPackEditorStorage.WriteBytesAtomically(destination, icon);
        }
        ContentEditorPublishingSettings copy =
            JsonUtility.FromJson<ContentEditorPublishingSettings>(JsonUtility.ToJson(settings));
        copy.IconPath = string.Empty;
        ContentPackEditorStorage.WriteBytesAtomically(
            path,
            System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(copy, true))
        );
        settings.IconPath = File.Exists(Path.Combine(folder, "icon.png"))
            ? Path.Combine(folder, "icon.png")
            : string.Empty;
    }

    /// <summary>
    /// Stores a confirmed icon without changing its imported source or the listing fields.
    /// </summary>
    /// <param name="project">The editable project receiving the icon.</param>
    /// <param name="bytes">The confirmed 256 by 256 PNG.</param>
    /// <returns>The project-owned icon path.</returns>
    internal static string SaveIcon(ContentPackEditorProject project, byte[] bytes)
    {
        ValidateIcon(bytes);
        string path = Path.Combine(project.PackRootPath, DirectoryName, "icon.png");
        ContentEditorPaths.VerifyWritePath(project, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        ContentPackEditorStorage.WriteBytesAtomically(path, bytes);
        return path;
    }

    /// <summary>
    /// Reads and checks the exact icon bytes that will be included in a release.
    /// </summary>
    /// <param name="path">The icon selected by the author.</param>
    /// <returns>The validated PNG bytes.</returns>
    internal static byte[] ReadIcon(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        ValidateIcon(bytes);
        return bytes;
    }

    /// <summary>
    /// Checks encoded icon data before writing a draft or creating a release.
    /// </summary>
    /// <param name="bytes">The complete candidate PNG.</param>
    private static void ValidateIcon(byte[] bytes)
    {
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.Length < signature.Length)
            throw new InvalidDataException("The package icon must be a PNG image.");
        for (int i = 0; i < signature.Length; i++)
            if (bytes[i] != signature[i])
                throw new InvalidDataException("The package icon must be a PNG image.");
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (
                !ImageConversion.LoadImage(texture, bytes, true)
                || texture.width != 256
                || texture.height != 256
            )
                throw new InvalidDataException(
                    "The package icon must be exactly 256 × 256 pixels."
                );
        }
        finally
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
