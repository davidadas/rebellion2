using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Saves and loads input binding overrides.
/// </summary>
internal sealed class InputBindingStore
{
    private const int _editableSlotCount = 2;
    private readonly InputActionAsset _asset;

    internal InputBindingStore(InputActionAsset asset)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        EnsureEditableSlots();
    }

    internal string SaveOverrides()
    {
        return _asset.SaveBindingOverridesAsJson();
    }

    internal void LoadOverrides(string serializedOverrides)
    {
        _asset.RemoveAllBindingOverrides();
        if (string.IsNullOrWhiteSpace(serializedOverrides))
            return;

        _asset.LoadBindingOverridesFromJson(serializedOverrides);
        MigrateLegacyRuntimeOverrides(serializedOverrides);
    }

    internal Guid GetSlotId(string actionPath, int slot)
    {
        InputAction action = _asset.FindAction(actionPath, true);
        return action.bindings[GetTopLevelBindingIndex(action, slot)].id;
    }

    internal void ApplySlotOverride(string actionPath, int slot, string path)
    {
        InputAction action = _asset.FindAction(actionPath, true);
        action.ApplyBindingOverride(GetTopLevelBindingIndex(action, slot), path);
    }

    internal string GetEffectiveSlotPath(string actionPath, int slot)
    {
        InputAction action = _asset.FindAction(actionPath, true);
        return action.bindings[GetTopLevelBindingIndex(action, slot)].effectivePath;
    }

    private void EnsureEditableSlots()
    {
        foreach (InputActionMap map in _asset.actionMaps)
        {
            if (map.name is not ("Global" or "Strategy"))
                continue;

            bool wasEnabled = map.enabled;
            map.Disable();
            foreach (InputAction action in map.actions)
            {
                if (action.name == "CancelOrSettings")
                    continue;

                int topLevelCount = CountTopLevelBindings(action);
                for (int slot = topLevelCount; slot < _editableSlotCount; slot++)
                {
                    action.AddBinding(
                        new InputBinding
                        {
                            id = CreateStableSlotId(map.name, action.name, slot),
                            path = string.Empty,
                            groups = "Keyboard&Mouse",
                        }
                    );
                }
            }

            if (wasEnabled)
                map.Enable();
        }
    }

    private void MigrateLegacyRuntimeOverrides(string serializedOverrides)
    {
        BindingOverrideList saved = JsonUtility.FromJson<BindingOverrideList>(serializedOverrides);
        if (saved?.bindings == null)
            return;

        HashSet<string> migratedSlots = new HashSet<string>();
        foreach (BindingOverrideEntry entry in saved.bindings)
        {
            if (entry == null || BindingIdExists(entry.id))
                continue;

            InputAction action = _asset.FindAction(entry.action, false);
            if (action == null)
                continue;

            for (int slot = 0; slot < _editableSlotCount; slot++)
            {
                int bindingIndex = GetTopLevelBindingIndex(action, slot);
                string slotKey = $"{entry.action}/{slot}";
                InputBinding binding = action.bindings[bindingIndex];
                if (migratedSlots.Contains(slotKey) || !string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                action.ApplyBindingOverride(
                    bindingIndex,
                    new InputBinding
                    {
                        overridePath = DeserializeNullable(entry.path),
                        overrideInteractions = DeserializeNullable(entry.interactions),
                        overrideProcessors = DeserializeNullable(entry.processors),
                    }
                );
                migratedSlots.Add(slotKey);
                break;
            }
        }
    }

    private bool BindingIdExists(string id)
    {
        if (!Guid.TryParse(id, out Guid bindingId))
            return false;

        foreach (InputBinding binding in _asset.bindings)
        {
            if (binding.id == bindingId)
                return true;
        }

        return false;
    }

    private static int CountTopLevelBindings(InputAction action)
    {
        int count = 0;
        foreach (InputBinding binding in action.bindings)
        {
            if (!binding.isPartOfComposite)
                count++;
        }

        return count;
    }

    private static Guid CreateStableSlotId(string mapName, string actionName, int slot)
    {
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(
            Encoding.UTF8.GetBytes($"rebellion2/{mapName}/{actionName}/binding/{slot}")
        );
        return new Guid(hash);
    }

    internal static int GetTopLevelBindingIndex(InputAction action, int slot)
    {
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));

        int topLevelSlot = 0;
        for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
        {
            if (action.bindings[bindingIndex].isPartOfComposite)
                continue;
            if (topLevelSlot == slot)
                return bindingIndex;
            topLevelSlot++;
        }

        throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private static string DeserializeNullable(string value)
    {
        return value == "null" ? null : value;
    }

    [Serializable]
    private sealed class BindingOverrideList
    {
        public BindingOverrideEntry[] bindings;
    }

    [Serializable]
    private sealed class BindingOverrideEntry
    {
        public string action;
        public string id;
        public string path;
        public string interactions;
        public string processors;
    }
}
