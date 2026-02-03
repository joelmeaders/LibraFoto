/**
 * Unit tests for api-client.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ApiClient, TransitionType, SourceType } from "./api-client";
import type { PhotoDto, DisplaySettingsDto } from "./types";
import { MediaType } from "./types";

// Mock config module
vi.mock("./config", () => ({
  getConfig: vi.fn(() => ({
    apiBaseUrl: "/api",
    debug: false,
    settingsPollingInterval: 60000,
    preloadCount: 10,
    preloadThreshold: 3,
    retryDelay: 100, // Short delay for tests
    maxRetries: 3,
    maxVideoDuration: 30,
  })),
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  },
}));

describe("ApiClient", () => {
  let apiClient: ApiClient;
  let fetchSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    apiClient = new ApiClient();
    fetchSpy = vi.spyOn(globalThis, "fetch");
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("config endpoints", () => {
    it("should return config on getDisplayConfig success", async () => {
      const mockConfig = { adminUrl: "http://localhost:4200" };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(mockConfig),
      } as Response);

      const result = await apiClient.getDisplayConfig();
      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.adminUrl).toBe("http://localhost:4200");
      }
    });

    it("should return error on getDisplayConfig failure", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: () => Promise.resolve({ code: "SERVER_ERROR", message: "Error" }),
      } as Response);

      const result = await apiClient.getDisplayConfig();
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.code).toBe("SERVER_ERROR");
      }
    });

    it("should handle network error on getDisplayConfig", async () => {
      fetchSpy.mockRejectedValueOnce(new Error("Network error"));

      const result = await apiClient.getDisplayConfig();
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.code).toBe("NETWORK_ERROR");
      }
    });
  });

  describe("photo endpoints", () => {
    it("should return photo on getNextPhoto success", async () => {
      const mockPhoto: PhotoDto = {
        id: 1,
        url: "/photos/1.jpg",
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(mockPhoto),
      } as Response);

      const result = await apiClient.getNextPhoto();
      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data).toEqual(mockPhoto);
      }
    });

    it("should return error on failure", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: () =>
          Promise.resolve({ code: "NOT_FOUND", message: "No photos" }),
      } as Response);

      const result = await apiClient.getNextPhoto();
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.code).toBe("NOT_FOUND");
      }
    });

    it("should handle preload with default count", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve([]),
      } as Response);

      await apiClient.getPreloadPhotos();
      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining("count=10"),
      );
    });
  });

  describe("resetSequence", () => {
    it("should POST to reset endpoint", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ success: true, message: "Reset" }),
      } as Response);

      const result = await apiClient.resetSequence();
      expect(result.success).toBe(true);
      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining("/display/photos/reset"),
        expect.objectContaining({ method: "POST" }),
      );
    });

    it("should handle network errors", async () => {
      fetchSpy.mockRejectedValueOnce(new Error("Network error"));

      const result = await apiClient.resetSequence();
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.code).toBe("NETWORK_ERROR");
      }
    });
  });

  describe("settings", () => {
    it("should return settings on success", async () => {
      const mockSettings: DisplaySettingsDto = {
        id: 1,
        name: "Default",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(mockSettings),
      } as Response);

      const result = await apiClient.getSettings();
      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.data.slideDuration).toBe(10);
      }
    });

    it("should retry without settingsId when settingsId is stale", async () => {
      const initialSettings: DisplaySettingsDto = {
        id: 7,
        name: "Initial",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      const refreshedSettings: DisplaySettingsDto = {
        id: 9,
        name: "Refreshed",
        slideDuration: 12,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(initialSettings),
      } as Response);

      await apiClient.getSettings();

      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ code: "NOT_FOUND", message: "Missing" }),
      } as Response);

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(refreshedSettings),
      } as Response);

      const result = await apiClient.getSettings();
      expect(result.success).toBe(true);

      expect(fetchSpy).toHaveBeenNthCalledWith(
        2,
        expect.stringContaining("settingsId=7"),
      );
      expect(fetchSpy).toHaveBeenNthCalledWith(3, "/api/display/settings");
    });

    it("should clear settingsId when settings refresh fails", async () => {
      const initialSettings: DisplaySettingsDto = {
        id: 11,
        name: "Initial",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(initialSettings),
      } as Response);

      await apiClient.getSettings();

      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ code: "NOT_FOUND", message: "Missing" }),
      } as Response);

      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ code: "NOT_FOUND", message: "Missing" }),
      } as Response);

      const result = await apiClient.getSettings();
      expect(result.success).toBe(false);

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ totalPhotos: 3 }),
      } as Response);

      await apiClient.getPhotoCount();
      expect(fetchSpy).toHaveBeenLastCalledWith(
        expect.not.stringContaining("settingsId="),
      );
    });

    it("should return sensible default settings", () => {
      const settings = apiClient.getDefaultSettings();
      expect(settings.slideDuration).toBe(10);
      expect(settings.transition).toBe(TransitionType.Fade);
      expect(settings.shuffle).toBe(true);
    });
  });

  describe("URL builders", () => {
    it("should build correct thumbnail URL", () => {
      expect(apiClient.getThumbnailUrl(123)).toBe("/api/media/thumbnails/123");
      expect(apiClient.getThumbnailUrl(456)).toBe("/api/media/thumbnails/456");
    });

    it("should return API URL for local photos and original URL for absolute URLs", () => {
      const localPhoto: PhotoDto = {
        id: 1,
        url: "/photos/test.jpg",
        mediaType: MediaType.Photo,
        width: 100,
        height: 100,
      };
      const httpPhoto: PhotoDto = {
        id: 1,
        url: "http://example.com/test.jpg",
        mediaType: MediaType.Photo,
        width: 100,
        height: 100,
      };
      const httpsPhoto: PhotoDto = {
        id: 1,
        url: "https://cdn.example.com/test.jpg",
        mediaType: MediaType.Photo,
        width: 100,
        height: 100,
      };

      expect(apiClient.getPhotoUrl(localPhoto)).toBe("/api/media/photos/1");
      expect(apiClient.getPhotoUrl(httpPhoto)).toBe(
        "http://example.com/test.jpg",
      );
      expect(apiClient.getPhotoUrl(httpsPhoto)).toBe(
        "https://cdn.example.com/test.jpg",
      );
    });
  });

  describe("retry logic", () => {
    it("should retry on 5xx errors and succeed", async () => {
      fetchSpy
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: () => Promise.resolve({ code: "ERROR" }),
        } as Response)
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: () => Promise.resolve({ code: "ERROR" }),
        } as Response)
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve({ totalPhotos: 100 }),
        } as Response);

      const result = await apiClient.getPhotoCount();
      expect(result.success).toBe(true);
      expect(fetchSpy).toHaveBeenCalledTimes(3);
    });

    it("should not retry on 4xx errors", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ code: "NOT_FOUND" }),
      } as Response);

      const result = await apiClient.getPhotoCount();
      expect(result.success).toBe(false);
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    it("should retry on network errors and succeed", async () => {
      fetchSpy
        .mockRejectedValueOnce(new Error("Network error"))
        .mockRejectedValueOnce(new Error("Network error"))
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve({ totalPhotos: 100 }),
        } as Response);

      const result = await apiClient.getPhotoCount();
      expect(result.success).toBe(true);
      expect(fetchSpy).toHaveBeenCalledTimes(3);
    });

    it("should give up after max retries", async () => {
      fetchSpy.mockRejectedValue(new Error("Network error"));

      const result = await apiClient.getPhotoCount();
      expect(result.success).toBe(false);
      expect(fetchSpy).toHaveBeenCalledTimes(4); // Initial + 3 retries
    });
  });

  describe("preload with images", () => {
    it("should return empty array when preload fails", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: () => Promise.resolve({ code: "ERROR" }),
      } as Response);

      const result = await apiClient.preloadPhotosWithImages();
      expect(result).toEqual([]);
    });

    it("should preload multiple photos and filter failed ones", async () => {
      const mockPhotos: PhotoDto[] = [
        {
          id: 1,
          url: "/photos/1.jpg",
          mediaType: MediaType.Photo,
          width: 100,
          height: 100,
        },
        {
          id: 2,
          url: "/photos/2.jpg",
          mediaType: MediaType.Photo,
          width: 100,
          height: 100,
        },
      ];

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(mockPhotos),
      } as Response);

      // Mock image preloading - first succeeds, second fails
      vi.spyOn(apiClient, "preloadImage")
        .mockResolvedValueOnce(undefined)
        .mockRejectedValueOnce(new Error("Failed to load"));

      const result = await apiClient.preloadPhotosWithImages(2);
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe(1);
    });
  });

  describe("settings polling", () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      apiClient.stopSettingsPolling();
      vi.useRealTimers();
    });

    it("should not call callback if settings unchanged", async () => {
      const mockSettings: DisplaySettingsDto = {
        id: 1,
        name: "Settings",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      fetchSpy.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSettings),
      } as Response);

      await apiClient.getSettings();

      const callback = vi.fn();
      apiClient.onSettingsChange(callback);
      apiClient.startSettingsPolling();

      await vi.advanceTimersByTimeAsync(60000);

      expect(callback).not.toHaveBeenCalled();
    });

    it("should allow unregistering callbacks", async () => {
      const mockSettings1: DisplaySettingsDto = {
        id: 1,
        name: "Settings 1",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      const mockSettings2 = { ...mockSettings1, slideDuration: 20 };

      fetchSpy
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve(mockSettings1),
        } as Response)
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve(mockSettings2),
        } as Response);

      await apiClient.getSettings();

      const callback = vi.fn();
      const unregister = apiClient.onSettingsChange(callback);
      unregister();

      apiClient.startSettingsPolling();
      await vi.advanceTimersByTimeAsync(60000);

      expect(callback).not.toHaveBeenCalled();
    });

    it("should not start polling twice", () => {
      apiClient.startSettingsPolling();
      apiClient.startSettingsPolling();

      // Should only have one timer
      expect(vi.getTimerCount()).toBe(1);
    });

    it("should stop polling when requested", () => {
      apiClient.startSettingsPolling();
      expect(vi.getTimerCount()).toBe(1);

      apiClient.stopSettingsPolling();
      expect(vi.getTimerCount()).toBe(0);
    });

    it("should continue polling on fetch errors", async () => {
      fetchSpy.mockRejectedValue(new Error("Network error"));

      apiClient.startSettingsPolling();

      // Should not throw when poll fails
      await expect(vi.advanceTimersByTimeAsync(60000)).resolves.not.toThrow();

      // Timer should still be running (count > 0)
      expect(vi.getTimerCount()).toBeGreaterThan(0);
    });
  });

  describe("image preloading", () => {
    it("should preload multiple images in parallel", async () => {
      const urls = ["/img1.jpg", "/img2.jpg", "/img3.jpg"];
      const preloadSpy = vi.spyOn(apiClient, "preloadImage");
      preloadSpy.mockResolvedValue(undefined);

      await apiClient.preloadImages(urls);

      expect(preloadSpy).toHaveBeenCalledTimes(3);
      urls.forEach((url) => {
        expect(preloadSpy).toHaveBeenCalledWith(url);
      });
    });

    it("should handle failures gracefully when preloading multiple images", async () => {
      const urls = ["/img1.jpg", "/img2.jpg"];
      const preloadSpy = vi.spyOn(apiClient, "preloadImage");
      preloadSpy
        .mockResolvedValueOnce(undefined)
        .mockRejectedValueOnce(new Error("Failed"));

      // Should not throw even if some images fail
      await expect(apiClient.preloadImages(urls)).resolves.toBeUndefined();
    });
  });

  describe("settingsId tracking", () => {
    it("should include settingsId in URL after loading settings", async () => {
      const mockSettings: DisplaySettingsDto = {
        id: 42,
        name: "Test",
        slideDuration: 10,
        transition: TransitionType.Fade,
        transitionDuration: 1000,
        sourceType: SourceType.All,
        shuffle: true,
      };

      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(mockSettings),
      } as Response);

      await apiClient.getSettings();

      // Next request should include settingsId
      fetchSpy.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve([]),
      } as Response);

      await apiClient.getPreloadPhotos();
      expect(fetchSpy).toHaveBeenLastCalledWith(
        expect.stringContaining("settingsId=42"),
      );
    });
  });

  describe("error handling edge cases", () => {
    it("should handle malformed JSON in error responses", async () => {
      fetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: "Internal Server Error",
        json: () => Promise.reject(new Error("Invalid JSON")),
      } as Response);

      const result = await apiClient.getNextPhoto();
      expect(result.success).toBe(false);
      // When JSON parsing fails, fetch will retry and eventually treat as network error
      if (!result.success) {
        expect(result.error.code).toMatch(/HTTP_500|NETWORK_ERROR/);
      }
    });

    it("should reset retry count on successful request", async () => {
      // First request fails multiple times then succeeds
      fetchSpy
        .mockRejectedValueOnce(new Error("Network error"))
        .mockRejectedValueOnce(new Error("Network error"))
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve({ totalPhotos: 10 }),
        } as Response);

      const result1 = await apiClient.getPhotoCount();
      expect(result1.success).toBe(true);
      expect(fetchSpy).toHaveBeenCalledTimes(3);

      fetchSpy.mockClear();

      // Second request should start with fresh retry count
      fetchSpy
        .mockRejectedValueOnce(new Error("Network error"))
        .mockResolvedValueOnce({
          ok: true,
          json: () => Promise.resolve({ totalPhotos: 10 }),
        } as Response);

      const result2 = await apiClient.getPhotoCount();
      expect(result2.success).toBe(true);
      expect(fetchSpy).toHaveBeenCalledTimes(2);
    });
  });
});
