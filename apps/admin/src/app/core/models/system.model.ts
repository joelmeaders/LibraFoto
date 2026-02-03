/**
 * Response containing system information and update status.
 */
export interface SystemInfoResponse {
  /** Current application version. */
  version: string;
  /** Current git commit hash (short). */
  commitHash: string | null;
  /** Whether an update is available. */
  updateAvailable: boolean;
  /** Latest available version (if update available). */
  latestVersion: string | null;
  /** Number of commits behind the latest version. */
  commitsBehind: number;
  /** Recent changelog entries (if update available). */
  changelog: string[] | null;
  /** System uptime as a duration string. */
  uptime: string;
  /** Whether the system is running in Docker. */
  isDocker: boolean;
  /** Runtime environment (Development, Production, etc). */
  environment: string;
}

/**
 * Response for update check operation.
 */
export interface UpdateCheckResponse {
  /** Whether an update is available. */
  updateAvailable: boolean;
  /** Current version. */
  currentVersion: string;
  /** Latest available version. */
  latestVersion: string | null;
  /** Number of commits behind. */
  commitsBehind: number;
  /** Recent changelog entries. */
  changelog: string[] | null;
  /** Error message if check failed. */
  error: string | null;
  /** Time when check was performed. */
  checkedAt: string;
}

/**
 * Response for update trigger operation.
 */
export interface UpdateTriggerResponse {
  /** Status message. */
  message: string;
  /** Estimated downtime in seconds. */
  estimatedDowntimeSeconds: number;
}
