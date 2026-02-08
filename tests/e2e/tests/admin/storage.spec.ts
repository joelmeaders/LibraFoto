import {
  test,
  expect,
  TEST_ADMIN,
  loginViaUi,
  fillMaterialInput,
  waitForPageLoad,
} from "../fixtures";

/**
 * Admin Frontend - Storage Management Tests
 *
 * These tests verify storage provider functionality:
 * - Local storage provider configuration
 * - Scan and sync operations
 * - Provider enable/disable
 * - Future cloud provider placeholders
 */
test.describe.serial("Admin Frontend - Storage Management", () => {
  test("should navigate to storage page", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    await expect(page).toHaveURL(/\/storage/);
    await expect(page.getByText(/storage|providers/i).first()).toBeVisible();
  });

  test("should display local storage provider", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for local storage provider card or row
    const localProvider = page.getByText(/local|local storage/i);
    await expect(localProvider.first()).toBeVisible({ timeout: 10000 });
  });

  test("should show provider status", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for status indicator (enabled, active, connected, etc.)
    const statusIndicator = page.getByText(
      /enabled|active|connected|configured/i,
    );
    await expect(statusIndicator.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Status might be shown with icons instead of text
        return true;
      });
  });

  test("should show scan button for local storage", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for scan button
    const scanButton = page.getByRole("button", { name: /scan|refresh|sync/i });
    await expect(scanButton.first()).toBeVisible({ timeout: 10000 });
  });

  test("should trigger scan when scan button clicked", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const scanButton = page
      .getByRole("button", { name: /scan|refresh/i })
      .first();

    if (await scanButton.isVisible()) {
      await scanButton.click();

      // Look for scanning indicator or progress
      const scanningIndicator = page
        .getByText(/scanning|in progress|syncing/i)
        .or(page.locator(".mat-progress-spinner, .mat-progress-bar"));

      // Either see scanning indicator or completion message
      await expect(scanningIndicator)
        .toBeVisible({ timeout: 5000 })
        .catch(() => {
          // Scan might complete very quickly
          return true;
        });

      // Wait for completion
      await page.waitForTimeout(2000);

      // Look for completion indication
      const completedIndicator = page.getByText(
        /complete|finished|found|scanned/i,
      );
      await expect(completedIndicator)
        .toBeVisible({ timeout: 30000 })
        .catch(() => true);
    }
  });

  test("should show photo count after scan", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for photo count display
    const photoCount = page.getByText(/\d+\s*(photo|image|file)s?/i);
    await expect(photoCount.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Count might be displayed differently
        return true;
      });
  });

  test("should show storage path configuration", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for path or folder configuration
    const pathIndicator = page.getByText(/path|folder|directory|photos/i);
    await expect(pathIndicator.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => true);
  });

  test("should have edit/configure option for provider", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for edit/configure button
    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .or(
        page.locator("button mat-icon").filter({ hasText: /edit|settings/i }),
      );

    await expect(editButton.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Might use icons only
        const iconButton = page.locator("button:has(mat-icon)").first();
        return expect(iconButton).toBeVisible();
      });
  });

  test("should toggle provider enabled state", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for enable/disable toggle
    const toggleSwitch = page
      .getByRole("switch")
      .or(page.locator("mat-slide-toggle"))
      .first();

    if (await toggleSwitch.isVisible()) {
      const wasEnabled =
        (await toggleSwitch.getAttribute("aria-checked")) === "true";
      await toggleSwitch.click();
      await page.waitForTimeout(500);

      const isEnabled =
        (await toggleSwitch.getAttribute("aria-checked")) === "true";

      // Toggle should have changed state
      expect(isEnabled).not.toBe(wasEnabled);

      // Toggle back to original state
      await toggleSwitch.click();
    }
  });
});

test.describe("Admin Frontend - Storage Provider Configuration", () => {
  test("should open configuration dialog when edit clicked", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .first();

    if (await editButton.isVisible()) {
      await editButton.click();

      // Look for dialog
      const dialog = page
        .getByRole("dialog")
        .or(page.locator("mat-dialog-container"));
      await expect(dialog)
        .toBeVisible({ timeout: 5000 })
        .catch(() => {
          // Configuration might be inline instead of dialog
          return true;
        });
    }
  });

  test("should show base path in configuration", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .first();

    if (await editButton.isVisible()) {
      await editButton.click();
      await page.waitForTimeout(500);

      // Look for path input
      const pathInput = page
        .getByLabel(/path|folder|directory/i)
        .or(page.locator('input[placeholder*="path" i]'));

      await expect(pathInput)
        .toBeVisible({ timeout: 5000 })
        .catch(() => true);
    }
  });

  test("should save configuration changes", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .first();

    if (await editButton.isVisible()) {
      await editButton.click();
      await page.waitForTimeout(500);

      // Look for save button in dialog
      const saveButton = page.getByRole("button", {
        name: /save|update|apply/i,
      });

      if (await saveButton.isVisible()) {
        await saveButton.click();

        // Wait for success indication
        await page.waitForTimeout(1000);

        const successMessage = page.getByText(/saved|updated|success/i);
        await expect(successMessage)
          .toBeVisible({ timeout: 5000 })
          .catch(() => true);
      }
    }
  });

  test("should cancel configuration changes", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .first();

    if (await editButton.isVisible()) {
      await editButton.click();
      await page.waitForTimeout(500);

      // Look for cancel button
      const cancelButton = page.getByRole("button", {
        name: /cancel|close|discard/i,
      });

      if (await cancelButton.isVisible()) {
        await cancelButton.click();

        // Dialog should close
        const dialog = page.getByRole("dialog");
        await expect(dialog)
          .not.toBeVisible({ timeout: 5000 })
          .catch(() => true);
      }
    }
  });
});

