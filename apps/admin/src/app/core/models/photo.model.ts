import { MediaType } from "./enums.model";

/**
 * Photo information for list views (optimized for grid display).
 */
export interface PhotoListDto {
  id: number;
  filename: string;
  thumbnailPath: string;
  width: number;
  height: number;
  mediaType: MediaType;
  dateTaken: string | null;
  dateAdded: string;
  location: string | null;
  albumCount: number;
  tagCount: number;
}

/**
 * Full photo details including related albums and tags.
 */
export interface PhotoDetailDto {
  id: number;
  filename: string;
  originalFilename: string;
  filePath: string;
  thumbnailPath: string | null;
  width: number;
  height: number;
  fileSize: number;
  mediaType: MediaType;
  duration: number | null;
  dateTaken: string | null;
  dateAdded: string;
  location: string | null;
  latitude: number | null;
  longitude: number | null;
  providerId: number | null;
  providerName: string | null;
  albums: AlbumSummaryDto[];
  tags: TagSummaryDto[];
}

/**
 * Minimal album info for photo detail view.
 */
export interface AlbumSummaryDto {
  id: number;
  name: string;
}

/**
 * Minimal tag info for photo detail view.
 */
export interface TagSummaryDto {
  id: number;
  name: string;
  color: string | null;
}

/**
 * Filter options for photo list queries.
 */
export interface PhotoFilterRequest {
  page?: number;
  pageSize?: number;
  albumId?: number;
  tagId?: number;
  dateFrom?: string;
  dateTo?: string;
  mediaType?: MediaType;
  search?: string;
  sortBy?: string;
  sortDirection?: string;
}

/**
 * Request to update a photo's metadata.
 */
export interface UpdatePhotoRequest {
  filename?: string | null;
  location?: string | null;
  dateTaken?: string | null;
}

/**
 * Request for bulk photo operations.
 */
export interface BulkPhotoRequest {
  photoIds: number[];
}

/**
 * Request to add photos to an album.
 */
export interface AddPhotosToAlbumRequest {
  photoIds: number[];
}

/**
 * Request to remove photos from an album.
 */
export interface RemovePhotosFromAlbumRequest {
  photoIds: number[];
}

/**
 * Request to add tags to photos.
 */
export interface AddTagsToPhotosRequest {
  photoIds: number[];
  tagIds: number[];
}

/**
 * Request to remove tags from photos.
 */
export interface RemoveTagsFromPhotosRequest {
  photoIds: number[];
  tagIds: number[];
}

/**
 * Result of a bulk operation.
 */
export interface BulkOperationResult {
  successCount: number;
  failedCount: number;
  errors: string[];
}

/**
 * Result of refreshing thumbnails.
 */
export interface RefreshThumbnailsResult {
  succeeded: number;
  failed: number;
  errors: string[];
}
