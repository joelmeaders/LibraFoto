/**
 * Slideshow Controller
 * Manages the photo/video slideshow display and transitions
 */

import {
  ApiClient,
  type PhotoDto,
  type DisplaySettingsDto,
  TransitionType,
  ImageFit,
} from "./api-client";
import { getConfig, logger } from "./config";
import { MediaType } from "./types";
import { generateQrCodeDataUrl } from "./qr-code";

/**
 * State of the slideshow.
 */
interface SlideshowState {
  isInitialized: boolean;
  isPaused: boolean;
  isLoading: boolean;
  error: string | null;
}

/**
 * Main slideshow controller class.
 * Manages photo/video display, transitions, and preloading.
 */
export class Slideshow {
  private apiClient: ApiClient;
  private config = getConfig();

  // State
  private settings: DisplaySettingsDto | null = null;
  private currentPhoto: PhotoDto | null = null;
  private preloadedPhotos: PhotoDto[] = [];
  private state: SlideshowState = {
    isInitialized: false,
    isPaused: false,
    isLoading: false,
    error: null,
  };

  // Timers
  private slideTimer: number | null = null;
  private preloadTimer: number | null = null;
  private videoMaxDurationTimer: number | null = null;
  private qrOverlayTimer: number | null = null;
  private noPhotosPollingTimer: number | null = null;

  // DOM elements
  private currentSlide: HTMLImageElement;
  private nextSlide: HTMLImageElement;
  private currentVideo: HTMLVideoElement | null = null;
  private videoContainer: HTMLElement;
  private loadingIndicator: HTMLElement | null;
  private errorIndicator: HTMLElement | null;
  private readonly qrOverlay: HTMLElement | null;
  private readonly blurBackground: HTMLElement | null;
  private readonly blurBackgroundCurrentImg: HTMLImageElement | null;
  private readonly blurBackgroundNextImg: HTMLImageElement | null;

  constructor(apiClient: ApiClient) {
    this.apiClient = apiClient;

    // Get DOM elements
    this.currentSlide = document.getElementById(
      "current-slide",
    ) as HTMLImageElement;
    this.nextSlide = document.getElementById("next-slide") as HTMLImageElement;
    this.videoContainer = document.getElementById(
      "video-container",
    ) as HTMLElement;
    this.loadingIndicator = document.getElementById("loading-indicator");
    this.errorIndicator = document.getElementById("error-indicator");
    this.qrOverlay = document.getElementById("qr-overlay");
    this.blurBackground = document.getElementById("blur-background");
    this.blurBackgroundCurrentImg = document.getElementById(
      "blur-background-current-img",
    ) as HTMLImageElement;
    this.blurBackgroundNextImg = document.getElementById(
      "blur-background-next-img",
    ) as HTMLImageElement;

    // Listen for settings changes
    this.apiClient.onSettingsChange((newSettings) => {
      this.handleSettingsChange(newSettings);
    });
  }

