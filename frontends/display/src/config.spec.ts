/**
 * Unit tests for config.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// We need to mock import.meta.env before importing config
vi.stubGlobal("import", {
  meta: {
    env: {
      VITE_API_BASE_URL: "",
      VITE_DEBUG: "",
      VITE_SETTINGS_POLL_INTERVAL: "",
      VITE_PRELOAD_COUNT: "",
      VITE_PRELOAD_THRESHOLD: "",
      VITE_RETRY_DELAY: "",
      VITE_MAX_RETRIES: "",
      VITE_MAX_VIDEO_DURATION: "",
      DEV: false,
    },
  },
});

describe("getConfig", () => {
  it("should return configuration with sensible defaults", async () => {
    const { getConfig } = await import("./config");
    const config = getConfig();

    expect(config.apiBaseUrl).toBe("/api");
    expect(config.settingsPollingInterval).toBe(5000);
    expect(config.preloadCount).toBe(10);
    expect(config.preloadThreshold).toBe(3);
    expect(config.retryDelay).toBe(5000);
    expect(config.maxRetries).toBe(3);
    expect(config.maxVideoDuration).toBe(30);
  });
});

describe("logger", () => {
  let consoleSpy: {
    info: ReturnType<typeof vi.spyOn>;
    warn: ReturnType<typeof vi.spyOn>;
    error: ReturnType<typeof vi.spyOn>;
  };

  beforeEach(() => {
    consoleSpy = {
      info: vi.spyOn(console, "info").mockImplementation(() => {}),
      warn: vi.spyOn(console, "warn").mockImplementation(() => {}),
      error: vi.spyOn(console, "error").mockImplementation(() => {}),
    };
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should log messages with [LibraFoto] prefix", async () => {
    const { logger } = await import("./config");

    logger.info("info message");
    logger.warn("warn message");
    logger.error("error message", { details: 123 });

    expect(consoleSpy.info).toHaveBeenCalledWith("[LibraFoto]", "info message");
    expect(consoleSpy.warn).toHaveBeenCalledWith("[LibraFoto]", "warn message");
    expect(consoleSpy.error).toHaveBeenCalledWith(
      "[LibraFoto]",
      "error message",
      { details: 123 },
    );
  });

  it("should support multiple arguments", async () => {
    const { logger } = await import("./config");

    logger.info("message", "arg1", { key: "value" }, 123);

    expect(consoleSpy.info).toHaveBeenCalledWith(
      "[LibraFoto]",
      "message",
      "arg1",
      { key: "value" },
      123,
    );
  });
});
