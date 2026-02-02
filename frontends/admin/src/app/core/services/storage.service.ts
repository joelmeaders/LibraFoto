import { inject, Injectable, signal } from "@angular/core";
import { Observable, tap } from "rxjs";
import { ApiService } from "./api.service";
import {
  StorageProviderDto,
  CreateStorageProviderRequest,
  UpdateStorageProviderRequest,
  SyncRequest,
  SyncResult,
  SyncStatus,
  ScanResult,
  PickerSessionDto,
  PickedMediaItemDto,
  ImportPickerItemsResponse,
  UploadResult,
  BatchUploadResult,
  UploadRequest,
} from "../models";

/**
 * Service for storage provider management and file operations.
 */
@Injectable({
  providedIn: "root",
})
export class StorageService {
  private readonly api = inject(ApiService);

  // Reactive state
  private readonly _providers = signal<StorageProviderDto[]>([]);
  private readonly _selectedProvider = signal<StorageProviderDto | null>(null);
  private readonly _isLoading = signal<boolean>(false);
  private readonly _syncStatus = signal<Map<number, SyncStatus>>(new Map());

  // Public readonly signals
  readonly providers = this._providers.asReadonly();
  readonly selectedProvider = this._selectedProvider.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly syncStatus = this._syncStatus.asReadonly();

  /**
   * Get all storage providers.
   */
  getProviders(): Observable<StorageProviderDto[]> {
    this._isLoading.set(true);
    return this.api
      .get<StorageProviderDto[]>("/api/admin/storage/providers")
      .pipe(
        tap((providers) => {
          this._providers.set(providers);
          this._isLoading.set(false);
        }),
      );
  }

  /**
   * Get a single storage provider by ID.
   */
  getProvider(id: number): Observable<StorageProviderDto> {
    return this.api
      .get<StorageProviderDto>(`/api/admin/storage/providers/${id}`)
      .pipe(tap((provider) => this._selectedProvider.set(provider)));
  }

  /**
   * Create a new storage provider.
   */
  createProvider(
    request: CreateStorageProviderRequest,
  ): Observable<StorageProviderDto> {
    return this.api
      .post<StorageProviderDto>("/api/admin/storage/providers", request)
      .pipe(
        tap((provider) => {
          this._providers.update((providers) => [...providers, provider]);
        }),
      );
  }

  /**
   * Update an existing storage provider.
   */
  updateProvider(
    id: number,
    request: UpdateStorageProviderRequest,
  ): Observable<StorageProviderDto> {
    return this.api
      .put<StorageProviderDto>(`/api/admin/storage/providers/${id}`, request)
      .pipe(
        tap((provider) => {
          this._providers.update((providers) =>
            providers.map((p) => (p.id === id ? provider : p)),
          );
          if (this._selectedProvider()?.id === id) {
            this._selectedProvider.set(provider);
          }
        }),
      );
  }

  /**
   * Delete a storage provider.
   */
  deleteProvider(id: number, deletePhotos = false): Observable<void> {
    const url = deletePhotos
      ? `/api/admin/storage/providers/${id}?deletePhotos=true`
      : `/api/admin/storage/providers/${id}`;
    return this.api.delete<void>(url).pipe(
      tap(() => {
        this._providers.update((providers) =>
          providers.filter((p) => p.id !== id),
        );
        if (this._selectedProvider()?.id === id) {
          this._selectedProvider.set(null);
        }
      }),
    );
  }

  /**
   * Disconnect a storage provider without deleting it.
   */
  disconnectProvider(id: number): Observable<StorageProviderDto> {
    return this.api
      .post<StorageProviderDto>(
        `/api/admin/storage/providers/${id}/disconnect`,
        {},
      )
      .pipe(
        tap((provider) => {
          this._providers.update((providers) =>
            providers.map((p) => (p.id === id ? provider : p)),
          );
        }),
      );
  }

  /**
   * Start a sync operation for a provider.
   */
  syncProvider(id: number, request?: SyncRequest): Observable<SyncResult> {
    return this.api.post<SyncResult>(
      `/api/admin/storage/sync/${id}`,
      request ?? {},
    );
  }

