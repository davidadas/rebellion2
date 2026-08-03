using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Verifies that parsed external models remain reusable across scene instances.
/// </summary>
public sealed class ContentModelCacheTests
{
    private const string _planetAddress = "Application/MainMenu/Models/planet";

    /// <summary>
    /// Preloads one model and creates independent hierarchies from the shared parsed resource.
    /// </summary>
    [Test]
    public async Task InstantiateAsync_PreloadedModel_CreatesIndependentInstances()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        using ContentAssets assets = new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        using ContentModelCache cache = new ContentModelCache(assets);
        GameObject firstParent = new GameObject("FirstModelParent");
        GameObject secondParent = new GameObject("SecondModelParent");
        ContentModelInstance first = null;
        ContentModelInstance second = null;
        try
        {
            await cache.PreloadAsync(new[] { _planetAddress });
            first = await cache.InstantiateAsync(
                _planetAddress,
                firstParent.transform,
                CancellationToken.None
            );
            second = await cache.InstantiateAsync(
                _planetAddress,
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