  /**
   * Starts the slideshow.
   */
  async start(): Promise<void> {
    logger.info("Starting slideshow...");
    this.showLoading(true);
    this.state.error = null;

    // Stop any existing no-photos polling (in case of restart)
    this.stopNoPhotosPolling();

    // Reset QR overlay to ensure clean state (important for bfcache restoration)
    if (this.qrOverlay) {
      this.qrOverlay.classList.add("hidden");
      this.qrOverlay.classList.remove("fade-out");
      this.qrOverlay.innerHTML = "";
    }
    if (this.qrOverlayTimer !== null) {
      clearTimeout(this.qrOverlayTimer);
      this.qrOverlayTimer = null;
    }

    // Hide error indicator if visible
    if (this.errorIndicator) {
      this.errorIndicator.classList.add("hidden");
      this.errorIndicator.innerHTML = "";
    }

    try {
      // Load settings
      const settingsResult = await this.apiClient.getSettings();
      if (settingsResult.success) {
        this.settings = settingsResult.data;
        logger.debug("Settings loaded:", this.settings);
      } else {
        logger.warn("Failed to load settings, using defaults");
        this.settings = this.apiClient.getDefaultSettings();
      }

      // Check if there are photos available
      const countResult = await this.apiClient.getPhotoCount();
      const hasPhotos = countResult.success && countResult.data.totalPhotos > 0;

      // Always fetch admin URL and generate QR code for startup display
      let qrCodeDataUrl: string | null = null;
      let adminUrl: string | undefined;

      const configResult = await this.apiClient.getDisplayConfig();
      if (configResult.success) {
        adminUrl = configResult.data.adminUrl;
        logger.debug("Admin URL from config:", adminUrl);
        qrCodeDataUrl = await generateQrCodeDataUrl(adminUrl);
        if (!qrCodeDataUrl) {
          logger.warn("Failed to generate QR code for admin URL");
        }
      } else {
        logger.warn(
          "Failed to fetch display config, QR code will not be shown",
        );
      }

      if (!hasPhotos) {
        // No photos - show error with QR code and poll for photos
        this.showError(
          "No photos available. Add some photos to get started!",
          qrCodeDataUrl,
          adminUrl,
        );
        // Poll for photos every 10 seconds and auto-start when available
        this.startNoPhotosPolling();
        return;
      }

      // Photos exist - show QR overlay for 30 seconds
      if (qrCodeDataUrl) {
        this.showQrOverlay(qrCodeDataUrl, adminUrl, 30);
      }

      // Initial preload of photos
      await this.preloadPhotos();

      if (this.preloadedPhotos.length === 0) {
        this.showError(
          "Could not load any photos. Please check your connection.",
        );
        return;
      }

      // Start the slideshow
      this.state.isInitialized = true;
      this.showLoading(false);
      await this.showNextSlide();

      // Start settings polling for real-time updates
      this.apiClient.startSettingsPolling();

      logger.info("Slideshow started successfully");
    } catch (error) {
      logger.error("Failed to start slideshow:", error);
      this.showError("Failed to start slideshow. Please refresh the page.");
    }
  }

  /**
   * Pauses the slideshow.
   */
  pause(): void {
    if (this.state.isPaused) return;

    this.state.isPaused = true;
    this.clearTimers();
    logger.debug("Slideshow paused");
  }

  /**
   * Resumes the slideshow.
   */
  resume(): void {
    if (!this.state.isPaused) return;

    this.state.isPaused = false;
    this.scheduleNextSlide();
    logger.debug("Slideshow resumed");
  }

  /**
   * Stops the slideshow completely.
   */
  stop(): void {
    this.clearTimers();
    this.stopNoPhotosPolling();
    this.apiClient.stopSettingsPolling();
    this.state.isInitialized = false;
    logger.info("Slideshow stopped");
  }

  /**
   * Starts polling for photos when none are available.
   * Automatically restarts the slideshow when photos become available.
   */
  private startNoPhotosPolling(): void {
    const pollInterval = 10000; // 10 seconds
    logger.info("No photos available, polling every 10 seconds...");

    this.noPhotosPollingTimer = window.setInterval(async () => {
      const settingsResult = await this.apiClient.getSettings();
      if (settingsResult.success) {
        this.settings = settingsResult.data;
      } else {
        logger.warn("Failed to refresh settings during no-photos polling");
      }

      const countResult = await this.apiClient.getPhotoCount();
      if (countResult.success && countResult.data.totalPhotos > 0) {
        logger.info(
          `Photos now available (${countResult.data.totalPhotos}), restarting slideshow...`,
        );
        this.stopNoPhotosPolling();
        // Restart the slideshow
        await this.start();
      }
    }, pollInterval);
  }

  /**
   * Stops the no-photos polling timer.
   */
  private stopNoPhotosPolling(): void {
    if (this.noPhotosPollingTimer !== null) {
      clearInterval(this.noPhotosPollingTimer);
      this.noPhotosPollingTimer = null;
    }
  }

  /**
   * Checks if the slideshow is currently paused.
   */
  isPaused(): boolean {
    return this.state.isPaused;
  }

  /**
   * Gets the current photo being displayed.
   */
  getCurrentPhoto(): PhotoDto | null {
    return this.currentPhoto;
  }

  // ============================================================================
  // Private Methods
  // ============================================================================

