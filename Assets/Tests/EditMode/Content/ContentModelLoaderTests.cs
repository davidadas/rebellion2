using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class ContentModelLoaderTests
{
    private const string _planetModelPath = "Assets/Content/Application/MainMenu/Models/planet.glb";

    [Test]
    public async Task LoadAsync_ValidGlb_ReturnsDisposableModel()
    {
        string filePath = Path.GetFullPath(_planetModelPath);
        Assert.That(File.Exists(filePath), Is.True, $"Test GLB is missing: {filePath}");

        GameObject parent = new GameObject("ContentModelLoaderTest");
        ContentModelInstance instance = null;
        try
        {
            instance = await ContentModelLoader.LoadAsync(
                filePath,
                parent.transform,
                CancellationToken.None
            );

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.ModelRoot, Is.Not.Null);
            Assert.That(instance.ModelRoot.IsChildOf(parent.transform), Is.True);

            instance.Dispose();
            Assert.That(instance.ModelRoot, Is.Null);
            instance = null;
        }
        finally
        {
            instance?.Dispose();
            Object.DestroyImmediate(parent);
        }
    }
}
