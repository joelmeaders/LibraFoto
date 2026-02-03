/**
 * API Client for LibraFoto Display Frontend
 * Handles communication with the backend API
 */

import {
  type PhotoDto,
  type DisplaySettingsDto,
  type PhotoCountResponse,
  type ResetResponse,
  type ApiError,
  type DisplayConfigResponse,
  TransitionType,
  SourceType,
  ImageFit,
} from "./types";
import { getConfig, logger, type AppConfig } from "./config";

// Re-export types for convenience
export type { PhotoDto, DisplaySettingsDto, ApiError };
export { TransitionType, SourceType, ImageFit };

/**
 * Result type for API operations that can fail.
 */
export type ApiResult<T> =
  | { success: true; data: T }
  | { success: false; error: ApiError };

/**
 * Event callback type for real-time updates.
 */
export type SettingsChangeCallback = (settings: DisplaySettingsDto) => void;

/**
 * API Client for the LibraFoto Display Frontend.
 * Handles all communication with the backend API including:
 * - Slideshow photo retrieval (next, current, preload)
 * - Display settings management
 * - Thumbnail URLs
 * - Polling for settings changes
 */
export class ApiClient {
  private config: AppConfig;
  private baseUrl: string;
  private settingsId: number | null = null;
  private settingsPollingTimer: number | null = null;
  private settingsChangeCallbacks: SettingsChangeCallback[] = [];
  private lastSettingsHash: string | null = null;
  private retryCount: Map<string, number> = new Map();

  constructor() {
    this.config = getConfig();
    this.baseUrl = this.config.apiBaseUrl;
    logger.debug("ApiClient initialized with base URL:", this.baseUrl);
  }

  // ============================================================================
  // Slideshow Endpoints
  // ============================================================================

  /**
   * Gets the next photo in the slideshow sequence.
   * Advances the slideshow position.
   */
  async getNextPhoto(): Promise<ApiResult<PhotoDto>> {
    return this.fetchWithRetry<PhotoDto>(
      this.buildUrl("/display/photos/next"),
      "getNextPhoto",
    );
  }

  /**
   * Gets the current photo without advancing the sequence.
   */
  async getCurrentPhoto(): Promise<ApiResult<PhotoDto>> {
    return this.fetchWithRetry<PhotoDto>(
      this.buildUrl("/display/photos/current"),
      "getCurrentPhoto",
    );
  }

  /**
   * Gets multiple photos for preloading.
   * @param count Number of photos to preload (1-50, default from config)
   */
  async getPreloadPhotos(count?: number): Promise<ApiResult<PhotoDto[]>> {
    const preloadCount = count ?? this.config.preloadCount;
    return this.fetchWithRetry<PhotoDto[]>(
      this.buildUrl("/display/photos/preload", {
        count: preloadCount.toString(),
      }),
      "getPreloadPhotos",
    );
  }

  /**
   * Gets the total count of photos available for the current settings.
   */
  async getPhotoCount(): Promise<ApiResult<PhotoCountResponse>> {
    return this.fetchWithRetry<PhotoCountResponse>(
      this.buildUrl("/display/photos/count"),
      "getPhotoCount",
    );
  }

  // ============================================================================
  // Config Endpoints
  // ============================================================================

  /**
   * Gets display configuration including admin URL.
   */
  async getDisplayConfig(): Promise<ApiResult<DisplayConfigResponse>> {
    // Use a simpler URL without settingsId for config endpoint
    const url = `${this.baseUrl}/display/config`;
    try {
      const response = await fetch(url);
      if (!response.ok) {
        const error = await this.parseError(response);
        logger.error("Failed to get display config:", error);
        return { success: false, error };
      }
      const data = await response.json();
      return { success: true, data };
    } catch (error) {
      logger.error("Network error getting display config:", error);
      return {
        success: false,
        error: this.createNetworkError(error),
      };
    }
  }

  /**
   * Resets the slideshow sequence to the beginning.
   */
  async resetSequence(): Promise<ApiResult<ResetResponse>> {
    const url = this.buildUrl("/display/photos/reset");
    try {
      const response = await fetch(url, { method: "POST" });
      if (!response.ok) {
        const error = await this.parseError(response);
        logger.error("Failed to reset sequence:", error);
        return { success: false, error };
      }
      const data = await response.json();
      logger.debug("Slideshow sequence reset:", data);
      return { success: true, data };
    } catch (error) {
      logger.error("Network error resetting sequence:", error);
      return {
        success: false,
        error: this.createNetworkError(error),
      };
    }
  }

  // ============================================================================
  // Settings Endpoints
  // ============================================================================

