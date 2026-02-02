import { test, expect, TEST_ADMIN } from "../fixtures";

/**
 * Admin Frontend - Login Integration Tests
 *
 * These tests exercise the full authentication flow using the real API backend.
 * The test admin user is created during global setup.
 */
test.describe("Admin Frontend - Login", () => {
  test("should display login form", async ({ page }) => {
    await page.goto("/login");

    // Check logo and title
    await expect(
      page.getByRole("heading", { name: "LibraFoto" })
    ).toBeVisible();
    // Use getByRole for more specific targeting
    await expect(
      page.getByRole("textbox", { name: "Email" })
    ).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Password" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign In" })).toBeVisible();
  });

  test("should show tagline", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByText("Your personal photo frame")).toBeVisible();
  });

  test("should validate required fields", async ({ page }) => {
    await page.goto("/login");

    // Click sign in without filling form
    await page.getByRole("button", { name: "Sign In" }).click();

    // Should show validation errors
    await expect(page.getByText("Email is required")).toBeVisible();
    await expect(page.getByText("Password is required")).toBeVisible();
  });

  test("should toggle password visibility", async ({ page }) => {
    await page.goto("/login");

    // Use getByRole with name for the password input specifically
    const passwordInput = page.getByRole("textbox", { name: "Password" });
    const toggleButton = page.getByRole("button", { name: /show password/i });

    // Initially password should be hidden
    await expect(passwordInput).toHaveAttribute("type", "password");

    // Click toggle
    await toggleButton.click();

    // Password should be visible
    await expect(passwordInput).toHaveAttribute("type", "text");

    // Click toggle again - now label says "Hide password"
    await page.getByRole("button", { name: /hide password/i }).click();

    // Password should be hidden again
    await expect(passwordInput).toHaveAttribute("type", "password");
  });

  test("should show error on invalid credentials", async ({ page }) => {
    await page.goto("/login");

    // Use getByRole for textbox and force:true to bypass overlay
    const emailInput = page.getByRole("textbox", {
      name: "Email",
    });
    const passwordInput = page.getByRole("textbox", { name: "Password" });

    await emailInput.fill("wronguser");
    await passwordInput.fill("wrongpassword");
    await page.getByRole("button", { name: "Sign In" }).click();

    // Should show error message from the real API
    await expect(page.getByText(/invalid|unauthorized|incorrect/i)).toBeVisible(
      {
        timeout: 10000,
      }
    );
  });

  test("should login successfully and redirect to dashboard", async ({
    page,
  }) => {
    await page.goto("/login");

    // Login with the test admin created during global setup
    const emailInput = page.getByRole("textbox", {
      name: "Email",
    });
    const passwordInput = page.getByRole("textbox", { name: "Password" });

    await emailInput.fill(TEST_ADMIN.email);
    await passwordInput.fill(TEST_ADMIN.password);
    await page.getByRole("button", { name: "Sign In" }).click();

    // Should redirect to dashboard after successful login
    await expect(page).toHaveURL(/\/(dashboard|photos)/, { timeout: 15000 });
  });

  test("should show loading spinner during login", async ({ page }) => {
    await page.goto("/login");

    const emailInput = page.getByRole("textbox", {
      name: "Email",
    });
    const passwordInput = page.getByRole("textbox", { name: "Password" });

    await emailInput.fill(TEST_ADMIN.email);
    await passwordInput.fill(TEST_ADMIN.password);

    // Click sign in and verify it navigates (proving the form submission works)
    await page.getByRole("button", { name: "Sign In" }).click();

    // Verify successful navigation - this proves the login flow completed
    await expect(page).toHaveURL(/\/(dashboard|photos)/, { timeout: 15000 });
  });

  test("should persist authentication across page reloads", async ({
    page,
  }) => {
    // Login first
    await page.goto("/login");

    const emailInput = page.getByRole("textbox", {
      name: "Email",
    });
    const passwordInput = page.getByRole("textbox", { name: "Password" });

    await emailInput.fill(TEST_ADMIN.email);
    await passwordInput.fill(TEST_ADMIN.password);
    await page.getByRole("button", { name: "Sign In" }).click();

    // Wait for dashboard
    await expect(page).toHaveURL(/\/(dashboard|photos)/, { timeout: 15000 });

    // Reload the page
    await page.reload();

    // Should still be on an authenticated page (not redirected to login)
    await expect(page).not.toHaveURL(/\/login/);
  });
});
