using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Units;

/// <summary>
/// Checks that a loaded authored scenario can construct and initialize a playable campaign.
/// </summary>
internal static class ContentEditorPackValidator
{
    private const int _mediaFrameBudgetMilliseconds = 8;
    private static readonly object _fieldDataKey = new object();

    /// <summary>
    /// Checks authored references, campaign startup, and media decoding while keeping authoring responsive.
    /// </summary>
    /// <param name="project">The saved working content to validate.</param>
    /// <param name="progress">Optional status reporting for the current validation check.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>Non-blocking authoring warnings after all validation checks succeed.</returns>
    public static async Task<IReadOnlyList<string>> ValidateAsync(
        ContentPackEditorProject project,
        Action<string> progress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        List<string> warnings = new List<string>();
        foreach (var check in GetStartupChecks(project, warnings))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke(
                $"Checking scenario: {check.Pack.Scenario.ID} ({check.Summary.PlayerFactionID}, {check.Summary.GalaxySize}, {check.Summary.Difficulty}, {check.Summary.ResourceAvailability})"
            );
            CampaignContentValidator.ValidateStartup(check.Pack, check.Summary);
            await Task.Yield();
        }
        await ValidateMediaAsync(project, progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Reports legal definitions that disable production, missions, or bookmark rows.
    /// </summary>
    /// <param name="pack">The validated scenario and effective inherited catalogs.</param>
    /// <returns>Warnings identifying the affected scenario and definition.</returns>
    private static IEnumerable<string> GetWarnings(ContentPack pack)
    {
        GameDataCatalog data = pack.GameData;
        string prefix = $"Scenario '{pack.Scenario.ID}': ";
        foreach (Building building in data.Buildings)
            if (building.ProductionType != ManufacturingType.None && building.ProcessRate == 0)
                yield return prefix
                    + $"Building '{building.TypeID}' has a zero production rate and cannot produce units.";
        IEnumerable<IManufacturable> products = data
            .Buildings.Cast<IManufacturable>()
            .Concat(data.CapitalShips)
            .Concat(data.Starfighters)
            .Concat(data.Regiments)
            .Concat(data.SpecialForces);
        foreach (IManufacturable product in products)
            if (product.ManufacturingFactionInstanceIDs is { Count: 0 })
                yield return prefix
                    + $"Product '{product.GetTypeID()}' has no allowed manufacturing factions and cannot be built.";
        foreach (var faction in data.Factions)
            if (
                ContentPackEditorFieldSchema.MissionIDs.All(id =>
                    faction.DisallowedMissionTypeIDs?.Contains(id) == true
                )
            )
                yield return prefix
                    + $"Faction '{faction.InstanceID}' disables every normal mission type.";
        foreach (FactionTheme theme in new FactionThemeLibrary(data.FactionThemes).GetAllThemes())
            if (theme.StrategyBookmarkLayout?.GetSlotCount() == 0)
                yield return prefix
                    + $"Faction theme '{theme.FactionInstanceID}' has no usable bookmark rows.";
    }

    /// <summary>
    /// Decodes referenced media once per resolved file while releasing each asset before continuing.
    /// </summary>
    /// <param name="project">The saved working content to validate.</param>
    /// <param name="progress">Optional status reporting for the current asset.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of referenced-media validation.</returns>
    private static async Task ValidateMediaAsync(
        ContentPackEditorProject project,
        Action<string> progress,
        CancellationToken cancellationToken
    )
    {
        ContentFileResolver resolver = project.CreateResolver();
        HashSet<string> checkedFiles = new HashSet<string>(StringComparer.Ordinal);
        Stopwatch batch = Stopwatch.StartNew();
        foreach (
            ContentPackEditorField field in project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => new[] { record }.Concat(record.Planets))
                .OrderBy(record => record.Catalog.Document.IsInherited)
                .SelectMany(record => record.GetFields())
                .Where(field => field.IsMedia)
        )
        foreach (string value in field.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            string address = field.GetMediaAddress(value);
            IEnumerable<string> addresses = field.IsMediaDirectory
                ? resolver
                    .EnumerateFileAddresses(address)
                    .Where(candidate =>
                        field.MediaExtensions.Contains(
                            Path.GetExtension(candidate),
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                : new[] { address };
            foreach (string candidate in addresses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string file =
                    resolver.ResolveFile(candidate, field.MediaExtensions)
                    ?? throw new FileNotFoundException("Media not found: " + candidate);
                if (!checkedFiles.Add(file))
                    continue;
                progress?.Invoke("Checking media: " + candidate);
                await ContentEditorMediaValidator.ValidateAsync(
                    resolver,
                    candidate,
                    field.MediaType,
                    cancellationToken
                );
                if (batch.ElapsedMilliseconds >= _mediaFrameBudgetMilliseconds)
                {
                    await Task.Yield();
                    batch.Restart();
                }
            }
        }
    }

    /// <summary>
    /// Validates authored fields and loads the scenarios needed by the startup checks.
    /// </summary>
    /// <param name="project">The working content to check.</param>
    /// <param name="warnings">Receives scenario-level warnings once per loaded scenario.</param>
    /// <returns>The loaded catalog and launch configuration for each startup check.</returns>
    private static IEnumerable<(ContentPack Pack, GameSummary Summary)> GetStartupChecks(
        ContentPackEditorProject project,
        ICollection<string> warnings
    )
    {
        ValidateFields(project);
        ContentFileResolver resolver = project.CreateResolver();
        string[] scenarios = project
            .Definition.ScenarioPaths.Select(path =>
                ContentDefinitionSerializer
                    .DeserializeText<ContentScenarioDefinition>(
                        ContentXmlOverrides.Load(resolver, "Pack/" + path)
                    )
                    .ID
            )
            .Append(project.Definition.DefaultScenarioID)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string scenario in scenarios)
        {
            ContentPack pack = ContentPackLoader.OpenFolder(
                project.ContentRootPath,
                project.ProjectRootPath,
                scenario
            );
            CampaignContentValidator.ValidateDefinitions(pack);
            foreach (string warning in GetWarnings(pack))
                warnings.Add(warning);
            foreach (GameSummary summary in CampaignContentValidator.GetStartupConfigurations(pack))
                yield return (pack, summary);
        }
    }

    /// <summary>
    /// Checks authored fields before startup, including values narrowed by deserialization.
    /// </summary>
    /// <param name="project">The saved project or immutable play/export snapshot.</param>
    internal static void ValidateFields(ContentPackEditorProject project)
    {
        if (project.IsDirty)
            throw new InvalidOperationException("Save the project before validating it.");
        ContentEditorValidationIssue issue = GetFieldErrors(project).FirstOrDefault();
        if (issue != null)
            throw issue.Exception;
    }

    /// <summary>
    /// Checks current document fields and their media references without saving the pack.
    /// </summary>
    /// <param name="project">The working pack or a saved snapshot.</param>
    /// <returns>All field failures from the completed structural pass.</returns>
    internal static IReadOnlyList<ContentEditorValidationIssue> GetFieldErrors(
        ContentPackEditorProject project
    )
    {
        IReadOnlyList<ContentPackEditorCatalog> catalogs = project.GetCatalogs();
        foreach (ContentPackEditorCatalog catalog in catalogs)
            catalog.ValidateAuthoredTypeIDs();
        ContentFileResolver resolver = project.CreateResolver();
        IReadOnlyDictionary<string, string> factions = project.GetFactionOptions();
        IReadOnlyDictionary<string, string> traits = project.GetTraitOptions();
        List<ContentEditorValidationIssue> issues = new List<ContentEditorValidationIssue>();
        foreach (
            ContentPackEditorRecord record in catalogs
                .SelectMany(catalog => catalog.Records)
                .SelectMany(record => new[] { record }.Concat(record.Planets))
        )
        foreach (ContentPackEditorField field in record.GetFields())
        {
            try
            {
                project.ValidateFieldValues(field, field.Values, resolver, factions, traits);
            }
            catch (ArgumentException exception)
            {
                string detail = exception.Message.Split('\n')[0];
                ArgumentException contextual = new ArgumentException(
                    detail.StartsWith(field.Document.Address + ":", StringComparison.Ordinal)
                        ? detail
                        : $"{field.Document.Address}: {record.ID} / {field.Caption}: {detail}",
                    exception
                );
                contextual.Data[_fieldDataKey] = field;
                issues.Add(new ContentEditorValidationIssue(contextual, field, detail));
            }
        }
        return issues;
    }

    /// <summary>
    /// Gets the authored field attached to a contextual validation failure.
    /// </summary>
    /// <param name="exception">The validation failure.</param>
    /// <returns>The responsible field, or null when the failure is not field-specific.</returns>
    internal static ContentPackEditorField GetValidationField(Exception exception) =>
        exception?.Data[_fieldDataKey] as ContentPackEditorField;
}