  /**
   * Handles settings changes from the API.
   */
  private handleSettingsChange(newSettings: DisplaySettingsDto): void {
    logger.info("Settings changed, reconfiguring...");

    const oldSettings = this.settings;
    this.settings = newSettings;

    // If source changed, reset preload queue
    if (
      oldSettings &&
      (oldSettings.sourceType !== newSettings.sourceType ||
        oldSettings.sourceId !== newSettings.sourceId ||
        oldSettings.shuffle !== newSettings.shuffle)
    ) {
      logger.debug("Source changed, clearing preload queue");
      this.preloadedPhotos = [];
      this.preloadPhotos();
    }

    // If imageFit changed, update object-fit style and blur background
    if (oldSettings && oldSettings.imageFit !== newSettings.imageFit) {
      logger.debug("ImageFit changed to:", newSettings.imageFit);

      // Always update object-fit style immediately
      const objectFit =
        newSettings.imageFit === ImageFit.Contain ? "contain" : "cover";
      this.currentSlide.style.objectFit = objectFit;
      this.nextSlide.style.objectFit = objectFit;

      // Update blur background visibility (needs photo URL)
      if (this.currentPhoto) {
        const photoUrl = this.apiClient.getPhotoUrl(this.currentPhoto);
        this.updateBlurBackground(photoUrl);
      } else {
        // No current photo, just toggle blur background visibility
        if (this.blurBackground) {
          if (newSettings.imageFit === ImageFit.Contain) {
            this.blurBackground.classList.remove("hidden");
          } else {
            this.blurBackground.classList.add("hidden");
          }
        }
      }
    }
  }

  /**
   * Shows the next slide in the sequence.
   */
  private async showNextSlide(): Promise<void> {
    if (this.state.isPaused || !this.state.isInitialized) return;

    // Clean up any current video
    this.cleanupVideo();

    // Clear Ken Burns from next slide only (current slide keeps animation until transition completes)
    this.clearKenBurnsAnimation(this.nextSlide);

    // Get next photo from preloaded queue or fetch new one
    let nextPhoto = this.preloadedPhotos.shift();

    if (!nextPhoto) {
      // Queue empty, try to fetch directly
      const result = await this.apiClient.getNextPhoto();
      if (result.success) {
        nextPhoto = result.data;
      }
    }

    if (!nextPhoto) {
      // No photos available, retry after delay
      logger.warn("No photo available, retrying...");
      this.scheduleNextSlide();
      return;
    }

    // Trigger background preload if queue is low
    if (this.preloadedPhotos.length < this.config.preloadThreshold) {
      this.schedulePreload();
    }

    // Transition to the new slide
    try {
      if (nextPhoto.mediaType === MediaType.Video) {
        await this.transitionToVideo(nextPhoto);
      } else {
        await this.transitionTo(nextPhoto);
      }
      this.currentPhoto = nextPhoto;
    } catch (error) {
      logger.error("Failed to transition to next slide:", error);
    }

    // Schedule next slide
    this.scheduleNextSlide();
  }

  /**
   * Schedules the next slide transition.
   */
  private scheduleNextSlide(): void {
    if (!this.settings || this.state.isPaused) return;

    // Calculate duration based on media type
    let duration = this.settings.slideDuration * 1000;

    // For videos, use video duration (capped by max video duration from settings)
    // Note: if video is playing, video end event handles transition, so we don't schedule
    if (this.currentPhoto?.mediaType === MediaType.Video && this.currentVideo) {
      // Video transition is handled by video ended event
      return;
    }

    this.slideTimer = window.setTimeout(() => {
      this.showNextSlide();
    }, duration);
  }

