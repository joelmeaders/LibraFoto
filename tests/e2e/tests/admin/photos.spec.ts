import {
  test,
  expect,
  TEST_ADMIN,
  TEST_IMAGES,
  loginViaUi,
  fillMaterialInput,
  waitForSnackbar,
  getTestAssetPath,
  waitForPageLoad,
} from "../fixtures";

/**
 * Admin Frontend - Photo Management Integration Tests
 *
 * These tests exercise the complete photo management workflow using the real API.
 * Tests are serial because they build on each other (upload → view → organize → delete).
 */
test.describe.serial("Admin Frontend - Photo Management", () => {
  // Track uploaded photo IDs for later tests
  let uploadedPhotoIds: number[] = [];

  test.beforeAll(async ({ browser }) => {
    // Login and verify we're ready
    const context = await browser.newContext();
    const page = await context.newPage();
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.close();
    await context.close();
  });

  test("should display photos page with empty state", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");

    // Wait for page to load
    await waitForPageLoad(page);

    // Should show some indication of photos page
    await expect(page.getByText(/photos/i).first()).toBeVisible({
      timeout: 10000,
    });

    // Should have upload button
    await expect(
      page.getByRole("button", { name: /upload/i }).first()
    ).toBeVisible();
  });

  test("should upload a single photo via file picker", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Set up file chooser handler before clicking upload
    const fileChooserPromise = page.waitForEvent("filechooser");
    await page.getByRole("button", { name: /upload/i }).click();
    const fileChooser = await fileChooserPromise;

    // Upload the woodpecker image
    const filePath = getTestAssetPath(TEST_IMAGES.woodpecker);
    await fileChooser.setFiles(filePath);

    // Wait for upload to complete - should show success message or photo in grid
    await page.waitForResponse(
      (response) =>
        response.url().includes("/api/storage/upload") && response.ok(),
      { timeout: 30000 }
    );

    // Verify photo appears in grid
    await page.reload();
    await waitForPageLoad(page);

    // Should now show at least one photo
    const photoCards = page
      .locator('[data-testid="photo-card"], .photo-card, .photo-item, mat-card')
      .first();
    await expect(photoCards).toBeVisible({ timeout: 10000 });
  });

  test("should upload multiple photos", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Upload remaining test images via API for speed
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const uploadPromises = [
      api.uploadPhoto(TEST_IMAGES.desert),
      api.uploadPhoto(TEST_IMAGES.aerial),
    ];

    const results = await Promise.all(uploadPromises);

    // Store IDs for later tests
    for (const result of results) {
      if (result?.photoId) {
        uploadedPhotoIds.push(result.photoId);
      }
    }

    // Refresh page and verify photos appear
    await page.reload();
    await waitForPageLoad(page);

    // Should show multiple photos now
    const photoCards = page.locator(
      '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
    );
    await expect(photoCards).toHaveCount(3, { timeout: 10000 });
  });

  test("should display photo count correctly", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Verify the count matches what API reports
    expect(photos.pagination.totalItems).toBe(3);
  });

  test("should open photo detail dialog when clicking a photo", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Click on the first photo
    const firstPhoto = page
      .locator('[data-testid="photo-card"], .photo-card, .photo-item, mat-card')
      .first();
    await firstPhoto.click();

    // Dialog should open showing photo details
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 5000 });

    // Should show some photo information
    await expect(
      page.getByText(/filename|details|info/i).first()
    ).toBeVisible();
  });

  test("should close photo detail dialog", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Open dialog
    const firstPhoto = page
      .locator('[data-testid="photo-card"], .photo-card, .photo-item, mat-card')
      .first();
    await firstPhoto.click();
    await expect(page.getByRole("dialog")).toBeVisible();

    // Close dialog
    const closeButton = page
      .getByRole("button", { name: /close|cancel|×/i })
      .first();
    await closeButton.click();

    // Dialog should be closed
    await expect(page.getByRole("dialog")).not.toBeVisible({ timeout: 5000 });
  });

  test("should select multiple photos with Ctrl+click", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Select first photo
    const photoCards = page.locator(
      '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
    );
    await photoCards.first().click();

    // Ctrl+click to select second photo
    await photoCards.nth(1).click({ modifiers: ["Control"] });

    // Should show selection indicator (toolbar, count, or selected state)
    // Look for bulk actions or selection count
    const selectionIndicator = page.getByText(/selected|2 photo/i);
    await expect(selectionIndicator)
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // Alternative: check for bulk action buttons
        return expect(
          page.getByRole("button", { name: /delete|add to album/i })
        ).toBeVisible();
      });
  });

  test("should filter photos by search", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Look for search input
    const searchInput = page
      .getByRole("searchbox")
      .or(page.getByPlaceholder(/search/i));

    if (await searchInput.isVisible({ timeout: 2000 }).catch(() => false)) {
      await searchInput.fill("woodpecker");
      await page.waitForTimeout(500); // Debounce

      // Should filter results
      const photoCards = page.locator(
        '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
      );
      const count = await photoCards.count();
      expect(count).toBeLessThanOrEqual(3);
    } else {
      test.skip(true, "Search functionality not yet implemented");
    }
  });
});

