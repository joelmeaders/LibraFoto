/**
 * Unit tests for qr-code.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { generateQrCodeDataUrl } from "./qr-code";

// Mock the qrcode library
vi.mock("qrcode", () => ({
  toDataURL: vi.fn(),
}));

// Mock config module
vi.mock("./config", () => ({
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
  },
}));

describe("qr-code", () => {
  let mockToDataURL: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    vi.resetModules();
    const qrcode = await import("qrcode");
    mockToDataURL = qrcode.toDataURL as ReturnType<typeof vi.fn>;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("generateQrCodeDataUrl", () => {
    it("should generate QR code with default options", async () => {
      const expectedDataUrl = "data:image/png;base64,abc123";
      mockToDataURL.mockResolvedValue(expectedDataUrl);

      const result = await generateQrCodeDataUrl("https://example.com/admin");

      expect(result).toBe(expectedDataUrl);
      expect(mockToDataURL).toHaveBeenCalledWith("https://example.com/admin", {
        width: 200,
        margin: 2,
        color: {
          dark: "#000000",
          light: "#ffffff",
        },
      });
    });

    it("should generate QR code with custom options", async () => {
      const expectedDataUrl = "data:image/png;base64,custom123";
      mockToDataURL.mockResolvedValue(expectedDataUrl);

      const result = await generateQrCodeDataUrl("https://example.com/admin", {
        width: 300,
        margin: 4,
        darkColor: "#333333",
        lightColor: "#eeeeee",
      });

      expect(result).toBe(expectedDataUrl);
      expect(mockToDataURL).toHaveBeenCalledWith("https://example.com/admin", {
        width: 300,
        margin: 4,
        color: {
          dark: "#333333",
          light: "#eeeeee",
        },
      });
    });

    it("should return null when QR generation fails", async () => {
      mockToDataURL.mockRejectedValue(new Error("QR generation failed"));

      const result = await generateQrCodeDataUrl("invalid-url");

      expect(result).toBeNull();
    });

    it("should handle partial custom options", async () => {
      const expectedDataUrl = "data:image/png;base64,partial123";
      mockToDataURL.mockResolvedValue(expectedDataUrl);

      const result = await generateQrCodeDataUrl("https://example.com", {
        width: 150,
      });

      expect(result).toBe(expectedDataUrl);
      expect(mockToDataURL).toHaveBeenCalledWith("https://example.com", {
        width: 150,
        margin: 2,
        color: {
          dark: "#000000",
          light: "#ffffff",
        },
      });
    });
  });
});
