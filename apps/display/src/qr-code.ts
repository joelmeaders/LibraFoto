/**
 * QR Code generation utilities for LibraFoto Display Frontend.
 * Used to display a QR code linking to the admin interface when no photos are available.
 */

import { toDataURL } from "qrcode";
import { logger } from "./config";

/**
 * Options for QR code generation.
 */
export interface QrCodeOptions {
  /** Width of the QR code in pixels. Default: 200 */
  width?: number;
  /** Margin around the QR code in modules. Default: 2 */
  margin?: number;
  /** Dark color (foreground). Default: "#000000" */
  darkColor?: string;
  /** Light color (background). Default: "#ffffff" */
  lightColor?: string;
}

const defaultOptions: Required<QrCodeOptions> = {
  width: 200,
  margin: 2,
  darkColor: "#000000",
  lightColor: "#ffffff",
};

/**
 * Generates a QR code as a data URL.
 * @param url The URL to encode in the QR code
 * @param options Optional configuration for the QR code appearance
 * @returns A data URL containing the QR code image, or null if generation fails
 */
export async function generateQrCodeDataUrl(
  url: string,
  options?: QrCodeOptions,
): Promise<string | null> {
  const opts = { ...defaultOptions, ...options };

  try {
    const dataUrl = await toDataURL(url, {
      width: opts.width,
      margin: opts.margin,
      color: {
        dark: opts.darkColor,
        light: opts.lightColor,
      },
    });

    logger.debug("QR code generated for URL:", url);
    return dataUrl;
  } catch (error) {
    logger.error("Failed to generate QR code:", error);
    return null;
  }
}
