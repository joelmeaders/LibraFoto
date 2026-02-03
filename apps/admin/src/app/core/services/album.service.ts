import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import {
  AlbumDto,
  CreateAlbumRequest,
  UpdateAlbumRequest,
  AddPhotosToAlbumRequest,
  RemovePhotosFromAlbumRequest,
  ReorderPhotosRequest,
  BulkOperationResult,
  PhotoListDto,
  PagedResult
} from '../models';

/**
 * Service for album management operations.
 */
@Injectable({
  providedIn: 'root'
})
export class AlbumService {
  private readonly api = inject(ApiService);

  // Reactive state
  private readonly _albums = signal<AlbumDto[]>([]);
  private readonly _selectedAlbum = signal<AlbumDto | null>(null);
  private readonly _isLoading = signal<boolean>(false);

  // Public readonly signals
  readonly albums = this._albums.asReadonly();
  readonly selectedAlbum = this._selectedAlbum.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  /**
   * Get all albums.
   */
  getAlbums(): Observable<AlbumDto[]> {
    this._isLoading.set(true);
    return this.api.get<AlbumDto[]>('/api/admin/albums').pipe(
      tap(albums => {
        this._albums.set(albums);
        this._isLoading.set(false);
      })
    );
  }

  /**
   * Get a single album by ID.
   */
  getAlbum(id: number): Observable<AlbumDto> {
    return this.api.get<AlbumDto>(`/api/admin/albums/${id}`).pipe(
      tap(album => this._selectedAlbum.set(album))
    );
  }

  /**
   * Create a new album.
   */
  createAlbum(request: CreateAlbumRequest): Observable<AlbumDto> {
    return this.api.post<AlbumDto>('/api/admin/albums', request).pipe(
      tap(album => {
        this._albums.update(albums => [...albums, album]);
      })
    );
  }

  /**
   * Update an existing album.
   */
  updateAlbum(id: number, request: UpdateAlbumRequest): Observable<AlbumDto> {
    return this.api.put<AlbumDto>(`/api/admin/albums/${id}`, request).pipe(
      tap(album => {
        this._albums.update(albums => albums.map(a => a.id === id ? album : a));
        if (this._selectedAlbum()?.id === id) {
          this._selectedAlbum.set(album);
        }
      })
    );
  }

  /**
   * Delete an album.
   */
  deleteAlbum(id: number): Observable<void> {
    return this.api.delete<void>(`/api/admin/albums/${id}`).pipe(
      tap(() => {
        this._albums.update(albums => albums.filter(a => a.id !== id));
        if (this._selectedAlbum()?.id === id) {
          this._selectedAlbum.set(null);
        }
      })
    );
  }

  /**
   * Add photos to an album.
   */
  addPhotosToAlbum(albumId: number, request: AddPhotosToAlbumRequest): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(`/api/admin/albums/${albumId}/photos`, request);
  }

  /**
   * Remove photos from an album.
   */
  removePhotosFromAlbum(albumId: number, request: RemovePhotosFromAlbumRequest): Observable<BulkOperationResult> {
    return this.api.post<BulkOperationResult>(`/api/admin/albums/${albumId}/photos/remove`, request);
  }

  /**
   * Reorder photos within an album.
   */
  reorderPhotos(albumId: number, request: ReorderPhotosRequest): Observable<void> {
    return this.api.put<void>(`/api/admin/albums/${albumId}/photos/reorder`, request);
  }

  /**
   * Get photos in an album (paginated).
   */
  getAlbumPhotos(albumId: number, page = 1, pageSize = 50): Observable<PagedResult<PhotoListDto>> {
    return this.api.get<PagedResult<PhotoListDto>>(`/api/admin/albums/${albumId}/photos`, { page, pageSize });
  }

  /**
   * Clear the selected album.
   */
  clearSelectedAlbum(): void {
    this._selectedAlbum.set(null);
  }

  /**
   * Get thumbnail URL for album cover.
   */
  getCoverThumbnailUrl(album: AlbumDto, size: string = ''): string | null {
    if (album.coverPhotoId) {
      let url = `${this.api['baseUrl']}/api/media/thumbnails/${album.coverPhotoId}`;
      if (size) {
        url += `/${size}`;
      }
      return url;
    }
    return null;
  }
}
