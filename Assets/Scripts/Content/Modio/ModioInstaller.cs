using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Rebellion.Util.Serialization;

namespace Rebellion.Content.Modio
{
    /// <summary>
    /// Installs verified community archives into the content layout shared with the launcher.
    /// </summary>
    public sealed class ModioInstaller
    {
        private readonly string contentRoot;
        private readonly string modsRoot;
        private readonly long gameID;

        /// <summary>
        /// Selects the installation's content and user-mod directories.
        /// </summary>
        /// <param name="contentRoot">The installed application content directory.</param>
        /// <param name="gameID">The mod.io game identifier.</param>
        public ModioInstaller(string contentRoot, long gameID)
        {
            this.contentRoot = Path.GetFullPath(
                contentRoot ?? throw new ArgumentNullException(nameof(contentRoot))
            );
            if (gameID <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameID));
            this.gameID = gameID;
            modsRoot = ContentFileResolver.ResolveModsRootPath(contentRoot);
        }

        /// <summary>
        /// Reads a mod's shared installation record without failing browsing on damaged metadata.
        /// </summary>
        /// <param name="modID">The catalog mod identifier.</param>
        /// <returns>The matching installation record, or null when unavailable.</returns>
        public ModioInstallation GetInstalled(long modID)
        {
            try
            {
                ModioInstallation record = ModioInstallation.Read(GetDirectory(modID));
                return
                    record?.SchemaVersion == 1 && record.GameID == gameID && record.ModID == modID
                    ? record
                    : null;
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or UnauthorizedAccessException
                            or SerializationException
                )
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts and validates a package before replacing only its own managed installation.
        /// </summary>
        /// <param name="mod">The current published mod and archive identity.</param>
        /// <param name="archivePath">The downloaded and verified ZIP path.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The completion of installation.</returns>
        public async Task InstallAsync(
            ModioMod mod,
            string archivePath,
            CancellationToken cancellationToken
        )
        {
            if (
                mod == null
                || mod.GameID != gameID
                || mod.ID <= 0
                || mod.File == null
                || mod.File.ID <= 0
                || mod.File.ModID != mod.ID
            )
                throw new InvalidDataException("The downloaded mod does not belong to this game.");
            string stagingRoot = Path.Combine(
                Path.GetDirectoryName(modsRoot),
                ".modio-staging",
                Guid.NewGuid().ToString("N")
            );
            string extracted = Path.Combine(stagingRoot, "package");
            string backup = Path.Combine(stagingRoot, "previous");
            bool committed = false;
            Directory.CreateDirectory(extracted);
            try
            {
                await ExtractAsync(archivePath, extracted, cancellationToken).ConfigureAwait(false);
                string package = ResolvePackage(extracted);
                ModioInstallation record = ValidatePackage(package, mod);
                foreach (
                    string path in Directory
                        .GetFiles(package)
                        .Where(path =>
                            string.Equals(
                                Path.GetFileName(path),
                                ModioInstallation.FileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                )
                    File.Delete(path);
                record.Write(package);
                cancellationToken.ThrowIfCancellationRequested();
                Commit(package, GetDirectory(mod.ID), backup, mod.ID);
                committed = true;
            }
            finally
            {
                if (Directory.Exists(stagingRoot) && (committed || !Directory.Exists(backup)))
                    Directory.Delete(stagingRoot, true);
            }
        }

        /// <summary>
        /// Returns the deterministic folder used by both installation surfaces.
        /// </summary>
        /// <param name="modID">The positive catalog mod identifier.</param>
        /// <returns>The managed installation path.</returns>
        private string GetDirectory(long modID)
        {
            if (modID <= 0)
                throw new ArgumentOutOfRangeException(nameof(modID));
            return Path.Combine(modsRoot, "modio-" + modID.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Extracts portable regular files while rejecting traversal, links, and conflicting paths.
        /// </summary>
        /// <param name="archivePath">The verified ZIP file.</param>
        /// <param name="destination">The empty staging directory.</param>
        /// <param name="cancellationToken">The owner's cancellation signal.</param>
        /// <returns>The completion of extraction.</returns>
        private static async Task ExtractAsync(
            string archivePath,
            string destination,
            CancellationToken cancellationToken
        )
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> pathCases = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = entry.FullName.Replace('\\', '/');
                string[] segments = relative.TrimEnd('/').Split('/');
                if (
                    segments.Any(segment =>
                        string.IsNullOrWhiteSpace(segment)
                        || segment is "." or ".."
                        || segment.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|', '\0' })
                            >= 0
                        || segment.EndsWith(".", StringComparison.Ordinal)
                        || segment.EndsWith(" ", StringComparison.Ordinal)
                    )
                    || relative.StartsWith("/", StringComparison.Ordinal)
                    || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
                )
                    throw new InvalidDataException(
                        "The mod archive contains an unsafe file path or symbolic link."
                    );
                string path = Path.GetFullPath(Path.Combine(destination, Path.Combine(segments)));
                string prefix = string.Empty;
                foreach (string segment in segments)
                {
                    prefix = prefix.Length == 0 ? segment : prefix + "/" + segment;
                    if (pathCases.TryGetValue(prefix, out string previous) && previous != prefix)
                        throw new InvalidDataException(
                            "The mod archive contains paths that differ only by letter case."
                        );
                    pathCases[prefix] = prefix;
                }
                if (
                    !path.StartsWith(
                        destination + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal
                    )
                )
                    throw new InvalidDataException(
                        "The mod archive contains a path outside its package."
                    );
                if (relative.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(path);
                    continue;
                }
                if (!files.Add(relative))
                    throw new InvalidDataException("The mod archive contains duplicate files.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using Stream source = entry.Open();
                using FileStream output = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true
                );
                await source.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Accepts an exported package at the ZIP root or inside one enclosing folder.
        /// </summary>
        /// <param name="extracted">The extraction directory.</param>
        /// <returns>The directory containing exactly one supported definition.</returns>
        private static string ResolvePackage(string extracted)
        {
            if (HasDefinition(extracted))
                return extracted;
            string[] directories = Directory
                .GetDirectories(extracted)
                .Where(path => Path.GetFileName(path) != "__MACOSX")
                .ToArray();
            if (
                directories.Length == 1
                && HasDefinition(directories[0])
                && Directory.GetFiles(extracted).All(path => Path.GetFileName(path) == ".DS_Store")
            )
                return directories[0];
            throw new InvalidDataException("The ZIP must contain one pack.xml or mod.xml package.");
        }

        /// <summary>
        /// Checks whether a directory contains a supported package definition.
        /// </summary>
        /// <param name="directory">The candidate package directory.</param>
        /// <returns>True when at least one supported definition exists.</returns>
        private static bool HasDefinition(string directory) =>
            File.Exists(Path.Combine(directory, "pack.xml"))
            || File.Exists(Path.Combine(directory, "mod.xml"));

        /// <summary>
        /// Validates the package identity and its installed base-pack requirements.
        /// </summary>
        /// <param name="package">The staged package directory.</param>
        /// <param name="mod">The current catalog metadata.</param>
        /// <returns>The shared installation record.</returns>
        private ModioInstallation ValidatePackage(string package, ModioMod mod)
        {
            bool isPack = File.Exists(Path.Combine(package, "pack.xml"));
            bool isMod = File.Exists(Path.Combine(package, "mod.xml"));
            if (isPack == isMod)
                throw new InvalidDataException(
                    "The package must contain either pack.xml or mod.xml."
                );
            string id;
            string version;
            string baseID;
            string baseVersion = null;
            if (isPack)
            {
                ContentPackDefinition definition =
                    ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                        Path.Combine(package, "pack.xml")
                    );
                id = definition.ID;
                version = definition.Version;
                baseID = definition.BasePackID;
                if (string.Equals(id, baseID, StringComparison.Ordinal))
                    throw new InvalidDataException("A content pack cannot inherit from itself.");
            }
            else
            {
                GameSerializer serializer = new GameSerializer(
                    typeof(ContentModDefinition),
                    new GameSerializerSettings { RootName = nameof(ContentModDefinition) }
                );
                using FileStream stream = File.OpenRead(Path.Combine(package, "mod.xml"));
                ContentModDefinition definition = (ContentModDefinition)
                    serializer.Deserialize(stream);
                id = definition.ID;
                version = definition.Version;
                baseID = definition.BasePackID;
                baseVersion = definition.BasePackVersion;
                if (
                    string.IsNullOrWhiteSpace(definition.DisplayName)
                    || string.IsNullOrWhiteSpace(baseID)
                    || string.IsNullOrWhiteSpace(baseVersion)
                    || !Directory.Exists(Path.Combine(package, "Content"))
                )
                    throw new InvalidDataException(
                        "The mod must declare a base pack and version and contain a Content folder."
                    );
            }
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
                throw new InvalidDataException(
                    "The content definition must declare its ID and version."
                );
            string destination = GetDirectory(mod.ID);
            ModioInstallation previous = GetInstalled(mod.ID);
            if (
                previous != null
                && (
                    previous.ContentID != id
                    || !File.Exists(Path.Combine(destination, isPack ? "pack.xml" : "mod.xml"))
                )
            )
                throw new InvalidDataException(
                    "An update cannot change the installed package's content ID or package type."
                );
            if (
                !string.IsNullOrWhiteSpace(baseID)
                && !ContentPackLoader
                    .DiscoverPacks(contentRoot)
                    .Any(pack =>
                        pack.ID == baseID && (baseVersion == null || pack.Version == baseVersion)
                    )
            )
                throw new InvalidDataException(
                    $"This mod requires content pack '{baseID}'{(baseVersion == null ? string.Empty : " version " + baseVersion)}. Install that pack first."
                );
            if (isPack)
                ContentPackLoader.CreateFolderResolver(
                    contentRoot,
                    package,
                    ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                        Path.Combine(package, "pack.xml")
                    )
                );
            IEnumerable<string> parents = isPack
                ? new[] { Path.Combine(contentRoot, "Packs"), modsRoot }
                : new[] { modsRoot };
            foreach (string parent in parents.Where(Directory.Exists))
            foreach (string directory in Directory.GetDirectories(parent))
            {
                if (string.Equals(directory, destination, StringComparison.Ordinal))
                    continue;
                string name = isPack ? "pack.xml" : "mod.xml";
                string path = Path.Combine(directory, name);
                if (!File.Exists(path))
                    continue;
                try
                {
                    using XmlReader reader = XmlReader.Create(
                        path,
                        new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            XmlResolver = null,
                        }
                    );
                    XElement root = XDocument.Load(reader).Root;
                    if (
                        root?.Name == (isPack ? "ContentPack" : nameof(ContentModDefinition))
                        && root.Element("ID")?.Value == id
                    )
                        throw new InvalidDataException(
                            $"A local copy of '{id}' is already installed. Remove that copy before installing this one."
                        );
                }
                catch (XmlException) { }
            }
            return new ModioInstallation
            {
                GameID = gameID,
                ModID = mod.ID,
                FileID = mod.File.ID,
                ContentID = id,
                ContentVersion = version,
                Name = mod.Name,
            };
        }

        /// <summary>
        /// Replaces only a recognized managed installation, restoring it if replacement fails.
        /// </summary>
        /// <param name="package">The validated staged package.</param>
        /// <param name="destination">The managed mod directory.</param>
        /// <param name="backup">The temporary previous-installation directory.</param>
        /// <param name="modID">The expected catalog owner.</param>
        private void Commit(string package, string destination, string backup, long modID)
        {
            Directory.CreateDirectory(modsRoot);
            if (Directory.Exists(destination))
            {
                if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException(
                        "A linked local mod directory will not be replaced."
                    );
                if (GetInstalled(modID) == null)
                    throw new InvalidDataException(
                        "The destination contains an unrecognized local mod and will not be replaced."
                    );
                Directory.Move(destination, backup);
            }
            try
            {
                Directory.Move(package, destination);
            }
            catch
            {
                if (Directory.Exists(backup) && !Directory.Exists(destination))
                    Directory.Move(backup, destination);
                throw;
            }
        }
    }
}
