/**
 * Unit tests for slideshow.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { JSDOM } from "jsdom";
import type { PhotoDto, DisplaySettingsDto } from "./types";
import { MediaType, TransitionType, SourceType } from "./types";

// Mock config module
vi.mock("./config", () => ({
  getConfig: vi.fn(() => ({
    apiBaseUrl: "/api",
    debug: false,
    settingsPollingInterval: 60000,
    preloadCount: 10,
    preloadThreshold: 3,
    retryDelay: 100,
    maxRetries: 3,
    maxVideoDuration: 30,
  })),
  logger: { debug: vi.fn(), info: vi.fn(), warn: vi.fn(), error: vi.fn() },
}));

// Mock qr-code module
vi.mock("./qr-code", () => ({
  generateQrCodeDataUrl: vi.fn(() =>
    Promise.resolve("data:image/png;base64,mockqrcode"),
  ),
}));

function setupDOM() {
  const dom = new JSDOM(`
    <!DOCTYPE html>
    <html>
      <body>
        <img id="current-slide" src="" />
        <img id="next-slide" src="" class="hidden" />
        <div id="video-container" class="hidden"></div>
        <div id="loading-indicator" class="hidden">Loading...</div>
        <div id="error-indicator" class="hidden"></div>
        <div id="qr-overlay" class="hidden"></div>
        <div id="overlay">
          <div id="overlay-date"></div>
          <div id="overlay-time"></div>
          <div id="overlay-location"></div>
        </div>
      </body>
    </html>
  `);
  return dom.window.document;
}

function createTestSettings(
  overrides: Partial<DisplaySettingsDto> = {},
): DisplaySettingsDto {
  return {
    id: 1,
    name: "Test",
    slideDuration: 10,
    transition: TransitionType.Fade,
    transitionDuration: 1000,
    sourceType: SourceType.All,
    shuffle: true,
    ...overrides,
  };
}

function createMockApiClient() {
  return {
    getSettings: vi.fn(),
    getDefaultSettings: vi.fn(() => createTestSettings()),
    getPhotoCount: vi.fn(),
    getNextPhoto: vi.fn(),
    preloadPhotosWithImages: vi.fn(),
    startSettingsPolling: vi.fn(),
    stopSettingsPolling: vi.fn(),
    onSettingsChange: vi.fn(() => () => {}),
    getPhotoUrl: vi.fn((photo: PhotoDto) => `/api/media/photos/${photo.id}`),
    preloadImage: vi.fn(),
    getDisplayConfig: vi.fn(() =>
      Promise.resolve({
        success: true,
        data: { adminUrl: "http://localhost:4200" },
      }),
    ),
  };
}

describe("Slideshow", () => {
  let document: Document;
  let Slideshow: typeof import("./slideshow").Slideshow;
  let mockApiClient: ReturnType<typeof createMockApiClient>;

  beforeEach(async () => {
    document = setupDOM();
    vi.stubGlobal("document", document);
    vi.resetModules();
    mockApiClient = createMockApiClient();
    const slideshowModule = await import("./slideshow");
    Slideshow = slideshowModule.Slideshow;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  describe("initialization", () => {
    it("should register for settings changes and start in unpaused state", () => {
      const slideshow = new Slideshow(mockApiClient as any);
      expect(mockApiClient.onSettingsChange).toHaveBeenCalled();
      expect(slideshow.isPaused()).toBe(false);
      expect(slideshow.getCurrentPhoto()).toBeNull();
    });
  });

  describe("pause/resume", () => {
    it("should toggle pause state correctly", () => {
      const slideshow = new Slideshow(mockApiClient as any);
      expect(slideshow.isPaused()).toBe(false);

      slideshow.pause();
      expect(slideshow.isPaused()).toBe(true);

      slideshow.resume();
      expect(slideshow.isPaused()).toBe(false);
    });
  });

  describe("stop", () => {
    it("should stop settings polling", () => {
      const slideshow = new Slideshow(mockApiClient as any);
      slideshow.stop();
      expect(mockApiClient.stopSettingsPolling).toHaveBeenCalled();
    });
  });

  describe("start", () => {
    it("should load settings from API or use defaults", async () => {
      const settings = createTestSettings();
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: settings,
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 0 },
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      expect(mockApiClient.getSettings).toHaveBeenCalled();
    });

    it("should use default settings when API fails", async () => {
      mockApiClient.getSettings.mockResolvedValue({
        success: false,
        error: { code: "ERROR" },
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 0 },
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      expect(mockApiClient.getDefaultSettings).toHaveBeenCalled();
    });

    it("should show error with QR code when no photos available", async () => {
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 0 },
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const errorIndicator = document.getElementById("error-indicator");
      expect(errorIndicator?.classList.contains("hidden")).toBe(false);
      expect(errorIndicator?.textContent).toContain("No photos");
      // Check for QR code elements
      expect(errorIndicator?.querySelector(".error-qr-code")).toBeTruthy();
      expect(errorIndicator?.querySelector(".error-qr-label")).toBeTruthy();
      expect(errorIndicator?.querySelector(".error-qr-url")?.textContent).toBe(
        "http://localhost:4200",
      );
    });

    it("should show error when preload fails", async () => {
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([]);

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const errorIndicator = document.getElementById("error-indicator");
      expect(errorIndicator?.classList.contains("hidden")).toBe(false);
    });

    it("should show QR overlay for 30 seconds when photos exist", async () => {
      const testPhoto: PhotoDto = {
        id: 1,
        fileName: "test.jpg",
        takenAt: new Date().toISOString(),
        mediaType: MediaType.Image,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const qrOverlay = document.getElementById("qr-overlay");
      // QR overlay should be visible (not hidden)
      expect(qrOverlay?.classList.contains("hidden")).toBe(false);
      // Should contain QR image and label
      expect(qrOverlay?.querySelector(".qr-image")).toBeTruthy();
      expect(qrOverlay?.querySelector(".qr-label")).toBeTruthy();
      expect(qrOverlay?.querySelector(".qr-url")?.textContent).toBe(
        "http://localhost:4200",
      );
      // Should show countdown (may have ticked down by 1 second)
      const countdownText =
        qrOverlay?.querySelector(".qr-countdown")?.textContent ?? "";
      expect(countdownText).toMatch(/Hiding in \d+s/);
    });

    it("should hide QR overlay after countdown even if slideshow is paused and resumed", async () => {
      // Use fake timers from the beginning, but allow promises to resolve
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        fileName: "test.jpg",
        takenAt: new Date().toISOString(),
        mediaType: MediaType.Image,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const qrOverlay = document.getElementById("qr-overlay");
      // QR overlay should be visible initially
      expect(qrOverlay?.classList.contains("hidden")).toBe(false);

      // Pause and resume the slideshow (simulating tab change)
      slideshow.pause();
      slideshow.resume();

      // QR overlay should still be visible
      expect(qrOverlay?.classList.contains("hidden")).toBe(false);

      // Advance time past the QR overlay duration (30 seconds + fade animation)
      await vi.advanceTimersByTimeAsync(30500);

      // QR overlay should now be hidden
      expect(qrOverlay?.classList.contains("hidden")).toBe(true);

      vi.useRealTimers();
    });

    it("should refresh settings during no-photos polling", async () => {
      vi.useFakeTimers();

      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount
        .mockResolvedValueOnce({
          success: true,
          data: { totalPhotos: 0 },
        })
        .mockResolvedValueOnce({
          success: true,
          data: { totalPhotos: 0 },
        });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      expect(mockApiClient.getSettings).toHaveBeenCalledTimes(1);

      await vi.advanceTimersByTimeAsync(10000);

      expect(mockApiClient.getSettings).toHaveBeenCalledTimes(2);
      expect(mockApiClient.getPhotoCount).toHaveBeenCalledTimes(2);

      vi.useRealTimers();
    });

    it("should restart slideshow when photos become available during polling", async () => {
      vi.useFakeTimers();

      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings(),
      });
      mockApiClient.getPhotoCount
        .mockResolvedValueOnce({
          success: true,
          data: { totalPhotos: 0 },
        })
        .mockResolvedValueOnce({
          success: true,
          data: { totalPhotos: 3 },
        });

      const slideshow = new Slideshow(mockApiClient as any);
      const originalStart = slideshow.start.bind(slideshow);
      const startSpy = vi
        .spyOn(slideshow, "start")
        .mockImplementationOnce(originalStart)
        .mockImplementation(async () => undefined);

      await slideshow.start();

      await vi.advanceTimersByTimeAsync(10000);

      expect(startSpy).toHaveBeenCalledTimes(2);
      expect(mockApiClient.getPhotoCount).toHaveBeenCalledTimes(2);

      vi.useRealTimers();
    });
  });

  describe("settings change", () => {
    it("should handle settings change callback", () => {
      let settingsCallback: ((settings: DisplaySettingsDto) => void) | null =
        null;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });

      new Slideshow(mockApiClient as any);
      const newSettings = createTestSettings({ slideDuration: 20 });
      settingsCallback?.(newSettings);

      // Settings callback should be registered
      expect(mockApiClient.onSettingsChange).toHaveBeenCalled();
    });

    it("should clear preload queue when source changes", () => {
      let settingsCallback: ((settings: DisplaySettingsDto) => void) | null =
        null;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([]);

      new Slideshow(mockApiClient as any);
      const oldSettings = createTestSettings({
        sourceType: SourceType.All,
      });
      settingsCallback?.(oldSettings);

      mockApiClient.preloadPhotosWithImages.mockClear();

      // Change source
      const newSettings = createTestSettings({
        sourceType: SourceType.Album,
        sourceId: 5,
      });
      settingsCallback?.(newSettings);

      expect(mockApiClient.preloadPhotosWithImages).toHaveBeenCalled();
    });
  });
});
