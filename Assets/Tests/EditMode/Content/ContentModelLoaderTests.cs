using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class ContentModelLoaderTests
{
    private const string _planetAddress = "Application/MainMenu/Models/planet";

    [Test]
    public async Task LoadAsync_ValidGlb_ReturnsDisposableModel()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        using ContentAssets assets = new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        string filePath = assets.ResolveFile(_planetAddress, ".glb");
        Assert.That(filePath, Is.Not.Null, $"Test GLB is missing: {_planetAddress}");

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
