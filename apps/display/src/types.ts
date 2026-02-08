/**
 * TypeScript interfaces matching the backend API DTOs
 * These types are derived from LibraFoto.Modules.Display.Models and LibraFoto.Data.Enums
 */

// ============================================================================
// Enums (matching LibraFoto.Data.Enums)
// ============================================================================

/**
 * Types of transitions between slides in the display.
 */
export enum TransitionType {
  /** Fade transition (crossfade between images). */
  Fade = 0,
  /** Slide transition (slides in from side). */
  Slide = 1,
  /** Ken Burns effect (slow pan and zoom while displaying). */
  KenBurns = 2,
}

/**
 * Source type for slideshow content filtering.
 */
export enum SourceType {
  /** Show all photos from all sources. */
  All = 0,
  /** Show photos from a specific album. */
  Album = 1,
  /** Show photos with a specific tag. */
  Tag = 2,
}

/**
 * Type of media file.
 */
export enum MediaType {
  /** Image file (JPEG, PNG, WebP, etc.) */
  Photo = 0,
  /** Video file (MP4, MOV, etc.) */
  Video = 1,
}

/**
 * Specifies how images should be fitted within the display area.
 */
export enum ImageFit {
  /** Scale image to fit within the display area (letterboxing may occur). */
  Contain = 0,
  /** Scale image to fill the display area (parts may be cropped). */
  Cover = 1,
}

// ============================================================================
// DTOs (matching LibraFoto.Modules.Display.Models)
// ============================================================================

/**
 * Photo data transfer object for display frontend.
 * Contains only the information needed for the slideshow.
 */
export interface PhotoDto {
  /** Unique identifier for the photo. */
  id: number;
  /** URL to the full-size photo for display. */
  url: string;
  /** URL to the thumbnail for preloading/preview. */
  thumbnailUrl?: string;
  /** Date the photo was taken (ISO 8601 string). */
  dateTaken?: string;
  /** Location where the photo was taken. */
  location?: string;
  /** Type of media (Photo or Video). */
  mediaType: MediaType;
  /** Duration in seconds for video files. Null for photos. */
  duration?: number;
  /** Width of the photo in pixels. */
  width: number;
  /** Height of the photo in pixels. */
  height: number;
}

/**
 * Display settings data transfer object.
 * Contains all settings needed to configure the slideshow display.
 */
export interface DisplaySettingsDto {
  /** Settings ID. */
  id: number;
  /** Name of this display configuration. */
  name: string;
  /** Duration each slide is displayed in seconds. */
  slideDuration: number;
  /** Type of transition between slides. */
  transition: TransitionType;
  /** Duration of the transition animation in milliseconds. */
  transitionDuration: number;
  /** Source type for filtering which photos to display. */
  sourceType: SourceType;
  /** ID of the source (Album or Tag) when SourceType is not All. */
  sourceId?: number;
  /** Whether to shuffle photos randomly. */
  shuffle: boolean;
  /** How images should be fitted within the display area. */
  imageFit: ImageFit;
}

/**
 * Response for photo count endpoint.
 */
export interface PhotoCountResponse {
  totalPhotos: number;
}

/**
 * Response for reset endpoint.
 */
export interface ResetResponse {
  success: boolean;
  message: string;
}

/**
 * API error response.
 */
export interface ApiError {
  code: string;
  message: string;
  details?: unknown;
}

/**
 * Response for display config endpoint.
 */
export interface DisplayConfigResponse {
  adminUrl: string;
}

// ============================================================================
// Application State Types
// ============================================================================

/**
 * Loading state for async operations.
 */
export interface LoadingState {
  isLoading: boolean;
  error: string | null;
}

/**
 * Slideshow state.
 */
export interface SlideshowState {
  currentPhoto: PhotoDto | null;
  preloadedPhotos: PhotoDto[];
  settings: DisplaySettingsDto | null;
  isPaused: boolean;
  isInitialized: boolean;
  error: string | null;
}

/**
 * Event types for slideshow updates.
 */
export type SlideshowEventType =
  | "photo-changed"
  | "settings-changed"
  | "error"
  | "paused"
  | "resumed";

/**
 * Event payload for slideshow events.
 */
export interface SlideshowEvent {
  type: SlideshowEventType;
  data?: PhotoDto | DisplaySettingsDto | string;
}
