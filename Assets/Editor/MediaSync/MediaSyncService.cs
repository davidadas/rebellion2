using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rebellion.Editor.Media
{
    /// <summary>
    /// Describes the local media repository state presented by the sync window.
    /// </summary>
    public sealed class MediaRepositoryInfo
    {
        public MediaRepositoryInfo(
            string rootPath,
            string currentBranch,
            bool isDirty,
            string[] branches
        )
        {
            RootPath = rootPath;
            CurrentBranch = currentBranch;
            IsDirty = isDirty;
            Branches = branches;
        }

        public string RootPath { get; }

        public string CurrentBranch { get; }

        public bool IsDirty { get; }

        public string[] Branches { get; }
    }

    /// <summary>
    /// Counts filesystem changes made by one media synchronization.
    /// </summary>
    public sealed class MediaSyncResult
    {
        public int CopiedFiles { get; internal set; }

        public int UnchangedFiles { get; internal set; }

        public int DeletedFiles { get; internal set; }

        internal void Add(MediaSyncResult other)
        {
            CopiedFiles += other.CopiedFiles;
            UnchangedFiles += other.UnchangedFiles;
            DeletedFiles += other.DeletedFiles;
        }
    }

    /// <summary>
    /// Inspects a sibling media checkout and mirrors its development assets into the Unity project.
    /// </summary>
    public static class MediaSyncService
    {
        private const string _mediaRepositoryName = "rebellion2-media";
        private const string _shortMediaRepositoryName = "reb2-media";
        private const string _lfsPointerHeader = "version https://git-lfs.github.com/spec/v1";

        private static readonly StringComparison _pathComparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        private static readonly StringComparer _pathComparer =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        /// <summary>
        /// Finds the conventional media checkout beside the Unity project.
        /// </summary>
        public static string FindDefaultMediaRoot(string projectRoot)
        {
            string absoluteProjectRoot = Path.GetFullPath(
                projectRoot ?? throw new ArgumentNullException(nameof(projectRoot))
            );
            DirectoryInfo parent = Directory.GetParent(absoluteProjectRoot);
            if (parent == null)
                return string.Empty;

            foreach (
                string directoryName in new[] { _mediaRepositoryName, _shortMediaRepositoryName }
            )
            {
                string candidate = Path.Combine(parent.FullName, directoryName);
                if (IsMediaRepository(candidate))
                    return candidate;
            }

            return parent
                    .EnumerateDirectories()
                    .Where(directory =>
                        directory.Name.Contains("media", StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(directory => directory.FullName)
                    .FirstOrDefault(IsMediaRepository)
                ?? Path.Combine(parent.FullName, _mediaRepositoryName);
        }

        /// <summary>
        /// Reads the current branch, worktree state, and local branches from a media checkout.
        /// </summary>
        public static MediaRepositoryInfo InspectRepository(string mediaRoot)
        {
            string absoluteMediaRoot = ValidateMediaRoot(mediaRoot);
            string currentBranch = RunGit(absoluteMediaRoot, "branch", "--show-current").Trim();
            bool isDirty = !string.IsNullOrWhiteSpace(
                RunGit(absoluteMediaRoot, "status", "--porcelain")
            );
            string[] branches = RunGit(
                    absoluteMediaRoot,
                    "for-each-ref",
                    "--format=%(refname:short)",
                    "refs/heads"
                )
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(branch => branch, StringComparer.Ordinal)
                .ToArray();

            return new MediaRepositoryInfo(absoluteMediaRoot, currentBranch, isDirty, branches);
        }

        /// <summary>
        /// Switches a clean media checkout to an existing local branch.
        /// </summary>
        public static void SwitchBranch(MediaRepositoryInfo repository, string branch)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentException("A media branch is required.", nameof(branch));
            if (string.Equals(repository.CurrentBranch, branch, StringComparison.Ordinal))
                return;
            if (repository.IsDirty)
            {
                throw new InvalidOperationException(
                    "The media checkout has uncommitted changes and cannot switch branches."
                );
            }
            if (!repository.Branches.Contains(branch, StringComparer.Ordinal))
                throw new InvalidOperationException($"Local media branch not found: {branch}");

            RunGit(repository.RootPath, "switch", "--", branch);
        }

        /// <summary>
        /// Mirrors runtime content and Main Menu models into the Unity project.
        /// </summary>
        public static MediaSyncResult Synchronize(
            string mediaRoot,
            string projectRoot,
            Action<string, float> reportProgress = null
        )
        {
            string absoluteMediaRoot = ValidateMediaRoot(mediaRoot);
            string absoluteProjectRoot = Path.GetFullPath(
                projectRoot ?? throw new ArgumentNullException(nameof(projectRoot))
            );
            string sourceContent = Path.Combine(absoluteMediaRoot, "Content");
            string sourceModels = Path.Combine(absoluteMediaRoot, "Models", "MainMenu");

            string[] contentFiles = GetSourceFiles(sourceContent);
            string[] modelFiles = GetSourceFiles(sourceModels);

            MediaSyncResult result = new MediaSyncResult();
            result.Add(
                MirrorDirectory(
                    sourceContent,
                    Path.Combine(absoluteProjectRoot, "Assets", "Content"),
                    contentFiles,
                    reportProgress
                )
            );
            result.Add(
                MirrorDirectory(
                    sourceModels,
                    Path.Combine(absoluteProjectRoot, "Assets", "Art", "Models", "MainMenu"),
                    modelFiles,
                    reportProgress
                )
            );

            reportProgress?.Invoke("Refreshing synchronized media", 1f);
            return result;
        }

        /// <summary>
        /// Mirrors one directory while retaining metadata for assets that still exist.
        /// </summary>
        public static MediaSyncResult MirrorDirectory(
            string sourceRoot,
            string destinationRoot,
            Action<string, float> reportProgress = null
        )
        {
            return MirrorDirectory(
                sourceRoot,
                destinationRoot,
                GetSourceFiles(sourceRoot),
                reportProgress
            );
        }

        private static MediaSyncResult MirrorDirectory(
            string sourceRoot,
            string destinationRoot,
            IReadOnlyList<string> sourceFiles,
            Action<string, float> reportProgress
        )
        {
            string absoluteSourceRoot = Path.GetFullPath(sourceRoot);
            string absoluteDestinationRoot = Path.GetFullPath(destinationRoot);
            EnsureNonOverlappingPaths(absoluteSourceRoot, absoluteDestinationRoot);
            Directory.CreateDirectory(absoluteDestinationRoot);

            HashSet<string> sourceRelativeFiles = new HashSet<string>(_pathComparer);
            HashSet<string> sourceRelativeDirectories = Directory
                .EnumerateDirectories(absoluteSourceRoot, "*", SearchOption.AllDirectories)
                .Select(path => NormalizeRelativePath(absoluteSourceRoot, path))
                .ToHashSet(_pathComparer);
            MediaSyncResult result = new MediaSyncResult();

            for (int index = 0; index < sourceFiles.Count; index++)
            {
                string sourcePath = sourceFiles[index];
                string relativePath = NormalizeRelativePath(absoluteSourceRoot, sourcePath);
                sourceRelativeFiles.Add(relativePath);
                string destinationPath = Path.Combine(
                    absoluteDestinationRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)
                );
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                reportProgress?.Invoke(
                    $"Synchronizing {relativePath}",
                    sourceFiles.Count == 0 ? 1f : (float)index / sourceFiles.Count
                );
                if (CopyIfChanged(sourcePath, destinationPath))
                    result.CopiedFiles++;
                else
                    result.UnchangedFiles++;
            }

            foreach (
                string destinationPath in Directory
                    .EnumerateFiles(absoluteDestinationRoot, "*", SearchOption.AllDirectories)
                    .ToArray()
            )
            {
                string relativePath = NormalizeRelativePath(
                    absoluteDestinationRoot,
                    destinationPath
                );
                if (
                    string.Equals(
                        Path.GetFileName(destinationPath),
                        ".DS_Store",
                        StringComparison.Ordinal
                    )
                )
                {
                    DeleteFile(destinationPath);
                    result.DeletedFiles++;
                    continue;
                }

                if (relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    string assetRelativePath = relativePath[..^".meta".Length];
                    if (
                        sourceRelativeFiles.Contains(assetRelativePath)
                        || sourceRelativeDirectories.Contains(assetRelativePath)
                    )
                    {
                        continue;
                    }
                }
                else if (sourceRelativeFiles.Contains(relativePath))
                {
                    continue;
                }

                DeleteFile(destinationPath);
                result.DeletedFiles++;
            }

            foreach (
                string directory in Directory
                    .EnumerateDirectories(absoluteDestinationRoot, "*", SearchOption.AllDirectories)
                    .OrderByDescending(path => path.Length)
            )
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }

            return result;
        }

        private static string ValidateMediaRoot(string mediaRoot)
        {
            if (string.IsNullOrWhiteSpace(mediaRoot))
                throw new ArgumentException(
                    "A media repository path is required.",
                    nameof(mediaRoot)
                );

            string absoluteMediaRoot = Path.GetFullPath(mediaRoot);
            if (!IsMediaRepository(absoluteMediaRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Media repository not found or incomplete: {absoluteMediaRoot}"
                );
            }

            string schemaPath = Path.Combine(
                absoluteMediaRoot,
                "Content",
                "Application",
                "Schemas",
                "game-config.xsd"
            );
            if (!File.Exists(schemaPath))
                throw new FileNotFoundException("Media content schema not found.", schemaPath);

            return absoluteMediaRoot;
        }

        private static bool IsMediaRepository(string path)
        {
            return Directory.Exists(path)
                && (
                    Directory.Exists(Path.Combine(path, ".git"))
                    || File.Exists(Path.Combine(path, ".git"))
                )
                && Directory.Exists(Path.Combine(path, "Content"))
                && Directory.Exists(Path.Combine(path, "Models", "MainMenu"));
        }

        private static string[] GetSourceFiles(string sourceRoot)
        {
            string absoluteSourceRoot = Path.GetFullPath(sourceRoot);
            if (!Directory.Exists(absoluteSourceRoot))
                throw new DirectoryNotFoundException(absoluteSourceRoot);

            string[] sourceFiles = Directory
                .EnumerateFiles(absoluteSourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => !IsExcludedSourceFile(path))
                .OrderBy(path => path, _pathComparer)
                .ToArray();
            foreach (string sourceFile in sourceFiles)
                ThrowIfLfsPointer(sourceFile);
            return sourceFiles;
        }

        private static bool IsExcludedSourceFile(string path)
        {
            return path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(path), ".DS_Store", StringComparison.Ordinal);
        }

        private static void ThrowIfLfsPointer(string path)
        {
            byte[] expected = Encoding.ASCII.GetBytes(_lfsPointerHeader);
            using FileStream stream = File.OpenRead(path);
            if (stream.Length < expected.Length)
                return;

            byte[] actual = new byte[expected.Length];
            if (stream.Read(actual, 0, actual.Length) != actual.Length)
                return;
            if (actual.SequenceEqual(expected))
            {
                throw new InvalidDataException(
                    $"Git LFS content has not been downloaded: {path}\nRun git lfs pull in the media repository and try again."
                );
            }
        }

        private static bool CopyIfChanged(string sourcePath, string destinationPath)
        {
            FileInfo source = new FileInfo(sourcePath);
            FileInfo destination = new FileInfo(destinationPath);
            if (
                destination.Exists
                && destination.Length == source.Length
                && destination.LastWriteTimeUtc == source.LastWriteTimeUtc
            )
            {
                return false;
            }

            if (destination.Exists && destination.IsReadOnly)
                destination.IsReadOnly = false;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
            File.SetLastWriteTimeUtc(destinationPath, source.LastWriteTimeUtc);
            return true;
        }

        private static void DeleteFile(string path)
        {
            FileInfo file = new FileInfo(path);
            if (file.IsReadOnly)
                file.IsReadOnly = false;
            file.Delete();
        }

        private static void EnsureNonOverlappingPaths(string sourceRoot, string destinationRoot)
        {
            string sourcePrefix =
                sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destinationPrefix =
                destinationRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (
                string.Equals(sourceRoot, destinationRoot, _pathComparison)
                || sourcePrefix.StartsWith(destinationPrefix, _pathComparison)
                || destinationPrefix.StartsWith(sourcePrefix, _pathComparison)
            )
            {
                throw new InvalidOperationException(
                    "Media source and destination directories must not overlap."
                );
            }
        }

        private static string NormalizeRelativePath(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }

        private static string RunGit(string workingDirectory, params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            try
            {
                using Process process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("Could not start Git.");

                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(output, error);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Git command failed ({process.ExitCode}): {error.Result.Trim()}"
                    );
                }

                return output.Result;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Git was not found. Install Git or add it to PATH.",
                    ex
                );
            }
        }
    }
}
