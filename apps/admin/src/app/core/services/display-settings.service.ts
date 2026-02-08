import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { DisplaySettingsDto, UpdateDisplaySettingsRequest } from '../models';

/**
 * Service for managing display/slideshow settings.
 */
@Injectable({
  providedIn: 'root'
})
export class DisplaySettingsService {
  private readonly api = inject(ApiService);

  // Reactive state
  private readonly _settings = signal<DisplaySettingsDto | null>(null);
  private readonly _allSettings = signal<DisplaySettingsDto[]>([]);
  private readonly _isLoading = signal<boolean>(false);

  // Public readonly signals
  readonly settings = this._settings.asReadonly();
  readonly allSettings = this._allSettings.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  /**
   * Get the current display settings.
   */
  getSettings(): Observable<DisplaySettingsDto> {
    this._isLoading.set(true);
    return this.api.get<DisplaySettingsDto>('/api/display/settings').pipe(
      tap(settings => {
        this._settings.set(settings);
        this._isLoading.set(false);
      })
    );
  }

  /**
   * Get all display settings configurations.
   */
  getAllSettings(): Observable<DisplaySettingsDto[]> {
    this._isLoading.set(true);
    return this.api.get<DisplaySettingsDto[]>('/api/display/settings/all').pipe(
      tap(settings => {
        this._allSettings.set(settings);
        this._isLoading.set(false);
      })
    );
  }

  /**
   * Get a specific settings configuration by ID.
   */
  getSettingsById(id: number): Observable<DisplaySettingsDto> {
    return this.api.get<DisplaySettingsDto>(`/api/display/settings/${id}`).pipe(
      tap(settings => this._settings.set(settings))
    );
  }

  /**
   * Update display settings.
   */
  updateSettings(id: number, request: UpdateDisplaySettingsRequest): Observable<DisplaySettingsDto> {
    return this.api.put<DisplaySettingsDto>(`/api/display/settings/${id}`, request).pipe(
      tap(settings => {
        this._settings.set(settings);
        this._allSettings.update(all => all.map(s => s.id === id ? settings : s));
      })
    );
  }

  /**
   * Create a new display settings configuration.
   */
  createSettings(request: UpdateDisplaySettingsRequest): Observable<DisplaySettingsDto> {
    return this.api.post<DisplaySettingsDto>('/api/display/settings', request).pipe(
      tap(settings => {
        this._allSettings.update(all => [...all, settings]);
      })
    );
  }

  /**
   * Delete a display settings configuration.
   */
  deleteSettings(id: number): Observable<void> {
    return this.api.delete<void>(`/api/display/settings/${id}`).pipe(
      tap(() => {
        this._allSettings.update(all => all.filter(s => s.id !== id));
        if (this._settings()?.id === id) {
          this._settings.set(null);
        }
      })
    );
  }

  /**
   * Set a configuration as the active one.
   */
  setActiveSettings(id: number): Observable<DisplaySettingsDto> {
    return this.api.post<DisplaySettingsDto>(`/api/display/settings/${id}/activate`, {}).pipe(
      tap(settings => this._settings.set(settings))
    );
  }

  /**
   * Reset settings to defaults.
   */
  resetToDefaults(id: number): Observable<DisplaySettingsDto> {
    return this.api.post<DisplaySettingsDto>(`/api/display/settings/${id}/reset`, {}).pipe(
      tap(settings => {
        this._settings.set(settings);
        this._allSettings.update(all => all.map(s => s.id === id ? settings : s));
      })
    );
  }

  /**
   * Clear cached settings.
   */
  clearSettings(): void {
    this._settings.set(null);
  }
}
