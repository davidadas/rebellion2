using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;
using UnityEngine;
using UnityEngine.Networking;

public static class ResourceManager
{
    private const string _contentDirectoryName = "Content";
    private const string _contentPathArgument = "-contentPath";
    private const string _hdArtRoot = "Art/HD";
    private const string _mainMenuSfxRoot = "Audio/SFX/MainMenu/";
    private const string _strategySfxRoot = "Audio/SFX/StrategyView/";
    private const string _strategyMessageRoot = "Messages/";

    private static readonly string[] _textureExtensions = { ".png", ".jpg", ".jpeg" };
    private static readonly Dictionary<string, AudioClip> _audioClips = new Dictionary<
        string,
        AudioClip
    >(StringComparer.Ordinal);
    private static readonly Dictionary<string, Task<AudioClip>> _audioLoads = new Dictionary<
        string,
        Task<AudioClip>
    >(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D> _textures = new Dictionary<
        string,
        Texture2D
    >(StringComparer.Ordinal);
    private static readonly HashSet<string> _unavailableTexturePaths = new HashSet<string>(
        StringComparer.Ordinal
    );

    private static Dictionary<string, string> _artPathsByName;
    private static string _contentRootPath;
    private static Task _initializationTask;

    internal static Task InitializeAsync()
    {
        _initializationTask ??= InitializeContentAsync();
        return _initializationTask;
    }

    internal static string ContentRootPath => _contentRootPath ??= ResolveContentRootPath();

    internal static Task<AudioClip> LoadAudioAsync(string path)
    {
        string normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
            throw new ArgumentException("An audio content path is required.", nameof(path));
        if (_audioClips.TryGetValue(normalizedPath, out AudioClip clip))
            return Task.FromResult(clip);
        if (_audioLoads.TryGetValue(normalizedPath, out Task<AudioClip> load))
            return load;

        load = LoadAndCacheAudioAsync(normalizedPath);
        _audioLoads.Add(normalizedPath, load);
        return load;
    }

    internal static bool TryGetExternalArtPath(Texture texture, out string path)
    {
        path = null;
        if (texture == null || string.IsNullOrEmpty(texture.name))
            return false;

        _artPathsByName ??= CreateArtPathMap();
        return _artPathsByName.TryGetValue(texture.name, out path);
    }

    internal static IReadOnlyList<string> GetExternalAnimationFramePaths(Texture firstFrame)
    {
        if (!TryGetExternalArtPath(firstFrame, out string firstFramePath))
            return Array.Empty<string>();

        string firstFrameName = Path.GetFileName(firstFramePath);
        int separatorIndex = firstFrameName.LastIndexOf('_');
        if (
            separatorIndex < 0
            || !int.TryParse(firstFrameName[(separatorIndex + 1)..], out int firstFrameNumber)
        )
            return Array.Empty<string>();

        string prefix = firstFrameName[..(separatorIndex + 1)];
        string directory = NormalizePath(Path.GetDirectoryName(firstFramePath));
        List<string> frames = new List<string>();
        for (int frame = firstFrameNumber; ; frame++)
        {
            string frameName = prefix + frame.ToString("D2");
            if (
                !_artPathsByName.TryGetValue(frameName, out string path)
                || !string.Equals(
                    NormalizePath(Path.GetDirectoryName(path)),
                    directory,
                    StringComparison.Ordinal
                )
            )
                break;

            frames.Add(path);
        }

        return frames;
    }

    internal static string GetVideoUrl(string path)
    {
        return new Uri(ResolveContentFile(path, ".mp4")).AbsoluteUri;
    }

    internal static void SetContentRootPathForTests(string path)
    {
        DestroyCachedAssets(_audioClips.Values);
        DestroyCachedAssets(_textures.Values);
        _contentRootPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        _initializationTask = null;
        _audioClips.Clear();
        _audioLoads.Clear();
        _textures.Clear();
        _unavailableTexturePaths.Clear();
        _artPathsByName = null;
    }

    public static T GetConfig<T>()
        where T : class
    {
        string typeName = typeof(T).Name;
        string filePath = NormalizePath(Path.Combine("Configs", typeName));
        byte[] bytes = File.ReadAllBytes(ResolveContentFile(filePath, ".xml"));
        GameSerializerSettings settings = new GameSerializerSettings { RootName = typeName };

        string schemaPath = ResolveOptionalContentFile(
            NormalizePath(Path.Combine("Configs", $"{typeName}Schema")),
            ".xml"
        );
        if (schemaPath != null)
        {
            XmlSchemaSet schemas = new XmlSchemaSet();
            using XmlReader schemaReader = XmlReader.Create(schemaPath);
            schemas.Add(null, schemaReader);
            settings.Schemas = schemas;
        }

        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using MemoryStream stream = new MemoryStream(bytes);
        object result = serializer.Deserialize(stream);

        return result as T ?? throw new Exception($"Failed to deserialize config: {typeName}");
    }

    public static T[] GetEntityData<T>()
        where T : BaseGameEntity
    {
        string typeName = typeof(T).Name;
        string pluralizedType = typeName.EndsWith("s") ? typeName : $"{typeName}s";
        string filePath = NormalizePath(Path.Combine("Data", pluralizedType));
        byte[] bytes = File.ReadAllBytes(ResolveContentFile(filePath, ".xml"));
        GameSerializerSettings settings = new GameSerializerSettings { RootName = pluralizedType };
        GameSerializer serializer = new GameSerializer(typeof(T[]), settings);

        using MemoryStream stream = new MemoryStream(bytes);
        object result = serializer.Deserialize(stream);

        return result as T[]
            ?? throw new Exception($"Failed to deserialize game data: {pluralizedType}");
    }

    public static T GetData<T>()
        where T : class
    {
        string typeName = typeof(T).Name;
        string filePath = NormalizePath(Path.Combine("Data", typeName));
        byte[] bytes = File.ReadAllBytes(ResolveContentFile(filePath, ".xml"));
        GameSerializerSettings settings = new GameSerializerSettings { RootName = typeName };
        GameSerializer serializer = new GameSerializer(typeof(T), settings);

        using MemoryStream stream = new MemoryStream(bytes);
        object result = serializer.Deserialize(stream);

        return result as T ?? throw new Exception($"Failed to deserialize data: {typeName}");
    }

    public static AudioClip GetAudio(string path)
    {
        string normalizedPath = NormalizePath(path);
        return _audioClips.TryGetValue(normalizedPath, out AudioClip clip)
            ? clip
            : throw new Exception($"Audio has not been preloaded at: {path}");
    }

    public static Texture2D GetTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string normalizedPath = NormalizePath(path);
        if (_textures.TryGetValue(normalizedPath, out Texture2D texture))
            return texture;
        if (_unavailableTexturePaths.Contains(normalizedPath))
            return null;

        return LoadAndCacheTexture(normalizedPath);
    }

