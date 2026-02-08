/**
 * Configuration for the LibraFoto Display Frontend
 * Environment variables and default settings
 */

/**
 * Application configuration loaded from environment variables.
 */
export interface AppConfig {
  /** Base URL for the API server */
  apiBaseUrl: string;
  /** Enable debug logging */
  debug: boolean;
  /** Polling interval for settings refresh (ms) */
  settingsPollingInterval: number;
  /** Number of photos to preload */
  preloadCount: number;
  /** Minimum preload queue size before fetching more */
  preloadThreshold: number;
  /** Retry delay for failed API calls (ms) */
  retryDelay: number;
  /** Maximum retry attempts */
  maxRetries: number;
  /** Maximum video duration in seconds before skipping */
  maxVideoDuration: number;
}

/**
 * Get the application configuration from environment variables.
 * In production, these are injected at build time by Vite.
 */
export function getConfig(): AppConfig {
  return {
    // API base URL - in development, Vite proxies /api to the backend
    // In production, this should be set to the actual API URL
    apiBaseUrl: import.meta.env.VITE_API_BASE_URL || "/api",

    // Debug mode
    debug: import.meta.env.VITE_DEBUG === "true" || import.meta.env.DEV,

    // Settings polling interval (default: 5 seconds)
    settingsPollingInterval: parseInt(
      import.meta.env.VITE_SETTINGS_POLL_INTERVAL || "5000",
      10,
    ),

    // Preload configuration
    preloadCount: parseInt(import.meta.env.VITE_PRELOAD_COUNT || "10", 10),
    preloadThreshold: parseInt(
      import.meta.env.VITE_PRELOAD_THRESHOLD || "3",
      10,
    ),

    // Retry configuration
    retryDelay: parseInt(import.meta.env.VITE_RETRY_DELAY || "5000", 10),
    maxRetries: parseInt(import.meta.env.VITE_MAX_RETRIES || "3", 10),

    // Video configuration
    maxVideoDuration: parseInt(
      import.meta.env.VITE_MAX_VIDEO_DURATION || "30",
      10,
    ),
  };
}

/**
 * Logger utility that respects debug configuration.
 */
export const logger = {
  debug: (...args: unknown[]) => {
    if (getConfig().debug) {
      console.debug("[LibraFoto]", ...args);
    }
  },
  info: (...args: unknown[]) => {
    console.info("[LibraFoto]", ...args);
  },
  warn: (...args: unknown[]) => {
    console.warn("[LibraFoto]", ...args);
  },
  error: (...args: unknown[]) => {
    console.error("[LibraFoto]", ...args);
  },
};

// Extend ImportMeta for TypeScript
declare global {
  interface ImportMetaEnv {
    readonly VITE_API_BASE_URL?: string;
    readonly VITE_DEBUG?: string;
    readonly VITE_SETTINGS_POLL_INTERVAL?: string;
    readonly VITE_PRELOAD_COUNT?: string;
    readonly VITE_PRELOAD_THRESHOLD?: string;
    readonly VITE_RETRY_DELAY?: string;
    readonly VITE_MAX_RETRIES?: string;
    readonly VITE_MAX_VIDEO_DURATION?: string;
    readonly DEV: boolean;
  }

  interface ImportMeta {
    readonly env: ImportMetaEnv;
  }
}

export {};
