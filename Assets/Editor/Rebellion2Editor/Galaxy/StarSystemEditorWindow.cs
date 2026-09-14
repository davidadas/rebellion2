using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Util.Serialization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors planet sectors and systems against the dedicated Galaxy Editor preview.
/// </summary>
public sealed class StarSystemEditorWindow : EditorWindow
{
    private const int _markerCenterOffset = 8;
    private const int _selectionRadius = 12;
    private const float _sourceWidth = 853.3333f;
    private const float _sourceHeight = 480f;

    private readonly List<PlanetSector> _sectors = new List<PlanetSector>();
    private PlanetSector _selectedSector;
    private Planet _selectedSystem;
    private FactionTheme _previewTheme;
    private Texture2D _backgroundTexture;
    private Texture2D _hudTexture;
    private Texture2D _planetMarkerTexture;
    private Vector2 _scroll;
    private string _documentPath;
    private bool _hasChanges;
    private PlanetSector _draggedSector;
    private Vector2Int _lastDragMapPosition;

    /// <summary>
    /// Opens the authoring controls and production-rendered galaxy preview.
    /// </summary>
    [MenuItem("Rebellion/Content/Star System Editor")]
    public static void Open()
    {
        StarSystemEditorWindow window = GetWindow<StarSystemEditorWindow>("Star Systems");
        window.minSize = new Vector2(900f, 700f);
        window.Show();
        window.Initialize();
    }

    /// <summary>
    /// Restores content data after a script reload.
    /// </summary>
    private void OnEnable()
    {
        EditorApplication.delayCall += Initialize;
    }

