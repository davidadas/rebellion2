using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Util.IO;
using UnityEngine;

/// <summary>
/// Installs community content into Mods while retaining package ownership and previous archives.
/// </summary>
public sealed class ModPackageStore
{
    private readonly string _contentRoot;
    private readonly string _modsRoot;
    private readonly string _stateRoot;
    private readonly ThunderstoreClient _client;
    private ThunderstorePackage[] _catalog;
    private ThunderstoreCommunityFilters _communityFilters;
    private string RegistryPath => Path.Combine(_stateRoot, "installed.json");
    private string CommunityFiltersPath => Path.Combine(_stateRoot, "community-filters.json");

    /// <summary>
    /// Gets the cached catalog path used for offline browsing.
    /// </summary>
    public string CatalogPath => Path.Combine(_stateRoot, "catalog.json");

    /// <summary>
    /// Binds package management to the same external content root used by the game.
    /// </summary>
    /// <param name="contentRoot">The installed Content directory.</param>
    /// <param name="client">The public package transport.</param>
    public ModPackageStore(string contentRoot, ThunderstoreClient client)
    {
        _contentRoot = Path.GetFullPath(contentRoot);
        _modsRoot = ContentFileResolver.ResolveModsRootPath(contentRoot);
        _stateRoot = Path.Combine(Directory.GetParent(_modsRoot).FullName, ".mod-manager");
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Restores or completes an interrupted filesystem publication before presenting installed content.
    /// </summary>
    public void RecoverInterruptedInstallations()
    {
        using FileStream lease = AcquireLease();
    }

    /// <summary>
    /// Finds native pack folders in Mods that are not owned by an installation receipt.
    /// </summary>
    /// <returns>The existing pack folders not owned by an installation receipt.</returns>
    public string[] GetUnmanagedPackFolders()
    {
        HashSet<string> managed = new(
            ReadRegistry()
                .Packages.SelectMany(package => package.Content)
                .Select(content => content.Folder),
            StringComparer.OrdinalIgnoreCase
        );
        return Directory.Exists(_modsRoot)
            ? Directory
                .EnumerateDirectories(_modsRoot)
                .Where(folder =>
                    !managed.Contains(Path.GetFileName(folder))
                    && File.Exists(Path.Combine(folder, "pack.xml"))
                )
                .OrderBy(folder => folder, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
    }

    /// <summary>
    /// Lists managed packages and manually installed native content without changing either.
    /// </summary>
    /// <returns>The installed package identities.</returns>
    public IReadOnlyList<InstalledModPackage> GetInstalled()
    {
        List<InstalledModPackage> result = ReadRegistry().Packages.ToList();
        HashSet<string> managed = new HashSet<string>(
            result.SelectMany(package => package.Content).Select(content => content.Folder),
            StringComparer.OrdinalIgnoreCase
        );
        if (Directory.Exists(_modsRoot))
            foreach (
                string folder in Directory
                    .EnumerateDirectories(_modsRoot)
                    .OrderBy(path => path, StringComparer.Ordinal)
            )
            {
                string name = Path.GetFileName(folder);
                if (
                    managed.Contains(name)
                    || (
                        !File.Exists(Path.Combine(folder, "pack.xml"))
                        && !File.Exists(Path.Combine(folder, "mod.xml"))
                    )
                )
                    continue;
                try
                {
                    InstalledModContent content = ReadContent(folder);
                    content.Folder = name;
                    result.Add(
                        new InstalledModPackage
                        {
                            PackageID = "local-" + HashText(name),
                            Name = content.DisplayName,
                            Author = "Local",
                            Version = content.Version,
                            Local = true,
                            Content = new[] { content },
                        }
                    );
                }
                catch (Exception exception)
                    when (exception
                            is IOException
                                or InvalidOperationException
                                or System.Xml.XmlException
                                or FormatException
                    )
                {
                    result.Add(
                        new InstalledModPackage
                        {
                            PackageID = "local-" + HashText(name),
                            Name = name,
                            Author = "Local",
                            Description =
                                "The content definition could not be read: " + exception.Message,
                            Local = true,
                            Content = new[]
                            {
                                new InstalledModContent
                                {
                                    Folder = name,
                                    ID = name,
                                    DisplayName = name,
                                },
                            },
                        }
                    );
                }
            }
        return result.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Returns missing or incompatible base-pack requirements without hiding installed content.
    /// </summary>
    /// <param name="package">The installed package.</param>
    /// <returns>The requirement preventing activation, or an empty string.</returns>
    public string GetCompatibilityIssue(InstalledModPackage package)
    {
        IReadOnlyList<ContentPackDefinition> packs = ContentPackLoader.DiscoverPacks(_contentRoot);
        foreach (InstalledModContent content in package.Content)
        {
            ContentPackDefinition basis = string.IsNullOrEmpty(content.BasePackID)
                ? null
                : packs.FirstOrDefault(pack => pack.ID == content.BasePackID);
            if (!string.IsNullOrEmpty(content.BasePackID) && basis == null)
                return "Requires " + content.BasePackID + ". Install that content pack first.";
            if (
                basis != null
                && !string.IsNullOrEmpty(content.BasePackVersion)
                && basis.Version != content.BasePackVersion
            )
                return "Requires "
                    + content.BasePackID
                    + " "
                    + content.BasePackVersion
                    + "; installed version is "
                    + basis.Version
                    + ".";
            try
            {
                InstalledModContent actual = ReadContent(ResolveFolder(content.Folder));
                if (actual.ID != content.ID || actual.Version != content.Version)
                    return "The installed content definition differs from its package record. Reinstall or import the edited content again.";
                string packID = content.IsPack ? content.ID : content.BasePackID;
                ContentPackLoader.CreatePackResolver(_contentRoot, packID);
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or InvalidOperationException
                            or System.Xml.XmlException
                            or ArgumentException
                )
            {
                return exception.Message;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Reads the shared catalogue snapshot without requiring a network connection.
    /// </summary>
    /// <returns>The last usable package catalogue, or an empty snapshot.</returns>
    public ThunderstorePackage[] ReadCatalog()
    {
        if (_catalog != null)
            return _catalog;
        try
        {
            _catalog = File.Exists(CatalogPath)
                ? ThunderstoreClient.ParseCatalog(File.ReadAllText(CatalogPath))
                : Array.Empty<ThunderstorePackage>();
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            _catalog = Array.Empty<ThunderstorePackage>();
        }
        return _catalog;
    }

    /// <summary>
    /// Refreshes the public catalogue used by browsing and package dependency selection.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The refreshed package snapshot.</returns>
    public async Task<ThunderstorePackage[]> RefreshCatalogAsync(
        CancellationToken cancellationToken
    )
    {
        var result = await _client.GetCatalogAsync(cancellationToken);
        SaveCatalog(result.Json);
        _catalog = result.Packages;
        return _catalog;
    }

    /// <summary>
    /// Saves the last successful public catalog for offline browsing.
    /// </summary>
    /// <param name="json">The validated response document.</param>
    public void SaveCatalog(string json)
    {
        AtomicTextFile.WriteAllText(CatalogPath, json);
        _catalog = null;
    }

    /// <summary>
    /// Reads the last successful community filters without requiring a network connection.
    /// </summary>
    /// <returns>The cached choices, or empty choices when no usable snapshot exists.</returns>
    public ThunderstoreCommunityFilters ReadCommunityFilters()
    {
        if (_communityFilters != null)
            return _communityFilters;
        try
        {
            _communityFilters = File.Exists(CommunityFiltersPath)
                ? ThunderstoreClient.ParseCommunityFilters(File.ReadAllText(CommunityFiltersPath))
                : new ThunderstoreCommunityFilters();
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            _communityFilters = new ThunderstoreCommunityFilters();
        }
        return _communityFilters;
    }

    /// <summary>
    /// Refreshes community filter metadata while retaining the last usable offline snapshot.
    /// </summary>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The refreshed community choices and section membership.</returns>
    public async Task<ThunderstoreCommunityFilters> RefreshCommunityFiltersAsync(
        CancellationToken cancellationToken
    )
    {
        var result = await _client.GetCommunityFiltersAsync(cancellationToken);
        AtomicTextFile.WriteAllText(CommunityFiltersPath, result.Json);
        _communityFilters = result.Filters;
        return _communityFilters;
    }

    /// <summary>
    /// Installs a release and its exact dependencies only after every payload has been staged.
    /// </summary>
    /// <param name="package">The selected community package.</param>
    /// <param name="version">The selected release.</param>
    /// <param name="catalog">The catalog used to resolve exact dependency references.</param>
    /// <param name="protectedContentIDs">Content currently in use by the caller.</param>
    /// <param name="progress">The user-facing operation progress.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the transaction.</returns>
    public async Task InstallAsync(
        ThunderstorePackage package,
        ThunderstorePackageVersion version,
        IReadOnlyList<ThunderstorePackage> catalog,
        IReadOnlyCollection<string> protectedContentIDs,
        Action<string, float> progress,
        CancellationToken cancellationToken
    )
    {
        using FileStream lease = AcquireLease();
        Registry registry = ReadRegistry();
        List<(ThunderstorePackage Package, ThunderstorePackageVersion Version)> plan = BuildPlan(
            package,
            version,
            catalog
        );
        string transaction = CreateTransaction();
        try
        {
            List<(InstalledModPackage Receipt, string Payload)> prepared = new();
            foreach (var item in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (
                    item.Package.full_name != package.full_name
                    && registry.Packages.Any(installed =>
                        installed.PackageID == item.Package.full_name
                        && installed.Version == item.Version.version_number
                        && installed.Content.All(content =>
                            Directory.Exists(ResolveFolder(content.Folder))
                        )
                    )
                )
                    continue;
                string archive = ArchivePath(item.Package.full_name, item.Version.version_number);
                if (
                    !File.Exists(archive)
                    || item.Version.file_size > 0
                        && new FileInfo(archive).Length != item.Version.file_size
                )
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(archive));
                    string temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await _client.DownloadAsync(
                            item.Version,
                            temporary,
                            value => progress?.Invoke("Downloading " + item.Package.name, value),
                            cancellationToken
                        );
                        if (File.Exists(archive))
                            File.Replace(temporary, archive, null);
                        else
                            File.Move(temporary, archive);
                    }
                    finally
                    {
                        if (File.Exists(temporary))
                            File.Delete(temporary);
                    }
                }
                progress?.Invoke("Preparing " + item.Package.name, 0);
                var staged = await PrepareAsync(
                    archive,
                    Path.Combine(transaction, "prepared", HashText(item.Package.full_name)),
                    item.Package,
                    item.Version,
                    cancellationToken
                );
                prepared.Add(staged);
            }
            await CommitAsync(
                registry,
                prepared,
                protectedContentIDs,
                transaction,
                progress,
                cancellationToken
            );
        }
        finally
        {
            CleanTransaction(transaction);
        }
    }

    /// <summary>
    /// Installs a native content archive without assigning it a guessed online author identity.
    /// </summary>
    /// <param name="archive">The archive selected by the player.</param>
    /// <param name="protectedContentIDs">Content currently in use by the caller.</param>
    /// <param name="progress">The user-facing operation progress.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the installation.</returns>
    public async Task InstallLocalAsync(
        string archive,
        IReadOnlyCollection<string> protectedContentIDs,
        Action<string, float> progress,
        CancellationToken cancellationToken
    )
    {
        using FileStream lease = AcquireLease();
        string transaction = CreateTransaction();
        try
        {
            var staged = await PrepareAsync(
                archive,
                Path.Combine(transaction, "prepared", "local"),
                null,
                null,
                cancellationToken
            );
            if (staged.Receipt.Dependencies.Length > 0)
                throw new InvalidDataException(
                    "This archive declares online dependencies. Install its Thunderstore listing so those dependencies can be resolved."
                );
            string cache = ArchivePath(staged.Receipt.PackageID, staged.Receipt.Version);
            Directory.CreateDirectory(Path.GetDirectoryName(cache));
            if (!File.Exists(cache))
                File.Copy(archive, cache);
            await CommitAsync(
                ReadRegistry(),
                new[] { staged },
                protectedContentIDs,
                transaction,
                progress,
                cancellationToken
            );
        }
        finally
        {
            CleanTransaction(transaction);
        }
    }

    /// <summary>
    /// Removes one package while preserving its files in a recovery folder and retaining its archives.
    /// </summary>
    /// <param name="package">The exact installed package selected by the player.</param>
    /// <param name="protectedContentIDs">Content currently in use by the caller.</param>
    public void Uninstall(
        InstalledModPackage package,
        IReadOnlyCollection<string> protectedContentIDs
    )
    {
        using FileStream lease = AcquireLease();
        Registry registry = ReadRegistry();
        InstalledModPackage current =
            GetInstalled().SingleOrDefault(item => item.PackageID == package.PackageID)
            ?? throw new InvalidOperationException(
                "This package is no longer installed. Refresh the list."
            );
        string reference = current.PackageID + "-" + current.Version;
        InstalledModPackage dependent = registry.Packages.FirstOrDefault(item =>
            item.PackageID != current.PackageID && item.Dependencies.Contains(reference)
        );
        if (dependent != null)
            throw new InvalidOperationException(
                dependent.Name + " requires this package. Remove that package first."
            );
        foreach (InstalledModContent content in current.Content)
        {
            EnsureNotProtected(content, protectedContentIDs);
            if (
                GetInstalled()
                    .Where(item => item.PackageID != current.PackageID)
                    .SelectMany(item => item.Content)
                    .Any(item => item.BasePackID == content.ID)
            )
                throw new InvalidOperationException(
                    "Another installed content pack requires " + content.DisplayName + "."
                );
        }
        string transaction = CreateTransaction();
        try
        {
            Registry replacement = new Registry
            {
                Packages = registry
                    .Packages.Where(item => item.PackageID != current.PackageID)
                    .ToArray(),
            };
            PublishFolders(
                replacement,
                current.Content.Select(content => (content.Folder, (string)null)),
                transaction,
                true
            );
        }
        finally
        {
            CleanTransaction(transaction);
        }
    }

    /// <summary>
    /// Finds exact dependency releases and rejects cycles or incompatible version requests.
    /// </summary>
    /// <param name="package">The requested package.</param>
    /// <param name="version">The requested release.</param>
    /// <param name="catalog">The available dependency listings.</param>
    /// <returns>A dependency-first installation order.</returns>
    private static List<(
        ThunderstorePackage Package,
        ThunderstorePackageVersion Version
    )> BuildPlan(
        ThunderstorePackage package,
        ThunderstorePackageVersion version,
        IReadOnlyList<ThunderstorePackage> catalog
    )
    {
        Dictionary<
            string,
            (ThunderstorePackage Package, ThunderstorePackageVersion Version)
        > releases = catalog
            .SelectMany(item => item.versions.Select(release => (Package: item, Version: release)))
            .ToDictionary(
                item => item.Package.full_name + "-" + item.Version.version_number,
                StringComparer.Ordinal
            );
        List<(ThunderstorePackage Package, ThunderstorePackageVersion Version)> result = new();
        HashSet<string> visiting = new(StringComparer.Ordinal);
        Dictionary<string, string> selected = new(StringComparer.Ordinal);
        VisitDependency(package, version, releases, selected, visiting, result);
        return result;
    }

    /// <summary>
    /// Adds one release after all of its exact dependencies have been resolved.
    /// </summary>
    /// <param name="package">The package being visited.</param>
    /// <param name="version">The selected release.</param>
    /// <param name="releases">Every available exact reference.</param>
    /// <param name="selected">The versions already selected for this transaction.</param>
    /// <param name="visiting">The recursion path used to detect cycles.</param>
    /// <param name="result">The dependency-first result.</param>
    private static void VisitDependency(
        ThunderstorePackage package,
        ThunderstorePackageVersion version,
        IReadOnlyDictionary<
            string,
            (ThunderstorePackage Package, ThunderstorePackageVersion Version)
        > releases,
        IDictionary<string, string> selected,
        ISet<string> visiting,
        ICollection<(ThunderstorePackage Package, ThunderstorePackageVersion Version)> result
    )
    {
        if (visiting.Contains(package.full_name))
            throw new InvalidDataException(
                "The package dependencies contain a cycle at " + package.full_name + "."
            );
        if (selected.TryGetValue(package.full_name, out string existing))
        {
            if (existing != version.version_number)
                throw new InvalidDataException(
                    "Dependencies require different versions of " + package.full_name + "."
                );
            return;
        }
        selected.Add(package.full_name, version.version_number);
        visiting.Add(package.full_name);
        foreach (string dependency in version.dependencies ?? Array.Empty<string>())
        {
            if (!releases.TryGetValue(dependency, out var next))
                throw new InvalidDataException(
                    "Required package is not available in this community: " + dependency
                );
            VisitDependency(next.Package, next.Version, releases, selected, visiting, result);
        }
        visiting.Remove(package.full_name);
        result.Add((package, version));
    }

    /// <summary>
    /// Extracts a supported native payload and records its content identities before installation.
    /// </summary>
    /// <param name="archivePath">The downloaded or locally selected archive.</param>
    /// <param name="staging">The owned package staging directory.</param>
    /// <param name="package">The verified listing, or null for a local import.</param>
    /// <param name="version">The verified release, or null for a local import.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The package receipt and staged payload root.</returns>
    private static async Task<(InstalledModPackage Receipt, string Payload)> PrepareAsync(
        string archivePath,
        string staging,
        ThunderstorePackage package,
        ThunderstorePackageVersion version,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(staging);
        string extracted = Path.Combine(staging, "archive");
        Directory.CreateDirectory(extracted);
        await Task.Run(
            () => ExtractArchiveAsync(archivePath, extracted, cancellationToken),
            cancellationToken
        );
        string manifestPath = Path.Combine(extracted, "manifest.json");
        Manifest manifest = File.Exists(manifestPath)
            ? JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath))
            : null;
        if (
            package != null
            && (manifest?.name != package.name || manifest.version_number != version.version_number)
        )
            throw new InvalidDataException(
                "The archive identity does not match the selected Thunderstore release."
            );
        string[] roots = HasDefinition(extracted)
            ? new[] { extracted }
            : new[]
            {
                extracted,
                Path.Combine(extracted, "Mods"),
                Path.Combine(extracted, "Content", "Packs"),
            }
                .Where(Directory.Exists)
                .SelectMany(Directory.EnumerateDirectories)
                .Where(HasDefinition)
                .ToArray();
        if (roots.Length == 0)
            throw new InvalidDataException(
                "The archive contains no supported pack.xml or mod.xml content."
            );
        string payload = Path.Combine(staging, "payload");
        Directory.CreateDirectory(payload);
        List<InstalledModContent> contents = new();
        foreach (string root in roots)
        {
            InstalledModContent content = ReadContent(root);
            ValidateFolderName(content.ID);
            if (contents.Any(item => item.ID == content.ID))
                throw new InvalidDataException(
                    "The archive declares the same content ID more than once: " + content.ID
                );
            content.Folder = content.ID;
            Directory.Move(root, Path.Combine(payload, content.Folder));
            contents.Add(content);
        }
        InstalledModPackage receipt = new InstalledModPackage
        {
            PackageID = package?.full_name ?? "local-" + HashText(contents[0].ID),
            Name = package?.name ?? contents[0].DisplayName,
            Author = package?.owner ?? "Local",
            Version = version?.version_number ?? manifest?.version_number ?? contents[0].Version,
            Description = version?.description ?? manifest?.description ?? string.Empty,
            Website = package?.package_url ?? manifest?.website_url ?? string.Empty,
            Dependencies = version?.dependencies ?? manifest?.dependencies ?? Array.Empty<string>(),
            Content = contents.ToArray(),
            Local = package == null,
        };
        receipt.Files = await Task.Run(
            () =>
                EnumerateFiles(payload)
                    .Select(path =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new InstalledModFile
                        {
                            Path = Path.GetRelativePath(payload, path).Replace('\\', '/'),
                            SHA256 = HashFile(path),
                        };
                    })
                    .ToArray(),
            cancellationToken
        );
        return (receipt, payload);
    }

