using System;
using System.Collections.Generic;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Tests.EditMode.Fakes
{
    /// <summary>
    /// In-memory <see cref="IFileSystem"/> that can be told to fail on demand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The save system's value lies almost entirely in what it does when things go wrong, and those
    /// paths cannot be exercised against a real disk: a test cannot reliably fill a volume, cannot
    /// force a rename to fail, and cannot kill the process midway through a write. This fake makes
    /// each of those a single line of test setup.
    /// </para>
    /// <para>
    /// It also keeps the suite fast and hermetic. No test touches the developer's real save file,
    /// and the tests leave nothing behind on disk.
    /// </para>
    /// </remarks>
    public sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> directories = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Paths whose next write must fail, simulating a full or unwritable volume.</summary>
        public HashSet<string> WriteFailurePaths { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Paths whose reads must fail, simulating an unreadable file.</summary>
        public HashSet<string> ReadFailurePaths { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Paths whose moves must fail, simulating a rename the platform refused.</summary>
        public HashSet<string> MoveFailurePaths { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Paths whose copies must fail, simulating a backup that could not be refreshed.</summary>
        public HashSet<string> CopyFailurePaths { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>When set, every attempt to create a directory fails.</summary>
        public bool DirectoryCreationFails { get; set; }

        /// <summary>Number of successful writes performed, used to assert that saves were coalesced.</summary>
        public int WriteCount { get; private set; }

        /// <summary>
        /// Seeds a file directly, bypassing the failure switches.
        /// </summary>
        /// <param name="path">Path to seed.</param>
        /// <param name="contents">Contents to store.</param>
        public void SeedFile(string path, string contents)
        {
            files[path] = contents;

            string directory = GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                directories.Add(directory);
            }
        }

        /// <summary>
        /// Reads a stored file directly, for assertions.
        /// </summary>
        /// <param name="path">Path to read.</param>
        /// <returns>The contents, or <c>null</c> if absent.</returns>
        public string PeekFile(string path) => files.TryGetValue(path, out string contents) ? contents : null;

        /// <inheritdoc />
        public bool FileExists(string path) => path != null && files.ContainsKey(path);

        /// <inheritdoc />
        public bool EnsureDirectory(string path)
        {
            if (DirectoryCreationFails || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            directories.Add(path);
            return true;
        }

        /// <inheritdoc />
        public bool TryReadAllText(string path, out string contents)
        {
            contents = null;

            if (path == null || ReadFailurePaths.Contains(path))
            {
                return false;
            }

            return files.TryGetValue(path, out contents);
        }

        /// <inheritdoc />
        public bool TryWriteAllText(string path, string contents)
        {
            if (path == null || contents == null || WriteFailurePaths.Contains(path))
            {
                return false;
            }

            files[path] = contents;
            WriteCount++;
            return true;
        }

        /// <inheritdoc />
        public bool TryCopy(string sourcePath, string destinationPath)
        {
            if (sourcePath == null || destinationPath == null || CopyFailurePaths.Contains(sourcePath))
            {
                return false;
            }

            if (!files.TryGetValue(sourcePath, out string contents))
            {
                return false;
            }

            files[destinationPath] = contents;
            return true;
        }

        /// <inheritdoc />
        public bool TryMove(string sourcePath, string destinationPath)
        {
            if (sourcePath == null || destinationPath == null || MoveFailurePaths.Contains(sourcePath))
            {
                return false;
            }

            if (!files.TryGetValue(sourcePath, out string contents))
            {
                return false;
            }

            files[destinationPath] = contents;
            files.Remove(sourcePath);
            return true;
        }

        /// <inheritdoc />
        public bool TryDelete(string path)
        {
            if (path == null)
            {
                return false;
            }

            files.Remove(path);
            return true;
        }

        /// <inheritdoc />
        public long GetFileSize(string path)
        {
            if (path == null || !files.TryGetValue(path, out string contents))
            {
                return -1;
            }

            return contents?.Length ?? 0;
        }

        /// <summary>
        /// Extracts a directory path without depending on the host platform's separator, so the
        /// fake behaves identically on Windows and macOS.
        /// </summary>
        /// <param name="path">Full file path.</param>
        /// <returns>The directory portion, or an empty string if there is none.</returns>
        private static string GetDirectoryName(string path)
        {
            int index = path.LastIndexOfAny(new[] { '/', '\\' });
            return index <= 0 ? string.Empty : path.Substring(0, index);
        }
    }
}
