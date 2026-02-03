/**
 * Unit tests for keyboard.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { exitFullscreenIfActive, handleEscapeKey } from "./keyboard";

// Mock the logger
vi.mock("./config", () => ({
  logger: {
    debug: vi.fn(),
    warn: vi.fn(),
  },
}));

describe("exitFullscreenIfActive", () => {
  let exitFullscreenMock: ReturnType<typeof vi.fn>;
  const originalFullscreenElement = Object.getOwnPropertyDescriptor(
    document,
    "fullscreenElement",
  );

  beforeEach(() => {
    exitFullscreenMock = vi.fn().mockResolvedValue(undefined);
    document.exitFullscreen = exitFullscreenMock;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    // Restore original property
    if (originalFullscreenElement) {
      Object.defineProperty(
        document,
        "fullscreenElement",
        originalFullscreenElement,
      );
    }
  });

  it("should call exitFullscreen and return true when in fullscreen mode", () => {
    // Simulate fullscreen state
    Object.defineProperty(document, "fullscreenElement", {
      value: document.body,
      writable: true,
      configurable: true,
    });

    const result = exitFullscreenIfActive();

    expect(result).toBe(true);
    expect(exitFullscreenMock).toHaveBeenCalled();
  });

  it("should return false and not call exitFullscreen when not in fullscreen mode", () => {
    Object.defineProperty(document, "fullscreenElement", {
      value: null,
      writable: true,
      configurable: true,
    });

    const result = exitFullscreenIfActive();

    expect(result).toBe(false);
    expect(exitFullscreenMock).not.toHaveBeenCalled();
  });
});

describe("handleEscapeKey", () => {
  let exitFullscreenMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    exitFullscreenMock = vi.fn().mockResolvedValue(undefined);
    document.exitFullscreen = exitFullscreenMock;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should attempt to exit fullscreen when called", () => {
    Object.defineProperty(document, "fullscreenElement", {
      value: document.body,
      writable: true,
      configurable: true,
    });

    handleEscapeKey();

    expect(exitFullscreenMock).toHaveBeenCalled();
  });
});
