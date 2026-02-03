import { request, FullConfig } from "@playwright/test";
import * as fs from "fs";
import * as path from "path";

const API_BASE_URL = "http://localhost:5179";

const TEST_ADMIN = {
  email: "testadmin@librafoto.local",
  password: "TestPassword123!",
};

const TEST_EDITOR = {
  email: "testeditor@librafoto.local",
  password: "TestPassword123!",
};

const TEST_GUEST = {
  email: "testguest@librafoto.local",
  password: "TestPassword123!",
};

/**
 * Global setup runs once before all tests.
 *
 * This ensures the test environment is in a known state:
 * 1. Create isolated test-data directory for test database and photos
 * 2. Wait for API to be ready
 * 3. Reset the database to a clean state
 * 4. Test admin user is created automatically by reset
 * 5. Create test editor and guest users for role-based testing
 */
async function globalSetup(config: FullConfig) {
  console.log("\n🔧 Running global setup...\n");

  // Ensure test-data directory exists for isolated test database and photos
  const testDataDir = path.join(process.cwd(), "../..", "test-data");
  if (!fs.existsSync(testDataDir)) {
    console.log(`📁 Creating test data directory: ${testDataDir}`);
    fs.mkdirSync(testDataDir, { recursive: true });
    console.log("✅ Test data directory created!\n");
  } else {
    console.log(`📁 Test data directory exists: ${testDataDir}\n`);
  }

  // Create API request context
  const apiContext = await request.newContext({
    baseURL: API_BASE_URL,
  });

  try {
    // Wait for API to be ready (up to 60 seconds)
    console.log("⏳ Waiting for API to be ready...");
    let apiReady = false;
    const maxAttempts = 30;

    for (let i = 0; i < maxAttempts; i++) {
      try {
        const response = await apiContext.get("/health");
        if (response.ok()) {
          apiReady = true;
          break;
        }
      } catch {
        // API not ready yet
      }
      await new Promise((resolve) => setTimeout(resolve, 2000));
      process.stdout.write(".");
    }

    if (!apiReady) {
      throw new Error("API failed to start within 60 seconds");
    }
    console.log("\n✅ API is ready!\n");

    // Reset database to clean state
    console.log("🔄 Resetting database to clean state...");
    const resetResponse = await apiContext.post("/api/test/reset");

    if (resetResponse.ok()) {
      const resetResult = await resetResponse.json();
      console.log(`✅ Database reset successful!`);
      console.log(`   Test admin email: ${resetResult.testAdminEmail}\n`);
    } else {
      const errorText = await resetResponse.text();
      console.error(`❌ Database reset failed: ${errorText}`);

      // Fall back to checking setup status if reset fails (e.g., in production)
      console.log("📋 Falling back to setup status check...");
      const setupResponse = await apiContext.get("/api/setup/status");
      const setupStatus = await setupResponse.json();

      if (setupStatus.isSetupRequired) {
        console.log("🔐 Setup required - creating test admin user...");

        const setupCompleteResponse = await apiContext.post(
          "/api/setup/complete",
          {
            data: {
              email: TEST_ADMIN.email,
              password: TEST_ADMIN.password,
            },
          }
        );

        if (setupCompleteResponse.ok()) {
          console.log(`✅ Test admin user created successfully!\n`);
        } else {
          const setupError = await setupCompleteResponse.text();
          console.error(`❌ Failed to create test admin: ${setupError}`);
          throw new Error(`Setup failed: ${setupError}`);
        }
      } else {
        // Try to login with test admin credentials
        const loginResponse = await apiContext.post("/api/auth/login", {
          data: {
            email: TEST_ADMIN.email,
            password: TEST_ADMIN.password,
          },
        });

        if (loginResponse.ok()) {
          console.log(`✅ Test admin user verified!\n`);
        } else {
          console.log(
            "⚠️  Test admin login failed - tests may need manual setup"
          );
          console.log(
            "   If this is a fresh database, delete librafoto.db and restart the API\n"
          );
        }
      }
    }

    // Verify test admin can login
    console.log("🔐 Verifying test admin login...");
    const loginResponse = await apiContext.post("/api/auth/login", {
      data: {
        email: TEST_ADMIN.email,
        password: TEST_ADMIN.password,
      },
    });

    if (loginResponse.ok()) {
      console.log(`✅ Test admin login verified!\n`);

      // Create test editor and guest users
      const loginData = await loginResponse.json();
      const authHeaders = { Authorization: `Bearer ${loginData.token}` };

      console.log("👥 Creating test editor and guest users...");

      // Create editor user (role: 1 = Editor)
      const editorResponse = await apiContext.post("/api/admin/users", {
        headers: authHeaders,
        data: {
          email: TEST_EDITOR.email,
          password: TEST_EDITOR.password,
          role: 1, // Editor
        },
      });

      if (editorResponse.ok()) {
        console.log(`   ✅ Editor user created`);
      } else {
        console.log(`   ⚠️  Editor user creation failed (may already exist)`);
      }

      // Create guest user (role: 0 = Guest)
      const guestResponse = await apiContext.post("/api/admin/users", {
        headers: authHeaders,
        data: {
          email: TEST_GUEST.email,
          password: TEST_GUEST.password,
          role: 0, // Guest
        },
      });

      if (guestResponse.ok()) {
        console.log(`   ✅ Guest user created\n`);
      } else {
        console.log(`   ⚠️  Guest user creation failed (may already exist)\n`);
      }
    } else {
      throw new Error("Test admin login failed after database reset");
    }

    console.log("🚀 Global setup complete! Ready to run tests.\n");
  } finally {
    await apiContext.dispose();
  }
}

export default globalSetup;