  /**
   * Get sync status for a provider.
   */
  getSyncStatus(id: number): Observable<SyncStatus> {
    return this.api
      .get<SyncStatus>(`/api/admin/storage/sync/${id}/status`)
      .pipe(
        tap((status) => {
          this._syncStatus.update((map) => {
            const newMap = new Map(map);
            newMap.set(id, status);
            return newMap;
          });
        }),
      );
  }

  /**
   * Scan a provider for new files without importing.
   */
  scanProvider(id: number): Observable<ScanResult> {
    return this.api.get<ScanResult>(`/api/admin/storage/sync/${id}/scan`);
  }

  /**
   * Upload a file to the default local storage.
   */
  uploadFile(file: File, options?: UploadRequest): Observable<UploadResult> {
    const formData = new FormData();
    formData.append("file", file);

    if (options) {
      if (options.albumId)
        formData.append("albumId", options.albumId.toString());
      if (options.tags)
        options.tags.forEach((tag) => formData.append("tags", tag));
      if (options.customFilename)
        formData.append("customFilename", options.customFilename);
      if (options.overwrite) formData.append("overwrite", "true");
    }

    return this.api.uploadFile<UploadResult>("/api/admin/upload", formData);
  }

  /**
   * Upload multiple files.
   */
  uploadFiles(
    files: File[],
    options?: UploadRequest,
  ): Observable<BatchUploadResult> {
    const formData = new FormData();
    files.forEach((file) => formData.append("files", file));

    if (options) {
      if (options.albumId)
        formData.append("albumId", options.albumId.toString());
      if (options.tags)
        options.tags.forEach((tag) => formData.append("tags", tag));
      if (options.overwrite) formData.append("overwrite", "true");
    }

    return this.api.uploadFile<BatchUploadResult>(
      "/api/admin/upload/batch",
      formData,
    );
  }

  /**
   * Upload a file via guest link.
   */
  guestUpload(
    file: File,
    linkCode: string,
    message?: string,
  ): Observable<UploadResult> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("linkCode", linkCode);
    if (message) formData.append("message", message);

    return this.api.uploadFile<UploadResult>(
      `/api/guest/upload/${linkCode}`,
      formData,
    );
  }

  /**
   * Clear the selected provider.
   */
  clearSelectedProvider(): void {
    this._selectedProvider.set(null);
  }

  /**
   * Get sync status for a specific provider from cache.
   */
  getCachedSyncStatus(providerId: number): SyncStatus | undefined {
    return this._syncStatus().get(providerId);
  }

  /**
   * Get Google Photos authorization URL.
   */
  getGooglePhotosAuthUrl(
    providerId: number,
  ): Observable<{ authorizationUrl: string }> {
    return this.api.get<{ authorizationUrl: string }>(
      `/api/storage/google-photos/${providerId}/authorize-url`,
    );
  }

  startGooglePhotosPickerSession(
    providerId: number,
    maxItemCount?: number,
  ): Observable<PickerSessionDto> {
    return this.api.post<PickerSessionDto>(
      `/api/storage/google-photos/${providerId}/picker/start`,
      maxItemCount ? { maxItemCount } : {},
    );
  }

  getGooglePhotosPickerSession(
    providerId: number,
    sessionId: string,
  ): Observable<PickerSessionDto> {
    return this.api.get<PickerSessionDto>(
      `/api/storage/google-photos/${providerId}/picker/sessions/${sessionId}`,
    );
  }

  getGooglePhotosPickerItems(
    providerId: number,
    sessionId: string,
  ): Observable<PickedMediaItemDto[]> {
    return this.api.get<PickedMediaItemDto[]>(
      `/api/storage/google-photos/${providerId}/picker/sessions/${sessionId}/items`,
    );
  }

  importGooglePhotosPickerItems(
    providerId: number,
    sessionId: string,
  ): Observable<ImportPickerItemsResponse> {
    return this.api.post<ImportPickerItemsResponse>(
      `/api/storage/google-photos/${providerId}/picker/sessions/${sessionId}/import`,
      {},
    );
  }

  deleteGooglePhotosPickerSession(
    providerId: number,
    sessionId: string,
  ): Observable<void> {
    return this.api.delete<void>(
      `/api/storage/google-photos/${providerId}/picker/sessions/${sessionId}`,
    );
  }
}
