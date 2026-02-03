import {
  test as base,
  Page,
  APIRequestContext,
  expect as playwrightExpect,
} from "@playwright/test";
import * as path from "path";
import * as fs from "fs";
import { fileURLToPath } from "url";

/**
 * LibraFoto Integration Test Fixtures
 *
 * These fixtures interact with the real API backend for true integration testing.
 */

// API Base URL
export const API_BASE_URL = "http://localhost:5179";

// Get __dirname equivalent for ES modules
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Test assets directory
export const TEST_ASSETS_DIR = path.join(__dirname, "..", "test-assets");

// Test user credentials
export const TEST_ADMIN = {
  email: "testadmin@librafoto.local",
  password: "TestPassword123!",
};

export const TEST_EDITOR = {
  email: "testeditor@librafoto.local",
  password: "TestPassword123!",
};

export const TEST_GUEST = {
  email: "testguest@librafoto.local",
  password: "TestPassword123!",
};

// Sample test images
export const TEST_IMAGES = {
  woodpecker: "sample-woodpecker.jpg",
  desert: "sample-desert.jpg",
  aerial: "sample-aerial.jpg",
};

// Storage for auth tokens between tests
export interface AuthState {
  token: string | null;
  refreshToken: string | null;
  userId: number | null;
  role: string | null;
}

let authState: AuthState = {
  token: null,
  refreshToken: null,
  userId: null,
  role: null,
};

// ============================================================================
// Type Definitions
// ============================================================================

export interface User {
  id: number;
  email: string;
  displayName?: string;
  role: "Admin" | "Editor" | "Guest";
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface Photo {
  id: number;
  filename: string;
  originalFilename: string;
  filePath: string;
  fileSize: number;
  mediaType: "Photo" | "Video";
  width?: number;
  height?: number;
  dateTaken?: string;
  dateAdded: string;
  latitude?: number;
  longitude?: number;
  locationName?: string;
  albums: AlbumSummary[];
  tags: TagSummary[];
}

export interface PhotoListItem {
  id: number;
  filename: string;
  thumbnailUrl: string;
  mediaType: "Photo" | "Video";
  dateTaken?: string;
  dateAdded: string;
}

export interface Album {
  id: number;
  name: string;
  description?: string;
  coverPhotoId?: number;
  coverPhotoUrl?: string;
  photoCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface AlbumSummary {
  id: number;
  name: string;
}

export interface Tag {
  id: number;
  name: string;
  color: string;
  photoCount: number;
  createdAt: string;
}

export interface TagSummary {
  id: number;
  name: string;
  color: string;
}

export interface GuestLink {
  id: string;
  name: string;
  expiresAt?: string;
  maxUploads?: number;
  currentUploads: number;
  targetAlbumId?: number;
  targetAlbumName?: string;
  createdById: number;
  createdByUsername: string;
  createdAt: string;
  isExpired: boolean;
  isLimitReached: boolean;
}

export interface StorageProvider {
  id: number;
  name: string;
  type: "Local" | "GooglePhotos" | "GoogleDrive" | "OneDrive";
  isEnabled: boolean;
  isConnected: boolean;
  photoCount: number;
  lastSyncAt?: string;
}

export interface DisplaySettings {
  id: number;
  slideDuration: number;
  transitionType: "None" | "Fade" | "Slide" | "Zoom";
  transitionDuration: number;
  shuffle: boolean;
  sourceType: "All" | "Album" | "Tag";
  sourceId?: number;
}

export interface PagedResult<T> {
  data: T[];
  pagination: {
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
  };
}

export interface ResetResult {
  success: boolean;
  message: string;
  testAdminUsername: string;
  testAdminEmail: string;
}

// ============================================================================
// API Client
// ============================================================================

/**
 * API Client for making direct API calls in tests
 */
export class ApiClient {
  constructor(private readonly request: APIRequestContext) {}

  // ==========================================================================
  // Database Management
  // ==========================================================================

  /**
   * Reset database to clean state (development only)
   */
  async resetDatabase(): Promise<ResetResult> {
    const response = await this.request.post(`${API_BASE_URL}/api/test/reset`);
    if (!response.ok()) {
      throw new Error(`Failed to reset database: ${response.status()}`);
    }
    // Clear local auth state after reset
    this.clearAuthState();
    return response.json();
  }

  // ==========================================================================
  // Setup & Auth
  // ==========================================================================

