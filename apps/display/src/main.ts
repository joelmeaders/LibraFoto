/**
 * LibraFoto Display Frontend
 * Main entry point for the digital picture frame slideshow
 */

import { Slideshow } from "./slideshow";
import { ApiClient } from "./api-client";
import { logger, getConfig } from "./config";
import { handleEscapeKey } from "./keyboard";
import "./styles.css";

// Application instance references
let slideshow: Slideshow | null = null;
let apiClient: ApiClient | null = null;

/**
 * Registers the service worker for offline support.
 */
async function registerServiceWorker(): Promise<void> {
  if ("serviceWorker" in navigator) {
    try {
      const registration = await navigator.serviceWorker.register("/sw.js", {
        scope: "/",
      });

      logger.info("Service Worker registered:", registration.scope);

      // Handle updates
      registration.addEventListener("updatefound", () => {
        const newWorker = registration.installing;
        if (newWorker) {
          newWorker.addEventListener("statechange", () => {
            if (
              newWorker.state === "installed" &&
              navigator.serviceWorker.controller
            ) {
              logger.info("New content available, refresh to update");
            }
          });
        }
      });
    } catch (error) {
      logger.warn("Service Worker registration failed:", error);
    }
  }
}

/**
 * Initializes and starts the application.
 */
async function initializeApp(): Promise<void> {
  const config = getConfig();
  logger.info("LibraFoto Display initializing...", {
    apiBaseUrl: config.apiBaseUrl,
    debug: config.debug,
  });

  try {
    // Register service worker first
    await registerServiceWorker();

    // Initialize components
    apiClient = new ApiClient();
    slideshow = new Slideshow(apiClient);

    // Start the slideshow
    await slideshow.start();

    // Hide cursor after a period of inactivity
    setupCursorHiding();

    logger.info("LibraFoto Display initialized successfully");
  } catch (error) {
    logger.error("Failed to initialize LibraFoto Display:", error);
    showFatalError(
      "Failed to start the slideshow. Please check your connection and refresh the page.",
    );
  }
}

/**
 * Displays a fatal error message to the user.
 */
function showFatalError(message: string): void {
  const errorElement = document.getElementById("error-indicator");
  if (errorElement) {
    errorElement.textContent = message;
    errorElement.classList.remove("hidden");
  }

  // Hide loading indicator if visible
  const loadingElement = document.getElementById("loading-indicator");
  if (loadingElement) {
    loadingElement.classList.add("hidden");
  }
}

/**
 * Sets up cursor hiding after inactivity.
 */
function setupCursorHiding(): void {
  let cursorTimeout: number | null = null;
  const CURSOR_HIDE_DELAY = 3000; // 3 seconds

  const showCursor = () => {
    document.body.classList.remove("cursor-hidden");

    if (cursorTimeout) {
      clearTimeout(cursorTimeout);
    }

    cursorTimeout = window.setTimeout(() => {
      document.body.classList.add("cursor-hidden");
    }, CURSOR_HIDE_DELAY);
  };

  // Show cursor on mouse movement
  document.addEventListener("mousemove", showCursor);

  // Initial hide after delay
  cursorTimeout = window.setTimeout(() => {
    document.body.classList.add("cursor-hidden");
  }, CURSOR_HIDE_DELAY);
}

/**
 * Handles visibility changes (pause when tab is hidden).
 */
function handleVisibilityChange(): void {
  if (!slideshow) return;

  if (document.hidden) {
    slideshow.pause();
    logger.debug("App paused (hidden)");
  } else {
    slideshow.resume();
    logger.debug("App resumed (visible)");
  }
}

/**
 * Handles window focus events.
 */
function handleFocus(): void {
  if (slideshow && !slideshow.isPaused()) {
    // Refresh settings when window regains focus
    logger.debug("Window focused, checking for updates...");
  }
}

/**
 * Handles keyboard events for manual control.
 */
function handleKeydown(event: KeyboardEvent): void {
  if (!slideshow) return;

  switch (event.key) {
    case " ":
    case "Space":
      // Toggle pause
      event.preventDefault();
      if (slideshow.isPaused()) {
        slideshow.resume();
        logger.debug("Slideshow resumed via keyboard");
      } else {
        slideshow.pause();
        logger.debug("Slideshow paused via keyboard");
      }
      break;

    case "F11":
      // Enter fullscreen (browser handles this, but log it)
      logger.debug("Fullscreen toggle requested");
      break;

    case "Escape":
      // Exit fullscreen mode
      handleEscapeKey();
      break;
  }
}

/**
 * Handles errors that occur during runtime.
 */
function handleError(event: ErrorEvent): void {
  logger.error("Runtime error:", event.error);
}

/**
 * Handles unhandled promise rejections.
 */
function handleUnhandledRejection(event: PromiseRejectionEvent): void {
  logger.error("Unhandled promise rejection:", event.reason);
}

// ============================================================================
// Event Listeners
// ============================================================================

// Start the app when DOM is ready
document.addEventListener("DOMContentLoaded", initializeApp);

// Handle page restoration from bfcache (back-forward cache)
// When a page is restored from bfcache, DOMContentLoaded doesn't fire,
// but the DOM and JS state are preserved. We need to reinitialize to show the QR code again.
window.addEventListener("pageshow", (event) => {
  if (event.persisted) {
    logger.debug("Page restored from bfcache, reinitializing...");
    // Stop existing slideshow if running
    slideshow?.stop();
    // Reinitialize the app
    initializeApp();
  }
});

// Handle visibility changes (pause when tab is hidden)
document.addEventListener("visibilitychange", handleVisibilityChange);

// Handle window focus
window.addEventListener("focus", handleFocus);

// Handle keyboard events
document.addEventListener("keydown", handleKeydown);

// Handle errors
window.addEventListener("error", handleError);
window.addEventListener("unhandledrejection", handleUnhandledRejection);

// Cleanup on page unload
window.addEventListener("beforeunload", () => {
  slideshow?.stop();
  logger.debug("App cleanup completed");
});
