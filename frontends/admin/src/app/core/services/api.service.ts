import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiError } from '../models';

/**
 * Base API service providing common HTTP functionality.
 * All other services should extend or use this service.
 */
@Injectable({
  providedIn: 'root'
})
export class ApiService {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = environment.apiBaseUrl;

  /**
   * Build HttpParams from an object, filtering out null/undefined values.
   */
  protected buildParams(params: Record<string, unknown>): HttpParams {
    let httpParams = new HttpParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') {
        if (value instanceof Date) {
          httpParams = httpParams.set(key, value.toISOString());
        } else if (Array.isArray(value)) {
          value.forEach(v => {
            httpParams = httpParams.append(key, String(v));
          });
        } else {
          httpParams = httpParams.set(key, String(value));
        }
      }
    });

    return httpParams;
  }

  /**
   * Standard error handler for HTTP requests.
   */
  protected handleError(error: HttpErrorResponse): Observable<never> {
    let apiError: ApiError;

    if (error.error instanceof ErrorEvent) {
      // Client-side error
      apiError = {
        code: 'CLIENT_ERROR',
        message: error.error.message
      };
    } else if (error.error && typeof error.error === 'object' && 'code' in error.error) {
      // Server returned an ApiError
      apiError = error.error as ApiError;
    } else {
      // Other server error
      apiError = {
        code: `HTTP_${error.status}`,
        message: error.message || 'An unexpected error occurred',
        details: error.error
      };
    }

    console.error('API Error:', apiError);
    return throwError(() => apiError);
  }

  /**
   * GET request with error handling.
   */
  get<T>(path: string, params?: Record<string, unknown>): Observable<T> {
    const options = params ? { params: this.buildParams(params) } : {};
    return this.http.get<T>(`${this.baseUrl}${path}`, options).pipe(
      catchError(error => this.handleError(error))
    );
  }

  /**
   * POST request with error handling.
   */
  post<T>(path: string, body?: unknown): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body).pipe(
      catchError(error => this.handleError(error))
    );
  }

  /**
   * PUT request with error handling.
   */
  put<T>(path: string, body?: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, body).pipe(
      catchError(error => this.handleError(error))
    );
  }

  /**
   * PATCH request with error handling.
   */
  patch<T>(path: string, body?: unknown): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}${path}`, body).pipe(
      catchError(error => this.handleError(error))
    );
  }

  /**
   * DELETE request with error handling.
   */
  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`).pipe(
      catchError(error => this.handleError(error))
    );
  }

  /**
   * Upload files with FormData.
   */
  uploadFile<T>(path: string, formData: FormData): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, formData).pipe(
      catchError(error => this.handleError(error))
    );
  }
}