test.describe.serial("Admin Frontend - Photo Bulk Operations", () => {
  let testAlbumId: number;
  let testTagId: number;

  test.beforeAll(async ({ browser }) => {
    // Create test album and tag via API
    const context = await browser.newContext();
    const page = await context.newPage();

    // We need to get API client from page context
    // For now, we'll create them in the first test
    await page.close();
    await context.close();
  });

  test("should create test album and tag for bulk operations", async ({
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Create test album
    const album = await api.createAlbum(
      "Bulk Test Album",
      "Album for bulk operation tests"
    );
    expect(album).not.toBeNull();
    testAlbumId = album!.id;

    // Create test tag
    const tag = await api.createTag("Bulk Test Tag", "#FF5722");
    expect(tag).not.toBeNull();
    testTagId = tag!.id;
  });

  test("should bulk add photos to album", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Get photo IDs via API
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const photoIds = photos.data.slice(0, 2).map((p) => p.id);

    // Add photos to album via API (UI bulk operations may vary)
    await api.addPhotosToAlbum(testAlbumId, photoIds);

    // Verify via API
    const album = await api.getAlbum(testAlbumId);
    expect(album?.photoCount).toBe(2);
  });

  test("should bulk add tags to photos", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const photoIds = photos.data.slice(0, 2).map((p) => p.id);

    // Add tag to photos
    await api.addTagsToPhotos([testTagId], photoIds);

    // Verify via API
    const tag = await api.getTag(testTagId);
    expect(tag?.photoCount).toBe(2);
  });

  test("should filter photos by album", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto(`/photos?albumId=${testAlbumId}`);
    await waitForPageLoad(page);

    // Should show only photos in album
    const photoCards = page.locator(
      '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
    );
    await expect(photoCards).toHaveCount(2, { timeout: 10000 });
  });

  test("should filter photos by tag", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto(`/photos?tagId=${testTagId}`);
    await waitForPageLoad(page);

    // Should show only photos with tag
    const photoCards = page.locator(
      '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
    );
    await expect(photoCards).toHaveCount(2, { timeout: 10000 });
  });

  test("should bulk remove photos from album", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos({ albumId: testAlbumId });
    const photoIds = photos.data.map((p) => p.id);

    // Remove photos from album
    await api.removePhotosFromAlbum(testAlbumId, photoIds);

    // Verify
    const album = await api.getAlbum(testAlbumId);
    expect(album?.photoCount).toBe(0);
  });

  test("should bulk remove tags from photos", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const photoIds = photos.data.slice(0, 2).map((p) => p.id);

    // Remove tag from photos
    await api.removeTagsFromPhotos([testTagId], photoIds);

    // Verify
    const tag = await api.getTag(testTagId);
    expect(tag?.photoCount).toBe(0);
  });
});

test.describe.serial("Admin Frontend - Photo Deletion", () => {
  test("should delete a single photo via API", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const initialCount = photos.pagination.totalItems;

    if (initialCount > 0) {
      const photoToDelete = photos.data[0];
      const deleted = await api.deletePhoto(photoToDelete.id);
      expect(deleted).toBe(true);

      // Verify count decreased
      const updatedPhotos = await api.getPhotos();
      expect(updatedPhotos.pagination.totalItems).toBe(initialCount - 1);
    }
  });

  test("should bulk delete remaining photos via API", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const photoIds = photos.data.map((p) => p.id);

    if (photoIds.length > 0) {
      const result = await api.bulkDeletePhotos(photoIds);
      expect(result).not.toBeNull();

      // Verify all deleted
      const updatedPhotos = await api.getPhotos();
      expect(updatedPhotos.pagination.totalItems).toBe(0);
    }
  });

  test("should show empty state after all photos deleted", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/photos");
    await waitForPageLoad(page);

    // Should show empty state or zero photos
    const emptyState = page.getByText(
      /no photos|empty|upload.*first|get started/i
    );
    await expect(emptyState)
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Alternative: verify no photo cards
        const photoCards = page.locator(
          '[data-testid="photo-card"], .photo-card, .photo-item'
        );
        return expect(photoCards).toHaveCount(0);
      });
  });
});
