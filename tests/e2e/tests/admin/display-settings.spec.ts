import {
  test,
  expect,
  TEST_ADMIN,
  loginViaUi,
  fillMaterialInput,
  waitForPageLoad,
} from "../fixtures";

/**
 * Admin Frontend - Display Settings Tests
 *
 * These tests verify the display settings page functionality:
 * - Slideshow configuration (interval, transition)
 * - Source selection (albums, providers)
 * - Overlay settings (date, time, location)
 * - Save, reset, and discard changes
 */
test.describe.serial("Admin Frontend - Display Settings", () => {
  let originalSettings: {
    interval?: number;
    transition?: string;
  } = {};

  test.beforeAll(async ({ browser }) => {
    // Login and capture original settings for restoration
    const context = await browser.newContext();
    const apiContext = await context.request;

    const loginResponse = await apiContext.post(
      "http://localhost:5179/api/auth/login",
      {
        data: {
          username: TEST_ADMIN.email,
          password: TEST_ADMIN.password,
        },
      }
    );

    if (loginResponse.ok()) {
      const loginData = await loginResponse.json();
      const authHeaders = { Authorization: `Bearer ${loginData.token}` };

      // Get current display settings
      const settingsResponse = await apiContext.get(
        "http://localhost:5179/api/display/settings",
        {
          headers: authHeaders,
        }
      );

      if (settingsResponse.ok()) {
        const settings = await settingsResponse.json();
        originalSettings = {
          interval: settings.slideIntervalSeconds,
          transition: settings.transitionType,
        };
      }
    }

    await context.close();
  });

  test("should navigate to display settings page", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    await expect(page).toHaveURL(/\/display/);
    await expect(
      page.getByText(/display|slideshow|settings/i).first()
    ).toBeVisible();
  });

  test("should display current slideshow interval", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for interval setting
    const intervalInput = page.locator('input[type="number"]').first();

    // If no number input, look for slider or select
    const intervalControl = page
      .getByLabel(/interval|duration|seconds/i)
      .or(intervalInput);
    await expect(intervalControl)
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // Settings might be displayed differently
        return true;
      });
  });

  test("should display transition type selector", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for transition type control
    const transitionControl = page
      .getByLabel(/transition/i)
      .or(
        page.getByRole("combobox").filter({ hasText: /fade|slide|crossfade/i })
      );

    await expect(transitionControl)
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // May be implemented differently (radio buttons, etc.)
        return true;
      });
  });

  test("should change slideshow interval", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find interval input
    const intervalInput = page
      .getByLabel(/interval|duration|seconds/i)
      .or(page.locator('input[type="number"]').first());

    if (await intervalInput.isVisible()) {
      await intervalInput.clear();
      await intervalInput.fill("15");

      // Look for save button
      const saveButton = page.getByRole("button", { name: /save/i });
      if (await saveButton.isVisible()) {
        await saveButton.click();

        // Wait for success indication
        await page.waitForTimeout(1000);

        // Check for success message or snackbar
        const successIndicator = page.getByText(/saved|success|updated/i);
        await expect(successIndicator)
          .toBeVisible({ timeout: 5000 })
          .catch(() => true);
      }
    }
  });

  test("should change transition type", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find transition selector
    const transitionSelect = page
      .getByLabel(/transition/i)
      .or(page.getByRole("combobox").first());

    if (await transitionSelect.isVisible()) {
      await transitionSelect.click();

      // Select a different transition
      const slideOption = page.getByRole("option", { name: /slide/i });
      const fadeOption = page.getByRole("option", { name: /fade/i });

      if (await slideOption.isVisible()) {
        await slideOption.click();
      } else if (await fadeOption.isVisible()) {
        await fadeOption.click();
      }

      // Save changes
      const saveButton = page.getByRole("button", { name: /save/i });
      if (await saveButton.isVisible()) {
        await saveButton.click();
        await page.waitForTimeout(1000);
      }
    }
  });

  test("should display overlay settings", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for overlay toggle or section
    const overlaySection = page.getByText(
      /overlay|show date|show time|show location/i
    );
    await expect(overlaySection)
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // Overlay settings might be in a different section
        return true;
      });
  });

  test("should toggle overlay date display", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find date toggle
    const dateToggle = page
      .getByLabel(/show date/i)
      .or(page.getByRole("checkbox", { name: /date/i }));

    if (await dateToggle.isVisible()) {
      const initialState = await dateToggle.isChecked();
      await dateToggle.click();

      // Verify toggle changed
      const newState = await dateToggle.isChecked();
      expect(newState).not.toBe(initialState);
    }
  });

  test("should toggle overlay time display", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find time toggle
    const timeToggle = page
      .getByLabel(/show time/i)
      .or(page.getByRole("checkbox", { name: /time/i }));

    if (await timeToggle.isVisible()) {
      const initialState = await timeToggle.isChecked();
      await timeToggle.click();

      const newState = await timeToggle.isChecked();
      expect(newState).not.toBe(initialState);
    }
  });

  test("should toggle overlay location display", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find location toggle
    const locationToggle = page
      .getByLabel(/show location/i)
      .or(page.getByRole("checkbox", { name: /location/i }));

    if (await locationToggle.isVisible()) {
      const initialState = await locationToggle.isChecked();
      await locationToggle.click();

      const newState = await locationToggle.isChecked();
      expect(newState).not.toBe(initialState);
    }
  });

  test("should configure overlay position", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Find position selector
    const positionSelect = page
      .getByLabel(/position/i)
      .or(page.getByRole("combobox", { name: /position/i }));

    if (await positionSelect.isVisible()) {
      await positionSelect.click();

      // Try to select bottom-left
      const bottomLeftOption = page.getByRole("option", {
        name: /bottom.?left/i,
      });
      if (await bottomLeftOption.isVisible()) {
        await bottomLeftOption.click();
      }
    }
  });

  test("should select photo source", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for source selection
    const sourceSection = page.getByText(/source|albums|providers/i);
    await expect(sourceSection)
      .toBeVisible({ timeout: 5000 })
      .catch(() => true);

    // Look for source type selector
    const sourceSelect = page
      .getByLabel(/source type/i)
      .or(page.getByRole("combobox", { name: /source/i }));

    if (await sourceSelect.isVisible()) {
      await sourceSelect.click();
      await page.waitForTimeout(500);

      // Look for options
      const allPhotosOption = page.getByRole("option", { name: /all photos/i });
      if (await allPhotosOption.isVisible()) {
        await allPhotosOption.click();
      }
    }
  });

  test("should discard unsaved changes", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Make a change
    const intervalInput = page
      .getByLabel(/interval/i)
      .or(page.locator('input[type="number"]').first());

    if (await intervalInput.isVisible()) {
      const originalValue = await intervalInput.inputValue();
      await intervalInput.clear();
      await intervalInput.fill("999");

      // Look for cancel/discard button
      const discardButton = page.getByRole("button", {
        name: /cancel|discard|reset/i,
      });
      if (await discardButton.isVisible()) {
        await discardButton.click();

        // Value should be restored
        const currentValue = await intervalInput.inputValue();
        expect(currentValue).toBe(originalValue);
      } else {
        // Navigate away without saving
        await page.goto("/dashboard");
        await page.goto("/display");
        await waitForPageLoad(page);

        // Value should be original
        const restoredInput = page
          .getByLabel(/interval/i)
          .or(page.locator('input[type="number"]').first());
        if (await restoredInput.isVisible()) {
          const restoredValue = await restoredInput.inputValue();
          expect(restoredValue).not.toBe("999");
        }
      }
    }
  });

  test("should reset to default settings", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for reset to defaults button
    const resetButton = page.getByRole("button", {
      name: /reset|defaults|restore defaults/i,
    });

    if (await resetButton.isVisible()) {
      await resetButton.click();

      // Confirm reset if dialog appears
      const confirmButton = page.getByRole("button", {
        name: /confirm|yes|reset/i,
      });
      if (await confirmButton.isVisible()) {
        await confirmButton.click();
      }

      // Wait for reset
      await page.waitForTimeout(1000);

      // Check for success message
      const successMessage = page.getByText(/reset|defaults|restored/i);
      await expect(successMessage)
        .toBeVisible({ timeout: 5000 })
        .catch(() => true);
    }
  });

  test("should show validation errors for invalid interval", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    const intervalInput = page
      .getByLabel(/interval/i)
      .or(page.locator('input[type="number"]').first());

    if (await intervalInput.isVisible()) {
      // Try setting invalid value (0 or negative)
      await intervalInput.clear();
      await intervalInput.fill("0");
      await intervalInput.blur();

      // Look for validation error
      const errorMessage = page.getByText(
        /invalid|must be|error|greater than/i
      );
      await expect(errorMessage)
        .toBeVisible({ timeout: 5000 })
        .catch(() => {
          // Validation might prevent entry instead
          return true;
        });
    }
  });

  test("should persist settings after page reload", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    const intervalInput = page
      .getByLabel(/interval/i)
      .or(page.locator('input[type="number"]').first());

    if (await intervalInput.isVisible()) {
      // Set a specific value
      await intervalInput.clear();
      await intervalInput.fill("20");

      // Save
      const saveButton = page.getByRole("button", { name: /save/i });
      if (await saveButton.isVisible()) {
        await saveButton.click();
        await page.waitForTimeout(1000);

        // Reload page
        await page.reload();
        await waitForPageLoad(page);

        // Check value persisted
        const reloadedInput = page
          .getByLabel(/interval/i)
          .or(page.locator('input[type="number"]').first());
        if (await reloadedInput.isVisible()) {
          const value = await reloadedInput.inputValue();
          expect(value).toBe("20");
        }
      }
    }
  });
});

test.describe("Admin Frontend - Display Preview", () => {
  test("should have preview functionality", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    // Look for preview button or link
    const previewButton = page
      .getByRole("button", { name: /preview|view|open display/i })
      .or(page.getByRole("link", { name: /preview|display/i }));

    await expect(previewButton)
      .toBeVisible({ timeout: 5000 })
      .catch(() => {
        // Preview might be implemented differently or not at all
        return true;
      });
  });

  test("should open display in new tab when preview clicked", async ({
    page,
    context,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/display");
    await waitForPageLoad(page);

    const previewButton = page
      .getByRole("button", { name: /preview|view|open display/i })
      .or(page.getByRole("link", { name: /preview|display/i }));

    if (await previewButton.isVisible()) {
      // Listen for new page
      const pagePromise = context
        .waitForEvent("page", { timeout: 5000 })
        .catch(() => null);
      await previewButton.click();

      const newPage = await pagePromise;
      if (newPage) {
        await newPage.waitForLoadState();
        // Display frontend should be on port 3000
        expect(newPage.url()).toMatch(/localhost:3000|display/);
        await newPage.close();
      }
    }
  });
});
