using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Assigns unit artwork to the same faction, unit, and image-slot layout as pack definitions.
/// </summary>
internal static class ContentEditorArtworkPaths
{
    /// <summary>
    /// Gets a stable image address for a direct unit artwork member.
    /// </summary>
    /// <param name="field">The image slot receiving artwork.</param>
    /// <param name="preserveCurrent">Whether an existing unit-folder filename should be retained.</param>
    /// <returns>The extensionless slot address, or null for other editor fields.</returns>
    internal static string GetAddress(ContentPackEditorField field, bool preserveCurrent = true)
    {
        if (
            field.MediaType != ContentPackEditorMediaType.Image
            || field.IsList
            || field.IsMediaDirectory
            || field.XmlAttributeName != null
            || field.Container != null && field.Container != field.Record.Element
        )
            return null;
        string category = field.Record.Kind switch
        {
            "Officer" => "Officers",
            "CapitalShip" => "CapitalShips",
            "Starfighter" => "Starfighters",
            "Regiment" => "Regiments",
            "SpecialForce" => "SpecialForces",
            "Building" => "Facilities",
            _ => null,
        };
        if (category == null)
            return null;
        string id = field.Record.ID;
        string faction = field.Record.Catalog.Faction;
        ValidateSegment(id);
        ValidateSegment(faction);
        string scope = faction == "Shared" ? "Shared" : "Factions/" + faction;
        string folder = "Pack/" + scope + "/Units/" + category + "/" + id + "/";
        string inherited = field.Document.GetInheritedRecordMember(
            field.Record.Element,
            field.MemberName
        );
        string current = field.Value;
        if (ContentEditorUnitArtworkLayout.IsHeadquartersIcon(field.Record))
            foreach (string value in new[] { current, inherited })
                if (
                    !string.IsNullOrWhiteSpace(value)
                    && value.StartsWith("Pack/", StringComparison.Ordinal)
                    && !IsLegacy(value)
                )
                    return Path.ChangeExtension(value, null);
        string member = field.MemberName;
        if (member.EndsWith("ImagePath", StringComparison.Ordinal))
            member = member[..^"ImagePath".Length];
        string role = Regex.Replace(member, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
        foreach (
            string value in preserveCurrent ? new[] { current, inherited } : new[] { inherited }
        )
            if (
                !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(folder, StringComparison.Ordinal)
                && !value[folder.Length..].Contains('/')
                && (
                    Path.GetFileNameWithoutExtension(value) == role
                    || Path.GetFileNameWithoutExtension(value)
                        .EndsWith("-" + role, StringComparison.Ordinal)
                    || preserveCurrent
                        && value == current
                        && field
                            .Record.Element.Elements()
                            .Count(element => !element.HasElements && element.Value == value) == 1
                )
            )
                return Path.ChangeExtension(value, null);
        return folder + role;
    }

    /// <summary>
    /// Recognizes the import and generated-output locations used by older editor versions.
    /// </summary>
    /// <param name="address">The saved pack image address.</param>
    /// <returns>Whether the address belongs to the editor's previous artwork layout.</returns>
    internal static bool IsLegacy(string address) =>
        address?.StartsWith("Pack/Assets/", StringComparison.Ordinal) == true;

    /// <summary>
    /// Rejects identifiers that would change the directory structure or collide across platforms.
    /// </summary>
    /// <param name="value">The authored unit or faction identifier.</param>
    private static void ValidateSegment(string value)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0
            || value.EndsWith('.')
            || value.EndsWith(' ')
        )
            throw new InvalidDataException(
                "Unit artwork requires a unit and faction ID that can be used as folder names."
            );
    }
}
