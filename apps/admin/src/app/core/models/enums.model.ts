/**
 * Type of media file.
 */
export enum MediaType {
  Photo = 0,
  Video = 1,
}

/**
 * Types of transitions between slides in the display.
 */
export enum TransitionType {
  Fade = 0,
  Slide = 1,
  KenBurns = 2,
}

/**
 * Source type for slideshow content filtering.
 */
export enum SourceType {
  All = 0,
  Album = 1,
  Tag = 2,
}

/**
 * Types of storage providers for photo sources.
 */
export enum StorageProviderType {
  Local = 0,
  GooglePhotos = 1,
  GoogleDrive = 2,
  OneDrive = 3,
}

/**
 * User roles in LibraFoto.
 */
export enum UserRole {
  Guest = 0,
  Editor = 1,
  Admin = 2,
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