  /**
   * Check if setup is required
   */
  async getSetupStatus(): Promise<{ isSetupRequired: boolean }> {
    const response = await this.request.get(`${API_BASE_URL}/api/setup/status`);
    return response.json();
  }

  /**
   * Complete initial setup with admin user
   */
  async completeSetup(email: string, password: string): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/setup/complete`,
      {
        data: { email, password },
      }
    );
    if (response.ok()) {
      const data = await response.json();
      this.setAuthFromResponse(data);
      return data;
    }
    return null;
  }

  /**
   * Login with credentials
   */
  async login(email: string, password: string): Promise<any> {
    const response = await this.request.post(`${API_BASE_URL}/api/auth/login`, {
      data: { email, password },
    });
    if (response.ok()) {
      const data = await response.json();
      this.setAuthFromResponse(data);
      return data;
    }
    return null;
  }

  /**
   * Logout current user
   */
  async logout(): Promise<void> {
    await this.request.post(`${API_BASE_URL}/api/auth/logout`, {
      headers: this.getAuthHeaders(),
    });
    this.clearAuthState();
  }

  /**
   * Get current user
   */
  async getCurrentUser(): Promise<User | null> {
    const response = await this.request.get(`${API_BASE_URL}/api/auth/me`, {
      headers: this.getAuthHeaders(),
    });
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  // ==========================================================================
  // User Management
  // ==========================================================================

  /**
   * Get all users (paginated)
   */
  async getUsers(
    page: number = 1,
    pageSize: number = 20
  ): Promise<PagedResult<User>> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/users?page=${page}&pageSize=${pageSize}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return {
      data: [],
      pagination: { page, pageSize, totalItems: 0, totalPages: 0 },
    };
  }

  /**
   * Create a new user (requires admin auth)
   */
  async createUser(
    email: string,
    password: string,
    role: "Admin" | "Editor" | "Guest"
  ): Promise<User | null> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/users`,
      {
        headers: this.getAuthHeaders(),
        data: { email, password, role },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Update a user
   */
  async updateUser(
    id: number,
    data: {
      displayName?: string;
      email?: string;
      role?: string;
      isActive?: boolean;
    }
  ): Promise<User | null> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/users/${id}`,
      {
        headers: this.getAuthHeaders(),
        data,
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Delete a user
   */
  async deleteUser(id: number): Promise<boolean> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/users/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.ok();
  }

  // ==========================================================================
  // Photo Management
  // ==========================================================================

  /**
   * Get all photos (paginated)
   */
  async getPhotos(
    page: number = 1,
    pageSize: number = 20,
    filters?: { albumId?: number; tagId?: number; search?: string }
  ): Promise<PagedResult<PhotoListItem>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    if (filters?.albumId) params.set("albumId", filters.albumId.toString());
    if (filters?.tagId) params.set("tagId", filters.tagId.toString());
    if (filters?.search) params.set("search", filters.search);

    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/photos?${params}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return {
      data: [],
      pagination: { page, pageSize, totalItems: 0, totalPages: 0 },
    };
  }

  /**
   * Get photo details
   */
  async getPhoto(id: number): Promise<Photo | null> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/photos/${id}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Upload a photo from test assets
   */
  async uploadPhoto(filename: string): Promise<any> {
    const filePath = path.join(TEST_ASSETS_DIR, filename);
    if (!fs.existsSync(filePath)) {
      throw new Error(`Test asset not found: ${filePath}`);
    }

    const response = await this.request.post(
      `${API_BASE_URL}/api/storage/upload`,
      {
        headers: this.getAuthHeaders(),
        multipart: {
          file: {
            name: filename,
            mimeType: "image/jpeg",
            buffer: fs.readFileSync(filePath),
          },
        },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    const errorText = await response.text();
    throw new Error(`Upload failed: ${response.status()} - ${errorText}`);
  }

  /**
   * Upload multiple photos
   */
  async uploadPhotos(filenames: string[]): Promise<any[]> {
    const results = [];
    for (const filename of filenames) {
      const result = await this.uploadPhoto(filename);
      results.push(result);
    }
    return results;
  }

  /**
   * Delete a photo
   */
  async deletePhoto(id: number): Promise<boolean> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/photos/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.ok();
  }

  /**
   * Bulk delete photos
   */
  async bulkDeletePhotos(photoIds: number[]): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/photos/bulk-delete`,
      {
        headers: this.getAuthHeaders(),
        data: { photoIds },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Add photos to album
   */
  async addPhotosToAlbum(albumId: number, photoIds: number[]): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/photos/bulk-add-to-album`,
      {
        headers: this.getAuthHeaders(),
        data: { albumId, photoIds },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Remove photos from album
   */
  async removePhotosFromAlbum(
    albumId: number,
    photoIds: number[]
  ): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/photos/bulk-remove-from-album`,
      {
        headers: this.getAuthHeaders(),
        data: { albumId, photoIds },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Add tags to photos
   */
  async addTagsToPhotos(tagIds: number[], photoIds: number[]): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/photos/bulk-add-tags`,
      {
        headers: this.getAuthHeaders(),
        data: { tagIds, photoIds },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Remove tags from photos
   */
  async removeTagsFromPhotos(
    tagIds: number[],
    photoIds: number[]
  ): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/photos/bulk-remove-tags`,
      {
        headers: this.getAuthHeaders(),
        data: { tagIds, photoIds },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Get display photos (public endpoint)
   */
  async getDisplayPhotos(count: number = 100): Promise<any[]> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/display/photos?count=${count}`
    );
    if (response.ok()) {
      return response.json();
    }
    return [];
  }

  // ==========================================================================
  // Album Management
  // ==========================================================================

  /**
   * Get all albums
   */
  async getAlbums(): Promise<Album[]> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/albums`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return [];
  }

  /**
   * Get album by ID
   */
  async getAlbum(id: number): Promise<Album | null> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/albums/${id}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Create an album
   */
  async createAlbum(name: string, description?: string): Promise<Album | null> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/albums`,
      {
        headers: this.getAuthHeaders(),
        data: { name, description },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Update an album
   */
  async updateAlbum(
    id: number,
    data: { name?: string; description?: string }
  ): Promise<Album | null> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/albums/${id}`,
      {
        headers: this.getAuthHeaders(),
        data,
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Delete an album
   */
  async deleteAlbum(id: number): Promise<boolean> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/albums/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.ok();
  }

  /**
   * Set album cover photo
   */
  async setAlbumCover(albumId: number, photoId: number): Promise<Album | null> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/albums/${albumId}/cover/${photoId}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Remove album cover photo
   */
  async removeAlbumCover(albumId: number): Promise<Album | null> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/albums/${albumId}/cover`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  // ==========================================================================
  // Tag Management
  // ==========================================================================

  /**
   * Get all tags
   */
  async getTags(): Promise<Tag[]> {
    const response = await this.request.get(`${API_BASE_URL}/api/admin/tags`, {
      headers: this.getAuthHeaders(),
    });
    if (response.ok()) {
      return response.json();
    }
    return [];
  }

  /**
   * Get tag by ID
   */
  async getTag(id: number): Promise<Tag | null> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/tags/${id}`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Create a tag
   */
  async createTag(name: string, color?: string): Promise<Tag | null> {
    const response = await this.request.post(`${API_BASE_URL}/api/admin/tags`, {
      headers: this.getAuthHeaders(),
      data: { name, color: color || "#4CAF50" },
    });
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Update a tag
   */
  async updateTag(
    id: number,
    data: { name?: string; color?: string }
  ): Promise<Tag | null> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/tags/${id}`,
      {
        headers: this.getAuthHeaders(),
        data,
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Delete a tag
   */
  async deleteTag(id: number): Promise<boolean> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/tags/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.ok();
  }

  // ==========================================================================
  // Guest Link Management
  // ==========================================================================

  /**
   * Get all guest links
   */
  async getGuestLinks(): Promise<GuestLink[]> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/guest-links`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return [];
  }

  /**
   * Create a guest link
   */
  async createGuestLink(data: {
    name: string;
    expiresAt?: string;
    maxUploads?: number;
    targetAlbumId?: number;
  }): Promise<GuestLink | null> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/guest-links`,
      {
        headers: this.getAuthHeaders(),
        data,
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Delete a guest link
   */
  async deleteGuestLink(id: string): Promise<boolean> {
    const response = await this.request.delete(
      `${API_BASE_URL}/api/admin/guest-links/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.ok();
  }

  /**
   * Validate a guest link (public endpoint)
   */
  async validateGuestLink(linkId: string): Promise<any> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/guest/validate/${linkId}`
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  // ==========================================================================
  // Storage Management
  // ==========================================================================

  /**
   * Get storage providers
   */
  async getStorageProviders(): Promise<StorageProvider[]> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/admin/storage/providers`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return [];
  }

  /**
   * Sync a storage provider
   */
  async syncStorageProvider(providerId: number): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/storage/providers/${providerId}/sync`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Scan for new files
   */
  async scanForFiles(providerId: number): Promise<any> {
    const response = await this.request.post(
      `${API_BASE_URL}/api/admin/storage/providers/${providerId}/scan`,
      { headers: this.getAuthHeaders() }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Enable/disable a storage provider
   */
  async setProviderEnabled(providerId: number, enabled: boolean): Promise<any> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/storage/providers/${providerId}`,
      {
        headers: this.getAuthHeaders(),
        data: { isEnabled: enabled },
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  // ==========================================================================
  // Display Settings
  // ==========================================================================

  /**
   * Get display settings
   */
  async getDisplaySettings(): Promise<DisplaySettings | null> {
    const response = await this.request.get(
      `${API_BASE_URL}/api/display/settings`
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  /**
   * Update display settings
   */
  async updateDisplaySettings(
    settings: Partial<DisplaySettings>
  ): Promise<DisplaySettings | null> {
    const response = await this.request.put(
      `${API_BASE_URL}/api/admin/display/settings`,
      {
        headers: this.getAuthHeaders(),
        data: settings,
      }
    );
    if (response.ok()) {
      return response.json();
    }
    return null;
  }

  // ==========================================================================
  // Auth State Management
  // ==========================================================================

  private setAuthFromResponse(data: any): void {
    authState.token = data.token;
    authState.refreshToken = data.refreshToken;
    authState.userId = data.user?.id;
    authState.role = data.user?.role;
  }

  /**
   * Get auth headers
   */
  private getAuthHeaders(): Record<string, string> {
    if (authState.token) {
      return { Authorization: `Bearer ${authState.token}` };
    }
    return {};
  }

  /**
   * Get current auth state
   */
  getAuthState(): AuthState {
    return authState;
  }

  /**
   * Set auth state (for restoring from storage)
   */
  setAuthState(state: AuthState): void {
    authState = state;
  }

  /**
   * Clear auth state
   */
  clearAuthState(): void {
    authState = {
      token: null,
      refreshToken: null,
      userId: null,
      role: null,
    };
  }

  /**
   * Check if currently authenticated
   */
  isAuthenticated(): boolean {
    return authState.token !== null;
  }
}

// ============================================================================
// Test Fixtures
// ============================================================================

/**
 * Extended test fixture with API client and helpers
 */
export const test = base.extend<{
  api: ApiClient;
  authenticatedPage: Page;
}>({
  /**
   * API client for direct API interactions
   */
  api: async ({ playwright }, use) => {
    const apiContext = await playwright.request.newContext({
      baseURL: API_BASE_URL,
    });
    const client = new ApiClient(apiContext);
    await use(client);
    await apiContext.dispose();
  },

  /**
   * Page that's pre-authenticated with test admin user
   */
  authenticatedPage: async ({ page, api }, use) => {
    // Login via API first
    const loginResult = await api.login(TEST_ADMIN.email, TEST_ADMIN.password);

    if (loginResult) {
      // Set auth token in localStorage before navigating
      await page.addInitScript((token) => {
        localStorage.setItem("auth_token", token);
      }, loginResult.token);

      // Also store user info
      await page.addInitScript((user) => {
        localStorage.setItem("auth_user", JSON.stringify(user));
      }, loginResult.user);
    }

    await use(page);
  },
});

export { expect } from "@playwright/test";

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Helper to wait for Angular to stabilize
 */
export async function waitForAngular(page: Page): Promise<void> {
  await page.waitForFunction(() => {
    const win = globalThis as unknown as {
      getAllAngularTestabilities?: () => Array<{ isStable: () => boolean }>;
    };
    if (win.getAllAngularTestabilities) {
      return win.getAllAngularTestabilities().every((t) => t.isStable());
    }
    return true;
  });
}

/**
 * Helper to fill Material form field
 * Uses getByRole for more reliable targeting of input elements
 */
export async function fillMaterialInput(
  page: Page,
  label: string,
  value: string
): Promise<void> {
  // Use getByRole for textbox with name matching the label
  const input = page.getByRole("textbox", { name: label });
  // Use fill directly - it clears and types
  await input.fill(value);
}

/**
 * Helper to click Material button by text
 */
export async function clickMaterialButton(
  page: Page,
  text: string
): Promise<void> {
  await page.getByRole("button", { name: text }).click();
}

/**
 * Helper to wait for API response
 */
export async function waitForApiResponse(
  page: Page,
  urlPattern: string | RegExp
): Promise<void> {
  await page.waitForResponse((response) => {
    if (typeof urlPattern === "string") {
      return response.url().includes(urlPattern);
    }
    return urlPattern.test(response.url());
  });
}

/**
 * Helper to login via the UI
 */
export async function loginViaUi(
  page: Page,
  email: string,
  password: string
): Promise<void> {
  await page.goto("/login");

  // Use getByRole for reliable targeting
  const emailInput = page.getByRole("textbox", {
    name: "Email",
  });
  const passwordInput = page.getByRole("textbox", { name: "Password" });

  await emailInput.fill(email);
  await passwordInput.fill(password);
  await page.getByRole("button", { name: "Sign In" }).click();

  // Wait for navigation to complete
  await page.waitForURL(/\/(dashboard|photos|setup)/, { timeout: 15000 });
}

/**
 * Helper to login via the UI as a specific user
 */
export async function loginAs(
  page: Page,
  user: { email: string; password: string }
): Promise<void> {
  await loginViaUi(page, user.email, user.password);
}

/**
 * Helper to ensure test admin user exists
 */
export async function ensureTestAdmin(api: ApiClient): Promise<void> {
  // First check if setup is required
  const setupStatus = await api.getSetupStatus();

  if (setupStatus.isSetupRequired) {
    // Complete setup with test admin
    await api.completeSetup(TEST_ADMIN.email, TEST_ADMIN.password);
  } else {
    // Try to login
    const loginResult = await api.login(TEST_ADMIN.email, TEST_ADMIN.password);
    if (!loginResult) {
      // If login fails, the test admin doesn't exist - this is an error state
      throw new Error(
        "Test admin user does not exist and setup is already complete"
      );
    }
  }
}

/**
 * Helper to upload test images via the page UI
 */
export async function uploadTestImageViaUi(
  page: Page,
  imageName: string
): Promise<void> {
  const filePath = path.join(TEST_ASSETS_DIR, imageName);

  // Set up file chooser handler before clicking upload
  const fileChooserPromise = page.waitForEvent("filechooser");
  await page.getByRole("button", { name: /upload/i }).click();
  const fileChooser = await fileChooserPromise;
  await fileChooser.setFiles(filePath);
}

/**
 * Helper to select photos in the grid by clicking with Ctrl key
 */
export async function selectPhotosInGrid(
  page: Page,
  count: number
): Promise<void> {
  // Click on photo cards to select them
  const photoCards = page.locator(
    '[data-testid="photo-card"], .photo-card, .photo-item, mat-card'
  );
  for (let i = 0; i < count; i++) {
    await photoCards.nth(i).click({ modifiers: ["Control"] });
  }
}

/**
 * Helper to wait for snackbar message
 */
export async function waitForSnackbar(
  page: Page,
  textPattern?: string | RegExp
): Promise<void> {
  const snackbar = page.locator(
    "mat-snack-bar-container, .mat-mdc-snack-bar-container"
  );
  await playwrightExpect(snackbar).toBeVisible({ timeout: 10000 });
  if (textPattern) {
    await playwrightExpect(snackbar).toContainText(textPattern);
  }
}

/**
 * Helper to close any open dialogs
 */
export async function closeDialog(page: Page): Promise<void> {
  const closeButton = page.getByRole("button", { name: /close|cancel/i });
  if (await closeButton.isVisible({ timeout: 1000 }).catch(() => false)) {
    await closeButton.click();
  }
}

/**
 * Helper to confirm a delete dialog
 */
export async function confirmDelete(page: Page): Promise<void> {
  const dialog = page.getByRole("dialog");
  await playwrightExpect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: /delete|confirm|yes/i }).click();
}

/**
 * Get the file path for a test asset
 */
export function getTestAssetPath(filename: string): string {
  return path.join(TEST_ASSETS_DIR, filename);
}

/**
 * Helper to wait for page to be fully loaded
 */
export async function waitForPageLoad(page: Page): Promise<void> {
  await page.waitForLoadState("networkidle");
  await waitForAngular(page);
}
