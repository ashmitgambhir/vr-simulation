using System;
using System.IO;
using System.Text;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Services
{
    /// <summary>
    /// <see cref="IFileSystem"/> backed by the real device file system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every member converts exceptions into return values. Storage on a standalone headset can
    /// fail for reasons the application cannot prevent — the volume is full, the app was denied
    /// access, the process is being torn down mid-call — and an unhandled
    /// <see cref="IOException"/> during an autosave would end the session over something entirely
    /// recoverable.
    /// </para>
    /// <para>
    /// Writes are flushed through to the storage device before returning. Without that,
    /// <see cref="File.WriteAllText(string, string)"/> can report success while the bytes are still
    /// in an operating system buffer, which on a headset that is about to sleep or run out of
    /// battery is indistinguishable from never having written at all.
    /// </para>
    /// </remarks>
    public sealed class UnityFileSystem : IFileSystem
    {
        /// <summary>
        /// UTF-8 without a byte order mark. A BOM is legal but confuses external tooling that
        /// reads the analytics export, and buys nothing here.
        /// </summary>
        private static readonly UTF8Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly IExperienceLogger logger;

        /// <summary>
        /// Creates a file system wrapper.
        /// </summary>
        /// <param name="logger">Destination for failure diagnostics. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <c>null</c>.</exception>
        public UnityFileSystem(IExperienceLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return File.Exists(path);
            }
            catch (Exception exception)
            {
                LogFailure($"Could not test for the existence of '{path}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    return true;
                }

                Directory.CreateDirectory(path);
                return Directory.Exists(path);
            }
            catch (Exception exception)
            {
                LogFailure($"Could not create the directory '{path}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryReadAllText(string path, out string contents)
        {
            contents = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                contents = File.ReadAllText(path, FileEncoding);
                return true;
            }
            catch (Exception exception)
            {
                LogFailure($"Could not read '{path}'.", exception);
                contents = null;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryWriteAllText(string path, string contents)
        {
            if (string.IsNullOrWhiteSpace(path) || contents == null)
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !EnsureDirectory(directory))
                {
                    return false;
                }

                byte[] bytes = FileEncoding.GetBytes(contents);

                // FileMode.Create truncates any existing file. The stream is flushed with
                // flushToDisk set, which pushes the bytes past the operating system cache to the
                // storage device itself before this method reports success.
                using (var stream = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: false))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                return true;
            }
            catch (Exception exception)
            {
                LogFailure($"Could not write '{path}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryCopy(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                return false;
            }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    return false;
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                return true;
            }
            catch (Exception exception)
            {
                LogFailure($"Could not copy '{sourcePath}' to '{destinationPath}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryMove(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                return false;
            }

            try
            {
                if (!File.Exists(sourcePath))
                {
                    return false;
                }

                // File.Move refuses to overwrite on the .NET Standard profile Unity targets, so the
                // destination is removed first. This is the one window in the write sequence where
                // the primary save is briefly absent, which is precisely why the backup is
                // refreshed before this point rather than after it.
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(sourcePath, destinationPath);
                return true;
            }
            catch (Exception exception)
            {
                LogFailure($"Could not move '{sourcePath}' to '{destinationPath}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return !File.Exists(path);
            }
            catch (Exception exception)
            {
                LogFailure($"Could not delete '{path}'.", exception);
                return false;
            }
        }

        /// <inheritdoc />
        public long GetFileSize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return -1;
            }

            try
            {
                var info = new FileInfo(path);
                return info.Exists ? info.Length : -1;
            }
            catch (Exception exception)
            {
                LogFailure($"Could not measure '{path}'.", exception);
                return -1;
            }
        }

        /// <summary>
        /// Records a file operation failure.
        /// </summary>
        /// <param name="message">What was being attempted.</param>
        /// <param name="exception">The underlying exception.</param>
        private void LogFailure(string message, Exception exception) =>
            logger.LogException(LogCategory.Persistence, message, exception);
    }
}
