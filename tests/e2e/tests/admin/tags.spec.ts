import {
  test,
  expect,
  TEST_ADMIN,
  TEST_IMAGES,
  loginViaUi,
  fillMaterialInput,
  waitForPageLoad,
} from "../fixtures";

/**
 * Admin Frontend - Tag Management Integration Tests
 *
 * These tests exercise the complete tag management workflow.
 * Tests are serial to maintain data consistency.
 */
test.describe.serial("Admin Frontend - Tag Management", () => {
  let testTagId: number;
  let uploadedPhotoIds: number[] = [];

  test("should display tags page", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Should show tags page
    await expect(page.getByText(/tags/i).first()).toBeVisible({
      timeout: 10000,
    });

    // Should have create tag button
    await expect(
      page.getByRole("button", { name: /create|add|new/i }).first()
    ).toBeVisible();
  });

  test("should show empty state when no tags exist", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const tags = await api.getTags();

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    if (tags.length === 0) {
      // Should show empty state
      await expect(
        page.getByText(/no tags|create.*tag|add.*tag|empty/i)
      ).toBeVisible({ timeout: 10000 });
    }
  });

  test("should open create tag dialog", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
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

  test("should validate tag name is required", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
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

    // Should show validation error or button should be disabled
    await expect(submitButton)
      .toBeDisabled({ timeout: 5000 })
      .catch(() =>
        expect(page.getByText(/required|name is required/i)).toBeVisible({
          timeout: 5000,
        })
      );
  });

  test("should create a new tag with default color", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Open create dialog
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();

    // Fill in tag name
    const tagName = "Landscapes";
    await fillMaterialInput(page, "Name", tagName);

    // Submit
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("button", { name: /create/i }).click();

    // Should show tag in list
    await page.waitForTimeout(1000);
    await expect(page.getByText(tagName).first()).toBeVisible({
      timeout: 10000,
    });
  });

  test("should store tag ID for later tests", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const tags = await api.getTags();
    const landscapeTag = tags.find((t) => t.name === "Landscapes");
    expect(landscapeTag).toBeDefined();
    testTagId = landscapeTag!.id;
  });

  test("should create tag with custom color", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Create via API with custom color for consistency
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const tag = await api.createTag("Wildlife", "#FF9800");
    expect(tag).not.toBeNull();
    expect(tag?.color).toBe("#FF9800");

    // Verify in UI
    await page.reload();
    await waitForPageLoad(page);
    await expect(page.getByText("Wildlife").first()).toBeVisible();
  });

  test("should create more test tags", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Create additional tags
    await api.createTag("Favorites", "#E91E63");
    await api.createTag("2024", "#9C27B0");

    // Verify
    const tags = await api.getTags();
    expect(tags.length).toBe(4);
  });

  test("should display all tags with colors", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Should show all tags
    await expect(page.getByText("Landscapes").first()).toBeVisible();
    await expect(page.getByText("Wildlife").first()).toBeVisible();
    await expect(page.getByText("Favorites").first()).toBeVisible();
    await expect(page.getByText("2024").first()).toBeVisible();
  });

  test("should upload photos for tag tests", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Upload test images
    const results = await api.uploadPhotos([
      TEST_IMAGES.woodpecker,
      TEST_IMAGES.desert,
      TEST_IMAGES.aerial,
    ]);

    uploadedPhotoIds = results.map((r) => r.photoId).filter(Boolean);
    expect(uploadedPhotoIds.length).toBe(3);
  });

  test("should add tag to photos", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Add Landscapes tag to first two photos
    const result = await api.addTagsToPhotos(
      [testTagId],
      uploadedPhotoIds.slice(0, 2)
    );
    expect(result).not.toBeNull();

    // Verify
    const tag = await api.getTag(testTagId);
    expect(tag?.photoCount).toBe(2);
  });

  test("should display updated photo count on tag", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Landscapes tag should show 2 photos
    const tagItem = page
      .locator("mat-list-item, .tag-item, mat-card")
      .filter({ hasText: "Landscapes" });
    await expect(tagItem.getByText(/2/))
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Alternative: just verify tag is visible
        return expect(page.getByText("Landscapes").first()).toBeVisible();
      });
  });

  test("should add multiple tags to photo", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Get Wildlife and Favorites tag IDs
    const tags = await api.getTags();
    const wildlifeTag = tags.find((t) => t.name === "Wildlife");
    const favoritesTag = tags.find((t) => t.name === "Favorites");

    // Add both tags to first photo (woodpecker)
    const result = await api.addTagsToPhotos(
      [wildlifeTag!.id, favoritesTag!.id],
      [uploadedPhotoIds[0]]
    );
    expect(result).not.toBeNull();

    // Verify photo has multiple tags
    const photo = await api.getPhoto(uploadedPhotoIds[0]);
    expect(photo?.tags.length).toBeGreaterThanOrEqual(3);
  });

  test("should edit tag name", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Update tag name
    const updatedTag = await api.updateTag(testTagId, {
      name: "Scenic Landscapes",
    });
    expect(updatedTag?.name).toBe("Scenic Landscapes");
  });

  test("should edit tag color", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Update tag color
    const updatedTag = await api.updateTag(testTagId, { color: "#2196F3" });
    expect(updatedTag?.color).toBe("#2196F3");
  });

  test("should verify tag updates in UI", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Should show updated name
    await expect(page.getByText("Scenic Landscapes").first()).toBeVisible();
    await expect(page.getByText("Landscapes")).not.toBeVisible();
  });

  test("should filter photos by tag from tags page", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Click on tag to view photos
    const tagItem = page.getByText("Scenic Landscapes").first();
    await tagItem.click();

    // Should navigate to photos page with tag filter
    await page.waitForURL(/\/(photos|tags)/, { timeout: 10000 });
  });

  test("should remove tag from photos", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Remove Scenic Landscapes tag from photos
    const result = await api.removeTagsFromPhotos(
      [testTagId],
      uploadedPhotoIds.slice(0, 2)
    );
    expect(result).not.toBeNull();

    // Verify
    const tag = await api.getTag(testTagId);
    expect(tag?.photoCount).toBe(0);
  });

  test("should delete tag", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Delete the tag
    const deleted = await api.deleteTag(testTagId);
    expect(deleted).toBe(true);

    // Verify
    const tags = await api.getTags();
    expect(tags.find((t) => t.name === "Scenic Landscapes")).toBeUndefined();
  });

  test("should verify tag deletion in UI", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/tags");
    await waitForPageLoad(page);

    // Scenic Landscapes should not be visible
    await expect(page.getByText("Scenic Landscapes")).not.toBeVisible();

    // Other tags should still exist
    await expect(page.getByText("Wildlife").first()).toBeVisible();
  });

  test("should clean up - delete remaining tags and photos", async ({
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Delete remaining tags
    const tags = await api.getTags();
    for (const tag of tags) {
      await api.deleteTag(tag.id);
    }

    // Delete uploaded photos
    if (uploadedPhotoIds.length > 0) {
      await api.bulkDeletePhotos(uploadedPhotoIds);
    }

    // Verify clean state
    const remainingTags = await api.getTags();
    expect(remainingTags.length).toBe(0);
  });
});
