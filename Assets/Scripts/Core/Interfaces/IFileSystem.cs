namespace VRSimulation.Core.Interfaces
{
    /// <summary>
    /// The subset of file operations the persistence layer needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The save system is the one part of the experience where a bug destroys something the player
    /// cannot get back, so it needs the most thorough tests in the project — including the failure
    /// paths: a truncated file, a disk that is full, a read that throws. Those cases are difficult
    /// and slow to provoke against a real disk and impossible to provoke reliably on a headset.
    /// </para>
    /// <para>
    /// Routing every file operation through this interface lets
    /// <see cref="VRSimulation.Core.Services.SaveService"/> be exercised in EditMode against an
    /// in-memory implementation that can be told to fail on demand, with no I/O and no test
    /// pollution of the developer's own save file (TRD 23).
    /// </para>
    /// <para>
    /// Implementations must not throw for the ordinary "it is not there" cases; they report those
    /// through return values. They may throw for genuinely exceptional conditions such as a denied
    /// permission, and callers are expected to handle that.
    /// </para>
    /// </remarks>
    public interface IFileSystem
    {
        /// <summary>
        /// Determines whether a file exists.
        /// </summary>
        /// <param name="path">Absolute path to test.</param>
        /// <returns><c>true</c> if the file exists and is readable.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Ensures a directory exists, creating it and any missing parents.
        /// </summary>
        /// <param name="path">Absolute directory path.</param>
        /// <returns><c>true</c> if the directory exists after the call.</returns>
        bool EnsureDirectory(string path);

        /// <summary>
        /// Reads a file as UTF-8 text.
        /// </summary>
        /// <param name="path">Absolute path to read.</param>
        /// <param name="contents">Receives the file contents, or <c>null</c> on failure.</param>
        /// <returns><c>true</c> if the file was read.</returns>
        bool TryReadAllText(string path, out string contents);

        /// <summary>
        /// Writes UTF-8 text, replacing any existing file.
        /// </summary>
        /// <remarks>
        /// Implementations must flush to the storage device before returning. A buffered write that
        /// is still in flight when the headset loses power is indistinguishable from a corrupt file
        /// (PRD edge case, "Player leaves during save"; "Low battery").
        /// </remarks>
        /// <param name="path">Absolute path to write.</param>
        /// <param name="contents">Text to write.</param>
        /// <returns><c>true</c> if the write completed and was flushed.</returns>
        bool TryWriteAllText(string path, string contents);

        /// <summary>
        /// Copies a file, overwriting the destination.
        /// </summary>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <param name="destinationPath">Absolute destination path.</param>
        /// <returns><c>true</c> if the copy completed.</returns>
        bool TryCopy(string sourcePath, string destinationPath);

        /// <summary>
        /// Moves a file, overwriting the destination.
        /// </summary>
        /// <remarks>
        /// This is the operation that makes a save atomic: the new contents are staged in a
        /// temporary file and then moved over the real one, so a reader never observes a
        /// half-written file. Implementations must prefer a same-volume rename where the platform
        /// offers one.
        /// </remarks>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <param name="destinationPath">Absolute destination path.</param>
        /// <returns><c>true</c> if the move completed.</returns>
        bool TryMove(string sourcePath, string destinationPath);

        /// <summary>
        /// Deletes a file if it exists.
        /// </summary>
        /// <param name="path">Absolute path to delete.</param>
        /// <returns><c>true</c> if the file is absent after the call.</returns>
        bool TryDelete(string path);

        /// <summary>
        /// Gets the size of a file in bytes.
        /// </summary>
        /// <param name="path">Absolute path to measure.</param>
        /// <returns>The size in bytes, or <c>-1</c> if the file does not exist or cannot be measured.</returns>
        long GetFileSize(string path);
    }
}
