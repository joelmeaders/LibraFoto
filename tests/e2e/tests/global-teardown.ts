import { FullConfig } from "@playwright/test";
import * as fs from "fs";
import * as path from "path";

/**
 * Global teardown runs once after all tests complete.
 *
 * Cleans up the isolated test data directory (test-data/) that was used during testing.
 * This includes:
 * - Test database (test-data/librafoto.db)
 * - Test photos (test-data/photos/)
 * - Any other test artifacts
 */
async function globalTeardown(config: FullConfig) {
  console.log("\n🧹 Running global teardown...\n");

  const testDataDir = path.join(process.cwd(), "../..", "test-data");

  try {
    if (fs.existsSync(testDataDir)) {
      console.log(`🗑️  Deleting test data directory: ${testDataDir}`);

      // Recursively delete the test-data directory
      fs.rmSync(testDataDir, { recursive: true, force: true });

      console.log("✅ Test data directory deleted successfully!\n");
    } else {
      console.log("ℹ️  Test data directory not found (already cleaned up)\n");
    }
  } catch (error) {
    console.error(
      `⚠️  Failed to delete test data directory: ${
        error instanceof Error ? error.message : error
      }`
    );
    console.log("   You may need to manually delete the test-data directory\n");
  }

  console.log("🏁 Global teardown complete!\n");
}

export default globalTeardown;
