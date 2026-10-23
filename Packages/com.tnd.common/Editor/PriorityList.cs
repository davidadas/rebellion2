using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TND.Common
{
    public class PriorityList<TItem>: ReorderableList
    {
        public PriorityList(SerializedObject serializedObject, SerializedProperty elements, string label, List<TItem> items, IItemAdapter itemAdapter)
            : base(serializedObject, elements, true, true, true, true)
        {
            drawHeaderCallback = (rect) =>
            {
                EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
            };
            drawElementCallback = (rect, idx, _, _) =>
            {
                var element = elements.GetArrayElementAtIndex(idx);
                var item = items.Find(x => itemAdapter.GetIdentifier(x) == element.stringValue);
                var displayName = item != null ? itemAdapter.GetDisplayName(item) : "[Unknown]" + element.stringValue;
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), displayName, EditorStyles.label);
            };
            onAddDropdownCallback = (rect, lst) =>
            {
                var availableIdentifiers = new List<string>();
                var availableNames = new List<GUIContent>();

                // Create a sorted list of upscalers that haven't been added to the fallback chain yet
                foreach (var item in items)
                {
                    string identifier = itemAdapter.GetIdentifier(item);

                    bool found = false;
                    foreach (SerializedProperty element in elements)
                    {
                        if (element.stringValue == identifier)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        availableIdentifiers.Add(identifier);
                        availableNames.Add(new GUIContent(itemAdapter.GetDisplayName(item)));
                    }
                }

                void SelectCallback(object userData, string[] options, int selected)
                {
                    // Add the upscaler to the end of the fallback chain
                    string selectedIdentifier = ((List<string>)userData)[selected];
                    int idx = lst.count;
                    elements.InsertArrayElementAtIndex(idx);
                    var newElement = elements.GetArrayElementAtIndex(idx);
                    newElement.stringValue = selectedIdentifier;
                    serializedObject.ApplyModifiedProperties();
                }

                EditorUtility.DisplayCustomMenu(rect, availableNames.ToArray(), availableNames.Count, SelectCallback, availableIdentifiers, false);
            };
        }

        public interface IItemAdapter
        {
            string GetIdentifier(TItem item);
            string GetDisplayName(TItem item);
        }
    }
}
