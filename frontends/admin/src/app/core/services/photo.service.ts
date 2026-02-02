import { inject, Injectable, signal } from "@angular/core";
import { Observable, tap } from "rxjs";
import { ApiService } from "./api.service";
import {
  PhotoListDto,
  PhotoDetailDto,
  PhotoFilterRequest,
  UpdatePhotoRequest,
  BulkOperationResult,
  AddTagsToPhotosRequest,
  RemoveTagsFromPhotosRequest,
  PagedResult,
  RefreshThumbnailsResult,
} from "../models";

/**
 * Service for photo CRUD operations and filtering.
 */
@Injectable({
  providedIn: "root",
})
export class PhotoService {
  private readonly api = inject(ApiService);

  // Reactive state
  private readonly _photos = signal<PhotoListDto[]>([]);
  private readonly _selectedPhoto = signal<PhotoDetailDto | null>(null);
  private readonly _pagination = signal<
    PagedResult<PhotoListDto>["pagination"] | null
  >(null);
  private readonly _isLoading = signal<boolean>(false);

  // Public readonly signals
  readonly photos = this._photos.asReadonly();
  readonly selectedPhoto = this._selectedPhoto.asReadonly();
  readonly pagination = this._pagination.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  /**
   * Get paginated list of photos with optional filters.
   */
  getPhotos(
    filter?: PhotoFilterRequest,
  ): Observable<PagedResult<PhotoListDto>> {
    this._isLoading.set(true);
    const params = filter ? { ...filter } : {};
    return this.api
      .get<PagedResult<PhotoListDto>>("/api/admin/photos", params)
      .pipe(
        tap((result) => {
          this._photos.set(result.data);
          this._pagination.set(result.pagination);
          this._isLoading.set(false);
        }),
      );
  }

  /**
   * Get full details for a single photo.
   */
  getPhoto(id: number): Observable<PhotoDetailDto> {
    return this.api
      .get<PhotoDetailDto>(`/api/admin/photos/${id}`)
      .pipe(tap((photo) => this._selectedPhoto.set(photo)));
  }

  /**
   * Update a photo's metadata.
   */
  updatePhoto(
    id: number,
    request: UpdatePhotoRequest,
  ): Observable<PhotoDetailDto> {
    return this.api
      .put<PhotoDetailDto>(`/api/admin/photos/${id}`, request)
      .pipe(tap((photo) => this._selectedPhoto.set(photo)));
  }

  /**
   * Delete a single photo.
   */
  deletePhoto(id: number): Observable<void> {
    return this.api.delete<void>(`/api/admin/photos/${id}`).pipe(
      tap(() => {
        this._photos.update((photos) => photos.filter((p) => p.id !== id));
        if (this._selectedPhoto()?.id === id) {
          this._selectedPhoto.set(null);
        }
      }),
    );
  }

  /**
   * Delete multiple photos.
   */
  deletePhotos(photoIds: number[]): Observable<BulkOperationResult> {
    return this.api
      .post<BulkOperationResult>("/api/admin/photos/bulk/delete", { photoIds })
      .pipe(
        tap(() => {
          this._photos.update((photos) =>
            photos.filter((p) => !photoIds.includes(p.id)),
          );
        }),
      );
  }

  /**
   * Add tags to multiple photos.
   */
  addTagsToPhotos(
    request: AddTagsToPhotosRequest,
  ): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(
      "/api/admin/photos/bulk/add-tags",
      request,
    );
  }

  /**
   * Remove tags from multiple photos.
   */
  removeTagsFromPhotos(
    request: RemoveTagsFromPhotosRequest,
  ): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(
      "/api/admin/photos/bulk/remove-tags",
      request,
    );
  }

  /**
   * Get the count of all photos.
   */
  getPhotoCount(): Observable<{ count: number }> {
    return this.api.get<{ count: number }>("/api/admin/photos/count");
  }

  /**
   * Clear the selected photo.
   */
  clearSelectedPhoto(): void {
    this._selectedPhoto.set(null);
  }

  /**
   * Get the URL for a photo thumbnail.
   */
  getThumbnailUrl(photo: PhotoListDto): string {
    if (photo.id) {
      return `${this.api["baseUrl"]}/api/media/thumbnails/${photo.id}`;
    }
    return "/assets/placeholder.png";
  }

  /**
   * Get the URL for a full-size photo.
   */
  getPhotoUrl(photo: PhotoDetailDto): string {
    return `${this.api["baseUrl"]}/api/media/photos/${photo.filePath}`;
  }

  /**
   * Refresh (regenerate) thumbnails for the specified photos.
   */
  refreshThumbnails(photoIds: number[]): Observable<RefreshThumbnailsResult> {
    return this.api.post<RefreshThumbnailsResult>(
      "/api/media/thumbnails/refresh",
      { photoIds },
    );
  }
}
