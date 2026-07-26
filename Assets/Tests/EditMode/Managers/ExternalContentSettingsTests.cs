using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class ExternalContentSettingsTests
{
    private const string _contentRoot = "Assets/Content";

    [Test]
    public void ContentLayout_LocalPackage_UsesRawExternalContentAndBuiltInScenes()
    {
        Assert.IsTrue(AssetDatabase.IsValidFolder(_contentRoot));
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "Assets/Content/Art",
                "Assets/Content/Audio",
                "Assets/Content/Configs",
                "Assets/Content/Data",
                "Assets/Content/Videos",
            },
            Directory
                .GetDirectories(_contentRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .ToArray()
        );
        Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Resources"));
        CollectionAssert.AreEquivalent(
            new[] { "Assets/TextMesh Pro/Resources" },
            Directory
                .GetDirectories("Assets", "Resources", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .ToArray()
        );

        string[] builtInScenes = EditorBuildSettings
            .scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "Assets/Scenes/BootScene.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/StrategyView.unity",
                "Assets/Scenes/SaveMenu.unity",
            },
            builtInScenes
        );
    }

    [Test]
    public void ContentPackage_ProjectReferencesDoNotIncludeAddressables()
    {
        string manifest = File.ReadAllText("Packages/manifest.json");
        string runtimeAssembly = File.ReadAllText("Assets/Scripts/GameAssembly.asmdef");
        string testsAssembly = File.ReadAllText("Assets/Tests/EditMode/EditMode.asmdef");

        StringAssert.DoesNotContain("com.unity.addressables", manifest);
        StringAssert.DoesNotContain("Unity.Addressables", runtimeAssembly);
        StringAssert.DoesNotContain("Unity.ResourceManager", runtimeAssembly);
        StringAssert.DoesNotContain("Unity.Addressables", testsAssembly);
        StringAssert.DoesNotContain("Unity.ResourceManager", testsAssembly);
    }

    [Test]
    public void CopyDirectory_RawContent_CopiesFilesAndSkipsUnityMetadata()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            nameof(ExternalContentSettingsTests),
            Guid.NewGuid().ToString("N")
        );
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Art", "UI"));
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(source, "Art", "UI", "image.png"), "image");
            File.WriteAllText(Path.Combine(source, "Art", "UI", "image.png.meta"), "metadata");
            File.WriteAllText(Path.Combine(source, ".DS_Store"), "metadata");
            File.WriteAllText(Path.Combine(destination, "removed.png"), "stale");

            CopyContentDirectory(source, destination);

            Assert.IsTrue(File.Exists(Path.Combine(destination, "Art", "UI", "image.png")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "Art", "UI", "image.png.meta")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, ".DS_Store")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "removed.png")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void ExternalContentArt_ContentBackedPrefabs_HaveRootOverrideComponent()
    {
        string[] missingComponents = AssetDatabase
            .FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(HasContentDependency)
            .Where(path =>
                AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ExternalContentArt>()
                == null
            )
            .ToArray();

        Assert.IsEmpty(
            missingComponents,
            $"Content-backed prefabs without external art overrides:{Environment.NewLine}{string.Join(Environment.NewLine, missingComponents)}"
        );
    }

    [Test]
    public void ExternalContentArt_ContentBackedPrefabTextures_ResolveExternalPaths()
    {
        string[] missingPaths = AssetDatabase
            .FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .SelectMany(path => AssetDatabase.GetDependencies(path, true))
            .Where(path =>
                path.StartsWith(_contentRoot + "/Art/", StringComparison.Ordinal)
                && IsTexturePath(path)
            )
            .Distinct(StringComparer.Ordinal)
            .Where(path =>
                !ResourceManager.TryGetExternalArtPath(
                    AssetDatabase.LoadAssetAtPath<Texture>(path),
                    out _
                )
            )
            .ToArray();

        Assert.IsEmpty(
            missingPaths,
            $"Prefab textures without external content paths:{Environment.NewLine}{string.Join(Environment.NewLine, missingPaths)}"
        );
    }

    [Test]
    public void ExternalAnimationFrames_NumberedMainMenuSequence_AreDiscoverable()
    {
        Texture2D firstFrame = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Content/Art/HD/UI/MainMenu/ui_mainmenu_exit_01.png"
        );

        IReadOnlyList<string> paths = ResourceManager.GetExternalAnimationFramePaths(firstFrame);

        Assert.AreEqual(30, paths.Count);
        Assert.AreEqual("Art/HD/UI/MainMenu/ui_mainmenu_exit_01", paths[0]);
        Assert.AreEqual("Art/HD/UI/MainMenu/ui_mainmenu_exit_30", paths[29]);
    }

    private static bool HasContentDependency(string prefabPath)
    {
        return AssetDatabase
            .GetDependencies(prefabPath, true)
            .Any(path => path.StartsWith(_contentRoot + "/", StringComparison.Ordinal));
    }

    private static bool IsTexturePath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyContentDirectory(string source, string destination)
    {
        Type exporterType = AppDomain
            .CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ExternalContentExporter"))
            .Single(type => type != null);
        MethodInfo copyMethod =
            exporterType.GetMethod("CopyDirectory", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(exporterType.FullName, "CopyDirectory");

        try
        {
            copyMethod.Invoke(null, new object[] { source, destination });
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }
}
