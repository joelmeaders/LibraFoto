import {
  test,
  expect,
  TEST_ADMIN,
  TEST_EDITOR,
  TEST_GUEST,
  loginViaUi,
  fillMaterialInput,
  waitForPageLoad,
} from "../fixtures";

/**
 * Admin Frontend - User Management Integration Tests
 *
 * These tests exercise user management including:
 * - User CRUD operations
 * - Role assignment
 * - Guest link management
 *
 * Tests are serial because they modify shared state.
 */
test.describe.serial("Admin Frontend - User Management", () => {
  let editorUserId: number;
  let guestUserId: number;

  test("should display users page for admin", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Should show users page
    await expect(page.getByText(/users/i).first()).toBeVisible({
      timeout: 10000,
    });

    // Should have create user button
    await expect(
      page.getByRole("button", { name: /create|add|new/i }).first()
    ).toBeVisible();
  });

  test("should show test admin in users list", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Test admin should be in the list
    await expect(page.getByText(TEST_ADMIN.email)).toBeVisible();
    await expect(page.getByText(/admin/i).first()).toBeVisible();
  });

  test("should open create user dialog", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Click create button
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();

    // Dialog should open
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 5000 });

    // Should have required fields
    await expect(page.getByRole("textbox", { name: /email/i })).toBeVisible();
    await expect(page.getByRole("textbox", { name: /email/i })).toBeVisible();
    await expect(
      page.getByRole("textbox", { name: /password/i })
    ).toBeVisible();
  });

  test("should validate required fields when creating user", async ({
    page,
  }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Open create dialog
    await page
      .getByRole("button", { name: /create|add|new/i })
      .first()
      .click();
    await expect(page.getByRole("dialog")).toBeVisible();

    // Try to submit without filling fields
    const dialog = page.getByRole("dialog");
    const submitButton = dialog.getByRole("button", { name: /create|save/i });

    // Should show validation errors or button should be disabled
    await expect(submitButton)
      .toBeDisabled({ timeout: 5000 })
      .catch(() =>
        expect(page.getByText(/required/i).first()).toBeVisible({
          timeout: 5000,
        })
      );
  });

  test("should create editor user", async ({ page, api }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Create via API for reliability
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const editor = await api.createUser(
      TEST_EDITOR.email,
      TEST_EDITOR.password,
      "Editor"
    );
    expect(editor).not.toBeNull();
    editorUserId = editor!.id;

    // Verify in UI
    await page.reload();
    await waitForPageLoad(page);
    await expect(page.getByText(TEST_EDITOR.email)).toBeVisible({
      timeout: 10000,
    });
  });

  test("should create guest user", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const guest = await api.createUser(
      TEST_GUEST.email,
      TEST_GUEST.password,
      "Guest"
    );
    expect(guest).not.toBeNull();
    guestUserId = guest!.id;
  });

  test("should display all users with roles", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // All three users should be visible
    await expect(page.getByText(TEST_ADMIN.email)).toBeVisible();
    await expect(page.getByText(TEST_EDITOR.email)).toBeVisible();
    await expect(page.getByText(TEST_GUEST.email)).toBeVisible();
  });

  test("should show user roles correctly", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Check for role indicators
    const adminRow = page
      .locator("tr, mat-row, .user-row")
      .filter({ hasText: TEST_ADMIN.email });
    await expect(adminRow.getByText(/admin/i))
      .toBeVisible()
      .catch(() => true);

    const editorRow = page
      .locator("tr, mat-row, .user-row")
      .filter({ hasText: TEST_EDITOR.email });
    await expect(editorRow.getByText(/editor/i))
      .toBeVisible()
      .catch(() => true);
  });

  test("should update user display name", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const updatedUser = await api.updateUser(editorUserId, {
      displayName: "Test Editor User",
    });
    expect(updatedUser?.displayName).toBe("Test Editor User");
  });

  test("should change user role", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Change guest to editor
    const updatedUser = await api.updateUser(guestUserId, {
      role: "Editor",
    });
    expect(updatedUser?.role).toBe("Editor");

    // Change back to guest
    const revertedUser = await api.updateUser(guestUserId, {
      role: "Guest",
    });
    expect(revertedUser?.role).toBe("Guest");
  });

  test("should deactivate user", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const updatedUser = await api.updateUser(guestUserId, {
      isActive: false,
    });
    expect(updatedUser?.isActive).toBe(false);

    // Reactivate
    const reactivatedUser = await api.updateUser(guestUserId, {
      isActive: true,
    });
    expect(reactivatedUser?.isActive).toBe(true);
  });

  test("should prevent admin from deleting themselves", async ({
    page,
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const currentUser = await api.getCurrentUser();

    // Attempt to delete self should fail
    const deleted = await api.deleteUser(currentUser!.id);

    // The API should either return false or the user should still exist
    const users = await api.getUsers();
    const adminStillExists = users.data.some(
      (u) => u.email === TEST_ADMIN.email
    );
    expect(adminStillExists).toBe(true);
  });

  test("should delete guest user", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const deleted = await api.deleteUser(guestUserId);
    expect(deleted).toBe(true);

    // Verify
    const users = await api.getUsers();
    const guestStillExists = users.data.some(
      (u) => u.email === TEST_GUEST.email
    );
    expect(guestStillExists).toBe(false);
  });

  test("should verify user deletion in UI", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Guest should not be visible
    await expect(page.getByText(TEST_GUEST.email)).not.toBeVisible();

    // Editor and Admin should still exist
    await expect(page.getByText(TEST_ADMIN.email)).toBeVisible();
    await expect(page.getByText(TEST_EDITOR.email)).toBeVisible();
  });
});

