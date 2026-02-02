/**
 * Keyboard handling utilities for the display frontend
 */

import { logger } from "./config";

/**
 * Exits fullscreen mode if currently in fullscreen.
 * @returns true if exit was attempted, false if not in fullscreen
 */
export function exitFullscreenIfActive(): boolean {
  if (document.fullscreenElement) {
    document.exitFullscreen().catch((err) => {
      logger.warn("Failed to exit fullscreen:", err);
    });
    logger.debug("Exiting fullscreen via Escape key");
    return true;
  }
  return false;
}

/**
 * Handles the Escape key press.
 * Currently exits fullscreen mode if active.
 */
export function handleEscapeKey(): void {
  exitFullscreenIfActive();
}
