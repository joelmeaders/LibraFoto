namespace LibraFoto.Modules.Storage.Models
{
    /// <summary>
    /// Request for uploading a file.
    /// Note: Actual file content comes via IFormFile in the endpoint.
    /// </summary>
    public record UploadRequest
    {
        /// <summary>
        /// Target album ID to add the uploaded file to (optional).
        /// </summary>
        public long? AlbumId { get; init; }

        /// <summary>
        /// Tags to apply to the uploaded file.
        /// </summary>
        public List<string>? Tags { get; init; }

        /// <summary>
        /// Custom filename to use (optional, defaults to original filename).
        /// </summary>
        public string? CustomFilename { get; init; }

        /// <summary>
        /// Whether to overwrite if a file with the same name exists.
        /// </summary>
        public bool Overwrite { get; init; } = false;
    }

    /// <summary>
    /// Request for guest link uploads.
    /// </summary>
    public record GuestUploadRequest
    {
        /// <summary>
        /// The guest link ID for authentication.
        /// </summary>
        public required string LinkId { get; init; }

        /// <summary>
        /// Optional message from the guest.
        /// </summary>
        public string? Message { get; init; }
    }
}
