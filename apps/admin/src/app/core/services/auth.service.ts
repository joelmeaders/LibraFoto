import { computed, inject, Injectable, signal } from "@angular/core";
import { Router } from "@angular/router";
import { Observable, tap, of, catchError } from "rxjs";
import { ApiService } from "./api.service";
import {
  LoginRequest,
  LoginResponse,
  RefreshTokenRequest,
  UserDto,
  SetupRequest,
  SetupStatusResponse,
  CreateUserRequest,
  UpdateUserRequest,
  PagedResult,
  CreateGuestLinkRequest,
  GuestLinkDto,
  GuestLinkPublicInfo,
  GuestLinkValidationResponse,
} from "../models";

const TOKEN_KEY = "librafoto_token";
const REFRESH_TOKEN_KEY = "librafoto_refresh_token";
const USER_KEY = "librafoto_user";
const TOKEN_EXPIRY_KEY = "librafoto_token_expiry";

/**
 * Authentication service handling login, logout, token management, and user state.
 */
@Injectable({
  providedIn: "root",
})
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  // Reactive state using signals
  private readonly _currentUser = signal<UserDto | null>(
    this.loadUserFromStorage(),
  );
  private readonly _isAuthenticated = signal<boolean>(this.hasValidToken());
  private readonly _isLoading = signal<boolean>(false);

  // Public readonly signals
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = this._isAuthenticated.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();

  // Computed values
  readonly isAdmin = computed(() => this._currentUser()?.role === 2);
  readonly isEditor = computed(() => {
    const role = this._currentUser()?.role;
    return role === 1 || role === 2;
  });

  /**
   * Check if setup is required (no admin user exists).
   */
  checkSetupStatus(): Observable<SetupStatusResponse> {
    return this.api.get<SetupStatusResponse>("/api/setup/status");
  }

  /**
   * Complete initial setup by creating the first admin user.
   */
  setup(request: SetupRequest): Observable<LoginResponse> {
    this._isLoading.set(true);
    return this.api.post<LoginResponse>("/api/setup/complete", request).pipe(
      tap((response) => {
        this.saveTokens(response);
        this._isLoading.set(false);
      }),
      catchError((error) => {
        this._isLoading.set(false);
        throw error;
      }),
    );
  }

  /**
   * Login with email and password.
   */
  login(request: LoginRequest): Observable<LoginResponse> {
    this._isLoading.set(true);
    return this.api.post<LoginResponse>("/api/auth/login", request).pipe(
      tap((response) => {
        this.saveTokens(response);
        this._isLoading.set(false);
      }),
      catchError((error) => {
        this._isLoading.set(false);
        throw error;
      }),
    );
  }

  /**
   * Refresh the access token using the refresh token.
   */
  refreshToken(): Observable<LoginResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return of(null as unknown as LoginResponse);
    }

    const request: RefreshTokenRequest = { refreshToken };
    return this.api.post<LoginResponse>("/api/auth/refresh", request).pipe(
      tap((response) => {
        this.saveTokens(response);
      }),
      catchError((error) => {
        this.logout();
        throw error;
      }),
    );
  }

  /**
   * Logout and clear all stored tokens.
   */
  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(TOKEN_EXPIRY_KEY);
    this._currentUser.set(null);
    this._isAuthenticated.set(false);
    this.router.navigate(["/login"]);
  }

  /**
   * Get the current access token.
   */
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  /**
   * Get the current refresh token.
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  /**
   * Check if the current token is expired or about to expire.
   */
  isTokenExpired(): boolean {
    const expiry = localStorage.getItem(TOKEN_EXPIRY_KEY);
    if (!expiry) return true;

    const expiryDate = new Date(expiry);
    // Consider expired if within 30 seconds of expiry
    return expiryDate.getTime() - Date.now() < 30000;
  }

  /**
   * Check if there's a valid token stored.
   */
  private hasValidToken(): boolean {
    const token = this.getToken();
    return !!token && !this.isTokenExpired();
  }

  /**
   * Load user from storage.
   */
  private loadUserFromStorage(): UserDto | null {
    const userJson = localStorage.getItem(USER_KEY);
    if (userJson) {
      try {
        return JSON.parse(userJson) as UserDto;
      } catch {
        return null;
      }
    }
    return null;
  }

  /**
   * Save tokens and user info to storage.
   */
  private saveTokens(response: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    localStorage.setItem(TOKEN_EXPIRY_KEY, response.expiresAt);
    this._currentUser.set(response.user);
    this._isAuthenticated.set(true);
  }

  // ==================== User Management ====================

  /**
   * Get all users (paginated).
   */
  getUsers(page = 1, pageSize = 20): Observable<PagedResult<UserDto>> {
    return this.api.get<PagedResult<UserDto>>("/api/admin/users", {
      page,
      pageSize,
    });
  }

  /**
   * Get a single user by ID.
   */
  getUser(id: number): Observable<UserDto> {
    return this.api.get<UserDto>(`/api/admin/users/${id}`);
  }

  /**
   * Create a new user.
   */
  createUser(request: CreateUserRequest): Observable<UserDto> {
    return this.api.post<UserDto>("/api/admin/users", request);
  }

  /**
   * Update an existing user.
   */
  updateUser(id: number, request: UpdateUserRequest): Observable<UserDto> {
    return this.api.put<UserDto>(`/api/admin/users/${id}`, request);
  }

  /**
   * Delete a user.
   */
  deleteUser(id: number): Observable<void> {
    return this.api.delete<void>(`/api/admin/users/${id}`);
  }

  // ==================== Guest Links ====================

  /**
   * Get all guest links (paginated).
   */
  getGuestLinks(
    page = 1,
    pageSize = 20,
  ): Observable<PagedResult<GuestLinkDto>> {
    return this.api.get<PagedResult<GuestLinkDto>>("/api/admin/guest-links", {
      page,
      pageSize,
    });
  }

  /**
   * Get a single guest link by ID.
   */
  getGuestLink(id: string): Observable<GuestLinkDto> {
    return this.api.get<GuestLinkDto>(`/api/admin/guest-links/${id}`);
  }

  /**
   * Create a new guest link.
   */
  createGuestLink(request: CreateGuestLinkRequest): Observable<GuestLinkDto> {
    return this.api.post<GuestLinkDto>("/api/admin/guest-links", request);
  }

  /**
   * Delete a guest link.
   */
  deleteGuestLink(id: string): Observable<void> {
    return this.api.delete<void>(`/api/admin/guest-links/${id}`);
  }

  /**
   * Get public info about a guest link (no auth required).
   */
  getGuestLinkInfo(linkCode: string): Observable<GuestLinkPublicInfo> {
    return this.api.get<GuestLinkPublicInfo>(`/api/guest/${linkCode}`);
  }

  /**
   * Validate a guest link.
   */
  validateGuestLink(linkCode: string): Observable<GuestLinkValidationResponse> {
    return this.api.get<GuestLinkValidationResponse>(
      `/api/guest/${linkCode}/validate`,
    );
  }
}
