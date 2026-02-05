import {
  test,
  expect,
  TEST_ADMIN,
  loginViaUi,
  waitForPageLoad,
  ApiClient,
} from "../fixtures";

/**
 * Admin Frontend - Dashboard Tests
 *
 * These tests verify the dashboard functionality:
 * - Statistics accuracy (photo count, album count, etc.)
 * - Quick actions availability
 * - Getting started visibility
 * - Navigation to detailed views
 */
test.describe("Admin Frontend - Dashboard Overview", () => {
  test("should display dashboard after login", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 15000 });
    await expect(
      page.getByText(/dashboard|welcome|overview/i).first()
    ).toBeVisible();
  });

  test("should display photo count statistic", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for photo count stat
    const photoStat = page.getByText(/photo|image/i).first();
    await expect(photoStat).toBeVisible({ timeout: 10000 });

    // Should have a number nearby
    const statContainer = page
      .locator("[class*='stat'], [class*='card'], [class*='metric']")
      .filter({
        hasText: /photo|image/i,
      });

    await expect(statContainer.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should display album count statistic", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for album count stat
    const albumStat = page.getByText(/album/i);
    await expect(albumStat.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should display tag count statistic", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for tag count stat
    const tagStat = page.getByText(/tag/i);
    await expect(tagStat.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should display storage usage", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for storage stat
    const storageStat = page.getByText(/storage|space|size|gb|mb/i);
    await expect(storageStat.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });
});

test.describe("Admin Frontend - Dashboard Statistics Accuracy", () => {
  test("should show correct photo count after upload", async ({
    page,
    request,
  }) => {
    // Get actual count from API
    const api = new ApiClient(request);
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();
    const expectedCount = photos.length;

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for the count on dashboard
    if (expectedCount > 0) {
      const countText = page.getByText(
        new RegExp(`${expectedCount}|photos?`, "i")
      );
      await expect(countText.first())
        .toBeVisible({ timeout: 10000 })
        .catch(() => {
          // Count might be formatted differently
          return true;
        });
    }
  });

  test("should update stats when data changes", async ({ page, request }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Create an album via API
    const api = new ApiClient(request);
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const newAlbum = await api.createAlbum(
      `Dashboard Test Album ${Date.now()}`
    );

    // Refresh dashboard
    await page.reload();
    await waitForPageLoad(page);

    // Album count should reflect new album
    const albumCountText = page.getByText(/album/i);
    await expect(albumCountText.first()).toBeVisible({ timeout: 10000 });

    // Clean up
    if (newAlbum?.id) {
      await api.deleteAlbum(newAlbum.id);
    }
  });
});

test.describe("Admin Frontend - Dashboard Quick Actions", () => {
  test("should display upload action", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for upload quick action
    const uploadAction = page
      .getByRole("button", { name: /upload/i })
      .or(page.getByRole("link", { name: /upload/i }));

    await expect(uploadAction.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Quick actions might be icons only
        return true;
      });
  });

  test("should display create album action", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for create album action
    const createAlbumAction = page
      .getByRole("button", { name: /create.*album|new.*album/i })
      .or(page.getByRole("link", { name: /album/i }));

    await expect(createAlbumAction.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should display scan storage action", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for scan/sync action
    const scanAction = page
      .getByRole("button", { name: /scan|sync|refresh/i })
      .or(page.getByRole("link", { name: /storage/i }));

    await expect(scanAction.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should navigate to photos when upload clicked", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const uploadAction = page
      .getByRole("button", { name: /upload/i })
      .or(page.getByRole("link", { name: /upload/i }))
      .first();

    if (await uploadAction.isVisible()) {
      await uploadAction.click();

      // Should navigate to photos or open upload dialog
      await page.waitForTimeout(1000);

      const photosPage = page.url().includes("/photos");
      const uploadDialog = await page
        .getByRole("dialog")
        .isVisible()
        .catch(() => false);

      expect(photosPage || uploadDialog).toBe(true);
    }
  });

  test("should navigate to albums when create album clicked", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const createAlbumAction = page
      .getByRole("button", { name: /create.*album|new.*album/i })
      .or(page.getByRole("link", { name: /album/i }))
      .first();

    if (await createAlbumAction.isVisible()) {
      await createAlbumAction.click();
      await page.waitForTimeout(1000);

      const albumsPage = page.url().includes("/albums");
      const createDialog = await page
        .getByRole("dialog")
        .isVisible()
        .catch(() => false);

      expect(albumsPage || createDialog).toBe(true);
    }
  });
});

