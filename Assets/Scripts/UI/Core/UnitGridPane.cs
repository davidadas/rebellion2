using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UnitGridPane : MonoBehaviour
{
    [SerializeField]
    private UnitTile tilePrefab;

    [SerializeField]
    private Transform contentRoot;

    private UIContext uiContext;
    private List<UnitTile> tiles = new();

    public event Action<UnitTile> TileClicked;
    public event Action<UnitTile> TileRightClicked;
    public event Action<UnitTile> TileDragStarted;
    public event Action<UnitTile, PointerEventData> TileDragEnded;

    public void Initialize(UIContext uiContext)
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        this.uiContext = uiContext;
    }

    public void Clear()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        tiles.Clear();
    }

    public void AddTile(ISceneNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (uiContext == null)
            throw new InvalidOperationException("UnitGridPane not initialized.");

        Sprite sprite = uiContext.GetSprite(node);

        if (sprite == null)
            throw new InvalidOperationException(
                $"Missing sprite for node '{node.GetDisplayName()}'"
            );

        string ownerId = node.OwnerInstanceID;
        Color factionColor = uiContext.GetFactionColor(ownerId);

        UnitTile tile = Instantiate(tilePrefab);
        tile.transform.SetParent(contentRoot, false);

        tile.Initialize(node, sprite, factionColor);

        tile.Clicked += HandleTileClicked;
        tile.RightClicked += HandleTileRightClicked;
        tile.DragStarted += HandleTileDragStarted;
        tile.DragEnded += HandleTileDragEnded;

        tiles.Add(tile);
    }

    public UnitTile GetTileAt(int index)
    {
        if (index < 0 || index >= tiles.Count)
            return null;

        return tiles[index];
    }

    public int Count => tiles.Count;

    private void HandleTileClicked(UnitTile tile)
    {
        TileClicked?.Invoke(tile);
    }

    private void HandleTileRightClicked(UnitTile tile)
    {
        TileRightClicked?.Invoke(tile);
    }

    private void HandleTileDragStarted(UnitTile tile)
    {
        TileDragStarted?.Invoke(tile);
    }

    private void HandleTileDragEnded(UnitTile tile, PointerEventData data)
    {
        TileDragEnded?.Invoke(tile, data);
    }
}