    /// <summary>
    /// Extracts ordinary content files on a worker without retaining the rendering context.
    /// </summary>
    /// <param name="archivePath">The owned source archive.</param>
    /// <param name="extracted">The empty extraction directory.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of path-checked extraction.</returns>
    private static async Task ExtractArchiveAsync(
        string archivePath,
        string extracted,
        CancellationToken cancellationToken
    )
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrEmpty(name))
                    continue;
                string[] parts = name.TrimEnd('/').Split('/');
                if (
                    parts.Any(part =>
                        string.IsNullOrEmpty(part) || part is "." or ".." || part.Contains(':')
                    )
                    || name.StartsWith("/", StringComparison.Ordinal)
                    || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
                )
                    throw new InvalidDataException("The archive contains an unsafe path: " + name);
                string target = Path.GetFullPath(Path.Combine(extracted, name));
                if (
                    !target.StartsWith(
                        extracted + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase
                    ) || !names.Add(name.TrimEnd('/'))
                )
                    throw new InvalidDataException(
                        "The archive contains overlapping or invalid paths: " + name
                    );
                if (name.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                if (
                    name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(name) == ".DS_Store"
                )
                    continue;
                string extension = Path.GetExtension(name).ToLowerInvariant();
                if (
                    extension
                    is ".cs"
                        or ".dll"
                        or ".exe"
                        or ".dylib"
                        or ".so"
                        or ".asmdef"
                        or ".asmref"
                        or ".rsp"
                        or ".sh"
                        or ".bat"
                        or ".ps1"
                )
                    throw new InvalidDataException(
                        "This content manager does not install executable code: " + name
                    );
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using Stream input = entry.Open();
                using FileStream output = new FileStream(
                    target,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true
                );
                await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }
    }

    /// <summary>
    /// Moves prepared folders into place and rolls back the entire operation if publication fails.
    /// </summary>
    /// <param name="registry">The prior ownership registry.</param>
    /// <param name="prepared">The validated package payloads.</param>
    /// <param name="protectedIDs">Content currently in use.</param>
    /// <param name="transaction">The transaction's owned recovery directory.</param>
    /// <param name="progress">The user-facing status callback.</param>
    /// <param name="cancellationToken">The requested cancellation before publication.</param>
    /// <returns>The completion of the committed installation.</returns>
    private async Task CommitAsync(
        Registry registry,
        IEnumerable<(InstalledModPackage Receipt, string Payload)> prepared,
        IReadOnlyCollection<string> protectedIDs,
        string transaction,
        Action<string, float> progress,
        CancellationToken cancellationToken
    )
    {
        var packages = prepared.ToArray();
        HashSet<string> folders = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in packages)
        {
            InstalledModPackage previous = registry.Packages.SingleOrDefault(package =>
                package.PackageID == item.Receipt.PackageID
            );
            foreach (
                InstalledModContent content in item.Receipt.Content.Concat(
                    previous?.Content ?? Array.Empty<InstalledModContent>()
                )
            )
                EnsureNotProtected(content, protectedIDs);
            if (
                previous != null
                && await Task.Run(() => HasLocalEdits(previous), cancellationToken)
            )
                throw new InvalidOperationException(
                    previous.Name
                        + " has local edits. Make a working copy or uninstall it before replacing this package."
                );
            foreach (InstalledModContent content in item.Receipt.Content)
            {
                if (!folders.Add(content.Folder))
                    throw new InvalidDataException(
                        "Two packages install to the same content folder: " + content.Folder
                    );
                if (
                    GetInstalled()
                        .Where(existing => existing.PackageID != item.Receipt.PackageID)
                        .SelectMany(existing => existing.Content)
                        .Any(existing => existing.ID == content.ID)
                    || Directory.Exists(Path.Combine(_contentRoot, "Packs"))
                        && Directory
                            .EnumerateDirectories(Path.Combine(_contentRoot, "Packs"))
                            .Where(HasDefinition)
                            .Any(path => ReadContent(path).ID == content.ID)
                )
                    throw new InvalidOperationException(
                        "Another installed content folder already declares "
                            + content.ID
                            + ". It will not be overwritten."
                    );
                string destination = ResolveFolder(content.Folder);
                if (
                    Directory.Exists(destination)
                    && previous?.Content.Any(old =>
                        old.Folder.Equals(content.Folder, StringComparison.OrdinalIgnoreCase)
                    ) != true
                )
                    throw new InvalidOperationException(
                        "A different or locally authored mod already uses "
                            + content.Folder
                            + ". It will not be overwritten."
                    );
            }
        }
        HashSet<string> replaced = new(
            packages.Select(item => item.Receipt.PackageID),
            StringComparer.Ordinal
        );
        Registry replacement = new Registry
        {
            Packages = registry
                .Packages.Where(item => !replaced.Contains(item.PackageID))
                .Concat(packages.Select(item => item.Receipt))
                .ToArray(),
        };
        foreach (InstalledModPackage package in replacement.Packages)
        foreach (string dependency in package.Dependencies)
            if (
                !replacement.Packages.Any(item => item.PackageID + "-" + item.Version == dependency)
            )
                throw new InvalidOperationException(
                    "The selected versions would break a dependency required by "
                        + package.Name
                        + ": "
                        + dependency
                );
        List<(string Folder, string Source)> changes = new();
        foreach (var item in packages)
        {
            InstalledModPackage previous = registry.Packages.SingleOrDefault(package =>
                package.PackageID == item.Receipt.PackageID
            );
            foreach (
                string folder in (previous?.Content ?? Array.Empty<InstalledModContent>())
                    .Concat(item.Receipt.Content)
                    .Select(content => content.Folder)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            )
            {
                string source = Path.Combine(item.Payload, folder);
                changes.Add((folder, Directory.Exists(source) ? source : null));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke("Installing prepared packages", 1);
        PublishFolders(replacement, changes, transaction, false);
    }

    /// <summary>
    /// Publishes a prepared folder set using the same recoverable transaction for installs and removals.
    /// </summary>
    /// <param name="replacement">The complete resulting ownership registry.</param>
    /// <param name="folders">Each affected folder and its prepared replacement, or null for removal.</param>
    /// <param name="transaction">The transaction's owned staging directory.</param>
    /// <param name="retainPrevious">Whether removed content must remain available for manual recovery.</param>
    private void PublishFolders(
        Registry replacement,
        IEnumerable<(string Folder, string Source)> folders,
        string transaction,
        bool retainPrevious
    )
    {
        var changes = folders.ToArray();
        Directory.CreateDirectory(_modsRoot);
        Directory.CreateDirectory(Path.Combine(transaction, "previous"));
        InstallJournal journal = new InstallJournal
        {
            PreviousRegistry = File.Exists(RegistryPath)
                ? AtomicTextFile.ReadAllText(RegistryPath)
                : string.Empty,
            NextRegistry = JsonUtility.ToJson(replacement, true),
            RetainPrevious = retainPrevious,
            Changes = changes
                .Select(change =>
                {
                    string destination = ResolveFolder(change.Folder);
                    EnsureNoLinks(destination);
                    return new FolderChange
                    {
                        Folder = change.Folder,
                        Backup = HashText(change.Folder),
                        HadPrevious = Directory.Exists(destination),
                    };
                })
                .ToArray(),
        };
        AtomicTextFile.WriteAllText(
            Path.Combine(transaction, "pending.json"),
            JsonUtility.ToJson(journal, true)
        );
        try
        {
            for (int i = 0; i < changes.Length; i++)
            {
                string destination = ResolveFolder(changes[i].Folder);
                if (journal.Changes[i].HadPrevious)
                    Directory.Move(
                        destination,
                        Path.Combine(transaction, "previous", journal.Changes[i].Backup)
                    );
                if (changes[i].Source != null)
                    Directory.Move(changes[i].Source, destination);
            }
            WriteRegistry(replacement);
        }
        catch
        {
            RecoverTransaction(transaction);
            throw;
        }
        RecoverTransaction(transaction);
    }

    /// <summary>
    /// Completes publication cleanup or restores pre-install folders after an interrupted operation.
    /// </summary>
    /// <param name="transaction">The owned transaction directory.</param>
    private void RecoverTransaction(string transaction)
    {
        string journalPath = Path.Combine(transaction, "pending.json");
        if (!File.Exists(journalPath))
            return;
        InstallJournal journal = JsonUtility.FromJson<InstallJournal>(
            AtomicTextFile.ReadAllText(journalPath)
        );
        if (
            journal?.Changes == null
            || journal.NextRegistry == null
            || journal.PreviousRegistry == null
        )
            throw new InvalidDataException(
                "An interrupted mod installation needs recovery: " + transaction
            );
        string current = File.Exists(RegistryPath)
            ? AtomicTextFile.ReadAllText(RegistryPath)
            : string.Empty;
        if (current != journal.NextRegistry)
        {
            if (current != journal.PreviousRegistry)
                throw new InvalidDataException(
                    "The installation records changed during an interrupted transaction. Retained files: "
                        + transaction
                );
            foreach (FolderChange change in journal.Changes.AsEnumerable().Reverse())
            {
                if (
                    change == null
                    || !Regex.IsMatch(change.Backup ?? string.Empty, "^[A-F0-9]{64}$")
                )
                    throw new InvalidDataException(
                        "An interrupted installation contains an invalid recovery path."
                    );
                string destination = ResolveFolder(change.Folder);
                string backup = Path.Combine(transaction, "previous", change.Backup);
                EnsureNoLinks(destination);
                EnsureNoLinks(backup);
                if (change.HadPrevious && !Directory.Exists(backup))
                    continue;
                if (Directory.Exists(destination))
                {
                    string recovery = Path.Combine(
                        _stateRoot,
                        "removed",
                        Guid.NewGuid().ToString("N")
                    );
                    Directory.CreateDirectory(recovery);
                    Directory.Move(destination, Path.Combine(recovery, change.Folder));
                }
                if (change.HadPrevious)
                    Directory.Move(backup, destination);
            }
        }
        else if (journal.RetainPrevious)
        {
            foreach (FolderChange change in journal.Changes)
            {
                ValidateFolderName(change.Folder);
                if (!Regex.IsMatch(change.Backup ?? string.Empty, "^[A-F0-9]{64}$"))
                    throw new InvalidDataException(
                        "An interrupted removal contains an invalid recovery path."
                    );
                string backup = Path.Combine(transaction, "previous", change.Backup);
                if (!Directory.Exists(backup))
                    continue;
                EnsureNoLinks(backup);
                string recovery = Path.Combine(
                    _stateRoot,
                    "removed",
                    Path.GetFileName(transaction)
                );
                Directory.CreateDirectory(recovery);
                Directory.Move(backup, Path.Combine(recovery, change.Folder));
            }
        }
        File.Delete(journalPath);
    }

    /// <summary>
    /// Removes disposable staging while retaining any unfinished recovery journal and its backups.
    /// </summary>
    /// <param name="transaction">The owned transaction directory.</param>
    private static void CleanTransaction(string transaction)
    {
        if (
            !File.Exists(Path.Combine(transaction, "pending.json")) && Directory.Exists(transaction)
        )
            Directory.Delete(transaction, true);
    }

    /// <summary>
    /// Detects changes to managed files while ignoring Unity metadata and platform desktop metadata.
    /// </summary>
    /// <param name="package">The prior package ownership receipt.</param>
    /// <returns>Whether installed files differ from their recorded release.</returns>
    private bool HasLocalEdits(InstalledModPackage package)
    {
        HashSet<string> actual = new(StringComparer.OrdinalIgnoreCase);
        foreach (InstalledModContent content in package.Content)
        {
            string folder = ResolveFolder(content.Folder);
            if (!Directory.Exists(folder))
                continue;
            foreach (string path in EnumerateFiles(folder))
                actual.Add(Path.GetRelativePath(_modsRoot, path).Replace('\\', '/'));
        }
        if (!actual.SetEquals(package.Files.Select(file => file.Path)))
            return actual.Count > 0;
        return package.Files.Any(file =>
            File.Exists(Path.Combine(_modsRoot, file.Path))
            && HashFile(Path.Combine(_modsRoot, file.Path)) != file.SHA256
        );
    }

    /// <summary>
    /// Reads one supported native definition without converting its identity or version.
    /// </summary>
    /// <param name="folder">The extracted or installed content directory.</param>
    /// <returns>The native content identity and its requirements.</returns>
    private static InstalledModContent ReadContent(string folder)
    {
        bool packExists = File.Exists(Path.Combine(folder, "pack.xml"));
        bool modExists = File.Exists(Path.Combine(folder, "mod.xml"));
        if (packExists == modExists)
            throw new InvalidDataException(
                "Each content folder must contain exactly one pack.xml or mod.xml definition."
            );
        InstalledModContent content;
        if (packExists)
        {
            ContentPackDefinition pack =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(folder, "pack.xml")
                );
            content = new InstalledModContent
            {
                ID = pack.ID,
                Version = pack.Version,
                DisplayName = pack.DisplayName,
                BasePackID = pack.BasePackID,
                IsPack = true,
            };
        }
        else
        {
            ContentModDefinition mod = ContentModDefinitionReader.Read(
                Path.Combine(folder, "mod.xml")
            );
            content = new InstalledModContent
            {
                ID = mod.ID,
                Version = mod.Version,
                DisplayName = mod.DisplayName,
                BasePackID = mod.BasePackID,
                BasePackVersion = mod.BasePackVersion,
            };
            if (
                string.IsNullOrWhiteSpace(content.BasePackID)
                || string.IsNullOrWhiteSpace(content.BasePackVersion)
            )
                throw new InvalidDataException(
                    "Add-on mods must declare their required base pack and version."
                );
        }
        if (
            string.IsNullOrWhiteSpace(content.ID)
            || string.IsNullOrWhiteSpace(content.Version)
            || string.IsNullOrWhiteSpace(content.DisplayName)
        )
            throw new InvalidDataException(
                "Content definitions must declare an ID, version, and display name."
            );
        return content;
    }

    /// <summary>
    /// Tests whether a directory contains a native content definition.
    /// </summary>
    /// <param name="folder">The candidate directory.</param>
    /// <returns>Whether a supported definition exists.</returns>
    private static bool HasDefinition(string folder) =>
        File.Exists(Path.Combine(folder, "pack.xml"))
        || File.Exists(Path.Combine(folder, "mod.xml"));

    /// <summary>
    /// Enumerates ordinary content files without following symbolic links.
    /// </summary>
    /// <param name="folder">The managed content directory.</param>
    /// <returns>The ordinary files below it.</returns>
    private static IEnumerable<string> EnumerateFiles(string folder)
    {
        EnsureNoLinks(folder);
        foreach (string path in Directory.EnumerateFileSystemEntries(folder))
        {
            EnsureNoLinks(path);
            if (Directory.Exists(path))
            {
                foreach (string child in EnumerateFiles(path))
                    yield return child;
            }
            else if (
                !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(path) != ".DS_Store"
            )
                yield return path;
        }
    }

    /// <summary>
    /// Rejects linked paths before reading, moving, or removing managed content.
    /// </summary>
    /// <param name="path">The managed path.</param>
    private static void EnsureNoLinks(string path)
    {
        if (
            (File.Exists(path) || Directory.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
        )
            throw new IOException(
                "Linked content paths cannot be replaced by the mod manager: " + path
            );
    }

    /// <summary>
    /// Resolves one receipt-owned folder without allowing access outside Mods.
    /// </summary>
    /// <param name="folder">The direct child folder name.</param>
    /// <returns>The absolute content folder.</returns>
    private string ResolveFolder(string folder)
    {
        ValidateFolderName(folder);
        return Path.Combine(_modsRoot, folder);
    }

    /// <summary>
    /// Prevents filesystem operations on content whose assets are currently in use.
    /// </summary>
    /// <param name="content">The native identity being changed.</param>
    /// <param name="protectedIDs">The caller's in-use content identities.</param>
    private static void EnsureNotProtected(
        InstalledModContent content,
        IReadOnlyCollection<string> protectedIDs
    )
    {
        if (protectedIDs?.Contains(content.ID) == true)
            throw new InvalidOperationException(
                content.DisplayName
                    + " is in use. Switch away from that pack or disable the mod and apply the change before replacing its files."
            );
    }

    /// <summary>
    /// Reads the ownership registry without treating damaged metadata as an empty installation.
    /// </summary>
    /// <returns>The recorded packages.</returns>
    private Registry ReadRegistry()
    {
        if (!File.Exists(RegistryPath))
            return new Registry();
        Registry registry = JsonUtility.FromJson<Registry>(
            AtomicTextFile.ReadAllText(RegistryPath)
        );
        if (
            registry?.Packages == null
            || registry.Packages.Any(package =>
                package?.Content == null || package.Files == null || package.Dependencies == null
            )
        )
            throw new InvalidDataException(
                "The mod manager's installation records could not be read."
            );
        if (
            registry.Packages.Any(package => string.IsNullOrEmpty(package.PackageID))
            || registry
                .Packages.Select(package => package.PackageID)
                .Distinct(StringComparer.Ordinal)
                .Count() != registry.Packages.Length
        )
            throw new InvalidDataException(
                "The mod manager's package records contain duplicate or missing identities."
            );
        HashSet<string> ownedFolders = new(StringComparer.OrdinalIgnoreCase);
        foreach (InstalledModPackage package in registry.Packages)
        {
            foreach (InstalledModContent content in package.Content)
            {
                if (content == null || string.IsNullOrWhiteSpace(content.ID))
                    throw new InvalidDataException(
                        "The mod manager's content records contain a missing identity."
                    );
                ValidateFolderName(content.Folder);
                if (!ownedFolders.Add(content.Folder))
                    throw new InvalidDataException(
                        "The mod manager's content records contain overlapping folders."
                    );
            }
            foreach (InstalledModFile file in package.Files)
                if (
                    file == null
                    || string.IsNullOrEmpty(file.Path)
                    || !package.Content.Any(content =>
                        file.Path.StartsWith(content.Folder + "/", StringComparison.Ordinal)
                    )
                    || !Regex.IsMatch(file.SHA256 ?? string.Empty, "^[A-F0-9]{64}$")
                )
                    throw new InvalidDataException(
                        "The mod manager's file records do not match their owning package."
                    );
        }
        foreach (InstalledModFile file in registry.Packages.SelectMany(package => package.Files))
            if (
                string.IsNullOrEmpty(file.Path)
                || file.Path.Split('/')
                    .Any(part => part is ".." or "." || string.IsNullOrEmpty(part))
                || file.Path.Contains('\\')
                || file.Path.Contains(':')
                || Path.IsPathRooted(file.Path)
            )
                throw new InvalidDataException(
                    "The mod manager's file records contain an invalid path."
                );
        return registry;
    }

    /// <summary>
    /// Atomically publishes the complete ownership registry.
    /// </summary>
    /// <param name="registry">The successfully installed package set.</param>
    private void WriteRegistry(Registry registry) =>
        AtomicTextFile.WriteAllText(RegistryPath, JsonUtility.ToJson(registry, true));

    /// <summary>
    /// Acquires the installation lease shared by game and editor processes.
    /// </summary>
    /// <returns>The lease released when its stream is disposed.</returns>
    private FileStream AcquireLease()
    {
        EnsureNoLinks(_modsRoot);
        EnsureNoLinks(_stateRoot);
        Directory.CreateDirectory(_stateRoot);
        FileStream lease;
        try
        {
            lease = new FileStream(
                Path.Combine(_stateRoot, "install.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None
            );
        }
        catch (IOException exception)
        {
            throw new IOException(
                "Another mod installation is in progress. Wait for it to finish and retry.",
                exception
            );
        }
        try
        {
            string transactions = Path.Combine(_stateRoot, "transactions");
            if (Directory.Exists(transactions))
                foreach (string transaction in Directory.EnumerateDirectories(transactions))
                {
                    EnsureNoLinks(transaction);
                    RecoverTransaction(transaction);
                    CleanTransaction(transaction);
                }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a transaction directory owned exclusively by this operation.
    /// </summary>
    /// <returns>The created staging and recovery directory.</returns>
    private string CreateTransaction()
    {
        string path = Path.Combine(_stateRoot, "transactions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Returns the stable archive-cache path for one exact package release.
    /// </summary>
    /// <param name="packageID">The package identity.</param>
    /// <param name="version">The distribution version.</param>
    /// <returns>The cached archive path.</returns>
    private string ArchivePath(string packageID, string version) =>
        Path.Combine(_stateRoot, "archives", HashText(packageID + "\0" + version) + ".zip");

    /// <summary>
    /// Validates a public package path segment before accepting catalog data.
    /// </summary>
    /// <param name="value">The author or package segment.</param>
    /// <param name="label">The field being checked.</param>
    public static void ValidateSegment(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"))
            throw new InvalidDataException("The listing contains an invalid " + label + ".");
    }

    /// <summary>
    /// Checks the numeric distribution version used by Thunderstore.
    /// </summary>
    /// <param name="version">The distribution release version.</param>
    public static void ValidateVersion(string version)
    {
        if (
            !System.Version.TryParse(version, out System.Version parsed)
            || parsed.Build < 0
            || parsed.Revision >= 0
        )
            throw new InvalidDataException("The listing contains an invalid package version.");
    }

    /// <summary>
    /// Validates a direct, portable Mods child directory name.
    /// </summary>
    /// <param name="name">The content folder name.</param>
    private static void ValidateFolderName(string name)
    {
        if (
            string.IsNullOrWhiteSpace(name)
            || name is "." or ".."
            || name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
        )
            throw new InvalidDataException("Content has an invalid installation folder name.");
    }

    /// <summary>
    /// Computes a stable file checksum for managed-file ownership checks.
    /// </summary>
    /// <param name="path">The ordinary file path.</param>
    /// <returns>The uppercase SHA-256 digest.</returns>
    private static string HashFile(string path)
    {
        using SHA256 hash = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
    }

    /// <summary>
    /// Produces a filesystem-safe cache key without changing the displayed package identity.
    /// </summary>
    /// <param name="value">The exact identity to key.</param>
    /// <returns>The stable SHA-256 cache key.</returns>
    private static string HashText(string value)
    {
        using SHA256 hash = SHA256.Create();
        return BitConverter
            .ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value)))
            .Replace("-", string.Empty);
    }

    /// <summary>
    /// Retains enough filesystem intent to recover an interrupted installation.
    /// </summary>
    [Serializable]
    private sealed class InstallJournal
    {
        public string PreviousRegistry;
        public string NextRegistry;
        public bool RetainPrevious;
        public FolderChange[] Changes;
    }

    /// <summary>
    /// Describes one owned folder replacement without storing a path outside the manager's roots.
    /// </summary>
    [Serializable]
    private sealed class FolderChange
    {
        public string Folder;
        public string Backup;
        public bool HadPrevious;
    }

    /// <summary>
    /// Stores installed distribution metadata separately from authored game definitions.
    /// </summary>
    [Serializable]
    private sealed class Registry
    {
        public InstalledModPackage[] Packages = Array.Empty<InstalledModPackage>();
    }

    /// <summary>
    /// Reads standard package identity when inspecting an archive.
    /// </summary>
    [Serializable]
    private sealed class Manifest
    {
        public string name;
        public string version_number;
        public string description;
        public string website_url;
        public string[] dependencies;
    }
}