  /**
   * Gets the currently active display settings.
   */
  async getSettings(): Promise<ApiResult<DisplaySettingsDto>> {
    const result = await this.fetchWithRetry<DisplaySettingsDto>(
      this.buildUrl("/display/settings"),
      "getSettings",
    );

    if (result.success) {
      this.settingsId = result.data.id;
      // Note: lastSettingsHash is only updated by polling logic to allow change detection
      return result;
    }

    if (this.settingsId !== null) {
      logger.warn(
        "Failed to load settings with settingsId, retrying without settingsId",
      );
      const fallback = await this.fetchWithRetry<DisplaySettingsDto>(
        `${this.baseUrl}/display/settings`,
        "getSettings:NoId",
      );
      if (fallback.success) {
        this.settingsId = fallback.data.id;
        return fallback;
      }
    }

    this.settingsId = null;
    return result;
  }

  /**
   * Gets all available display settings configurations.
   */
  async getAllSettings(): Promise<ApiResult<DisplaySettingsDto[]>> {
    return this.fetchWithRetry<DisplaySettingsDto[]>(
      this.buildUrl("/display/settings/all"),
      "getAllSettings",
    );
  }

  /**
   * Gets display settings by ID.
   */
  async getSettingsById(id: number): Promise<ApiResult<DisplaySettingsDto>> {
    return this.fetchWithRetry<DisplaySettingsDto>(
      this.buildUrl(`/display/settings/${id}`),
      `getSettingsById:${id}`,
    );
  }

  /**
   * Gets default display settings when API is unavailable.
   */
  getDefaultSettings(): DisplaySettingsDto {
    return {
      id: 0,
      name: "Default",
      slideDuration: 10,
      transition: TransitionType.Fade,
      transitionDuration: 1000,
      sourceType: SourceType.All,
      shuffle: true,
      imageFit: ImageFit.Contain,
    };
  }

  // ============================================================================
  // Thumbnail/Media Endpoints
  // ============================================================================

  /**
   * Builds the URL for a photo's thumbnail (400x400).
   * @param photoId The photo ID
   */
  getThumbnailUrl(photoId: number): string {
    return `${this.baseUrl}/media/thumbnails/${photoId}`;
  }

  /**
   * Builds the URL for a photo's full-size image.
   * This uses the URL provided in the PhotoDto, which may be a file path or cloud URL.
   */
  getPhotoUrl(photo: PhotoDto): string {
    // If the URL is already absolute, return it as-is
    if (photo.url.startsWith("http://") || photo.url.startsWith("https://")) {
      return photo.url;
    }
    // Otherwise, it's a relative path served by the API
    return `${this.baseUrl}/media/photos/${photo.id}`;
  }

  // ============================================================================
  // Real-time Updates (Polling)
  // ============================================================================

  /**
   * Starts polling for settings changes.
   * Calls registered callbacks when settings are updated.
   */
  startSettingsPolling(): void {
    if (this.settingsPollingTimer !== null) {
      return; // Already polling
    }

    logger.debug("Starting settings polling...");

    const poll = async () => {
      try {
        const result = await this.getSettings();
        if (result.success) {
          const newHash = this.hashSettings(result.data);
          if (
            this.lastSettingsHash !== null &&
            newHash !== this.lastSettingsHash
          ) {
            logger.info("Settings changed, notifying listeners");
            this.notifySettingsChange(result.data);
          }
          this.lastSettingsHash = newHash;
        }
      } catch (error) {
        logger.warn("Settings poll failed:", error);
      }
    };

    // Run initial poll to set baseline hash
    poll();

    this.settingsPollingTimer = window.setInterval(
      poll,
      this.config.settingsPollingInterval,
    );
  }

  /**
   * Stops polling for settings changes.
   */
  stopSettingsPolling(): void {
    if (this.settingsPollingTimer !== null) {
      clearInterval(this.settingsPollingTimer);
      this.settingsPollingTimer = null;
      logger.debug("Settings polling stopped");
    }
  }

  /**
   * Registers a callback to be called when settings change.
   * @returns A function to unregister the callback
   */
  onSettingsChange(callback: SettingsChangeCallback): () => void {
    this.settingsChangeCallbacks.push(callback);
    return () => {
      const index = this.settingsChangeCallbacks.indexOf(callback);
      if (index > -1) {
        this.settingsChangeCallbacks.splice(index, 1);
      }
    };
  }

  // ============================================================================
  // Image Preloading Utilities
  // ============================================================================

