using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class ContentModelCacheTests
{
    private const string _citadelAddress = "Application/MainMenu/Models/citadel";

    [Test]
    public async Task InstantiateAsync_PreloadedModel_CreatesIndependentInstancesAsync()
    {
        string contentRoot = Path.Combine(Application.dataPath, "Content");
        using ContentAssets assets = new ContentAssets(contentRoot, contentRoot);
        using ContentModelCache cache = new ContentModelCache(assets);
        GameObject firstParent = new GameObject("FirstModelParent");
        GameObject secondParent = new GameObject("SecondModelParent");
        ContentModelInstance first = null;
        ContentModelInstance second = null;
        try
        {
            await cache.PreloadAsync(new[] { _citadelAddress });
            first = await cache.InstantiateAsync(
                _citadelAddress,
                firstParent.transform,
                CancellationToken.None
            );
            second = await cache.InstantiateAsync(
                _citadelAddress,
                secondParent.transform,
                CancellationToken.None
            );

            Assert.That(first.ModelRoot.IsChildOf(firstParent.transform), Is.True);
            Assert.That(second.ModelRoot.IsChildOf(secondParent.transform), Is.True);
            Assert.That(first.ModelRoot, Is.Not.SameAs(second.ModelRoot));
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();
            Object.DestroyImmediate(firstParent);
            Object.DestroyImmediate(secondParent);
        }
    }
}