  /**
   * Transitions to a video with fade-in.
   */
  private async transitionToVideo(photo: PhotoDto): Promise<void> {
    if (!this.settings) return;

    const videoUrl = this.apiClient.getPhotoUrl(photo);
    logger.debug("Transitioning to video:", videoUrl);

    // Hide blur background for videos
    this.blurBackground?.classList.add("hidden");

    // Hide current image with fade out
    this.currentSlide.classList.add("fade-out");

    return new Promise((resolve, reject) => {
      // Create video element
      const video = document.createElement("video");
      video.src = videoUrl;
      video.muted = true; // Required for autoplay
      video.playsInline = true;
      video.autoplay = true;
      video.className = "video-player fade-in";

      // Handle video loaded
      video.oncanplay = () => {
        logger.debug("Video can play");
      };

      video.onplay = () => {
        logger.debug("Video started playing");
        // Hide the image
        this.currentSlide.style.opacity = "0";
        resolve();
      };

      // Handle video end - transition to next slide
      video.onended = () => {
        logger.debug("Video ended");
        this.showNextSlide();
      };

      // Handle video error
      video.onerror = (e) => {
        logger.error("Video error:", e);
        // Skip to next slide on error
        reject(new Error("Failed to load video"));
      };

      // Max duration handling - skip after maxVideoDuration seconds
      const maxDuration = this.config.maxVideoDuration ?? 30;
      if (photo.duration && photo.duration > maxDuration) {
        this.videoMaxDurationTimer = window.setTimeout(() => {
          logger.debug("Video max duration reached, skipping");
          this.showNextSlide();
        }, maxDuration * 1000);
      }

      // Add video to container
      this.videoContainer.innerHTML = "";
      this.videoContainer.appendChild(video);
      this.videoContainer.classList.remove("hidden");
      this.currentVideo = video;
    });
  }

  /**
   * Cleans up current video if any.
   */
  private cleanupVideo(): void {
    if (this.videoMaxDurationTimer !== null) {
      clearTimeout(this.videoMaxDurationTimer);
      this.videoMaxDurationTimer = null;
    }

    if (this.currentVideo) {
      this.currentVideo.pause();
      this.currentVideo.src = "";
      this.currentVideo.remove();
      this.currentVideo = null;
    }

    this.videoContainer.innerHTML = "";
    this.videoContainer.classList.add("hidden");

    // Show image container again
    this.currentSlide.classList.remove("fade-out");
    this.currentSlide.style.opacity = "1";
  }

  /**
   * Transitions to a new photo with the configured animation.
   */
  private async transitionTo(photo: PhotoDto): Promise<void> {
    if (!this.settings) return;

    const photoUrl = this.apiClient.getPhotoUrl(photo);

    // Preload the image first
    try {
      await this.apiClient.preloadImage(photoUrl);
    } catch (error) {
      logger.warn(
        "Failed to preload image, attempting to display anyway:",
        error,
      );
    }

    // Update blur background for contain mode
    this.updateBlurBackground(photoUrl);

    // Set up next slide
    this.nextSlide.src = photoUrl;

    // Get transition settings
    const transition = this.settings.transition;
    const duration = this.settings.transitionDuration;

    return new Promise((resolve) => {
      // Apply transition based on type
      if (transition === TransitionType.Fade) {
        this.applyFadeTransition(duration, resolve);
      } else if (transition === TransitionType.Slide) {
        this.applySlideTransition(duration, resolve);
      } else if (transition === TransitionType.KenBurns) {
        this.applyKenBurnsTransition(duration, resolve);
      } else {
        // Default: instant switch
        this.applyInstantTransition(photoUrl, resolve);
      }
    });
  }

  /**
   * Applies a fade transition between slides.
   */
  private applyFadeTransition(duration: number, onComplete: () => void): void {
    // Show next slide and start fade
    this.nextSlide.classList.remove("hidden");
    this.nextSlide.classList.add("visible", "fade-in");
    this.currentSlide.classList.add("fade-out");

    setTimeout(() => {
      // Swap slides
      const nextSrc = this.nextSlide.src;
      this.currentSlide.src = nextSrc;

      // Reset classes
      this.currentSlide.classList.remove("fade-out");
      this.nextSlide.classList.remove("visible", "fade-in");
      this.nextSlide.classList.add("hidden");
      this.nextSlide.src = "";

      onComplete();
    }, duration);
  }

  /**
   * Applies a slide transition between slides.
   */
  private applySlideTransition(duration: number, onComplete: () => void): void {
    // Show next slide and start slide animation
    this.nextSlide.classList.remove("hidden");
    this.nextSlide.classList.add("slide-in-left");
    this.currentSlide.classList.add("slide-out-left");

    setTimeout(() => {
      // Swap slides
      const nextSrc = this.nextSlide.src;
      this.currentSlide.src = nextSrc;

      // Reset classes
      this.currentSlide.classList.remove("slide-out-left");
      this.nextSlide.classList.remove("slide-in-left");
      this.nextSlide.classList.add("hidden");
      this.nextSlide.src = "";

      onComplete();
    }, duration);
  }

