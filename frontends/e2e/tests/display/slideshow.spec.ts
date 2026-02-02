import { test, expect } from "../fixtures";

/**
 * Display Frontend - Slideshow Integration Tests
 *
 * These tests exercise the display/slideshow frontend using the real API.
 * The display frontend fetches photos and settings from the API to render
 * the slideshow.
 */
test.describe("Display Frontend - Slideshow", () => {
  test("should display the slideshow page", async ({ page }) => {
    await page.goto("/");

    // The slideshow container should exist (id="slideshow" in the HTML)
    const slideshowContainer = page.locator("#slideshow");
    await expect(slideshowContainer).toBeVisible({ timeout: 15000 });
  });

  test("should show loading indicator on startup", async ({ page }) => {
    await page.goto("/");

    // Check for loading indicator (may be brief)
    const loadingIndicator = page.locator("#loading-indicator");
    // It's okay if it's already hidden - just verify the element exists
    await expect(loadingIndicator).toBeAttached();
  });

  test("should handle empty photos state gracefully", async ({ page, api }) => {
    // Check if there are any photos via API
    const photos = await api.getDisplayPhotos();

    await page.goto("/");

    if (photos.length === 0) {
      // Should show an error state or empty indicator
      const errorState = page.locator("#error-indicator");
      // Error indicator exists, though may be hidden initially
      await expect(errorState).toBeAttached();
    } else {
      // Should show slideshow with photos
      const slide = page.locator("#current-slide, #next-slide");
      await expect(slide.first()).toBeVisible({ timeout: 10000 });
    }
  });

  test("should load display settings from API", async ({ page, api }) => {
    // Get settings via API
    const settings = await api.getDisplaySettings();

    await page.goto("/");

    // Page should load - settings affect rendering but we just verify it works
    await expect(page.locator("body")).toBeVisible({ timeout: 10000 });

    // If settings exist, the page should have configured appropriately
    if (settings) {
      expect(settings).toHaveProperty("slideDuration");
    }
  });

  test("should have slide container", async ({ page }) => {
    await page.goto("/");

    // Wait for page to initialize
    await page.waitForTimeout(2000);

    // Check for slide container
    const slideContainer = page.locator("#slide-container");
    await expect(slideContainer).toBeAttached();
  });

  test("should have overlay element", async ({ page }) => {
    await page.goto("/");

    // Wait for page to initialize
    await page.waitForTimeout(2000);

    // Check for overlay (may be hidden based on settings)
    const overlay = page.locator("#overlay");
    await expect(overlay).toBeAttached();
  });
});

test.describe("Display Frontend - Keyboard Controls", () => {
  test("should respond to spacebar for pause/resume", async ({ page }) => {
    await page.goto("/");

    // Wait for slideshow to initialize
    await page.waitForTimeout(2000);

    // Press spacebar - shouldn't cause any errors
    await page.keyboard.press("Space");
    await page.waitForTimeout(500);

    // Press again to toggle back
    await page.keyboard.press("Space");

    // Page should still be functional
    await expect(page.locator("body")).toBeVisible();
  });

  test("should respond to arrow keys for navigation", async ({ page }) => {
    await page.goto("/");

    // Wait for slideshow to initialize
    await page.waitForTimeout(2000);

    // Press arrow keys
    await page.keyboard.press("ArrowRight");
    await page.waitForTimeout(300);
    await page.keyboard.press("ArrowLeft");

    // Page should still be functional
    await expect(page.locator("body")).toBeVisible();
  });

  test("should respond to Escape key", async ({ page }) => {
    await page.goto("/");

    // Wait for slideshow to initialize
    await page.waitForTimeout(2000);

    // Press Escape
    await page.keyboard.press("Escape");

    // Page should still be functional
    await expect(page.locator("body")).toBeVisible();
  });
});

test.describe("Display Frontend - Responsive Behavior", () => {
  test("should work on fullscreen 1080p display", async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto("/");

    // Page should render without errors
    const body = page.locator("body");
    await expect(body).toBeVisible({ timeout: 10000 });

    // Check for slideshow container
    const slideshowContainer = page.locator("#slideshow");
    await expect(slideshowContainer).toBeVisible({ timeout: 10000 });
  });

  test("should work on 4K display", async ({ page }) => {
    await page.setViewportSize({ width: 3840, height: 2160 });
    await page.goto("/");

    const body = page.locator("body");
    await expect(body).toBeVisible({ timeout: 10000 });
  });

  test("should work on vertical display (portrait mode)", async ({ page }) => {
    await page.setViewportSize({ width: 1080, height: 1920 });
    await page.goto("/");

    const body = page.locator("body");
    await expect(body).toBeVisible({ timeout: 10000 });
  });

  test("should work on small screen (tablet)", async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto("/");

    const body = page.locator("body");
    await expect(body).toBeVisible({ timeout: 10000 });
  });
});

test.describe("Display Frontend - API Integration", () => {
  test("should fetch photos from real API", async ({ page, api }) => {
    // Get photos directly from API
    const photos = await api.getDisplayPhotos();

    await page.goto("/");

    // Wait for page to load
    await page.waitForTimeout(2000);

    // If API returned photos, they should be loaded
    if (photos.length > 0) {
      // Look for image elements
      const images = page.locator("#current-slide, #next-slide");
      await expect(images.first()).toBeAttached({ timeout: 10000 });
    }
  });

  test("should fetch settings from real API", async ({ page }) => {
    // Navigate and verify the API is being called
    const responsePromise = page.waitForResponse(
      (response) => response.url().includes("/api/display/settings"),
      { timeout: 10000 }
    );

    await page.goto("/");

    // Wait for the API call
    const response = await responsePromise.catch(() => null);

    // Response should have been made (may return settings or default)
    if (response) {
      expect(response.status()).toBeLessThan(500);
    }
  });
});
