import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import {
  TagDto,
  CreateTagRequest,
  UpdateTagRequest,
  AddPhotosToTagRequest,
  RemovePhotosFromTagRequest,
  BulkOperationResult,
  PhotoListDto,
  PagedResult
} from '../models';

/**
 * Service for tag management operations.
 */
@Injectable({
  providedIn: 'root'
})
export class TagService {
  private readonly api = inject(ApiService);

  // Reactive state
  private readonly _tags = signal<TagDto[]>([]);
  private readonly _selectedTag = signal<TagDto | null>(null);
  private readonly _isLoading = signal<boolean>(false);

  // Public readonly signals
  readonly tags = this._tags.asReadonly();
  readonly selectedTag = this._selectedTag.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  /**
   * Get all tags.
   */
  getTags(): Observable<TagDto[]> {
    this._isLoading.set(true);
    return this.api.get<TagDto[]>('/api/admin/tags').pipe(
      tap(tags => {
        this._tags.set(tags);
        this._isLoading.set(false);
      })
    );
  }

  /**
   * Get a single tag by ID.
   */
  getTag(id: number): Observable<TagDto> {
    return this.api.get<TagDto>(`/api/admin/tags/${id}`).pipe(
      tap(tag => this._selectedTag.set(tag))
    );
  }

  /**
   * Create a new tag.
   */
  createTag(request: CreateTagRequest): Observable<TagDto> {
    return this.api.post<TagDto>('/api/admin/tags', request).pipe(
      tap(tag => {
        this._tags.update(tags => [...tags, tag]);
      })
    );
  }

  /**
   * Update an existing tag.
   */
  updateTag(id: number, request: UpdateTagRequest): Observable<TagDto> {
    return this.api.put<TagDto>(`/api/admin/tags/${id}`, request).pipe(
      tap(tag => {
        this._tags.update(tags => tags.map(t => t.id === id ? tag : t));
        if (this._selectedTag()?.id === id) {
          this._selectedTag.set(tag);
        }
      })
    );
  }

  /**
   * Delete a tag.
   */
  deleteTag(id: number): Observable<void> {
    return this.api.delete<void>(`/api/admin/tags/${id}`).pipe(
      tap(() => {
        this._tags.update(tags => tags.filter(t => t.id !== id));
        if (this._selectedTag()?.id === id) {
          this._selectedTag.set(null);
        }
      })
    );
  }

  /**
   * Add photos to a tag.
   */
  addPhotosToTag(tagId: number, request: AddPhotosToTagRequest): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(`/api/admin/tags/${tagId}/photos`, request);
  }

  /**
   * Remove photos from a tag.
   */
  removePhotosFromTag(tagId: number, request: RemovePhotosFromTagRequest): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(`/api/admin/tags/${tagId}/photos/remove`, request);
  }

  /**
   * Get photos with a tag (paginated).
   */
  getTagPhotos(tagId: number, page = 1, pageSize = 50): Observable<PagedResult<PhotoListDto>> {
    return this.api.get<PagedResult<PhotoListDto>>(`/api/admin/tags/${tagId}/photos`, { page, pageSize });
  }

  /**
   * Clear the selected tag.
   */
  clearSelectedTag(): void {
    this._selectedTag.set(null);
  }
}