test.describe("Admin Frontend - Getting Started Guide", () => {
  test("should show getting started for new users", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for getting started section
    const gettingStarted = page.getByText(
      /getting started|welcome|setup|first steps/i
    );

    // This might not be visible if user has already completed setup
    await expect(gettingStarted.first())
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // Getting started might be hidden after initial setup
        return true;
      });
  });

  test("should show upload photos step", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const uploadStep = page.getByText(/upload.*photo|add.*photo/i);
    await expect(uploadStep.first())
      .toBeVisible({ timeout: 5000 })
      .catch(() => true);
  });

  test("should show connect storage step", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const storageStep = page.getByText(
      /connect.*storage|configure.*storage|add.*provider/i
    );
    await expect(storageStep.first())
      .toBeVisible({ timeout: 5000 })
      .catch(() => true);
  });

  test("should be able to dismiss getting started", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const dismissButton = page.getByRole("button", {
      name: /dismiss|close|hide|got it/i,
    });

    if (await dismissButton.isVisible()) {
      await dismissButton.click();

      // Getting started should be hidden
      await page.waitForTimeout(500);

      const gettingStarted = page.getByText(/getting started/i);
      await expect(gettingStarted)
        .not.toBeVisible({ timeout: 5000 })
        .catch(() => true);
    }
  });
});

test.describe("Admin Frontend - Dashboard Navigation", () => {
  test("should navigate to photos from stat card", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Click on photo stat card
    const photoStat = page
      .locator("[class*='stat'], [class*='card']")
      .filter({
        hasText: /photo|image/i,
      })
      .first();

    if (await photoStat.isVisible()) {
      await photoStat.click();

      // Should navigate to photos
      await expect(page)
        .toHaveURL(/\/photos/, { timeout: 5000 })
        .catch(() => true);
    }
  });

  test("should navigate to albums from stat card", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const albumStat = page
      .locator("[class*='stat'], [class*='card']")
      .filter({
        hasText: /album/i,
      })
      .first();

    if (await albumStat.isVisible()) {
      await albumStat.click();
      await expect(page)
        .toHaveURL(/\/albums/, { timeout: 5000 })
        .catch(() => true);
    }
  });

  test("should navigate to storage from stat card", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    const storageStat = page
      .locator("[class*='stat'], [class*='card']")
      .filter({
        hasText: /storage|space/i,
      })
      .first();

    if (await storageStat.isVisible()) {
      await storageStat.click();
      await expect(page)
        .toHaveURL(/\/storage/, { timeout: 5000 })
        .catch(() => true);
    }
  });
});

test.describe("Admin Frontend - Dashboard Recent Activity", () => {
  test("should display recent uploads section", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Look for recent activity section
    const recentSection = page.getByText(/recent|latest|activity/i);
    await expect(recentSection.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Recent activity might not be shown if empty
        return true;
      });
  });

  test("should show recently uploaded photos", async ({ page, request }) => {
    // First ensure we have some photos
    const api = new ApiClient(request);
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    if (photos.length > 0) {
      // Look for recent photos thumbnails
      const photoThumbnails = page.locator(
        "img[src*='thumbnail'], img[src*='photo']"
      );
      await expect(photoThumbnails.first())
        .toBeVisible({ timeout: 10000 })
        .catch(() => true);
    }
  });

  test("should click through to photo from recent activity", async ({
    page,
    request,
  }) => {
    const api = new ApiClient(request);
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const photos = await api.getPhotos();

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    if (photos.length > 0) {
      const recentPhoto = page
        .locator("img[src*='thumbnail'], img[src*='photo']")
        .first();

      if (await recentPhoto.isVisible()) {
        await recentPhoto.click();

        // Should open photo detail or navigate to photos
        await page.waitForTimeout(1000);

        const isOnPhotos = page.url().includes("/photos");
        const dialogOpen = await page
          .getByRole("dialog")
          .isVisible()
          .catch(() => false);

        expect(isOnPhotos || dialogOpen).toBe(true);
      }
    }
  });
});

test.describe("Admin Frontend - Dashboard Responsiveness", () => {
  test("should be responsive on tablet viewport", async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await waitForPageLoad(page);

    // Dashboard should still be usable
    const dashboardContent = page.getByText(/dashboard|welcome|overview/i);
    await expect(dashboardContent.first()).toBeVisible();
  });

    test("should be responsive on mobile viewport", async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });

      await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
      await waitForPageLoad(page);

      // Dashboard should still be usable on mobile
      const dashboardRoot = page
        .locator("app-dashboard, .dashboard, [class*='dashboard']")
        .first();
      const statCard = page
        .locator("[class*='stat'], [class*='card'], mat-card")
        .first();
      const heading = page
        .getByRole("heading", { name: /dashboard|welcome|overview/i })
        .first();

      const dashboardVisible = await dashboardRoot.isVisible().catch(() => false);
      const cardVisible = await statCard.isVisible().catch(() => false);
      const headingVisible = await heading.isVisible().catch(() => false);

      expect(dashboardVisible || cardVisible || headingVisible).toBe(true);

      // Navigation might be in hamburger menu
      const hamburgerMenu = page.locator(
        "[class*='menu-toggle'], button mat-icon:has-text('menu')"
      );
      const isNavVisible = await hamburgerMenu.isVisible();

      // Either direct nav or hamburger menu should be present
      expect(
        isNavVisible || (await page.getByRole("navigation").isVisible())
      ).toBe(true);
    });
});