  /**
   * Preloads an image into browser cache.
   * @returns Promise that resolves when image is loaded
   */
  preloadImage(url: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => {
        logger.debug("Image preloaded:", url);
        resolve();
      };
      img.onerror = (error) => {
        logger.warn("Failed to preload image:", url, error);
        reject(new Error(`Failed to preload image: ${url}`));
      };
      img.src = url;
    });
  }

  /**
   * Preloads multiple images in parallel.
   */
  async preloadImages(urls: string[]): Promise<void> {
    await Promise.allSettled(urls.map((url) => this.preloadImage(url)));
  }

  /**
   * Preloads photos and their images for smooth slideshow transitions.
   * @returns Array of successfully preloaded photos
   */
  async preloadPhotosWithImages(count?: number): Promise<PhotoDto[]> {
    const result = await this.getPreloadPhotos(count);
    if (!result.success) {
      logger.warn("Failed to fetch photos for preloading");
      return [];
    }

    const photos = result.data;

    // Preload all photo images in parallel
    const preloadPromises = photos.map(async (photo) => {
      try {
        await this.preloadImage(this.getPhotoUrl(photo));
        return photo;
      } catch {
        return null;
      }
    });

    const results = await Promise.all(preloadPromises);
    return results.filter((p): p is PhotoDto => p !== null);
  }

  // ============================================================================
  // Private Helpers
  // ============================================================================

  /**
   * Builds a full URL with optional query parameters.
   */
  private buildUrl(path: string, params?: Record<string, string>): string {
    let url = `${this.baseUrl}${path}`;

    // Add settings ID if set
    const queryParams = new URLSearchParams();
    if (this.settingsId !== null) {
      queryParams.set("settingsId", this.settingsId.toString());
    }
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        queryParams.set(key, value);
      }
    }

    const queryString = queryParams.toString();
    if (queryString) {
      url += `?${queryString}`;
    }

    return url;
  }

  /**
   * Fetches data from an endpoint with retry logic.
   */
  private async fetchWithRetry<T>(
    url: string,
    operationKey: string,
  ): Promise<ApiResult<T>> {
    const currentRetry = this.retryCount.get(operationKey) ?? 0;

    try {
      const response = await fetch(url);

      if (!response.ok) {
        const error = await this.parseError(response);

        // Don't retry for client errors (4xx), only for server errors (5xx)
        if (response.status >= 500 && currentRetry < this.config.maxRetries) {
          logger.warn(
            `Request failed, retrying (${currentRetry + 1}/${
              this.config.maxRetries
            }):`,
            url,
          );
          this.retryCount.set(operationKey, currentRetry + 1);
          await this.delay(this.config.retryDelay);
          return this.fetchWithRetry<T>(url, operationKey);
        }

        logger.error(`Request failed (${response.status}):`, url, error);
        this.retryCount.delete(operationKey);
        return { success: false, error };
      }

      const data = await response.json();
      this.retryCount.delete(operationKey);
      return { success: true, data };
    } catch (error) {
      if (currentRetry < this.config.maxRetries) {
        logger.warn(
          `Network error, retrying (${currentRetry + 1}/${
            this.config.maxRetries
          }):`,
          url,
        );
        this.retryCount.set(operationKey, currentRetry + 1);
        await this.delay(this.config.retryDelay);
        return this.fetchWithRetry<T>(url, operationKey);
      }

      logger.error("Network error:", url, error);
      this.retryCount.delete(operationKey);
      return {
        success: false,
        error: this.createNetworkError(error),
      };
    }
  }

  /**
   * Parses an error response from the API.
   */
  private async parseError(response: Response): Promise<ApiError> {
    try {
      const body = await response.json();
      if (body.code && body.message) {
        return body as ApiError;
      }
      return {
        code: "UNKNOWN_ERROR",
        message: body.message || response.statusText,
        details: body,
      };
    } catch {
      return {
        code: `HTTP_${response.status}`,
        message: response.statusText || "Unknown error",
      };
    }
  }

  /**
   * Creates an error object for network failures.
   */
  private createNetworkError(error: unknown): ApiError {
    const message = error instanceof Error ? error.message : "Network error";
    return {
      code: "NETWORK_ERROR",
      message,
      details: error,
    };
  }

  /**
   * Creates a hash of settings for change detection.
   */
  private hashSettings(settings: DisplaySettingsDto): string {
    return JSON.stringify(settings);
  }

  /**
   * Notifies all registered callbacks of settings changes.
   */
  private notifySettingsChange(settings: DisplaySettingsDto): void {
    for (const callback of this.settingsChangeCallbacks) {
      try {
        callback(settings);
      } catch (error) {
        logger.error("Error in settings change callback:", error);
      }
    }
  }

  /**
   * Delays execution for a specified time.
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