    private static async Task InitializeContentAsync()
    {
        string contentRoot = ContentRootPath;
        if (!Directory.Exists(contentRoot))
            throw new DirectoryNotFoundException($"Content directory not found: {contentRoot}");
        if (!Directory.Exists(Path.Combine(contentRoot, "Configs")))
            throw new DirectoryNotFoundException($"Config directory not found: {contentRoot}");
        if (!Directory.Exists(Path.Combine(contentRoot, "Data")))
            throw new DirectoryNotFoundException($"Data directory not found: {contentRoot}");

        await Task.WhenAll(EnumerateImmediateAudioPaths().Select(LoadAudioAsync));
    }

    private static IEnumerable<string> EnumerateImmediateAudioPaths()
    {
        string audioRoot = Path.Combine(ContentRootPath, "Audio", "SFX");
        if (!Directory.Exists(audioRoot))
            yield break;

        foreach (
            string filePath in Directory.EnumerateFiles(
                audioRoot,
                "*.wav",
                SearchOption.AllDirectories
            )
        )
        {
            string path = NormalizePath(Path.GetRelativePath(ContentRootPath, filePath));
            if (IsImmediateAudioPath(path))
                yield return RemoveExtension(path);
        }
    }

    private static bool IsImmediateAudioPath(string path)
    {
        if (path.StartsWith(_mainMenuSfxRoot, StringComparison.Ordinal))
            return true;
        if (!path.StartsWith(_strategySfxRoot, StringComparison.Ordinal))
            return false;

        string relativePath = path[_strategySfxRoot.Length..];
        return !relativePath.Contains('/')
            || relativePath.StartsWith(
                _strategyMessageRoot + "sfx_strategyview_message_confirm_",
                StringComparison.Ordinal
            )
            || relativePath.StartsWith(
                _strategyMessageRoot + "sfx_strategyview_message_planetary_assault.",
                StringComparison.Ordinal
            );
    }

