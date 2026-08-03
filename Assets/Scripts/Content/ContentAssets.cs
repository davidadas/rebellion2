using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Loads and owns addressed media from application content and one active pack.
/// </summary>
public sealed class ContentAssets : IContentAssetSource, IDisposable
{
    private const string _packAddressPrefix = "Pack/";
    private const string _applicationAddressPrefix = "Application/";

    private static readonly string[] _textureExtensions = { ".png", ".jpg", ".jpeg" };
    private readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, Task<AudioClip>> audioLoads = new Dictionary<
        string,
        Task<AudioClip>
    >(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(
        StringComparer.Ordinal
    );
    private readonly HashSet<string> unavailableTextures = new HashSet<string>(
        StringComparer.Ordinal
    );

    private bool disposed;

    /// <summary>
    /// Gets the absolute root of the external content directory.
    /// </summary>
    public string ContentRootPath { get; }

    /// <summary>
    /// Gets the absolute root of the active pack.
    /// </summary>
    public string PackRootPath { get; }

    /// <summary>
    /// Creates an asset store for application content and one active pack.
    /// </summary>
    /// <param name="contentRootPath">The absolute external content root.</param>
    /// <param name="packRootPath">The absolute active pack root.</param>
    public ContentAssets(string contentRootPath, string packRootPath)
    {
        ContentRootPath = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        PackRootPath = Path.GetFullPath(
            packRootPath ?? throw new ArgumentNullException(nameof(packRootPath))
        );
    }

    /// <summary>
    /// Loads every texture and audio clip declared by a preload manifest.
    /// </summary>
    /// <param name="manifest">The preload manifest to load.</param>
    /// <returns>A task that completes when every declared asset is resident.</returns>
    public async Task PreloadAsync(ContentPreloadManifest manifest)
    {
        ThrowIfDisposed();
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));
        if (manifest.TexturesPerFrame <= 0)
        {
            throw new InvalidDataException(
                "Content preload manifests require a positive TexturesPerFrame value."
            );
        }

        IEnumerable<string> texturePaths = manifest.Textures.Concat(
            manifest.TextureDirectories.SelectMany(GetTextureAddresses)
        );
        int loadedTextureCount = 0;
        foreach (string path in texturePaths.Distinct(StringComparer.Ordinal))
        {
            _ =
                GetTexture(path)
                ?? throw new FileNotFoundException($"Preload texture was not found: {path}");
            loadedTextureCount++;
            if (loadedTextureCount % manifest.TexturesPerFrame == 0)
                await Task.Yield();
        }

