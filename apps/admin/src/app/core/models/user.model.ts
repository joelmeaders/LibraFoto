import { UserRole } from "./enums.model";

/**
 * User data transfer object (without sensitive data).
 */
export interface UserDto {
  id: number;
  email: string;
  role: UserRole;
  createdAt: string;
  lastLoginAt: string | null;
}

/**
 * Request model for user login.
 */
export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Response model for successful login.
 */
export interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: UserDto;
}

/**
 * Request model for creating a new user.
 */
export interface CreateUserRequest {
  email: string;
  password: string;
  role: UserRole;
}

/**
 * Request model for updating an existing user.
 */
export interface UpdateUserRequest {
  email?: string | null;
  password?: string | null;
  role?: UserRole | null;
}

/**
 * Request model for refreshing an access token.
 */
export interface RefreshTokenRequest {
  refreshToken: string;
}

/**
 * Result of token validation.
 */
export interface TokenValidationResult {
  isValid: boolean;
  userId: number | null;
}

/**
 * Request model for initial setup - creating the first admin user.
 */
export interface SetupRequest {
  email: string;
  password: string;
}

/**
 * Response model for setup status check.
 */
export interface SetupStatusResponse {
  isSetupRequired: boolean;
  message: string | null;
}

/**
 * Guest link data transfer object for display.
 */
export interface GuestLinkDto {
  id: string;
  name: string;
  createdAt: string;
  expiresAt: string | null;
  maxUploads: number | null;
  currentUploads: number;
  targetAlbumId: number | null;
  targetAlbumName: string | null;
  createdByUserId: number;
  createdByUsername: string;
  isActive: boolean;
}

/**
 * Request model for creating a guest upload link.
 */
export interface CreateGuestLinkRequest {
  name: string;
  expiresAt?: string | null;
  maxUploads?: number | null;
  targetAlbumId?: number | null;
}

/**
 * Response model for guest link validation.
 */
export interface GuestLinkValidationResponse {
  isValid: boolean;
  name: string | null;
  targetAlbumName: string | null;
  remainingUploads: number | null;
  message: string | null;
}

/**
 * Public information about a guest link (without sensitive data).
 */
export interface GuestLinkPublicInfo {
  name: string;
  targetAlbumName: string | null;
  isActive: boolean;
  remainingUploads: number | null;
  statusMessage: string | null;
}
