using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Interfaces
{
    /// <summary>
    /// Interface for storage providers that can read and optionally write files.
    /// Implementations include local storage, Google Photos, OneDrive, etc.
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>
        /// Unique identifier for this provider instance (matches database ID).
        /// </summary>
        long ProviderId { get; }

        /// <summary>
        /// The type of storage provider.
        /// </summary>
        StorageProviderType ProviderType { get; }

        /// <summary>
        /// Display name for this provider instance.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this provider supports file uploads.
        /// </summary>
        bool SupportsUpload { get; }

        /// <summary>
        /// Whether this provider supports folder watching for automatic sync.
        /// </summary>
        bool SupportsWatch { get; }

        /// <summary>
        /// Initializes the provider with configuration from the database.
        /// </summary>
        /// <param name="providerId">The database ID of the provider.</param>
        /// <param name="displayName">The display name for this provider.</param>
        /// <param name="configuration">JSON configuration string.</param>
        void Initialize(long providerId, string displayName, string? configuration);

        /// <summary>
        /// Gets files from the storage provider.
        /// </summary>
        /// <param name="folderId">Optional folder ID to list files from. Null for root.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of file information.</returns>
        Task<IEnumerable<StorageFileInfo>> GetFilesAsync(string? folderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file's contents as a byte array.
        /// Use for smaller files or when full content is needed in memory.
        /// </summary>
        /// <param name="fileId">The file ID or path within this provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>File contents as byte array.</returns>
        Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a stream for reading a file's contents.
        /// Use for large files or streaming scenarios.
        /// </summary>
        /// <param name="fileId">The file ID or path within this provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Stream for reading the file.</returns>
        Task<Stream> GetFileStreamAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a file to this storage provider.
        /// </summary>
        /// <param name="fileName">Original filename.</param>
        /// <param name="content">File content stream.</param>
        /// <param name="contentType">MIME content type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Upload result with file information.</returns>
        /// <exception cref="NotSupportedException">Thrown if provider doesn't support uploads.</exception>
        Task<UploadResult> UploadFileAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from this storage provider.
        /// </summary>
        /// <param name="fileId">The file ID or path to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted successfully, false if file not found.</returns>
        Task<bool> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file exists in this storage provider.
        /// </summary>
        /// <param name="fileId">The file ID or path to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if file exists.</returns>
        Task<bool> FileExistsAsync(string fileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the connection to this storage provider.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if connection is successful.</returns>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}
