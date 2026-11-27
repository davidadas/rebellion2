using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;

/// <summary>
/// Edits the drawing order of linked gameplay images without rewriting their image files.
/// </summary>
internal sealed class ContentEditorGameplayArtworkEditor
{
    private readonly ContentPackEditorView view;
    private readonly ContentPackEditorProject project;
    private readonly ContentPackEditorRecord record;
    private readonly ArtworkEffectPreviewFrame source;
    private readonly Action persist;
    private readonly Action<Action> execute;
    private readonly Stack<GameplayArtworkLayer[]> undo = new();
    private readonly Stack<GameplayArtworkLayer[]> redo = new();
    private GameplayArtworkLayer[] order;
    private GameplayArtworkLayer[] saved;
    private int selected;

    /// <summary>
    /// Captures the linked images and the unit's current drawing order for an isolated draft.
    /// </summary>
    /// <param name="view">The existing artwork workspace owner.</param>
    /// <param name="project">The working pack.</param>
    /// <param name="record">The unit whose images are being arranged.</param>
    /// <param name="source">The combined preview with its actual image bounds.</param>
    /// <param name="persist">Saves the pack and refreshes its form after applying the setting.</param>
    /// <param name="execute">Reports operation failures through the editor's existing error UI.</param>
    internal ContentEditorGameplayArtworkEditor(
        ContentPackEditorView view,
        ContentPackEditorProject project,
        ContentPackEditorRecord record,
        ArtworkEffectPreviewFrame source,
        Action persist,
        Action<Action> execute
    )
    {
        this.view = view;
        this.project = project;
        this.record = record;
        this.source = source;
        this.persist = persist;
        this.execute = execute;
        order = ReadOrder(record).ToArray();
        saved = order.ToArray();
        selected = source.Layers.Count > 0 ? source.Layers[0].Index : -1;
    }

    /// <summary>
    /// Reads a unit's effective setting, leaving older packs on the established drawing order.
    /// </summary>
    /// <param name="record">The effective unit definition in the editor.</param>
    /// <returns>The complete back-to-front stack.</returns>
    internal static IReadOnlyList<GameplayArtworkLayer> ReadOrder(ContentPackEditorRecord record) =>
        GameplayArtworkOrder.Resolve(
            record
                .GetField(nameof(BaseGameEntity.ArtworkLayerOrder))
                .Values.Select(value =>
                    (GameplayArtworkLayer)Enum.Parse(typeof(GameplayArtworkLayer), value)
                )
        );

    /// <summary>
    /// Presents linked artwork using the existing layer selection and ordering controls.
    /// </summary>
    internal void Show()
    {
        view.ShowGeneratedArtwork(
            "Combined gameplay layers",
            record.Name + " · Order applies to this unit’s cards and states",
            new[] { Preview(order) },
            0,
            selected,
            !order.SequenceEqual(saved),
            new ArtworkEffectActions
            {
                Select = index => selected = index,
                Reorder = Reorder,
                Save = () => execute(Save),
                SaveLabel = "Apply to all cards",
                Undo = undo.Count > 0 ? Undo : null,
                Redo = redo.Count > 0 ? Redo : null,
                ReadCurrent = () => new[] { Preview(saved) },
            }
        );
    }

    /// <summary>
    /// Places one linked image on the other side of its visible neighbor.
    /// </summary>
    /// <param name="index">The selected semantic layer identifier.</param>
    /// <param name="target">The neighboring semantic layer identifier.</param>
    private void Reorder(int index, int target)
    {
        int from = Array.IndexOf(order, (GameplayArtworkLayer)index);
        int to = Array.IndexOf(order, (GameplayArtworkLayer)target);
        if (from < 0 || to < 0 || from == to)
            return;
        undo.Push(order.ToArray());
        redo.Clear();
        var changed = order.ToList();
        GameplayArtworkLayer layer = changed[from];
        changed.RemoveAt(from);
        changed.Insert(to, layer);
        order = changed.ToArray();
        selected = index;
        Show();
    }

    /// <summary>
    /// Restores the previous draft order without changing any pack files.
    /// </summary>
    private void Undo()
    {
        redo.Push(order);
        order = undo.Pop();
        Show();
    }

    /// <summary>
    /// Reapplies a draft ordering step without changing any pack files.
    /// </summary>
    private void Redo()
    {
        undo.Push(order);
        order = redo.Pop();
        Show();
    }

    /// <summary>
    /// Saves only the unit's order setting, retaining the referenced images unchanged.
    /// </summary>
    private void Save()
    {
        project.SetFieldValues(
            record.GetField(nameof(BaseGameEntity.ArtworkLayerOrder)),
            order.Select(layer => layer.ToString())
        );
        persist();
        saved = order.ToArray();
        Show();
    }

    /// <summary>
    /// Arranges the borrowed textures without flattening active states into one image.
    /// </summary>
    /// <param name="stack">The semantic drawing order.</param>
    /// <returns>The combined frame for the current draft or saved setting.</returns>
    private ArtworkEffectPreviewFrame Preview(IEnumerable<GameplayArtworkLayer> stack) =>
        new(
            source.Caption,
            source.BaseImage,
            stack
                .SelectMany(layer => source.Layers.Where(image => image.Index == (int)layer))
                .ToArray(),
            size: source.Size,
            baseUv: source.BaseUv
        );
}
