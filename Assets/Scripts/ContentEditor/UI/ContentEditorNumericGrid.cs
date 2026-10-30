using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Aligns authored numeric lookup tables without adding values to sparse dictionaries.
/// </summary>
public sealed class ContentEditorNumericGrid
{
    private readonly Dictionary<(int Score, int Column), ContentPackEditorField> cells = new();
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<int> Scores { get; }
    public IReadOnlyList<ContentPackEditorField> Settings { get; }
    public bool IsProbability { get; }

    /// <summary>
    /// Projects the selected page's numeric dictionaries into one editable matrix.
    /// </summary>
    /// <param name="fields">The selected page's authored rules.</param>
    /// <param name="missionID">The optional mission identifier used to name its success curve.</param>
    public ContentEditorNumericGrid(
        IReadOnlyList<ContentPackEditorField> fields,
        string missionID = null
    )
    {
        var tables = fields
            .SelectMany(field =>
                field
                    .XmlElement.AncestorsAndSelf()
                    .TakeWhile(element => element != field.Record.Element)
                    .Where(element =>
                        ContentPackEditorFieldSchema.GetValueType(field.Record.Element, element)
                        == typeof(Dictionary<int, int>)
                    )
                    .Select(table => (Record: field.Record, Table: table))
            )
            .GroupBy(table => table.Table)
            .Select(group => group.First())
            .OrderBy(table => table.Table.Name.LocalName == missionID ? 0 : 1)
            .ToArray();
        IsProbability =
            tables.Length > 0
            && tables.All(table =>
                table.Table.Ancestors().Any(element => element.Name == "ProbabilityTables")
            );
        Columns = tables
            .Select(table =>
                table.Table.Name.LocalName == missionID
                    ? "Success"
                    : ContentPackEditorField.FormatName(table.Table.Name.LocalName)
            )
            .ToArray();
        SortedSet<int> scores = new();
        foreach (var table in tables)
        foreach (XElement entry in table.Table.Elements("Entry"))
            scores.Add(int.Parse(entry.Element("Key").Value, CultureInfo.InvariantCulture));
        if (tables.Length > 0 && scores.Count == 0)
            scores.Add(0);
        Scores = scores.ToArray();
        for (int column = 0; column < tables.Length; column++)
            foreach (int score in Scores)
                cells.Add(
                    (score, column),
                    CreateCell(tables[column].Record, tables[column].Table, score, Columns[column])
                );
        Settings = fields
            .Where(field =>
                field.XmlElement.AncestorsAndSelf().Contains(field.Record.Element)
                && !tables.Any(table => field.XmlElement.AncestorsAndSelf().Contains(table.Table))
            )
            .ToArray();
    }

    /// <summary>
    /// Gets an editable cell, leaving absent entries detached until a value is entered.
    /// </summary>
    /// <param name="score">The authored score threshold.</param>
    /// <param name="column">The probability curve column.</param>
    /// <returns>The editable field, or null when the coordinates are outside the grid.</returns>
    public ContentPackEditorField GetCell(int score, int column) =>
        cells.TryGetValue((score, column), out var field) ? field : null;

    /// <summary>
    /// Binds an optional dictionary entry to the ordinary validated project field contract.
    /// </summary>
    /// <param name="record">The owning rules record.</param>
    /// <param name="table">The existing probability table.</param>
    /// <param name="score">The row's threshold.</param>
    /// <param name="caption">The column's readable name.</param>
    /// <returns>A field supporting insertion, editing and clearing through project history.</returns>
    private static ContentPackEditorField CreateCell(
        ContentPackEditorRecord record,
        XElement table,
        int score,
        string caption
    )
    {
        string key = score.ToString(CultureInfo.InvariantCulture);
        XElement entry =
            table.Elements("Entry").FirstOrDefault(item => item.Element("Key")?.Value == key)
            ?? new XElement("Entry", new XElement("Key", key), new XElement("Value"));
        return new ContentPackEditorField(
            record,
            entry.Element("Value"),
            caption + " / " + key,
            valueTypeOverride: typeof(int),
            groupOverride: table
                .AncestorsAndSelf()
                .TakeWhile(element => element != record.Element)
                .Last()
                .Name.LocalName,
            attach: () =>
            {
                if (entry.Parent != null)
                    return;
                XElement next = table
                    .Elements("Entry")
                    .FirstOrDefault(item =>
                        int.Parse(item.Element("Key").Value, CultureInfo.InvariantCulture) > score
                    );
                if (next == null)
                    table.Add(entry);
                else
                    next.AddBeforeSelf(entry);
            },
            detach: () =>
            {
                if (entry.Parent != null)
                    entry.Remove();
            }
        );
    }
}