  /**
   * Applies a Ken Burns transition (pan and zoom effect).
   * The image fades in, then slowly pans and zooms during the slide duration.
   */
  private applyKenBurnsTransition(
    duration: number,
    onComplete: () => void,
  ): void {
    // Freeze current slide at its final Ken Burns position before fading
    this.freezeKenBurnsPosition(this.currentSlide);

    // Generate random pan direction for variety
    const xDir = Math.random() > 0.5 ? 1 : -1;
    const yDir = Math.random() > 0.5 ? 1 : -1;
    const xAmount = (Math.random() * 1 + 0.5) * xDir; // 0.5% to 1.5%
    const yAmount = (Math.random() * 1 + 0.5) * yDir; // 0.5% to 1.5%

    // Set CSS custom properties for the Ken Burns animation
    const slideDuration = this.settings?.slideDuration ?? 10;
    this.nextSlide.style.setProperty("--slide-duration", `${slideDuration}s`);
    this.nextSlide.style.setProperty("--transition-duration", `${duration}ms`);
    this.nextSlide.style.setProperty("--ken-burns-x", `${xAmount}%`);
    this.nextSlide.style.setProperty("--ken-burns-y", `${yAmount}%`);

    // Show next slide with Ken Burns fade-in, then apply Ken Burns pan/zoom
    this.nextSlide.classList.remove("hidden");
    this.nextSlide.classList.add("visible", "ken-burns-fade-in");
    this.currentSlide.classList.add("fade-out");

    // After fade transition, start the Ken Burns pan/zoom effect
    setTimeout(() => {
      // Remove fade class and add Ken Burns pan/zoom
      this.nextSlide.classList.remove("ken-burns-fade-in");
      this.nextSlide.classList.add("ken-burns");

      // Swap slides
      const nextSrc = this.nextSlide.src;
      this.currentSlide.src = nextSrc;

      // Clear the frozen position and reset current slide
      this.clearKenBurnsAnimation(this.currentSlide);

      // Copy Ken Burns properties to current slide
      this.currentSlide.style.setProperty(
        "--slide-duration",
        `${slideDuration}s`,
      );
      this.currentSlide.style.setProperty("--ken-burns-x", `${xAmount}%`);
      this.currentSlide.style.setProperty("--ken-burns-y", `${yAmount}%`);
      this.currentSlide.classList.add("ken-burns");

      // Reset next slide
      this.currentSlide.classList.remove("fade-out");
      this.nextSlide.classList.remove("visible", "ken-burns");
      this.nextSlide.classList.add("hidden");
      this.nextSlide.src = "";

      onComplete();
    }, duration);
  }

  /**
   * Freezes the Ken Burns animation at its current position.
   * Gets the computed transform and applies it as an inline style,
   * then removes the animation class so it doesn't snap back.
   */
  private freezeKenBurnsPosition(element: HTMLElement): void {
    if (!element.classList.contains("ken-burns")) return;

    // Get the current computed transform
    const computedStyle = window.getComputedStyle(element);
    const currentTransform = computedStyle.transform;

    // Apply the current transform as an inline style
    if (currentTransform && currentTransform !== "none") {
      element.style.transform = currentTransform;
    }

    // Remove the animation class (the inline transform keeps it in place)
    element.classList.remove("ken-burns");
  }

  /**
   * Clears Ken Burns animation from an element.
   */
  private clearKenBurnsAnimation(element: HTMLElement): void {
    element.classList.remove("ken-burns", "ken-burns-fade-in");
    element.style.removeProperty("--slide-duration");
    element.style.removeProperty("--transition-duration");
    element.style.removeProperty("--ken-burns-x");
    element.style.removeProperty("--ken-burns-y");
    element.style.removeProperty("transform"); // Clear frozen position
  }

  /**
   * Applies an instant transition (no animation).
   */
  private applyInstantTransition(
    photoUrl: string,
    onComplete: () => void,
  ): void {
    this.currentSlide.src = photoUrl;
    onComplete();
  }

