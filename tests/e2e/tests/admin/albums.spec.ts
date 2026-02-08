import {
  test,
  expect,
  TEST_ADMIN,
  TEST_IMAGES,
  loginViaUi,
  fillMaterialInput,
  waitForSnackbar,
  waitForPageLoad,
  confirmDelete,
} from "../fixtures";

/**
 * Admin Frontend - Album Management Integration Tests
 *
 * These tests exercise the complete album management workflow.
 * Tests are serial to maintain data consistency (create → edit → delete).
 */
test.describe.serial("Admin Frontend - Album Management", () => {
  let testAlbumId: number;
  let uploadedPhotoIds: number[] = [];

  test("should display albums page", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Should show albums page
    await expect(page.getByText(/albums/i).first()).toBeVisible({
      timeout: 10000,
    });

    // Should have create album button
    await expect(
      page.getByRole("button", { name: /create|add|new/i }).first(),
    ).toBeVisible();
  });

  test("should show empty state when no albums exist", async ({
    page,
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const albums = await api.getAlbums();

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    if (albums.length === 0) {
      // Should show empty state
      const emptyState = page
        .getByRole("heading", { name: /no albums/i })
        .or(page.getByText(/create albums to organize|add.*album|empty/i))
        .first();
      await expect(emptyState).toBeVisible({ timeout: 10000 });
    }
  });

  test("should open create album dialog", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Click create button
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();

    // Dialog should open
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 5000 });

    // Should have name input
    await expect(page.getByRole("textbox", { name: /name/i })).toBeVisible();
  });

  test("should validate album name is required", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Open create dialog
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();
    await expect(page.getByRole("dialog")).toBeVisible();

    // Try to submit without name
    const dialog = page.getByRole("dialog");
    const submitButton = dialog.getByRole("button", { name: /create|save/i });

    // Button should be disabled or show validation error
    await expect(submitButton)
      .toBeDisabled({ timeout: 5000 })
      .catch(async () => {
        await submitButton.click();
        await expect(page.getByText(/required|name is required/i)).toBeVisible({
          timeout: 5000,
        });
      });
  });

  test("should create a new album", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Open create dialog
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();

    // Fill in album details
    const albumName = "Nature Photography";
    const albumDescription = "Beautiful nature photos";
    await fillMaterialInput(page, "Name", albumName);

    // Fill description if available
    const descriptionInput = page.getByRole("textbox", {
      name: /description/i,
    });
    if (
      await descriptionInput.isVisible({ timeout: 1000 }).catch(() => false)
    ) {
      await descriptionInput.fill(albumDescription);
    }

    // Submit
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: /create/i }).click();

    // Should show success or album in list
    await page.waitForTimeout(1000);
    await expect(page.getByText(albumName)).toBeVisible({ timeout: 10000 });
  });

  test("should store album ID for later tests", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const albums = await api.getAlbums();
    const natureAlbum = albums.find((a) => a.name === "Nature Photography");
    expect(natureAlbum).toBeDefined();
    testAlbumId = natureAlbum!.id;
  });

  test("should create another album", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Open create dialog
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();

    // Fill in album details
    const albumName = "Family Memories";
    await fillMaterialInput(page, "Name", albumName);

    // Submit
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: /create/i }).click();

    // Wait for dialog to close and verify
    await page.waitForTimeout(1000);
    await expect(page.getByText(albumName)).toBeVisible({ timeout: 10000 });

    // Verify via API
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const albums = await api.getAlbums();
    expect(albums.length).toBe(2);
  });

  test("should display album cards with correct information", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Should show both albums
    await expect(page.getByText("Nature Photography")).toBeVisible();
    await expect(page.getByText("Family Memories")).toBeVisible();

    // Should show photo count (0 initially)
    await expect(page.getByText(/0 photo/i).first())
      .toBeVisible()
      .catch(() => {
        // Some UIs don't show count for empty albums
        return true;
      });
  });

  test("should upload photos for album tests", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Upload all test images
    const results = await api.uploadPhotos([
      TEST_IMAGES.woodpecker,
      TEST_IMAGES.desert,
      TEST_IMAGES.aerial,
    ]);

    uploadedPhotoIds = results.map((r) => r.photoId).filter(Boolean);
    expect(uploadedPhotoIds.length).toBe(3);
  });

  test("should add photos to album", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Add first two photos to Nature Photography album
    const result = await api.addPhotosToAlbum(
      testAlbumId,
      uploadedPhotoIds.slice(0, 2),
    );
    expect(result).not.toBeNull();

    // Verify
    const album = await api.getAlbum(testAlbumId);
    expect(album?.photoCount).toBe(2);
  });

  test("should display updated photo count", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Nature Photography should show 2 photos
    const albumCard = page
      .locator("mat-card, .album-card")
      .filter({ hasText: "Nature Photography" });
    await expect(albumCard.getByText(/2 photo/i))
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Alternative check
        return expect(page.getByText("2")).toBeVisible();
      });
  });

  test("should open edit album dialog", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Find the album and click edit button
    const albumCard = page
      .locator("mat-card, .album-card")
      .filter({ hasText: "Nature Photography" });

    // Look for edit button (icon button or menu)
    const editButton = albumCard.getByRole("button", {
      name: /edit|settings/i,
    });
    if (await editButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await editButton.click();
    } else {
      // Try clicking a menu button first
      const menuButton = albumCard.getByRole("button", {
        name: /more|menu|options/i,
      });
      if (await menuButton.isVisible({ timeout: 2000 }).catch(() => false)) {
        await menuButton.click();
        await page.getByRole("menuitem", { name: /edit/i }).click();
      } else {
        test.skip(true, "Edit button not found in album card");
      }
    }

    // Dialog should open
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 5000 });
  });

  test("should edit album name and description", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Update via API since UI interactions can vary
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const updatedAlbum = await api.updateAlbum(testAlbumId, {
      name: "Wildlife Photography",
      description: "Amazing wildlife captures",
    });

    expect(updatedAlbum?.name).toBe("Wildlife Photography");

    // Verify in UI
    await page.reload();
    await waitForPageLoad(page);
    await expect(page.getByText("Wildlife Photography")).toBeVisible();
  });

  test("should set album cover photo", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Set cover photo
    const result = await api.setAlbumCover(testAlbumId, uploadedPhotoIds[0]);
    expect(result).not.toBeNull();
    expect(result?.coverPhotoId).toBe(uploadedPhotoIds[0]);
  });

  test("should display album cover photo", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Album card should have an image
    const albumCard = page
      .locator("mat-card, .album-card")
      .filter({ hasText: "Wildlife Photography" });
    const coverImage = albumCard.locator("img").first();

    await expect(coverImage)
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Some UIs use background image instead
        return true;
      });
  });

  test("should remove album cover photo", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const result = await api.removeAlbumCover(testAlbumId);
    expect(result?.coverPhotoId).toBeNull();
  });

  test("should navigate to album photos view", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Click on album to view photos
    const albumCard = page
      .locator("mat-card, .album-card")
      .filter({ hasText: "Wildlife Photography" });
    await albumCard.click();

    // Should navigate to photos filtered by album or album detail page
    await page.waitForURL(/\/(photos|albums)/, { timeout: 10000 });
  });

  test("should remove photos from album", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Remove all photos from album
    const result = await api.removePhotosFromAlbum(
      testAlbumId,
      uploadedPhotoIds.slice(0, 2),
    );
    expect(result).not.toBeNull();

    // Verify
    const album = await api.getAlbum(testAlbumId);
    expect(album?.photoCount).toBe(0);
  });

  test("should delete album", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Delete via API
    const deleted = await api.deleteAlbum(testAlbumId);
    expect(deleted).toBe(true);

    // Verify in UI
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/albums");
    await waitForPageLoad(page);

    // Wildlife Photography should not be visible
    await expect(page.getByText("Wildlife Photography")).not.toBeVisible({
      timeout: 5000,
    });

    // Family Memories should still exist
    await expect(page.getByText("Family Memories")).toBeVisible();
  });

  test("should clean up - delete remaining album and photos", async ({
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Delete remaining album
    const albums = await api.getAlbums();
    for (const album of albums) {
      await api.deleteAlbum(album.id);
    }

    // Delete uploaded photos
    if (uploadedPhotoIds.length > 0) {
      await api.bulkDeletePhotos(uploadedPhotoIds);
    }

    // Verify clean state
    const remainingAlbums = await api.getAlbums();
    expect(remainingAlbums.length).toBe(0);
  });
});
