using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game.Messages;
using UnityEngine;

/// <summary>
/// Applies semantic authoring requests while keeping project mutations out of Unity views.
/// </summary>
public sealed class ContentPackEditorWorkspaceController : IDisposable
{
    private readonly ContentPackEditorView view;
    private readonly ContentPackEditorSession session = new ContentPackEditorSession();
    private ContentAssets assets;

    public ContentPackEditorProject Project => session.Project;

    /// <summary>
    /// Connects the workspace's semantic editing requests to their application owner.
    /// </summary>
    /// <param name="view">The authored workspace presentation.</param>
    public ContentPackEditorWorkspaceController(ContentPackEditorView view)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        view.DuplicateRequested += DuplicateRecord;
        view.AddUnitRequested += AddUnit;
        view.AddTraitRequested += AddTrait;
        view.AddEventRequested += AddEvent;
        view.AddFactionRequested += AddFaction;
        view.AddSectorRequested += AddSector;
        view.AddPlanetRequested += AddPlanet;
        view.AddMessageRequested += AddMessage;
        view.AddNamePoolRequested += AddNamePool;
        view.RemoveMessageRequested += RemoveMessage;
        view.ClearFieldsRequested += ClearFields;
        view.DeleteRequested += DeleteRecord;
        view.XmlSubmitted += ApplyXml;
        view.FieldSubmitted += ApplyField;
        view.FieldValidationRequested += ValidateField;
        view.CollectionAddRequested += AddCollectionEntry;
        view.CollectionRemoveRequested += RemoveCollectionEntry;
        view.CollectionQuantitiesSubmitted += ApplyCollectionQuantities;
        view.MapEditStarted += session.BeginMapEdit;
        view.MapMoved += MoveRecord;
        view.MapEditEnded += session.EndMapEdit;
        view.SelectionRequested += SelectRecord;
        view.SectionRequested += SelectSection;
    }

    /// <summary>
    /// Presents a newly resolved workspace before replacing the controller's active project.
    /// </summary>
    /// <param name="project">The working content.</param>
    /// <param name="contentAssets">The assets whose lifetime is owned by the application.</param>
    public void RenderProject(ContentPackEditorProject project, ContentAssets contentAssets)
    {
        ContentPackEditorProject previousProject = session.Project;
        ContentPackEditorSelection previousSelection = session.Selection;
        session.Open(project);
        try
        {
            view.RenderProject(project, contentAssets);
        }
        catch
        {
            session.Restore(previousProject, previousSelection);
            throw;
        }
        assets = contentAssets;
    }

    /// <summary>
    /// Releases bindings to a closed project without disposing application-owned media.
    /// </summary>
    public void ClearProject()
    {
        view.ClearProject();
        session.Open(null);
        assets = null;
    }

    /// <summary>
    /// Computes an integer layout off-thread and commits only a complete result as one undoable edit.
    /// </summary>
    /// <param name="layout">The captured record positions and rendered footprints.</param>
    /// <param name="cancellationToken">Cancellation before any authored coordinates are changed.</param>
    /// <returns>The number of moved map records.</returns>
    public async Task<int> ArrangeMapAsync(
        ContentEditorMapLayout layout,
        CancellationToken cancellationToken = default
    )
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));
        ContentPackEditorProject current =
            Project ?? throw new InvalidOperationException("Open a content pack first.");
        Vector2Int[] positions = await Task.Run(
            () =>
                RectangleLayout.Arrange(
                    layout.Items,
                    layout.Bounds,
                    layout.Gap,
                    layout.CoordinateScale,
                    cancellationToken
                ),
            cancellationToken
        );
        cancellationToken.ThrowIfCancellationRequested();
        if (Project != current)
            throw new OperationCanceledException("The map workspace was replaced.");
        Vector2Int[] authoredPositions = positions
            .Select(position => new Vector2Int(
                checked(position.x + layout.Origin.x),
                checked(position.y + layout.Origin.y)
            ))
            .ToArray();
        for (int i = 0; i < layout.Records.Count; i++)
        {
            ContentPackEditorRecord record = layout.Records[i];
            current.RequireOwnedRecord(record);
            if (
                (long)record.X - layout.Origin.x != layout.Items[i].Position.x
                || (long)record.Y - layout.Origin.y != layout.Items[i].Position.y
            )
                throw new ArgumentException("The map changed. Try arranging it again.");
        }
        var placements = layout
            .Records.Select(
                (record, i) => (Record: record, Position: authoredPositions[i], Index: i)
            )
            .Where(item => positions[item.Index] != layout.Items[item.Index].Position)
            .Select(item => (item.Record, item.Position.x, item.Position.y))
            .ToArray();
        int moved = placements.Length;
        using (current.BeginEdit("Auto arrange map"))
            current.Move(placements);
        if (moved > 0)
            view.ShowArrangedMap();
        return moved;
    }

    /// <summary>
    /// Applies one scalar or collection edit through the validated project boundary.
    /// </summary>
    /// <param name="field">The authored member.</param>
    /// <param name="values">The complete proposed values.</param>
    private void ApplyField(ContentPackEditorField field, IReadOnlyList<string> values)
    {
        if (field.IsList)
            Project.SetFieldValues(field, values);
        else
            Project.SetField(field, values.Single());
    }

    /// <summary>
    /// Adds an independent setup entry through project history.
    /// </summary>
    /// <param name="collection">The destination collection.</param>
    private void AddCollectionEntry(ContentPackEditorCollection collection)
    {
        Project.AddCollectionEntry(collection);
        view.RefreshInspector();
    }

    /// <summary>
    /// Deletes one setup entry through project history.
    /// </summary>
    /// <param name="collection">The owning collection.</param>
    /// <param name="index">The selected entry.</param>
    private void RemoveCollectionEntry(ContentPackEditorCollection collection, int index)
    {
        Project.RemoveCollectionEntry(collection, index);
        view.RefreshInspector();
    }

    /// <summary>
    /// Applies selected unit quantities without rewriting unaffected entries.
    /// </summary>
    /// <param name="collection">The unit collection.</param>
    /// <param name="values">The selected types and quantities.</param>
    private void ApplyCollectionQuantities(
        ContentPackEditorCollection collection,
        IReadOnlyDictionary<string, int> values
    )
    {
        Project.SetCollectionQuantities(collection, values);
        view.RefreshInspector();
    }

    /// <summary>
    /// Validates unchanged fields without committing another history entry.
    /// </summary>
    /// <param name="field">The field whose form is being left.</param>
    private void ValidateField(ContentPackEditorField field) => Project.ValidateField(field);

    /// <summary>
    /// Records a requested selection before rendering its derived inspector state.
    /// </summary>
    /// <param name="record">The requested definition.</param>
    /// <param name="page">Its logical navigation page.</param>
    /// <param name="sector">The requested map context.</param>
    private void SelectRecord(
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        ContentPackEditorRecord sector
    )
    {
        session.Select(record, page, sector);
        view.RenderSelection(session.Selection);
    }

    /// <summary>
    /// Records a requested inspector tab before updating the controls.
    /// </summary>
    /// <param name="section">The requested inspector tab.</param>
    private void SelectSection(string section)
    {
        session.SelectSection(section);
        view.RenderSection(section);
    }

    /// <summary>
    /// Applies a scoped XML draft and presents the resulting record at the same scope.
    /// </summary>
    /// <param name="scope">The block being edited.</param>
    /// <param name="text">The replacement XML.</param>
    /// <param name="scopeIndex">The visible scope to retain.</param>
    private void ApplyXml(ContentEditorXmlScope scope, string text, int scopeIndex) =>
        view.ShowAppliedXml(Project.SetXml(scope, text), scopeIndex);

    /// <summary>
    /// Creates an independent copy through the project identity and history rules.
    /// </summary>
    /// <param name="record">The selected source definition.</param>
    private void DuplicateRecord(ContentPackEditorRecord record) =>
        view.ShowCreatedRecord(Project.DuplicateRecord(record));

    /// <summary>
    /// Creates a blank unit with the recruiting or manufacturing faction explicitly requested.
    /// </summary>
    /// <param name="catalog">The destination unit catalog.</param>
    /// <param name="factionInstanceID">The requested faction, or null for an unassigned unit.</param>
    private void AddUnit(ContentPackEditorCatalog catalog, string factionInstanceID) =>
        RevealCreatedRecord(Project.AddUnit(catalog, factionInstanceID));

    /// <summary>
    /// Creates a new trait without using another definition as a template.
    /// </summary>
    /// <param name="catalog">The destination trait catalog.</param>
    private void AddTrait(ContentPackEditorCatalog catalog) =>
        RevealCreatedRecord(Project.AddTrait(catalog));

    /// <summary>
    /// Creates an event draft through the normal project identity and history rules.
    /// </summary>
    /// <param name="catalog">The destination event catalog.</param>
    private void AddEvent(ContentPackEditorCatalog catalog) =>
        RevealCreatedRecord(Project.AddEvent(catalog));

    /// <summary>
    /// Requests explicit acknowledgement before adding an untested extra faction.
    /// </summary>
    private void AddFaction()
    {
        if (Project.GetFactionOptions().Count >= 2)
            view.ConfirmAdditionalFaction(CreateFaction);
        else
            CreateFaction();
    }

    /// <summary>
    /// Creates the faction after any required acknowledgement has completed.
    /// </summary>
    private void CreateFaction() => RevealCreatedRecord(Project.AddFaction());

    /// <summary>
    /// Creates a sector at the position selected by the map presentation.
    /// </summary>
    /// <param name="catalog">The galaxy catalog.</param>
    /// <param name="position">The requested galaxy coordinates.</param>
    private void AddSector(ContentPackEditorCatalog catalog, Vector2Int position) =>
        RevealCreatedRecord(Project.AddSector(catalog, position.x, position.y));

    /// <summary>
    /// Creates a planet in the explicitly requested sector.
    /// </summary>
    /// <param name="sector">The destination sector.</param>
    /// <param name="position">The requested absolute coordinates.</param>
    private void AddPlanet(ContentPackEditorRecord sector, Vector2Int position) =>
        RevealCreatedRecord(Project.AddPlanet(sector, position.x, position.y));

    /// <summary>
    /// Creates a faction message and focuses its title after the project is refreshed.
    /// </summary>
    /// <param name="faction">The recipient faction.</param>
    /// <param name="type">The requested message category.</param>
    private void AddMessage(ContentPackEditorRecord faction, MessageType type)
    {
        ContentPackEditorField title = Project.AddFactionMessage(faction, type);
        view.RenderProject(Project, assets);
        view.FocusField(title);
    }

    /// <summary>
    /// Applies a grouped field clear before rebuilding the current inspector from its edited definitions.
    /// </summary>
    /// <param name="fields">The fields requested by the inspector.</param>
    private void ClearFields(IReadOnlyList<ContentPackEditorField> fields)
    {
        Project.ClearFields(fields);
        view.RenderProject(Project, assets);
    }

    /// <summary>
    /// Adds a ship-name pool and reveals its identifier in the current faction inspector.
    /// </summary>
    /// <param name="faction">The faction receiving the pool.</param>
    private void AddNamePool(ContentPackEditorRecord faction)
    {
        ContentPackEditorField id = Project.AddFactionNamePool(faction);
        view.RenderProject(Project, assets);
        view.FocusField(id);
    }

    /// <summary>
    /// Removes a confirmed message through the project's history boundary.
    /// </summary>
    /// <param name="title">The title identifying the message.</param>
    private void RemoveMessage(ContentPackEditorField title)
    {
        Project.RemoveFactionMessage(title);
        view.RenderProject(Project, assets);
        view.RenderStatus("Starting message deleted. Undo to restore it.", false);
    }

    /// <summary>
    /// Removes a definition through project-wide cleanup before selecting a remaining sibling.
    /// </summary>
    /// <param name="record">The confirmed definition to remove.</param>
    private void DeleteRecord(ContentPackEditorRecord record)
    {
        ContentPackEditorRecord parent = ContentPackEditorProjector.GetParentSector(record);
        Project.RemoveRecord(record);
        view.ShowRemovedRecord(record, parent);
    }

    /// <summary>
    /// Applies a map drag while the session owns its undo group.
    /// </summary>
    /// <param name="record">The moved definition.</param>
    /// <param name="position">Its requested absolute coordinates.</param>
    private void MoveRecord(ContentPackEditorRecord record, Vector2Int position)
    {
        Project.Move(record, position.x, position.y);
        view.RefreshProjectState();
    }

    /// <summary>
    /// Opens a newly authored definition with the session's general inspector tab selected.
    /// </summary>
    /// <param name="record">The definition just created.</param>
    private void RevealCreatedRecord(ContentPackEditorRecord record)
    {
        session.SelectSection("General");
        view.ShowCreatedRecord(record);
    }

    /// <summary>
    /// Removes subscriptions and closes gestures without owning the Unity view or its assets.
    /// </summary>
    public void Dispose()
    {
        view.DuplicateRequested -= DuplicateRecord;
        view.AddUnitRequested -= AddUnit;
        view.AddTraitRequested -= AddTrait;
        view.AddEventRequested -= AddEvent;
        view.AddFactionRequested -= AddFaction;
        view.AddSectorRequested -= AddSector;
        view.AddPlanetRequested -= AddPlanet;
        view.AddMessageRequested -= AddMessage;
        view.AddNamePoolRequested -= AddNamePool;
        view.RemoveMessageRequested -= RemoveMessage;
        view.ClearFieldsRequested -= ClearFields;
        view.DeleteRequested -= DeleteRecord;
        view.XmlSubmitted -= ApplyXml;
        view.FieldSubmitted -= ApplyField;
        view.FieldValidationRequested -= ValidateField;
        view.CollectionAddRequested -= AddCollectionEntry;
        view.CollectionRemoveRequested -= RemoveCollectionEntry;
        view.CollectionQuantitiesSubmitted -= ApplyCollectionQuantities;
        view.MapEditStarted -= session.BeginMapEdit;
        view.MapMoved -= MoveRecord;
        view.MapEditEnded -= session.EndMapEdit;
        view.SelectionRequested -= SelectRecord;
        view.SectionRequested -= SelectSection;
        session.Dispose();
        assets = null;
    }
}