    /// <summary>
    /// Draws document commands and the selected authored object.
    /// </summary>
    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.HelpBox(
            "Drag any star on the map to move its entire sector. The sector and every contained system retain their relative placement.",
            MessageType.Info
        );
        DrawGalaxyCanvas();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawSectorSelection();
        EditorGUILayout.Space(8f);
        if (_selectedSystem != null)
            DrawSystemInspector();
        else if (_selectedSector != null)
            DrawSectorInspector();
        else
            EditorGUILayout.HelpBox("Create or select a sector to begin.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Draws document lifecycle commands and status.
    /// </summary>
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton))
                ReplaceDocument(Array.Empty<PlanetSector>(), null);
            if (GUILayout.Button("Reload Default Campaign", EditorStyles.toolbarButton))
                LoadActivePack();
            if (GUILayout.Button("Open XML", EditorStyles.toolbarButton))
                OpenDocument();
            if (GUILayout.Button("Save As…", EditorStyles.toolbarButton))
                SaveDocumentAs();
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{_sectors.Count} sectors{(_hasChanges ? " · UNSAVED" : string.Empty)}",
                EditorStyles.miniLabel
            );
        }
    }

    /// <summary>
    /// Draws the sector picker and sector collection commands.
    /// </summary>
    private void DrawSectorSelection()
    {
        EditorGUILayout.LabelField("PLANET SECTORS", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            string[] names = _sectors
                .Select(sector => sector.DisplayName ?? "Untitled Sector")
                .ToArray();
            int current = Mathf.Max(0, _sectors.IndexOf(_selectedSector));
            using (new EditorGUI.DisabledScope(names.Length == 0))
            {
                int selected = EditorGUILayout.Popup(current, names);
                if (names.Length > 0 && selected != current)
                    SelectSector(_sectors[selected]);
            }
            if (GUILayout.Button("+", GUILayout.Width(28f)))
                AddSector();
            using (new EditorGUI.DisabledScope(_selectedSector == null))
            {
                if (GUILayout.Button("−", GUILayout.Width(28f)))
                    RemoveSector();
            }
        }
    }

    /// <summary>
    /// Draws editable planet-sector fields and its planetary systems.
    /// </summary>
    private void DrawSectorInspector()
    {
        EditorGUILayout.LabelField("PLANET SECTOR", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _selectedSector.DisplayName = EditorGUILayout.TextField(
            "Name",
            _selectedSector.DisplayName
        );
        _selectedSector.TypeID = EditorGUILayout.TextField("Type ID", _selectedSector.TypeID);
        _selectedSector.InstanceID = EditorGUILayout.TextField(
            "Instance ID",
            _selectedSector.InstanceID
        );
        _selectedSector.SectorType = (PlanetSectorType)
            EditorGUILayout.EnumPopup("Region", _selectedSector.SectorType);
        _selectedSector.Visibility = (GameSize)
            EditorGUILayout.EnumPopup("Minimum Galaxy", _selectedSector.Visibility);
        _selectedSector.Importance = (PlanetSectorImportance)
            EditorGUILayout.EnumPopup("Importance", _selectedSector.Importance);
        _selectedSector.PositionX = EditorGUILayout.IntSlider(
            "Galaxy X",
            _selectedSector.PositionX,
            0,
            852
        );
        _selectedSector.PositionY = EditorGUILayout.IntSlider(
            "Galaxy Y",
            _selectedSector.PositionY,
            0,
            479
        );
        CompleteChanges();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("PLANETS / SYSTEMS", EditorStyles.boldLabel);
        foreach (Planet system in _selectedSector.GetChildren<Planet>(includeDisabled: true))
        {
            if (GUILayout.Button(system.DisplayName ?? "Untitled System", GUILayout.Height(25f)))
            {
                _selectedSystem = system;
                RenderPreview();
            }
        }
        if (GUILayout.Button("+ Add Planet", GUILayout.Height(28f)))
            AddSystem();
    }

    /// <summary>
    /// Draws editable planetary-system fields.
    /// </summary>
    private void DrawSystemInspector()
    {
        if (GUILayout.Button("‹ Back to Sector", EditorStyles.miniButton))
        {
            _selectedSystem = null;
            RenderPreview();
            return;
        }
        EditorGUILayout.LabelField("PLANET / SYSTEM", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The current game model uses one Planet record for each named star system on the galaxy map.",
            MessageType.None
        );
        EditorGUI.BeginChangeCheck();
        _selectedSystem.DisplayName = EditorGUILayout.TextField(
            "Name",
            _selectedSystem.DisplayName
        );
        _selectedSystem.TypeID = EditorGUILayout.TextField("Type ID", _selectedSystem.TypeID);
        _selectedSystem.InstanceID = EditorGUILayout.TextField(
            "Instance ID",
            _selectedSystem.InstanceID
        );
        _selectedSystem.PositionX = EditorGUILayout.IntSlider(
            "Galaxy X",
            _selectedSystem.PositionX,
            0,
            852
        );
        _selectedSystem.PositionY = EditorGUILayout.IntSlider(
            "Galaxy Y",
            _selectedSystem.PositionY,
            0,
            479
        );
        _selectedSystem.PlanetIconPath = EditorGUILayout.TextField(
            "Planet Artwork",
            _selectedSystem.PlanetIconPath
        );
        _selectedSystem.EncyclopediaImagePath = EditorGUILayout.TextField(
            "Encyclopedia Art",
            _selectedSystem.EncyclopediaImagePath
        );
        EditorGUILayout.LabelField("Description");
        _selectedSystem.Description = EditorGUILayout.TextArea(
            _selectedSystem.Description ?? string.Empty,
            GUILayout.MinHeight(90f)
        );
        CompleteChanges();
        if (GUILayout.Button("Remove Planet", GUILayout.Height(28f)))
            RemoveSystem();
    }

    /// <summary>
    /// Records completed field edits and refreshes the production preview.
    /// </summary>
    private void CompleteChanges()
    {
        if (!EditorGUI.EndChangeCheck())
            return;
        _hasChanges = true;
        RenderPreview();
    }

    /// <summary>
    /// Loads the active content used by the editor canvas.
    /// </summary>
    private void Initialize()
    {
        EditorApplication.delayCall -= Initialize;
        if (_sectors.Count == 0)
            LoadActivePack();
        Repaint();
    }

    /// <summary>
    /// Draws and directly manipulates the galaxy using source-space content coordinates.
    /// </summary>
    private void DrawGalaxyCanvas()
    {
        float availableWidth = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 20f);
        float scale = availableWidth / _sourceWidth;
        Rect layout = GUILayoutUtility.GetRect(
            availableWidth,
            _sourceHeight * scale,
            GUILayout.ExpandWidth(true)
        );
        scale = Mathf.Min(layout.width / _sourceWidth, layout.height / _sourceHeight);
        Rect canvas = new Rect(
            layout.x + (layout.width - _sourceWidth * scale) / 2f,
            layout.y,
            _sourceWidth * scale,
            _sourceHeight * scale
        );
        EditorGUI.DrawRect(canvas, Color.black);
        DrawGalaxyBackground(canvas, scale);
        DrawSystems(canvas, scale);
        if (_hudTexture != null)
            GUI.DrawTexture(canvas, _hudTexture, ScaleMode.StretchToFill, true);
        HandleGalaxyInput(canvas, scale);
    }

    /// <summary>
    /// Draws the active theme's galaxy background at its production source bounds.
    /// </summary>
    /// <param name="canvas">The editor canvas rectangle.</param>
    /// <param name="scale">The displayed pixels per source unit.</param>
    private void DrawGalaxyBackground(Rect canvas, float scale)
    {
        if (_backgroundTexture == null)
            return;
        SourcePointLayout origin = _previewTheme?.GalaxyBackground?.SourcePosition;
        Rect bounds = new Rect(
            canvas.x + (origin?.X ?? 0) * scale,
            canvas.y + (origin?.Y ?? 0) * scale,
            UILayout.ToSourceUnits(_backgroundTexture.width) * scale,
            UILayout.ToSourceUnits(_backgroundTexture.height) * scale
        );
        GUI.DrawTexture(bounds, _backgroundTexture, ScaleMode.StretchToFill, true);
    }

    /// <summary>
    /// Draws every system marker at its production galaxy-map coordinate.
    /// </summary>
    /// <param name="canvas">The editor canvas rectangle.</param>
    /// <param name="scale">The displayed pixels per source unit.</param>
    private void DrawSystems(Rect canvas, float scale)
    {
        if (_planetMarkerTexture == null)
            return;
        SourcePointLayout origin = _previewTheme?.GalaxyBackground?.SourcePosition;
        float width = UILayout.ToSourceUnits(_planetMarkerTexture.width) * scale;
        float height = UILayout.ToSourceUnits(_planetMarkerTexture.height) * scale;
        foreach (PlanetSector sector in _sectors)
        {
            foreach (Planet planet in sector.GetChildren<Planet>(true))
            {
                Rect marker = new Rect(
                    canvas.x + ((origin?.X ?? 0) + planet.PositionX) * scale,
                    canvas.y + ((origin?.Y ?? 0) + planet.PositionY) * scale,
                    width,
                    height
                );
                GUI.DrawTexture(marker, _planetMarkerTexture, ScaleMode.StretchToFill, true);
                if (sector == _selectedSector)
                    EditorGUI.DrawRect(
                        new Rect(marker.x, marker.y, marker.width, 1f),
                        Color.yellow
                    );
            }
        }
    }

    /// <summary>
    /// Handles selection and sector translation directly in the editor event loop.
    /// </summary>
    /// <param name="canvas">The editor canvas rectangle.</param>
    /// <param name="scale">The displayed pixels per source unit.</param>
    private void HandleGalaxyInput(Rect canvas, float scale)
    {
        Event current = Event.current;
        if (current == null || !canvas.Contains(current.mousePosition))
            return;

        Vector2Int mapPosition = GetMapPosition(canvas, scale, current.mousePosition);
        if (current.type == EventType.MouseDown && current.button == 0)
        {
            _draggedSector = null;
            if (FindSystemAt(mapPosition, out PlanetSector sector) == null)
                return;
            _selectedSector = sector;
            _selectedSystem = null;
            _draggedSector = sector;
            _lastDragMapPosition = mapPosition;
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 0 && _draggedSector != null)
        {
            TranslateSector(mapPosition - _lastDragMapPosition);
            _lastDragMapPosition = mapPosition;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0)
        {
            _draggedSector = null;
        }
    }

    /// <summary>
    /// Converts a canvas point into galaxy-map coordinates.
    /// </summary>
    /// <param name="canvas">The editor canvas rectangle.</param>
    /// <param name="scale">The displayed pixels per source unit.</param>
    /// <param name="point">The editor-window pointer position.</param>
    /// <returns>The corresponding galaxy-map position.</returns>
    private Vector2Int GetMapPosition(Rect canvas, float scale, Vector2 point)
    {
        SourcePointLayout origin = _previewTheme?.GalaxyBackground?.SourcePosition;
        return new Vector2Int(
            Mathf.RoundToInt((point.x - canvas.x) / scale) - (origin?.X ?? 0),
            Mathf.RoundToInt((point.y - canvas.y) / scale) - (origin?.Y ?? 0)
        );
    }

    /// <summary>
    /// Translates one sector and all of its planets by a source-space delta.
    /// </summary>
    /// <param name="delta">The source-space movement delta.</param>
    private void TranslateSector(Vector2Int delta)
    {
        if (_draggedSector == null || delta == Vector2Int.zero)
            return;
        _draggedSector.PositionX += delta.x;
        _draggedSector.PositionY += delta.y;
        foreach (Planet planet in _draggedSector.GetChildren<Planet>(true))
        {
            planet.PositionX += delta.x;
            planet.PositionY += delta.y;
        }
        MarkChanged();
    }

    /// <summary>
    /// Finds the closest star marker within the editor selection radius.
    /// </summary>
    /// <param name="mapPosition">The pointer position in galaxy-map coordinates.</param>
    /// <param name="sector">Receives the selected star's sector.</param>
    /// <returns>The selected system, or null when no marker was hit.</returns>
    private Planet FindSystemAt(Vector2Int mapPosition, out PlanetSector sector)
    {
        sector = null;
        Planet closest = null;
        int closestDistance = _selectionRadius * _selectionRadius + 1;
        foreach (PlanetSector candidateSector in _sectors)
        {
            foreach (Planet candidate in candidateSector.GetChildren<Planet>(true))
            {
                int dx = candidate.PositionX + _markerCenterOffset - mapPosition.x;
                int dy = candidate.PositionY + _markerCenterOffset - mapPosition.y;
                int distance = dx * dx + dy * dy;
                if (distance >= closestDistance)
                    continue;
                closestDistance = distance;
                closest = candidate;
                sector = candidateSector;
            }
        }
        return closest;
    }

    /// <summary>
    /// Refreshes the editor-owned galaxy canvas.
    /// </summary>
    private void RenderPreview()
    {
        Repaint();
    }

    /// <summary>
    /// Loads the active campaign's planet sectors into the authoring document.
    /// </summary>
    private void LoadActivePack()
    {
        try
        {
            ContentPack pack = ContentPackEditor.LoadActivePack();
            _previewTheme = pack.GameData.FactionThemes.First(theme =>
                string.Equals(
                    theme.FactionInstanceID,
                    pack.Scenario.DefaultPlayerFactionID,
                    StringComparison.OrdinalIgnoreCase
                )
            );
            _backgroundTexture = ContentPackEditor.Assets.GetTexture(
                _previewTheme.GalaxyBackground.ImagePath
            );
            _hudTexture = ContentPackEditor.Assets.GetTexture(
                _previewTheme.TacticalHUDLayout.ImagePath
            );
            _planetMarkerTexture = ContentPackEditor.Assets.GetTexture(
                GalaxyMapProjector.GetPlanetIconPath(_previewTheme.GalaxyBackground.PlanetIcons, 0)
            );
            ReplaceDocument(pack.GameData.PlanetSectors, null);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Unable to load default campaign", exception.Message, "OK");
        }
    }

    /// <summary>
    /// Prompts for and loads a planet-sector XML document.
    /// </summary>
    private void OpenDocument()
    {
        string path = EditorUtility.OpenFilePanel("Open planet sectors", string.Empty, "xml");
        if (string.IsNullOrWhiteSpace(path))
            return;
        using FileStream stream = File.OpenRead(path);
        ReplaceDocument((PlanetSector[])CreateSerializer().Deserialize(stream), path);
    }

    /// <summary>
    /// Prompts for a destination and writes the authoring document.
    /// </summary>
    private void SaveDocumentAs()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save planet sectors",
            string.IsNullOrWhiteSpace(_documentPath)
                ? string.Empty
                : Path.GetDirectoryName(_documentPath),
            string.IsNullOrWhiteSpace(_documentPath)
                ? "planet-sectors.xml"
                : Path.GetFileName(_documentPath),
            "xml"
        );
        if (string.IsNullOrWhiteSpace(path))
            return;
        using FileStream stream = File.Create(path);
        CreateSerializer().Serialize(stream, _sectors.ToArray());
        _documentPath = path;
        _hasChanges = false;
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Creates the existing game serializer for planet-sector documents.
    /// </summary>
    /// <returns>The configured serializer.</returns>
    private static GameSerializer CreateSerializer()
    {
        return new GameSerializer(
            typeof(PlanetSector[]),
            new GameSerializerSettings { RootName = "PlanetSectors" }
        );
    }

    /// <summary>
    /// Replaces all authoring data and refreshes the preview.
    /// </summary>
    /// <param name="sectors">The replacement sectors.</param>
    /// <param name="path">The source document path.</param>
    private void ReplaceDocument(IEnumerable<PlanetSector> sectors, string path)
    {
        _sectors.Clear();
        _sectors.AddRange(sectors ?? Array.Empty<PlanetSector>());
        _selectedSector = _sectors.FirstOrDefault();
        _selectedSystem = null;
        _documentPath = path;
        _hasChanges = false;
        RenderPreview();
        Repaint();
    }

    /// <summary>
    /// Adds and selects a new planet sector.
    /// </summary>
    private void AddSector()
    {
        int number = _sectors.Count + 1;
        PlanetSector sector = new PlanetSector
        {
            DisplayName = $"New Sector {number}",
            TypeID = $"PS{number:000}",
            InstanceID = $"NEW_SECTOR_{number:00}",
            Visibility = GameSize.Small,
            Importance = PlanetSectorImportance.Medium,
            PositionX = 300,
            PositionY = 180,
        };
        _sectors.Add(sector);
        SelectSector(sector);
        MarkChanged();
    }

    /// <summary>
    /// Removes the selected planet sector after confirmation.
    /// </summary>
    private void RemoveSector()
    {
        if (
            _selectedSector == null
            || !EditorUtility.DisplayDialog(
                "Remove sector?",
                $"Remove '{_selectedSector.DisplayName}' and all of its systems?",
                "Remove",
                "Cancel"
            )
        )
            return;
        _sectors.Remove(_selectedSector);
        _selectedSector = _sectors.FirstOrDefault();
        _selectedSystem = null;
        MarkChanged();
    }

    /// <summary>
    /// Selects a sector and refreshes preview emphasis.
    /// </summary>
    /// <param name="sector">The sector to select.</param>
    private void SelectSector(PlanetSector sector)
    {
        _selectedSector = sector;
        _selectedSystem = null;
        RenderPreview();
    }

    /// <summary>
    /// Adds and selects a new planetary system.
    /// </summary>
    private void AddSystem()
    {
        int number = _selectedSector.GetChildren<Planet>(includeDisabled: true).Count + 1;
        Planet system = new Planet
        {
            DisplayName = $"New System {number}",
            TypeID = $"{_selectedSector.TypeID}PL{number:00}",
            InstanceID = $"NEW_SYSTEM_{number:00}",
            PositionX = _selectedSector.PositionX + 12 + number * 5,
            PositionY = _selectedSector.PositionY + 12 + number * 5,
        };
        system.SetParent(_selectedSector);
        _selectedSystem = system;
        MarkChanged();
    }

    /// <summary>
    /// Removes the selected planetary system.
    /// </summary>
    private void RemoveSystem()
    {
        _selectedSystem.SetParent(null);
        _selectedSystem = null;
        MarkChanged();
    }

    /// <summary>
    /// Marks the document dirty and refreshes all presentation.
    /// </summary>
    private void MarkChanged()
    {
        _hasChanges = true;
        RenderPreview();
        Repaint();
    }
}
