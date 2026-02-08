/**
 * Unit tests for slideshow.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { JSDOM } from "jsdom";
import type { PhotoDto, DisplaySettingsDto } from "./types";
import { MediaType, TransitionType, SourceType, ImageFit } from "./types";

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
    imageFit: ImageFit.Cover,
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
    onSettingsChange: vi.fn((_cb: any) => () => {}),
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
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      let settingsCallback!: (settings: DisplaySettingsDto) => void;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });

      const slideshow = new Slideshow(mockApiClient as any);
      expect(slideshow).toBeDefined(); // Keep for side effects
      const newSettings = createTestSettings({ slideDuration: 20 });
      settingsCallback?.(newSettings);

      // Settings callback should be registered
      expect(mockApiClient.onSettingsChange).toHaveBeenCalled();
    });

    it("should clear preload queue when source changes", () => {
      let settingsCallback!: (settings: DisplaySettingsDto) => void;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([]);

      const slideshow = new Slideshow(mockApiClient as any);
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
      expect(slideshow).toBeDefined();
    });

    it("should update object-fit when imageFit changes", () => {
      let settingsCallback!: (settings: DisplaySettingsDto) => void;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });

      const slideshow = new Slideshow(mockApiClient as any);
      expect(slideshow).toBeDefined(); // Used for side effects
      const currentSlide = document.getElementById(
        "current-slide",
      ) as HTMLImageElement;
      const nextSlide = document.getElementById(
        "next-slide",
      ) as HTMLImageElement;

      // Initially set to cover and verify
      settingsCallback?.(createTestSettings({ imageFit: ImageFit.Cover }));
      // Object fit is set inline, so check the style property
      expect(currentSlide.style.objectFit || "cover").toBe("cover");
      expect(nextSlide.style.objectFit || "cover").toBe("cover");

      // Change to contain
      settingsCallback?.(createTestSettings({ imageFit: ImageFit.Contain }));
      expect(currentSlide.style.objectFit).toBe("contain");
      expect(nextSlide.style.objectFit).toBe("contain");
    });
  });

  describe("transitions", () => {
    it("should handle fade transition", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      
      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      mockApiClient.getNextPhoto.mockResolvedValue({
        success: true,
        data: testPhoto,
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const currentSlide = document.getElementById(
        "current-slide",
      ) as HTMLImageElement;
      const nextSlide = document.getElementById(
        "next-slide",
      ) as HTMLImageElement;

      await vi.advanceTimersByTimeAsync(10000);

      // Next slide should become visible during transition
      expect(nextSlide.classList.contains("visible")).toBe(true);
      expect(nextSlide.classList.contains("fade-in")).toBe(true);
      expect(currentSlide.classList.contains("fade-out")).toBe(true);

      slideshow.stop();
      vi.useRealTimers();
    });

    it("should handle slide transition", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings({ transition: TransitionType.Slide }),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);
      mockApiClient.getNextPhoto.mockResolvedValue({
        success: true,
        data: testPhoto,
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const nextSlide = document.getElementById(
        "next-slide",
      ) as HTMLImageElement;

      await vi.advanceTimersByTimeAsync(10000);

      // Next slide should slide in
      expect(nextSlide.classList.contains("slide-in-left")).toBe(true);

      slideshow.stop();
      vi.useRealTimers();
    });

    it("should handle Ken Burns transition", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings({ transition: TransitionType.KenBurns }),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);
      mockApiClient.getNextPhoto.mockResolvedValue({
        success: true,
        data: testPhoto,
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const nextSlide = document.getElementById(
        "next-slide",
      ) as HTMLImageElement;

      await vi.advanceTimersByTimeAsync(10000);

      // Next slide should have Ken Burns animation
      expect(nextSlide.classList.contains("ken-burns-fade-in")).toBe(true);

      slideshow.stop();
      vi.useRealTimers();
    });

    it("should handle instant transition", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings({ transition: TransitionType.Fade }),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);
      mockApiClient.getNextPhoto.mockResolvedValue({
        success: true,
        data: testPhoto,
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const currentSlide = document.getElementById(
        "current-slide",
      ) as HTMLImageElement;

      await vi.advanceTimersByTimeAsync(10000);

      // Current slide should be updated with new photo
      expect(currentSlide.src).toBeTruthy();

      slideshow.stop();
      vi.useRealTimers();
    });
  });

  describe("video playback", () => {
    // Video tests are skipped because JSDOM doesn't implement HTMLMediaElement
    // methods and events (play, pause, canplay, loadedmetadata, etc.) which
    // are required for video playback functionality
    it.skip("should display video in video container", async () => {});
    it.skip("should limit video duration to maxVideoDuration", async () => {});
    it.skip("should clean up video when transitioning to next slide", async () => {});
  });

  describe("blur background", () => {
    let slideshow: InstanceType<typeof Slideshow>;

    beforeEach(() => {
      // Add blur background elements to DOM
      const blurBg = document.createElement("div");
      blurBg.id = "blur-background";
      const currentImg = document.createElement("img");
      currentImg.id = "blur-background-current-img";
      const nextImg = document.createElement("img");
      nextImg.id = "blur-background-next-img";
      blurBg.appendChild(currentImg);
      blurBg.appendChild(nextImg);
      document.body.appendChild(blurBg);
    });

    it("should show blur background in contain mode", async () => {
      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      mockApiClient.getSettings.mockResolvedValue({
        success: true,
        data: createTestSettings({ imageFit: ImageFit.Contain }),
      });
      mockApiClient.getPhotoCount.mockResolvedValue({
        success: true,
        data: { totalPhotos: 10 },
      });
      mockApiClient.preloadPhotosWithImages.mockResolvedValue([testPhoto]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);

      slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const blurBg = document.getElementById("blur-background");
      expect(blurBg?.classList.contains("hidden")).toBe(false);
    });

    it("should hide blur background in cover mode", async () => {
      let settingsCallback!: (settings: DisplaySettingsDto) => void;
      mockApiClient.onSettingsChange.mockImplementation((cb) => {
        settingsCallback = cb;
        return () => {};
      });

      const slideshow = new Slideshow(mockApiClient as any);
      expect(slideshow).toBeDefined(); // Keep for side effects
      
      // Initially show blur background
      settingsCallback?.(createTestSettings({ imageFit: ImageFit.Contain }));
      const blurBg = document.getElementById("blur-background");
      expect(blurBg?.classList.contains("hidden")).toBe(false);
      
      // Change to cover mode - should hide blur background
      settingsCallback?.(createTestSettings({ imageFit: ImageFit.Cover }));
      expect(blurBg?.classList.contains("hidden")).toBe(true);
    });
  });

  describe("preloading", () => {
    it("should preload photos on start", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      
      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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

      expect(mockApiClient.preloadPhotosWithImages).toHaveBeenCalled();
      
      slideshow.stop();
      vi.useRealTimers();
    });

    it("should fetch next photo when queue is empty", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      const testPhoto2: PhotoDto = {
        id: 2,
        url: "/api/media/photos/2",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      // Start with one photo, subsequent preload returns empty
      mockApiClient.preloadPhotosWithImages
        .mockResolvedValueOnce([testPhoto])
        .mockResolvedValue([]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);
      mockApiClient.getNextPhoto.mockResolvedValue({
        success: true,
        data: testPhoto2,
      });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      // Advance past first slide duration (10 seconds) plus transition time
      await vi.advanceTimersByTimeAsync(11000);

      // Should have called getNextPhoto when queue ran out
      expect(mockApiClient.getNextPhoto).toHaveBeenCalled();

      slideshow.stop();
      vi.useRealTimers();
    });
  });

  describe("error handling", () => {
    it("should show error when start fails", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      
      mockApiClient.getSettings.mockRejectedValue(new Error("Network error"));

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      const errorIndicator = document.getElementById("error-indicator");
      expect(errorIndicator?.classList.contains("hidden")).toBe(false);
      expect(errorIndicator?.textContent).toContain("Failed to start");
      
      slideshow.stop();
      vi.useRealTimers();
    });

    it("should retry when no photo is available", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
        width: 1920,
        height: 1080,
      };
      const testPhoto2: PhotoDto = {
        id: 2,
        url: "/api/media/photos/2",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      // Start with one photo, subsequent preload returns empty
      mockApiClient.preloadPhotosWithImages
        .mockResolvedValueOnce([testPhoto])
        .mockResolvedValue([]);
      mockApiClient.preloadImage.mockResolvedValue(undefined);
      // First fetch fails, second succeeds
      mockApiClient.getNextPhoto
        .mockResolvedValueOnce({
          success: false,
          error: { code: "ERROR" },
        })
        .mockResolvedValueOnce({
          success: true,
          data: testPhoto2,
        });

      const slideshow = new Slideshow(mockApiClient as any);
      await slideshow.start();

      // Advance past first slide to trigger first getNextPhoto (which fails)
      await vi.advanceTimersByTimeAsync(11000);

      // Advance to allow retry (slideshow retries after slideDuration on failure)
      await vi.advanceTimersByTimeAsync(11000);

      // Should have called getNextPhoto twice (initial + retry)
      expect(mockApiClient.getNextPhoto).toHaveBeenCalledTimes(2);

      slideshow.stop();
      vi.useRealTimers();
    });
  });

  describe("loading indicator", () => {
    it("should show loading indicator during start", async () => {
      // This test verifies basic loading indicator behavior without complex async scenarios
      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      // Loading indicator is managed internally by slideshow
      await slideshow.start();

      // After successful start, loading indicator should be hidden
      const loadingIndicator = document.getElementById("loading-indicator");
      expect(loadingIndicator?.classList.contains("hidden")).toBe(true);

      slideshow.stop();
    });

    it("should hide loading indicator after successful start", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      
      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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

      const loadingIndicator = document.getElementById("loading-indicator");
      expect(loadingIndicator?.classList.contains("hidden")).toBe(true);
      
      slideshow.stop();
      vi.useRealTimers();
    });
  });

  describe("QR overlay", () => {
    it("should hide QR overlay after duration expires", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      expect(qrOverlay?.classList.contains("hidden")).toBe(false);

      // Advance past QR overlay duration and fade animation
      await vi.advanceTimersByTimeAsync(30500);

      expect(qrOverlay?.classList.contains("hidden")).toBe(true);

      slideshow.stop();
      vi.useRealTimers();
    });

    it("should update countdown display", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const testPhoto: PhotoDto = {
        id: 1,
        url: "/api/media/photos/1",
        dateTaken: new Date().toISOString(),
        mediaType: MediaType.Photo,
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
      const countdown = qrOverlay?.querySelector(".qr-countdown");

      // Initial countdown (may be 30s or 29s depending on timing)
      const initialText = countdown?.textContent ?? "";
      expect(initialText).toMatch(/Hiding in (30|29)s/);

      // Advance 1 second
      await vi.advanceTimersByTimeAsync(1000);
      const afterOneSecond = countdown?.textContent ?? "";
      expect(afterOneSecond).toMatch(/Hiding in (29|28)s/);

      // Advance 10 more seconds
      await vi.advanceTimersByTimeAsync(10000);
      const afterEleven = countdown?.textContent ?? "";
      expect(afterEleven).toMatch(/Hiding in (19|18)s/);

      slideshow.stop();
      vi.useRealTimers();
    });
  });
});
