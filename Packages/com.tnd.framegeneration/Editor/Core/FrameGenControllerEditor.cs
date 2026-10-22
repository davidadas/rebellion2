using System;
using System.Collections.Generic;
using TND.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TND.FrameGen
{
    [CustomEditor(typeof(TNDFrameGeneration))]
    public class FrameGenControllerEditor: Editor
    {
        internal static readonly string OnlineDocumentation = "https://docs.google.com/document/d/1L8C8H_RGuyip7aI0ckqqiAKeg7RB07yKb8EQNCDSw0Y";

        private SerializedProperty _enableOnStartProperty;
        private SerializedProperty _providerChainProperty;
        private SerializedProperty _frameMultiplierProperty;
        private SerializedProperty _useDynamicMFGProperty;
        private SerializedProperty _dynamicFramerateTargetProperty;
        private SerializedProperty _distortionFieldProperty;
        private SerializedProperty _debugFlagsProperty;

        private readonly List<IFrameGenProvider> _frameGenProviders = new();
        private ReorderableList _frameGenProvidersList;

        protected virtual void OnEnable()
        {
            var frameGenScript = serializedObject.targetObject as FrameGenController;
            if (frameGenScript == null)
                return;

            serializedObject.Update();

            _enableOnStartProperty = serializedObject.FindProperty(nameof(FrameGenController.enableFrameGenerationOnStart));
            _providerChainProperty = serializedObject.FindProperty(nameof(FrameGenController.providerChain));
            _frameMultiplierProperty = serializedObject.FindProperty(nameof(FrameGenController.frameMultiplier));
            _useDynamicMFGProperty = serializedObject.FindProperty(nameof(FrameGenController.useDynamicMFG));
            _dynamicFramerateTargetProperty = serializedObject.FindProperty(nameof(FrameGenController.dynamicFramerateTarget));
            _distortionFieldProperty = serializedObject.FindProperty(nameof(FrameGenController.distortionField));
            _debugFlagsProperty = serializedObject.FindProperty(nameof(FrameGenController.debugFlags));

            _frameGenProviders.Clear();
            _frameGenProviders.AddRange(FrameGenProviderRegistry.GetFrameGenProviders());
            _frameGenProviders.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));

            _frameGenProvidersList = new PriorityList<IFrameGenProvider>(serializedObject, _providerChainProperty, "Frame Generation by Priority", _frameGenProviders, new FrameGenProviderItemAdapter());
        }

        private struct FrameGenProviderItemAdapter : PriorityList<IFrameGenProvider>.IItemAdapter
        {
            public string GetIdentifier(IFrameGenProvider item) => item.Identifier;
            public string GetDisplayName(IFrameGenProvider item) => item.DisplayName;
        }

        protected virtual void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            EditorVisuals.GenerateHeader(OnlineDocumentation);

            if (DrawValidations())
            {
                if (_frameGenProvidersList.count == 0)
                {
                    EditorGUILayout.HelpBox("No FG providers have been added to the 'Frame Generation by Priority' list.\r\nAs long as this list is empty, the TND Framework will not perform any frame generation.", MessageType.Warning);
                }
                EditorVisuals.GenerateCrossPromo();
                DrawSettings();
            }

            EditorVisuals.GenerateFooter();
        }

        protected virtual bool DrawValidations()
        {
            return true;
        }

        private void DrawSettings()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_enableOnStartProperty);
            EditorGUILayout.Space();

            _frameGenProvidersList.DoLayoutList();

            //EditorGUILayout.PropertyField(_distortionFieldProperty);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Multi-Frame Generation", EditorStyles.boldLabel);
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginDisabledGroup(_useDynamicMFGProperty.boolValue);
                EditorGUILayout.PropertyField(_frameMultiplierProperty);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.PropertyField(_useDynamicMFGProperty, new GUIContent("Dynamic MFG"));
                if (_useDynamicMFGProperty.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_dynamicFramerateTargetProperty);
                    EditorGUI.indentLevel--;
                }

                if (_frameMultiplierProperty.intValue > 2 || _useDynamicMFGProperty.boolValue)
                {
                    EditorGUILayout.HelpBox("Frame Multiplier and Dynamic MFG settings only apply to Frame Generation providers that support Multi-Frame Generation, and only on supported hardware.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_debugFlagsProperty, new GUIContent("Debug Flags"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