  /**
   * Updates the blur background image for contain mode.
   * Shows a zoomed, blurred version of the current photo behind the main image.
   * Also updates the object-fit style for the main images based on imageFit setting.
   * Uses crossfade transition to smoothly transition between backgrounds.
   */
  private updateBlurBackground(photoUrl: string): void {
    if (
      !this.settings ||
      !this.blurBackground ||
      !this.blurBackgroundCurrentImg ||
      !this.blurBackgroundNextImg
    ) {
      return;
    }

    // Update object-fit style for main images based on imageFit setting
    const objectFit =
      this.settings.imageFit === ImageFit.Contain ? "contain" : "cover";
    this.currentSlide.style.objectFit = objectFit;
    this.nextSlide.style.objectFit = objectFit;

    // Only show blur background in contain mode
    if (this.settings.imageFit === ImageFit.Contain) {
      this.blurBackground.classList.remove("hidden");

      // Set up next blur background image
      this.blurBackgroundNextImg.src = photoUrl;

      // Apply fade transition to blur backgrounds
      this.blurBackgroundNextImg.classList.remove("hidden");
      this.blurBackgroundNextImg.classList.add("visible", "fade-in");
      this.blurBackgroundCurrentImg.classList.add("fade-out");

      // Get transition duration from settings
      const duration = this.settings.transitionDuration;

      // After transition completes, swap the blur background images
      setTimeout(() => {
        if (this.blurBackgroundCurrentImg && this.blurBackgroundNextImg) {
          // Copy next src to current
          this.blurBackgroundCurrentImg.src = this.blurBackgroundNextImg.src;

          // Reset classes
          this.blurBackgroundCurrentImg.classList.remove("fade-out");
          this.blurBackgroundCurrentImg.classList.add("visible");
          this.blurBackgroundNextImg.classList.remove("visible", "fade-in");
          this.blurBackgroundNextImg.classList.add("hidden");
          this.blurBackgroundNextImg.src = "";
        }
      }, duration);
    } else {
      this.blurBackground.classList.add("hidden");
    }
  }

  /**
   * Preloads photos into the queue.
   */
  private async preloadPhotos(): Promise<void> {
    logger.debug("Preloading photos...");

    const photos = await this.apiClient.preloadPhotosWithImages(
      this.config.preloadCount,
    );

    if (photos.length > 0) {
      this.preloadedPhotos.push(...photos);
      logger.debug(
        `Preloaded ${photos.length} photos, queue size: ${this.preloadedPhotos.length}`,
      );
    } else {
      logger.warn("No photos returned from preload");
    }
  }

  /**
   * Schedules a background preload operation.
   */
  private schedulePreload(): void {
    // Don't schedule if already pending
    if (this.preloadTimer !== null) return;

    this.preloadTimer = window.setTimeout(async () => {
      this.preloadTimer = null;
      await this.preloadPhotos();
    }, 100); // Small delay to not block current transition
  }

  /**
   * Clears slideshow-related timers (not the QR overlay timer).
   * The QR overlay timer is intentionally not cleared here because
   * the QR code should fade out independently of slideshow pause/resume.
   */
  private clearTimers(): void {
    if (this.slideTimer !== null) {
      clearTimeout(this.slideTimer);
      this.slideTimer = null;
    }
    if (this.preloadTimer !== null) {
      clearTimeout(this.preloadTimer);
      this.preloadTimer = null;
    }
    if (this.videoMaxDurationTimer !== null) {
      clearTimeout(this.videoMaxDurationTimer);
      this.videoMaxDurationTimer = null;
    }
    // Note: qrOverlayTimer is intentionally not cleared here
    // The QR overlay should continue its countdown even when paused
  }

  /**
   * Shows or hides the loading indicator.
   */
  private showLoading(show: boolean): void {
    this.state.isLoading = show;
    if (this.loadingIndicator) {
      this.loadingIndicator.classList.toggle("hidden", !show);
    }
  }

