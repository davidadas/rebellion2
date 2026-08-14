using System;
using System.IO;
using Rebellion.Editor.Media;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Selects a local media branch and synchronizes its development assets into the Unity project.
/// </summary>
public sealed class MediaSyncWindow : EditorWindow
{
    private const string _mediaPathPreference = "Rebellion.MediaSync.RepositoryPath";

    private string _mediaPath;
    private MediaRepositoryInfo _repository;
    private int _selectedBranchIndex;
    private string _statusMessage;
    private MessageType _statusType = MessageType.Info;

    [MenuItem("Rebellion/Build/Sync Media...", false, 90)]
    public static void Open()
    {
        MediaSyncWindow window = GetWindow<MediaSyncWindow>("Sync Media");
        window.minSize = new Vector2(560f, 280f);
        window.Show();
    }

#pragma warning disable RCS1213 // Unity invokes EditorWindow callbacks by name.
    private void OnEnable()
    {
        string projectRoot = GetProjectRoot();
        _mediaPath = EditorPrefs.GetString(
            _mediaPathPreference,
            MediaSyncService.FindDefaultMediaRoot(projectRoot)
        );
        RefreshRepository();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Development Media", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Synchronizes external content using the same source trees as CI. Files removed from "
                + "the selected media branch are removed locally, while Unity metadata for retained "
                + "assets is preserved.",
            MessageType.Info
        );

        EditorGUILayout.Space();
        DrawRepositoryPath();
        EditorGUILayout.Space();
        DrawRepositoryState();
        EditorGUILayout.Space();
        DrawSyncButton();

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }
    }
#pragma warning restore RCS1213

    private void DrawRepositoryPath()
    {
        EditorGUILayout.BeginHorizontal();
        string nextPath = EditorGUILayout.TextField("Media repository", _mediaPath);
        if (!string.Equals(nextPath, _mediaPath, StringComparison.Ordinal))
        {
            _mediaPath = nextPath;
            EditorPrefs.SetString(_mediaPathPreference, _mediaPath);
            _repository = null;
        }

        if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
        {
            string selected = EditorUtility.OpenFolderPanel(
                "Select rebellion2-media checkout",
                _mediaPath,
                string.Empty
            );
            if (!string.IsNullOrWhiteSpace(selected))
            {
                _mediaPath = selected;
                EditorPrefs.SetString(_mediaPathPreference, _mediaPath);
                RefreshRepository();
            }
        }

        if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
            RefreshRepository();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRepositoryState()
    {
        if (_repository == null)
        {
            EditorGUILayout.HelpBox(
                "Select a valid local rebellion2-media checkout.",
                MessageType.Warning
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Current media branch",
            string.IsNullOrWhiteSpace(_repository.CurrentBranch)
                ? "Detached HEAD"
                : _repository.CurrentBranch
        );
        if (_repository.IsDirty)
        {
            EditorGUILayout.HelpBox(
                "The media checkout has uncommitted changes. They will be included when syncing "
                    + "the current branch; switching branches is disabled.",
                MessageType.Warning
            );
        }

        using (new EditorGUI.DisabledScope(_repository.IsDirty))
        {
            _selectedBranchIndex = EditorGUILayout.Popup(
                "Source branch",
                _selectedBranchIndex,
                _repository.Branches
            );
        }
        EditorGUILayout.LabelField(
            "Branch list",
            "Local branches only; fetch or create media branches outside Unity."
        );
    }

    private void DrawSyncButton()
    {
        bool hasBranch =
            (_repository?.Branches.Length ?? 0) > 0
            && _selectedBranchIndex >= 0
            && _selectedBranchIndex < _repository.Branches.Length;
        using (new EditorGUI.DisabledScope(!hasBranch || EditorApplication.isCompiling))
        {
            string selectedBranch = hasBranch
                ? _repository.Branches[_selectedBranchIndex]
                : string.Empty;
            bool switchesBranch =
                hasBranch
                && !string.Equals(
                    selectedBranch,
                    _repository.CurrentBranch,
                    StringComparison.Ordinal
                );
            string label = switchesBranch ? "Switch Branch and Sync" : "Sync Media";
            if (GUILayout.Button(label, GUILayout.Height(32f)))
                Synchronize(selectedBranch);
        }
    }

    private void RefreshRepository()
    {
        try
        {
            _repository = MediaSyncService.InspectRepository(_mediaPath);
            int currentBranchIndex = Array.IndexOf(_repository.Branches, _repository.CurrentBranch);
            _selectedBranchIndex = Mathf.Max(0, currentBranchIndex);
            _statusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _repository = null;
            _selectedBranchIndex = 0;
            SetStatus(ex.Message, MessageType.Error);
        }

        Repaint();
    }

    private void Synchronize(string selectedBranch)
    {
        bool switchesBranch = !string.Equals(
            selectedBranch,
            _repository.CurrentBranch,
            StringComparison.Ordinal
        );
        if (switchesBranch && _repository.IsDirty)
        {
            SetStatus(
                "Commit, stash, or discard media changes before switching branches.",
                MessageType.Error
            );
            return;
        }

        string dirtyWarning = _repository.IsDirty
            ? "\n\nThe current media worktree is dirty; uncommitted changes will be copied."
            : string.Empty;
        string branchAction = switchesBranch
            ? $"Switch rebellion2-media to '{selectedBranch}', then synchronize it?"
            : $"Synchronize rebellion2-media branch '{selectedBranch}'?";
        if (
            !EditorUtility.DisplayDialog(
                "Synchronize Development Media",
                branchAction
                    + "\n\nDestination files absent from the source branch will be deleted."
                    + dirtyWarning,
                "Synchronize",
                "Cancel"
            )
        )
        {
            return;
        }

        try
        {
            if (switchesBranch)
                MediaSyncService.SwitchBranch(_repository, selectedBranch);

            AssetDatabase.StartAssetEditing();
            MediaSyncResult result;
            try
            {
                result = MediaSyncService.Synchronize(
                    _mediaPath,
                    GetProjectRoot(),
                    (description, progress) =>
                        EditorUtility.DisplayProgressBar(
                            "Synchronizing Media",
                            description,
                            progress
                        )
                );
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            SetStatus(
                $"Media synchronized from '{selectedBranch}': {result.CopiedFiles} copied, "
                    + $"{result.UnchangedFiles} unchanged, {result.DeletedFiles} deleted.",
                MessageType.Info
            );
            RefreshRepositoryPreservingStatus();
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            SetStatus(ex.Message, MessageType.Error);
            RefreshRepositoryPreservingStatus();
        }
    }

    private void RefreshRepositoryPreservingStatus()
    {
        string message = _statusMessage;
        MessageType type = _statusType;
        RefreshRepository();
        _statusMessage = message;
        _statusType = type;
    }

    private void SetStatus(string message, MessageType type)
    {
        _statusMessage = message;
        _statusType = type;
        Repaint();
    }

    private static string GetProjectRoot()
    {
        DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
        return projectDirectory?.FullName
            ?? throw new InvalidOperationException(
                "Could not resolve the Unity project directory."
            );
    }
}
