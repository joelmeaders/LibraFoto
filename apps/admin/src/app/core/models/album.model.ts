/**
 * Album information for list and detail views.
 */
export interface AlbumDto {
  id: number;
  name: string;
  description: string | null;
  coverPhotoId: number | null;
  coverPhotoThumbnail: string | null;
  dateCreated: string;
  sortOrder: number;
  photoCount: number;
}

/**
 * Request to create a new album.
 */
export interface CreateAlbumRequest {
  name: string;
  description?: string | null;
  coverPhotoId?: number | null;
}

/**
 * Request to update an album.
 */
export interface UpdateAlbumRequest {
  name?: string | null;
  description?: string | null;
  coverPhotoId?: number | null;
  sortOrder?: number | null;
}

/**
 * Request to add photos to a tag.
 */
export interface AddPhotosToTagRequest {
  photoIds: number[];
}

/**
 * Request to remove photos from a tag.
 */
export interface RemovePhotosFromTagRequest {
  photoIds: number[];
}

/**
 * Request to reorder photos in an album.
 */
export interface ReorderPhotosRequest {
  photoOrders: PhotoOrder[];
}

/**
 * Photo ID and sort order pair.
 */
export interface PhotoOrder {
  photoId: number;
  sortOrder: number;
}
