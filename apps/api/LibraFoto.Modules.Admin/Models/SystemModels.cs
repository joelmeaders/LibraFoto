namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Response containing system information and update status.
    /// </summary>
    public record SystemInfoResponse
    {
        /// <summary>
        /// Current application version.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Current git commit hash (short).
        /// </summary>
        public string? CommitHash { get; init; }

        /// <summary>
        /// Whether an update is available.
        /// </summary>
        public bool UpdateAvailable { get; init; }

        /// <summary>
        /// Latest available version (if update available).
        /// </summary>
        public string? LatestVersion { get; init; }

        /// <summary>
        /// Number of commits behind the latest version.
        /// </summary>
        public int? CommitsBehind { get; init; }

        /// <summary>
        /// Recent changelog entries (if update available).
        /// </summary>
        public IReadOnlyList<string>? Changelog { get; init; }

        /// <summary>
        /// Last time update check was performed.
        /// </summary>
        public DateTime? LastChecked { get; init; }

        /// <summary>
        /// System uptime.
        /// </summary>
        public TimeSpan Uptime { get; init; }

        /// <summary>
        /// Whether the system is running in Docker.
        /// </summary>
        public bool IsDocker { get; init; }

        /// <summary>
        /// Runtime environment (Development, Production, etc).
        /// </summary>
        public required string Environment { get; init; }
    }

    /// <summary>
    /// Response for update check operation.
    /// </summary>
    public record UpdateCheckResponse
    {
        /// <summary>
        /// Whether an update is available.
        /// </summary>
        public bool UpdateAvailable { get; init; }

        /// <summary>
        /// Current version.
        /// </summary>
        public required string CurrentVersion { get; init; }

        /// <summary>
        /// Latest available version.
        /// </summary>
        public string? LatestVersion { get; init; }

        /// <summary>
        /// Number of commits behind.
        /// </summary>
        public int CommitsBehind { get; init; }

        /// <summary>
        /// Recent changelog entries.
        /// </summary>
        public IReadOnlyList<string> Changelog { get; init; } = [];

        /// <summary>
        /// Error message if check failed.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Time when check was performed.
        /// </summary>
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for update trigger operation.
    /// </summary>
    /// <param name="Message">Status message about the update.</param>
    /// <param name="EstimatedDowntimeSeconds">Estimated downtime in seconds.</param>
    public record UpdateTriggerResponse(string Message, int EstimatedDowntimeSeconds);
}
