using System.Diagnostics;
using LibraFoto.Modules.Admin.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Admin.Services
{
    /// <summary>
    /// Service for checking system info and available updates.
    /// </summary>
    public interface ISystemService
    {
        /// <summary>
        /// Gets current system information.
        /// </summary>
        Task<SystemInfoResponse> GetSystemInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks for available updates from the remote repository.
        /// </summary>
        Task<UpdateCheckResponse> CheckForUpdatesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Triggers the application update process.
        /// </summary>
        Task<UpdateTriggerResponse> TriggerUpdateAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of system service using git for update checks.
    /// </summary>
    public class SystemService : ISystemService
    {
        private readonly ILogger<SystemService> _logger;
        private readonly IHostEnvironment _environment;
        private readonly IMemoryCache _cache;
        private readonly DateTime _startTime;

        private const string UpdateCacheKey = "UpdateCheck";
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public SystemService(
            ILogger<SystemService> logger,
            IHostEnvironment environment,
            IMemoryCache cache)
        {
            _logger = logger;
            _environment = environment;
            _cache = cache;
            _startTime = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public async Task<SystemInfoResponse> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        {
            var version = GetCurrentVersion();
            var commitHash = await GetCurrentCommitAsync(cancellationToken);
            var isDocker = IsRunningInDocker();

            // Try to get cached update info
            UpdateCheckResponse? updateInfo = null;
            if (_cache.TryGetValue(UpdateCacheKey, out UpdateCheckResponse? cached))
            {
                updateInfo = cached;
            }

            return new SystemInfoResponse
            {
                Version = version,
                CommitHash = commitHash,
                UpdateAvailable = updateInfo?.UpdateAvailable ?? false,
                LatestVersion = updateInfo?.LatestVersion,
                CommitsBehind = updateInfo?.CommitsBehind,
                Changelog = updateInfo?.Changelog,
                LastChecked = updateInfo?.CheckedAt,
                Uptime = DateTime.UtcNow - _startTime,
                IsDocker = isDocker,
                Environment = _environment.EnvironmentName
            };
        }

        /// <inheritdoc />
        public async Task<UpdateCheckResponse> CheckForUpdatesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            // Return cached result if available and not forcing refresh
            if (!forceRefresh && _cache.TryGetValue(UpdateCacheKey, out UpdateCheckResponse? cached))
            {
                return cached!;
            }

            var currentVersion = GetCurrentVersion();

            // Check if git is available
            if (!await IsGitAvailableAsync(cancellationToken))
            {
                var noGitResponse = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    Error = "Git is not available. Update checks are disabled.",
                    CheckedAt = DateTime.UtcNow
                };

                return noGitResponse;
            }

            try
            {
                // Fetch from remote
                var fetchResult = await RunGitCommandAsync("fetch origin --quiet", cancellationToken);
                if (!fetchResult.Success)
                {
                    _logger.LogWarning("Failed to fetch from remote: {Error}", fetchResult.Error);
                    return new UpdateCheckResponse
                    {
                        UpdateAvailable = false,
                        CurrentVersion = currentVersion,
                        Error = "Failed to fetch from remote repository",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                // Get current and remote commits
                var currentCommit = await RunGitCommandAsync("rev-parse HEAD", cancellationToken);
                var remoteCommit = await RunGitCommandAsync("rev-parse origin/main", cancellationToken);

                if (!remoteCommit.Success)
                {
                    // Try master branch
                    remoteCommit = await RunGitCommandAsync("rev-parse origin/master", cancellationToken);
                }

                if (!currentCommit.Success || !remoteCommit.Success)
                {
                    return new UpdateCheckResponse
                    {
                        UpdateAvailable = false,
                        CurrentVersion = currentVersion,
                        Error = "Failed to compare versions",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                var isUpToDate = currentCommit.Output?.Trim() == remoteCommit.Output?.Trim();

                if (isUpToDate)
                {
                    var upToDateResponse = new UpdateCheckResponse
                    {
                        UpdateAvailable = false,
                        CurrentVersion = currentVersion,
                        LatestVersion = currentVersion,
                        CommitsBehind = 0,
                        Changelog = [],
                        CheckedAt = DateTime.UtcNow
                    };

                    _cache.Set(UpdateCacheKey, upToDateResponse, _cacheDuration);
                    return upToDateResponse;
                }

                // Count commits behind
                var countResult = await RunGitCommandAsync("rev-list --count HEAD..origin/main", cancellationToken);
                if (!countResult.Success)
                {
                    countResult = await RunGitCommandAsync("rev-list --count HEAD..origin/master", cancellationToken);
                }

                var commitsBehind = 0;
                if (countResult.Success && int.TryParse(countResult.Output?.Trim(), out var count))
                {
                    commitsBehind = count;
                }

                // Get changelog
                var logResult = await RunGitCommandAsync("log --oneline HEAD..origin/main -10", cancellationToken);
                if (!logResult.Success)
                {
                    logResult = await RunGitCommandAsync("log --oneline HEAD..origin/master -10", cancellationToken);
                }

                var changelog = new List<string>();
                if (logResult.Success && !string.IsNullOrEmpty(logResult.Output))
                {
                    changelog = logResult.Output
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToList();
                }

                // Try to get latest version from .version file on remote
                var latestVersion = currentVersion;
                var showResult = await RunGitCommandAsync("show origin/main:.version", cancellationToken);
                if (!showResult.Success)
                {
                    showResult = await RunGitCommandAsync("show origin/master:.version", cancellationToken);
                }

                if (showResult.Success && !string.IsNullOrEmpty(showResult.Output))
                {
                    latestVersion = showResult.Output.Trim();
                }

                var response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    CommitsBehind = commitsBehind,
                    Changelog = changelog,
                    CheckedAt = DateTime.UtcNow
                };

                _cache.Set(UpdateCacheKey, response, _cacheDuration);

                _logger.LogInformation(
                    "Update available: {CurrentVersion} -> {LatestVersion} ({CommitsBehind} commits behind)",
                    currentVersion, latestVersion, commitsBehind);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");

                return new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    Error = "Error checking for updates: " + ex.Message,
                    CheckedAt = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc />
        public Task<UpdateTriggerResponse> TriggerUpdateAsync(CancellationToken cancellationToken = default)
        {
            var isDocker = IsRunningInDocker();
            var scriptPath = GetUpdateScriptPath(isDocker);

            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("Update script not found at {ScriptPath}", scriptPath);
                return Task.FromResult(new UpdateTriggerResponse(
                    "Update script not found. Please update manually.",
                    0));
            }

            _logger.LogInformation("Triggering update from script: {ScriptPath}", scriptPath);

            // Start the update process in background (fire-and-forget)
            Task.Run(async () =>
            {
                try
                {
                    // Give time for the response to be sent
                    await Task.Delay(1000, CancellationToken.None);

                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "bash",
                            Arguments = $"\"{scriptPath}\" --force",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = GetRepositoryRoot()
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync(CancellationToken.None);

                    _logger.LogInformation("Update script completed with exit code: {ExitCode}", process.ExitCode);

                    // Trigger restart - in Docker the container should restart automatically
                    // For non-Docker, attempt a graceful application restart
                    if (isDocker)
                    {
                        _logger.LogInformation("Running in Docker, container will restart automatically");
                    }
                    else
                    {
                        _logger.LogInformation("Attempting application restart");
                        Environment.Exit(0); // This will cause the application to restart if managed by a service
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to run update script");
                }
            }, CancellationToken.None);

            return Task.FromResult(new UpdateTriggerResponse(
                "Update triggered successfully. The application will restart shortly.",
                isDocker ? 30 : 60));
        }

        private static string GetUpdateScriptPath(bool isDocker)
        {
            if (isDocker)
            {
                return "/app/scripts/update.sh";
            }

            // For local development, find update.sh relative to the repository root
            var repoRoot = GetRepositoryRoot();
            return Path.Combine(repoRoot, "update.sh");
        }

        private static string GetCurrentVersion()
        {
            // Try to read from .version file
            var versionFile = Path.Combine(AppContext.BaseDirectory, ".version");
            if (File.Exists(versionFile))
            {
                return File.ReadAllText(versionFile).Trim();
            }

            // Try environment variable
            var envVersion = Environment.GetEnvironmentVariable("VERSION");
            if (!string.IsNullOrEmpty(envVersion))
            {
                return envVersion;
            }

            // Fallback to assembly version
            var assembly = typeof(SystemService).Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString(3) ?? "1.0.0";
        }

        private async Task<string?> GetCurrentCommitAsync(CancellationToken cancellationToken)
        {
            var result = await RunGitCommandAsync("rev-parse --short HEAD", cancellationToken);
            return result.Success ? result.Output?.Trim() : null;
        }

        private static bool IsRunningInDocker()
        {
            // Check for /.dockerenv file
            if (File.Exists("/.dockerenv"))
            {
                return true;
            }

            // Check cgroup for docker
            try
            {
                var cgroup = File.ReadAllText("/proc/1/cgroup");
                return cgroup.Contains("docker") || cgroup.Contains("kubepods");
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken)
        {
            var result = await RunGitCommandAsync("--version", cancellationToken);
            return result.Success;
        }

        private async Task<(bool Success, string? Output, string? Error)> RunGitCommandAsync(
            string arguments,
            CancellationToken cancellationToken)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = GetRepositoryRoot()
                    }
                };

                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Git command failed: git {Arguments}", arguments);
                return (false, null, ex.Message);
            }
        }

        private static string GetRepositoryRoot()
        {
            // In Docker, we're likely at /app
            var appDir = AppContext.BaseDirectory;

            // Try to find .git directory by walking up
            var dir = new DirectoryInfo(appDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback to app directory
            return appDir;
        }
    }
}
