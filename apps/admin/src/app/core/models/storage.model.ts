import { MediaType, StorageProviderType } from "./enums.model";

/**
 * DTO for storage provider information sent to the frontend.
 */
export interface StorageProviderDto {
  id: number;
  type: StorageProviderType;
  name: string;
  isEnabled: boolean;
  supportsUpload: boolean;
  supportsWatch: boolean;
  lastSyncDate: string | null;
  photoCount: number;
  isConnected: boolean | null;
  statusMessage: string | null;
}

export interface PickerPollingConfig {
  pollInterval?: string | null;
  timeoutIn?: string | null;
}

export interface PickerSessionDto {
  sessionId: string;
  pickerUri: string;
  mediaItemsSet: boolean;
  expireTime?: string | null;
  pollingConfig?: PickerPollingConfig | null;
}

export interface PickedMediaItemDto {
  id: string;
  type: string;
  mimeType?: string | null;
  filename?: string | null;
  width?: number | null;
  height?: number | null;
  createTime?: string | null;
  videoProcessingStatus?: string | null;
  thumbnailUrl?: string | null;
}

/**
 * Request to create a new storage provider.
 */
export interface CreateStorageProviderRequest {
  type: StorageProviderType;
  name: string;
  configuration?: string | null;
  isEnabled?: boolean;
}

/**
 * Request to update an existing storage provider.
 */
export interface UpdateStorageProviderRequest {
  name?: string | null;
  configuration?: string | null;
  isEnabled?: boolean | null;
}

/**
 * Configuration for local storage provider.
 */
export interface LocalStorageConfiguration {
  basePath: string;
  organizeByDate: boolean;
  watchForChanges: boolean;
}

/**
 * Information about a file in a storage provider.
 */
export interface StorageFileInfo {
  fileId: string;
  fileName: string;
  fullPath: string | null;
  fileSize: number;
  contentType: string | null;
  mediaType: MediaType;
  createdDate: string | null;
  modifiedDate: string | null;
  contentHash: string | null;
  width: number | null;
  height: number | null;
  duration: number | null;
  isFolder: boolean;
  parentFolderId: string | null;
}

/**
 * Request parameters for a sync operation.
 */
export interface SyncRequest {
  fullSync?: boolean;
  removeDeleted?: boolean;
  skipExisting?: boolean;
  maxFiles?: number;
  folderId?: string | null;
  recursive?: boolean;
}

/**
 * Result of a sync operation.
 */
export interface SyncResult {
  providerId: number;
  providerName: string;
  success: boolean;
  errorMessage: string | null;
  filesAdded: number;
  filesUpdated: number;
  filesRemoved: number;
  filesSkipped: number;
  totalFilesProcessed: number;
  totalFilesFound: number;
  startTime: string;
  endTime: string;
  errors: string[];
}

/**
 * Current status of a sync operation.
 */
export interface SyncStatus {
  providerId: number;
  isInProgress: boolean;
  progressPercent: number;
  currentOperation: string | null;
  filesProcessed: number;
  totalFiles: number | null;
  startTime: string | null;
  lastSyncResult: SyncResult | null;
}

/**
 * Result of scanning a provider for files without importing.
 */
export interface ScanResult {
  providerId: number;
  success: boolean;
  errorMessage: string | null;
  totalFilesFound: number;
  newFilesCount: number;
  existingFilesCount: number;
  newFilesTotalSize: number;
  sampleNewFiles: StorageFileInfo[];
}

export interface ImportPickerItemsResponse {
  imported: number;
  failed: number;
}

/**
 * Request for uploading a file.
 */
export interface UploadRequest {
  albumId?: number | null;
  tags?: string[] | null;
  customFilename?: string | null;
  overwrite?: boolean;
}

/**
 * Result of an upload operation.
 */
export interface UploadResult {
  success: boolean;
  errorMessage: string | null;
  photoId: number | null;
  fileId: string | null;
  fileName: string | null;
  filePath: string | null;
  fileSize: number;
  contentType: string | null;
  fileUrl: string | null;
  thumbnailUrl: string | null;
}

/**
 * Result of a batch upload operation.
 */
export interface BatchUploadResult {
  totalFiles: number;
  successfulUploads: number;
  failedUploads: number;
  results: UploadResult[];
  allSuccessful: boolean;
}

/**
 * Request for guest link uploads.
 */
export interface GuestUploadRequest {
  linkId: string;
  message?: string | null;
}

/**
 * Result of scanning local storage for files.
 */
export interface ScannedFile {
  fileId: string;
  fileName: string;
  fullPath: string;
  fileSize: number;
  mediaType: MediaType;
}