    private static async Task<AudioClip> LoadAndCacheAudioAsync(string path)
    {
        string filePath = ResolveContentFile(path, ".wav");
        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
            new Uri(filePath).AbsoluteUri,
            AudioType.WAV
        );
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
            throw new IOException($"Audio could not be loaded at '{filePath}': {request.error}");

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
            throw new IOException($"Audio could not be decoded at '{filePath}'.");

        clip.name = Path.GetFileNameWithoutExtension(filePath);
        _audioClips[path] = clip;
        return clip;
    }

    private static Texture2D LoadAndCacheTexture(string path)
    {
        string filePath = ResolveOptionalContentFile(path, _textureExtensions);
        if (filePath == null)
        {
            _unavailableTexturePaths.Add(path);
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (IOException)
        {
            _unavailableTexturePaths.Add(path);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            _unavailableTexturePaths.Add(path);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, true))
        {
            DestroyCachedAssets(new[] { texture });
            _unavailableTexturePaths.Add(path);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(filePath);
        texture.wrapMode = TextureWrapMode.Clamp;
        _textures[path] = texture;
        return texture;
    }

    private static Dictionary<string, string> CreateArtPathMap()
    {
        Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
        string artRoot = Path.Combine(ContentRootPath, _hdArtRoot);
        if (!Directory.Exists(artRoot))
            return paths;

        foreach (string extension in _textureExtensions)
        {
            foreach (
                string filePath in Directory.EnumerateFiles(
                    artRoot,
                    "*" + extension,
                    SearchOption.AllDirectories
                )
            )
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                if (!paths.ContainsKey(name))
                {
                    string relativePath = NormalizePath(
                        Path.GetRelativePath(ContentRootPath, filePath)
                    );
                    paths.Add(name, RemoveExtension(relativePath));
                }
            }
        }

        return paths;
    }

    private static string ResolveContentRootPath()
    {
        string commandLinePath = GetCommandLineContentPath();
        if (commandLinePath != null)
            return Path.GetFullPath(commandLinePath);

#if UNITY_EDITOR
        return Path.GetFullPath(Path.Combine(Application.dataPath, _contentDirectoryName));
#else
        DirectoryInfo playerDirectory =
            Application.platform == RuntimePlatform.OSXPlayer
                ? Directory.GetParent(Application.dataPath)?.Parent
                : Directory.GetParent(Application.dataPath);
        if (playerDirectory == null)
            throw new InvalidOperationException(
                "The player content directory could not be resolved."
            );

        return Path.Combine(playerDirectory.FullName, _contentDirectoryName);
#endif
    }

    private static string GetCommandLineContentPath()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (
                string.Equals(
                    arguments[index],
                    _contentPathArgument,
                    StringComparison.OrdinalIgnoreCase
                ) && !string.IsNullOrWhiteSpace(arguments[index + 1])
            )
                return arguments[index + 1];
        }

        return null;
    }

    private static string ResolveContentFile(string path, params string[] extensions)
    {
        return ResolveOptionalContentFile(path, extensions)
            ?? throw new FileNotFoundException($"Content file not found: {path}");
    }

    private static string ResolveOptionalContentFile(string path, params string[] extensions)
    {
        if (Path.IsPathRooted(path?.Trim() ?? string.Empty))
            throw new ArgumentException("Content paths must be relative.", nameof(path));

        string normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
            return null;

        string exactPath = ResolveSafePath(normalizedPath);
        if (File.Exists(exactPath))
            return exactPath;

        foreach (string extension in extensions)
        {
            string candidatePath = ResolveSafePath(normalizedPath + extension);
            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return null;
    }

    private static string ResolveSafePath(string relativePath)
    {
        string rootPath = Path.GetFullPath(ContentRootPath);
        string candidatePath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        string requiredPrefix =
            rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw new ArgumentException("Content paths cannot leave the content directory.");

        return candidatePath;
    }

    private static string NormalizePath(string path)
    {
        return path?.Trim().TrimStart('/', '\\').Replace('\\', '/');
    }

    private static string RemoveExtension(string path)
    {
        int extensionIndex = path.LastIndexOf('.');
        return extensionIndex < 0 ? path : path[..extensionIndex];
    }

    private static void DestroyCachedAssets<T>(IEnumerable<T> assets)
        where T : UnityEngine.Object
    {
        foreach (T asset in assets)
        {
            if (asset == null)
                continue;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(asset);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
