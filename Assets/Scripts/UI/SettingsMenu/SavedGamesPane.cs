using System;
using System.Collections.Generic;
using Rebellion.Game;
using UnityEngine;

public sealed class SavedGamesPane : MonoBehaviour
{
    [SerializeField]
    private Transform itemContainer;

    [SerializeField]
    private SavedGameMenuItem itemPrefab;

    private GameRuntime runtime;

    public void Initialize(GameRuntime runtime)
    {
        if (runtime == null)
            throw new ArgumentNullException(nameof(runtime));

        this.runtime = runtime;
        Rebuild();
    }

    public void Rebuild()
    {
        Clear();

        List<SaveMenuItemData> items = BuildItems();

        foreach (SaveMenuItemData item in items)
        {
            SavedGameMenuItem view = Instantiate(itemPrefab, itemContainer);

            view.Bind(item, OnLoadClicked, OnSaveClicked);
        }
    }

    private List<SaveMenuItemData> BuildItems()
    {
        IReadOnlyList<SaveGameEntry> saves = SaveGameManager.Instance.GetSavedGames();

        List<SaveMenuItemData> items = new List<SaveMenuItemData>(saves.Count + 1);

        items.Add(
            new SaveMenuItemData
            {
                Id = "new",
                DisplayName = "New Save",

                FactionIcon = null,

                CanLoad = false,
                CanSave = runtime.HasActiveGame,

                Type = SaveMenuItemType.NewSave,
            }
        );

        foreach (SaveGameEntry save in saves)
        {
            items.Add(
                new SaveMenuItemData
                {
                    Id = save.FileName,
                    DisplayName = save.FileName,

                    FactionIcon = null, // later from metadata

                    CanLoad = true,
                    CanSave = runtime.HasActiveGame,

                    Type = SaveMenuItemType.ExistingSave,
                }
            );
        }

        return items;
    }

    private void OnLoadClicked(string id)
    {
        if (id == "new")
            return;

        Debug.Log($"Load {id}");
        // runtime.LoadGame(id);
    }

    private void OnSaveClicked(string fileName)
    {
        Debug.Log($"Save {fileName}");

        if (!runtime.HasActiveGame)
            return;

        GameRoot game = runtime.GetActiveGame();
        SaveGameManager.Instance.SaveGameData(game, fileName);
    }

    private void Clear()
    {
        for (int i = itemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(itemContainer.GetChild(i).gameObject);
        }
    }
}