        await Task.WhenAll(manifest.Audio.Distinct(StringComparer.Ordinal).Select(LoadAudioAsync));
    }

    /// <summary>
    /// Enumerates texture addresses beneath one content directory.
    /// </summary>
    /// <param name="directoryAddress">The content directory address.</param>
    /// <returns>The discovered texture addresses.</returns>
    private IEnumerable<string> GetTextureAddresses(string directoryAddress)
    {
        string normalizedAddress = NormalizeAddress(directoryAddress);
        string directoryPath = ResolveAddressPath(normalizedAddress);
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(
                $"Content directory not found: {directoryAddress}"
            );

        return Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(path =>
                _textureExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase
                )
            )
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                string relativePath = Path.GetRelativePath(directoryPath, path).Replace('\\', '/');
                relativePath = relativePath[..^Path.GetExtension(relativePath).Length];
                return normalizedAddress.TrimEnd('/') + "/" + relativePath;
            });
    }

    /// <summary>
    /// Resolves and caches a texture from an explicitly scoped content address.
    /// </summary>
    /// <param name="path">The application or pack content address.</param>
    /// <returns>The loaded texture, or null when the address cannot be loaded.</returns>
    public Texture2D GetTexture(string path)
    {
        ThrowIfDisposed();
        string normalizedPath = NormalizeAddress(path);
        if (string.IsNullOrEmpty(normalizedPath))
            return null;
        if (textures.TryGetValue(normalizedPath, out Texture2D texture))
        {
            if (texture != null)
                return texture;

            // Unity can destroy transient editor-preview objects when a prefab stage closes while
            // domain reload is disabled. Never retain a dead Unity object in the content cache.
            textures.Remove(normalizedPath);
        }
        if (unavailableTextures.Contains(normalizedPath))
            return null;

        string filePath = ResolveOptionalAssetFile(normalizedPath, _textureExtensions);
        if (filePath == null)
        {
            unavailableTextures.Add(normalizedPath);
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes, true))
            {
                DestroyAsset(texture);
                unavailableTextures.Add(normalizedPath);
                return null;
            }
        }
        catch (IOException)
        {
            unavailableTextures.Add(normalizedPath);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            unavailableTextures.Add(normalizedPath);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(filePath);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        textures.Add(normalizedPath, texture);
        return texture;
    }

    /// <summary>
    /// Resolves and caches a sprite backed by an addressed texture.
    /// </summary>
    public Sprite GetSprite(string path)
    {
        string normalizedPath = NormalizeAddress(path);
        if (string.IsNullOrEmpty(normalizedPath))
            return null;
        if (sprites.TryGetValue(normalizedPath, out Sprite sprite) && sprite != null)
            return sprite;

        sprites.Remove(normalizedPath);
        Texture2D texture = GetTexture(normalizedPath);
        if (texture == null)
            return null;

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = texture.name;
        sprites.Add(normalizedPath, sprite);
        return sprite;
    }

    /// <summary>
    /// Resolves an addressed content file with the given extension to a safe absolute path,
    /// without loading it. Used by the model loader, which owns asynchronous glTF instantiation.
    /// </summary>
    /// <param name="address">The application- or pack-relative content address.</param>
    /// <param name="extension">The required file extension, including the leading dot.</param>
    /// <returns>The resolved absolute path, or null when the file cannot be found.</returns>
    public string ResolveFile(string address, string extension)
    {
        ThrowIfDisposed();
        return ResolveOptionalAssetFile(address, extension);
    }

    /// <summary>
    /// Asynchronously resolves and caches audio from an explicitly scoped content address.
    /// </summary>
    /// <param name="path">The application or pack content address.</param>
    /// <returns>A task containing the loaded audio clip.</returns>
    public Task<AudioClip> LoadAudioAsync(string path)
    {
        ThrowIfDisposed();
        string normalizedPath = NormalizeAddress(path);
        if (string.IsNullOrEmpty(normalizedPath))
            throw new ArgumentException("An audio content path is required.", nameof(path));
        if (audioClips.TryGetValue(normalizedPath, out AudioClip clip))
            return Task.FromResult(clip);
        if (audioLoads.TryGetValue(normalizedPath, out Task<AudioClip> load))
            return load;

        load = LoadAndCacheAudioAsync(normalizedPath);
        audioLoads.Add(normalizedPath, load);
        return load;
    }

    /// <summary>
    /// Gets audio that has already been loaded by a preload manifest.
    /// </summary>
    /// <param name="path">The application or pack content address.</param>
    /// <returns>The resident audio clip.</returns>
    public AudioClip GetPreloadedAudio(string path)
    {
        ThrowIfDisposed();
        string normalizedPath = NormalizeAddress(path);
        return audioClips.TryGetValue(normalizedPath, out AudioClip clip)
            ? clip
            : throw new InvalidOperationException($"Audio has not been preloaded at: {path}");
    }

    /// <summary>
    /// Resolves a video content address to a local file URL.
    /// </summary>
    /// <param name="path">The application or pack video address.</param>
    /// <returns>The local video file URL.</returns>
    public string GetVideoUrl(string path)
    {
        ThrowIfDisposed();
        return new Uri(ResolveAssetFile(path, ".mp4")).AbsoluteUri;
    }

    /// <summary>
    /// Releases every texture and audio clip owned by this application asset store.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        foreach (AudioClip clip in audioClips.Values)
            DestroyAsset(clip);
        foreach (Sprite sprite in sprites.Values)
            DestroyAsset(sprite);
        foreach (Texture2D texture in textures.Values)
            DestroyAsset(texture);

        audioClips.Clear();
        audioLoads.Clear();
        textures.Clear();
        sprites.Clear();
        unavailableTextures.Clear();
        disposed = true;
    }

    /// <summary>
    /// Loads one audio file and stores the completed clip in this asset store.
    /// </summary>
    /// <param name="path">The normalized audio content address.</param>
    /// <returns>A task containing the loaded audio clip.</returns>
    private async Task<AudioClip> LoadAndCacheAudioAsync(string path)
    {
        await Task.Yield();
        try
        {
            string filePath = ResolveAssetFile(path, ".wav");
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                new Uri(filePath).AbsoluteUri,
                AudioType.WAV
            );
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException(
                    $"Audio could not be loaded at '{filePath}': {request.error}"
                );

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
                throw new IOException($"Audio could not be decoded at '{filePath}'.");

            clip.name = Path.GetFileNameWithoutExtension(filePath);
            audioClips[path] = clip;
            return clip;
        }
        finally
        {
            audioLoads.Remove(path);
        }
    }

    /// <summary>
    /// Resolves a required asset file from an address and supported extensions.
    /// </summary>
    /// <param name="path">The explicitly scoped content address.</param>
    /// <param name="extensions">The supported file extensions.</param>
    /// <returns>The absolute existing file path.</returns>
    private string ResolveAssetFile(string path, params string[] extensions)
    {
        return ResolveOptionalAssetFile(path, extensions)
            ?? throw new FileNotFoundException($"Content file not found: {path}");
    }

    /// <summary>
    /// Resolves an optional asset file from an address and supported extensions.
    /// </summary>
    /// <param name="path">The explicitly scoped content address.</param>
    /// <param name="extensions">The supported file extensions.</param>
    /// <returns>The absolute existing file path, or null when no file matches.</returns>
    private string ResolveOptionalAssetFile(string path, params string[] extensions)
    {
        string normalizedPath = NormalizeAddress(path);
        if (string.IsNullOrEmpty(normalizedPath))
            return null;

        string exactPath = ResolveAddressPath(normalizedPath);
        if (File.Exists(exactPath))
            return exactPath;

        foreach (string extension in extensions)
        {
            string candidatePath = ResolveAddressPath(normalizedPath + extension);
            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return null;
    }

    /// <summary>
    /// Resolves an application or pack address to its absolute path boundary.
    /// </summary>
    /// <param name="path">The normalized, explicitly scoped address.</param>
    /// <returns>The resolved absolute path.</returns>
    private string ResolveAddressPath(string path)
    {
        if (path.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal))
            return ResolveSafePath(ContentRootPath, path);
        if (path.StartsWith(_packAddressPrefix, StringComparison.Ordinal))
            return ResolveSafePath(PackRootPath, path[_packAddressPrefix.Length..]);

        throw new ArgumentException(
            $"Content addresses must begin with '{_applicationAddressPrefix}' or '{_packAddressPrefix}'.",
            nameof(path)
        );
    }

    /// <summary>
    /// Resolves a relative path while preventing traversal outside its root.
    /// </summary>
    /// <param name="rootPath">The absolute path boundary.</param>
    /// <param name="relativePath">The relative content path.</param>
    /// <returns>The resolved absolute path.</returns>
    private static string ResolveSafePath(string rootPath, string relativePath)
    {
        string absoluteRoot = Path.GetFullPath(rootPath);
        string candidatePath = Path.GetFullPath(Path.Combine(absoluteRoot, relativePath));
        string requiredPrefix =
            absoluteRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw new ArgumentException("Content paths cannot leave their content root.");

        return candidatePath;
    }

    /// <summary>
    /// Normalizes a content address to forward-slash relative form.
    /// </summary>
    /// <param name="path">The content address to normalize.</param>
    /// <returns>The normalized address, or null when the input is null.</returns>
    private static string NormalizeAddress(string path)
    {
        if (path == null)
            return null;

        string normalizedPath = path.Trim();
        if (
            Path.IsPathRooted(normalizedPath)
            || normalizedPath.StartsWith("/", StringComparison.Ordinal)
            || normalizedPath.StartsWith("\\", StringComparison.Ordinal)
        )
            throw new ArgumentException("Content addresses must be relative.", nameof(path));

        return normalizedPath.Replace('\\', '/');
    }

    /// <summary>
    /// Destroys an owned Unity asset using the lifecycle appropriate to the current context.
    /// </summary>
    /// <param name="asset">The owned asset to destroy.</param>
    private static void DestroyAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(asset);
        else
            UnityEngine.Object.DestroyImmediate(asset);
    }

    /// <summary>
    /// Rejects asset operations after this store has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ContentAssets));
    }
}
