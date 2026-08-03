using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Owns parsed GLB resources for the application lifetime and creates lightweight scene instances
/// from them. Repeated requests for one address share the same load task.
/// </summary>
public sealed class ContentModelCache : IDisposable
{
    private readonly ContentAssets contentAssets;
    private readonly Dictionary<string, Task<ContentModelResource>> loads = new Dictionary<
        string,
        Task<ContentModelResource>
    >(StringComparer.Ordinal);
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private bool disposed;

    /// <summary>
    /// Creates a model cache backed by the active external content source.
    /// </summary>
    public ContentModelCache(ContentAssets assets)
    {
        contentAssets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    /// <summary>
    /// Parses all requested models so later scene instantiation does not touch disk or decode GLB data.
    /// </summary>
    public Task PreloadAsync(IEnumerable<string> addresses)
    {
        ThrowIfDisposed();
        if (addresses == null)
            throw new ArgumentNullException(nameof(addresses));

        return Task.WhenAll(addresses.Distinct(StringComparer.Ordinal).Select(GetOrLoadAsync));
    }

    /// <summary>
    /// Instantiates a parsed model beneath an authored scene transform.
    /// </summary>
    public async Task<ContentModelInstance> InstantiateAsync(
        string address,
        Transform parent,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDisposed();
        ContentModelResource resource = await GetOrLoadAsync(address);
        cancellationToken.ThrowIfCancellationRequested();
        return await resource.InstantiateAsync(parent, cancellationToken);
    }

    /// <summary>
    /// Releases all successfully parsed model resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        shutdown.Cancel();
        foreach (Task<ContentModelResource> load in loads.Values)
        {
            if (load.Status == TaskStatus.RanToCompletion)
                load.Result.Dispose();
        }

        loads.Clear();
        shutdown.Dispose();
    }

    /// <summary>
    /// Returns the shared parse task for an address, starting it when necessary.
    /// </summary>
    private Task<ContentModelResource> GetOrLoadAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A model content address is required.", nameof(address));
        if (loads.TryGetValue(address, out Task<ContentModelResource> load))
            return load;

        string filePath =
            contentAssets.ResolveFile(address, ".glb")
            ?? throw new InvalidOperationException($"Content model is missing: {address}");
        load = LoadOwnedResourceAsync(filePath);
        loads.Add(address, load);
        return load;
    }

    /// <summary>
    /// Loads a resource owned by this cache and releases it if shutdown wins the load race.
    /// </summary>
    private async Task<ContentModelResource> LoadOwnedResourceAsync(string filePath)
    {
        ContentModelResource resource = await ContentModelLoader.LoadResourceAsync(
            filePath,
            shutdown.Token
        );
        if (!disposed)
            return resource;

        resource.Dispose();
        throw new ObjectDisposedException(nameof(ContentModelCache));
    }

    /// <summary>
    /// Rejects model operations after application-owned resources have been released.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ContentModelCache));
    }
}