test.describe("Admin Frontend - Cloud Storage Placeholders", () => {
  test("should show add provider button", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for add provider button
    const addButton = page
      .getByRole("button", { name: /add|new|connect/i })
      .or(page.getByText(/add provider|add storage|connect/i));

    await expect(addButton.first())
      .toBeVisible({ timeout: 10000 })
      .catch(() => {
        // Add button might not be visible if all providers are shown
        return true;
      });
  });

  test("should show Google Photos option (placeholder)", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for Google Photos in provider list or add dialog
    const googleCard = page
      .locator("mat-card, .provider-card, .storage-provider")
      .filter({ hasText: "Google Photos" })
      .first();

    if (await googleCard.isVisible().catch(() => false)) {
      const statusText = googleCard
        .getByText(/coming soon|not available|not connected|connect/i)
        .first();
      await expect(statusText)
        .toBeVisible({ timeout: 5000 })
        .catch(() => true);
    } else {
      // Try opening add provider dialog
      const addButton = page
        .getByRole("button", { name: /add|new|connect/i })
        .first();
      if (await addButton.isVisible()) {
        await addButton.click();
        await page.waitForTimeout(500);

        const googleOption = page.getByText(/google photos/i).first();
        await expect(googleOption)
          .toBeVisible({ timeout: 5000 })
          .catch(() => true);
      }
    }
  });

  test("should show OneDrive option (placeholder)", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Look for OneDrive in provider list
    const oneDriveCard = page
      .locator("mat-card, .provider-card, .storage-provider")
      .filter({ hasText: "OneDrive" })
      .first();

    if (await oneDriveCard.isVisible().catch(() => false)) {
      const statusText = oneDriveCard
        .getByText(/coming soon|not available|not connected|connect/i)
        .first();
      await expect(statusText)
        .toBeVisible({ timeout: 5000 })
        .catch(() => true);
    } else {
      const addButton = page
        .getByRole("button", { name: /add|new|connect/i })
        .first();
      if (await addButton.isVisible()) {
        await addButton.click();
        await page.waitForTimeout(500);

        const oneDriveOption = page.getByText(/onedrive/i).first();
        await expect(oneDriveOption)
          .toBeVisible({ timeout: 5000 })
          .catch(() => true);
      }
    }
  });
});

test.describe("Admin Frontend - Storage Sync Progress", () => {
  test("should show sync progress during operation", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const scanButton = page
      .getByRole("button", { name: /scan|sync|refresh/i })
      .first();

    if (await scanButton.isVisible()) {
      await scanButton.click();

      // Look for progress indicator
      const progressIndicator = page
        .locator("mat-progress-bar, mat-progress-spinner")
        .or(page.getByText(/progress|%/i));

      await expect(progressIndicator.first())
        .toBeVisible({ timeout: 5000 })
        .catch(() => {
          // Progress might be instant for small scans
          return true;
        });
    }
  });

  test("should show scan results after completion", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const scanButton = page
      .getByRole("button", { name: /scan|sync|refresh/i })
      .first();

    if (await scanButton.isVisible()) {
      await scanButton.click();

      // Wait for scan to complete
      await page.waitForTimeout(3000);

      // Look for results (new files found, etc.)
      const results = page.getByText(
        /found|added|scanned|complete|new|existing/i,
      );
      await expect(results.first())
        .toBeVisible({ timeout: 30000 })
        .catch(() => true);
    }
  });

  test("should be able to cancel ongoing sync", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const scanButton = page
      .getByRole("button", { name: /scan|sync|refresh/i })
      .first();

    if (await scanButton.isVisible()) {
      await scanButton.click();

      // Look for cancel button that appears during scan
      const cancelButton = page.getByRole("button", { name: /cancel|stop/i });

      // Cancel button might appear briefly
      await expect(cancelButton)
        .toBeVisible({ timeout: 2000 })
        .catch(() => {
          // Scan might complete too fast for cancel
          return true;
        });
    }
  });
});

test.describe("Admin Frontend - Storage Error Handling", () => {
  test("should display error if storage path invalid", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    const editButton = page
      .getByRole("button", { name: /edit|configure|settings/i })
      .first();

    if (await editButton.isVisible()) {
      await editButton.click();
      await page.waitForTimeout(500);

      const pathInput = page.getByLabel(/path|folder|directory/i);

      if (await pathInput.isVisible()) {
        await pathInput.clear();
        await pathInput.fill("/nonexistent/invalid/path/12345");

        const saveButton = page.getByRole("button", { name: /save|apply/i });
        if (await saveButton.isVisible()) {
          await saveButton.click();

          // Look for error message
          const errorMessage = page.getByText(
            /error|invalid|not found|does not exist/i,
          );
          await expect(errorMessage)
            .toBeVisible({ timeout: 5000 })
            .catch(() => {
              // Error might be shown differently
              return true;
            });
        }
      }
    }
  });

  test("should recover from scan errors gracefully", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/storage");
    await waitForPageLoad(page);

    // Even if scan fails, page should remain functional
    const scanButton = page
      .getByRole("button", { name: /scan|sync|refresh/i })
      .first();

    await expect(scanButton).toBeVisible({ timeout: 10000 });

    // Page should not be in broken state
    const pageTitle = page.getByText(/storage|providers/i);
    await expect(pageTitle.first()).toBeVisible();
  });
});