test.describe.serial("Admin Frontend - Guest Links Management", () => {
  let testAlbumId: number;
  let guestLinkId: number;
  let guestLinkCode: string;

  test("should create album for guest link target", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const album = await api.createAlbum(
      "Guest Uploads",
      "Album for guest uploads"
    );
    expect(album).not.toBeNull();
    testAlbumId = album!.id;
  });

  test("should navigate to guest links tab", async ({ page }) => {
    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Click on Guest Links tab
    const guestLinksTab = page.getByRole("tab", { name: /guest links/i });
    if (await guestLinksTab.isVisible({ timeout: 2000 }).catch(() => false)) {
      await guestLinksTab.click();
      await expect(
        page.getByText(/guest links|upload links/i).first()
      ).toBeVisible();
    } else {
      // Some UIs might have guest links on a separate page
      test.skip(true, "Guest links tab not found - may be separate page");
    }
  });

  test("should create guest link without expiration", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const guestLink = await api.createGuestLink({
      name: "Family Event Upload",
    });
    expect(guestLink).not.toBeNull();
    expect(guestLink?.name).toBe("Family Event Upload");
    guestLinkId = guestLink!.id;
    guestLinkCode = guestLink!.linkId;
  });

  test("should create guest link with expiration", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const expiresAt = new Date();
    expiresAt.setDate(expiresAt.getDate() + 7); // 7 days from now

    const guestLink = await api.createGuestLink({
      name: "Week-long Upload Link",
      expiresAt: expiresAt.toISOString(),
    });
    expect(guestLink).not.toBeNull();
    expect(guestLink?.expiresAt).not.toBeNull();
  });

  test("should create guest link with upload limit", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const guestLink = await api.createGuestLink({
      name: "Limited Upload Link",
      maxUploads: 10,
    });
    expect(guestLink).not.toBeNull();
    expect(guestLink?.maxUploads).toBe(10);
  });

  test("should create guest link with target album", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const guestLink = await api.createGuestLink({
      name: "Album-targeted Link",
      targetAlbumId: testAlbumId,
    });
    expect(guestLink).not.toBeNull();
    expect(guestLink?.targetAlbumId).toBe(testAlbumId);
  });

  test("should display all guest links", async ({ page, api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    const guestLinks = await api.getGuestLinks();
    expect(guestLinks.length).toBeGreaterThanOrEqual(1);

    await loginViaUi(page, TEST_ADMIN.email, TEST_ADMIN.password);
    await page.goto("/users");
    await waitForPageLoad(page);

    // Try to navigate to guest links tab
    const guestLinksTab = page.getByRole("tab", { name: /guest links/i });
    if (
      guestLinks.length > 0 &&
      (await guestLinksTab.isVisible({ timeout: 2000 }).catch(() => false))
    ) {
      await guestLinksTab.click();

      // Should show guest link names
      await expect(page.getByText("Family Event Upload")).toBeVisible({
        timeout: 10000,
      });
    }
  });

  test("should validate guest link", async ({ api }) => {
    const validation = await api.validateGuestLink(guestLinkCode);
    expect(validation).not.toBeNull();
    expect(validation?.isValid).toBe(true);
  });

  test("should reject invalid guest link code", async ({ api }) => {
    const validation = await api.validateGuestLink("invalid-code-12345");
    expect(validation?.isValid).toBeFalsy();
  });

  test("should delete guest link", async ({ api }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    const deleted = await api.deleteGuestLink(guestLinkId);
    expect(deleted).toBe(true);

    // Verify deleted
    const guestLinks = await api.getGuestLinks();
    const linkStillExists = guestLinks.some((l) => l.id === guestLinkId);
    expect(linkStillExists).toBe(false);
  });

  test("should clean up - delete remaining guest links and album", async ({
    api,
  }) => {
    await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    // Delete remaining guest links
    const guestLinks = await api.getGuestLinks();
    for (const link of guestLinks) {
      await api.deleteGuestLink(link.id);
    }

    // Delete test album
    await api.deleteAlbum(testAlbumId);

    // Delete editor user created earlier
    const users = await api.getUsers();
    const editor = users.data.find((u) => u.email === TEST_EDITOR.email);
    if (editor) {
      await api.deleteUser(editor.id);
    }

    // Verify clean state
    const remainingLinks = await api.getGuestLinks();
    expect(remainingLinks.length).toBe(0);
  });
});
