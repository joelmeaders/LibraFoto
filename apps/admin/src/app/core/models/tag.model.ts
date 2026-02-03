/**
 * Tag information for list and detail views.
 */
export interface TagDto {
  id: number;
  name: string;
  color: string | null;
  photoCount: number;
}

/**
 * Request to create a new tag.
 */
export interface CreateTagRequest {
  name: string;
  color?: string | null;
}

/**
 * Request to update a tag.
 */
export interface UpdateTagRequest {
  name?: string | null;
  color?: string | null;
}