  /**
   * Shows an error message with optional QR code.
   */
  private showError(
    message: string,
    qrCodeDataUrl?: string | null,
    adminUrl?: string,
  ): void {
    this.state.error = message;
    this.showLoading(false);

    if (this.errorIndicator) {
      // Clear previous content
      this.errorIndicator.innerHTML = "";

      // Add message
      const messageEl = document.createElement("div");
      messageEl.className = "error-message";
      messageEl.textContent = message;
      this.errorIndicator.appendChild(messageEl);

      // Add QR code if available
      if (qrCodeDataUrl) {
        const qrContainer = document.createElement("div");
        qrContainer.className = "error-qr-container";

        const qrImage = document.createElement("img");
        qrImage.className = "error-qr-code";
        qrImage.src = qrCodeDataUrl;
        qrImage.alt = "Scan to open admin panel";
        qrContainer.appendChild(qrImage);

        const qrLabel = document.createElement("div");
        qrLabel.className = "error-qr-label";
        qrLabel.textContent = "Scan to open admin panel";
        qrContainer.appendChild(qrLabel);

        if (adminUrl) {
          const urlLabel = document.createElement("div");
          urlLabel.className = "error-qr-url";
          urlLabel.textContent = adminUrl;
          qrContainer.appendChild(urlLabel);
        }

        this.errorIndicator.appendChild(qrContainer);
      }

      this.errorIndicator.classList.remove("hidden");
    } else {
      logger.error("Error:", message);
    }
  }

  /**
   * Shows the QR code overlay for a specified duration.
   * @param qrCodeDataUrl The data URL of the QR code image
   * @param adminUrl The admin URL to display
   * @param durationSeconds How long to show the overlay (in seconds)
   */
  private showQrOverlay(
    qrCodeDataUrl: string,
    adminUrl: string | undefined,
    durationSeconds: number,
  ): void {
    if (!this.qrOverlay) {
      logger.warn("QR overlay element not found");
      return;
    }

    // Clear previous content and timer
    this.qrOverlay.innerHTML = "";
    if (this.qrOverlayTimer !== null) {
      clearTimeout(this.qrOverlayTimer);
      this.qrOverlayTimer = null;
    }

    // Create QR image
    const qrImage = document.createElement("img");
    qrImage.className = "qr-image";
    qrImage.src = qrCodeDataUrl;
    qrImage.alt = "Scan to open admin panel";
    this.qrOverlay.appendChild(qrImage);

    // Create label
    const qrLabel = document.createElement("div");
    qrLabel.className = "qr-label";
    qrLabel.textContent = "Scan to open admin panel";
    this.qrOverlay.appendChild(qrLabel);

    // Create URL display
    if (adminUrl) {
      const urlLabel = document.createElement("div");
      urlLabel.className = "qr-url";
      urlLabel.textContent = adminUrl;
      this.qrOverlay.appendChild(urlLabel);
    }

    // Create countdown display
    const countdownLabel = document.createElement("div");
    countdownLabel.className = "qr-countdown";
    countdownLabel.textContent = `Hiding in ${durationSeconds}s`;
    this.qrOverlay.appendChild(countdownLabel);

    // Show the overlay
    this.qrOverlay.classList.remove("hidden", "fade-out");
    logger.info(
      `QR overlay shown, classList: ${this.qrOverlay.classList.toString()}, duration: ${durationSeconds}s`,
    );

    // Update countdown every second
    let remainingSeconds = durationSeconds;
    const countdownInterval = window.setInterval(() => {
      remainingSeconds--;
      if (remainingSeconds > 0) {
        countdownLabel.textContent = `Hiding in ${remainingSeconds}s`;
      } else {
        clearInterval(countdownInterval);
      }
    }, 1000);

    // Schedule fade-out and hide
    this.qrOverlayTimer = window.setTimeout(() => {
      this.hideQrOverlay();
      clearInterval(countdownInterval);
    }, durationSeconds * 1000);
  }

  /**
   * Hides the QR code overlay with a fade-out animation.
   */
  private hideQrOverlay(): void {
    if (!this.qrOverlay) return;

    this.qrOverlay.classList.add("fade-out");

    // Remove from DOM after animation completes
    setTimeout(() => {
      if (this.qrOverlay) {
        this.qrOverlay.classList.add("hidden");
        this.qrOverlay.classList.remove("fade-out");
      }
    }, 500); // Match the CSS transition duration

    if (this.qrOverlayTimer !== null) {
      clearTimeout(this.qrOverlayTimer);
      this.qrOverlayTimer = null;
    }

    logger.debug("QR overlay hidden");
  }
}
