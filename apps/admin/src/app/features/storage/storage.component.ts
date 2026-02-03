import { Component, inject, signal, OnInit, OnDestroy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatListModule } from "@angular/material/list";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatMenuModule } from "@angular/material/menu";
import { MatChipsModule } from "@angular/material/chips";
import { MatTooltipModule } from "@angular/material/tooltip";
import { StorageService } from "../../core/services/storage.service";
import { CacheService, CacheStatus } from "../../core/services/cache.service";
import { StorageProviderDto, SyncStatus } from "../../core/models";
import { StorageProviderType } from "../../core/models/enums.model";
import { interval, Subject, takeUntil } from "rxjs";
import { GooglePhotosPickerComponent } from "./google-photos-picker.component";

@Component({
  selector: "app-storage",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatMenuModule,
    MatChipsModule,
    MatTooltipModule,
    GooglePhotosPickerComponent,
  ],
  template: `
    <div class="storage-container">
      <div class="header">
        <h1>Storage Providers</h1>
      </div>

      @if (isLoading()) {
        <div class="loading">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      } @else {
        <div class="providers-grid">
          @for (provider of providers(); track provider.id) {
            <mat-card [class.syncing]="isSyncing(provider.id)">
              <mat-card-header>
                <mat-icon
                  mat-card-avatar
                  [class]="getProviderIconClass(provider.type)"
                >
                  {{ getProviderIcon(provider.type) }}
                </mat-icon>
                <mat-card-title>{{ provider.name }}</mat-card-title>
                <mat-card-subtitle>
                  @if (provider.type === StorageProviderType.Local) {
                    <span class="status status-chip local-status">
                      <mat-icon>check_circle</mat-icon> Active
                    </span>
                  } @else if (provider.isConnected === true) {
                    @if (provider.type === StorageProviderType.GooglePhotos) {
                      <span class="status status-chip connected-status">
                        <mat-icon>cloud_done</mat-icon> Ready to pick photos
                      </span>
                    } @else {
                      <span class="status status-chip connected-status">
                        <mat-icon>cloud_done</mat-icon> Connected
                      </span>
                    }
                  } @else if (provider.isConnected === false) {
                    <span class="status status-chip disconnected-status">
                      <mat-icon>cloud_off</mat-icon> Disconnected
                    </span>
                  } @else {
                    <span class="status status-chip unknown-status">
                      <mat-icon>help_outline</mat-icon> Unknown
                    </span>
                  }
                </mat-card-subtitle>
              </mat-card-header>

              <mat-card-content>
                <p>{{ getProviderDescription(provider.type) }}</p>

                <div class="provider-stats">
                  <div class="stat">
                    <mat-icon>photo_library</mat-icon>
                    <span>{{ provider.photoCount }} photos</span>
                  </div>
                  @if (provider.lastSyncDate) {
                    <div class="stat">
                      <mat-icon>sync</mat-icon>
                      <span
                        >Last sync:
                        {{ formatDate(provider.lastSyncDate) }}</span
                      >
                    </div>
                  }
                </div>

                @if (provider.statusMessage) {
                  <div class="status-message">{{ provider.statusMessage }}</div>
                }
                @if (
                  provider.type === StorageProviderType.GooglePhotos &&
                  provider.isConnected
                ) {
                  <app-google-photos-picker
                    [providerId]="provider.id"
                    (imported)="loadProviders()"
                  ></app-google-photos-picker>
                }
                @if (isSyncing(provider.id)) {
                  <div class="sync-progress">
                    <mat-progress-bar
                      mode="determinate"
                      [value]="getSyncProgress(provider.id)"
                    ></mat-progress-bar>
                    <span class="progress-text">
                      {{ getSyncStatusText(provider.id) }}
                    </span>
                  </div>
                }

                <div class="provider-badges">
                  @if (provider.supportsUpload) {
                    <span class="badge" matTooltip="Supports file upload"
                      >Upload</span
                    >
                  }
                  @if (provider.supportsWatch) {
                    <span class="badge" matTooltip="Watches for file changes"
                      >Watch</span
                    >
                  }
                  @if (!provider.isEnabled) {
                    <span class="badge disabled">Disabled</span>
                  }
                </div>
              </mat-card-content>

              <mat-card-actions>
                @if (
                  provider.type === StorageProviderType.Local ||
                  (provider.type !== StorageProviderType.GooglePhotos &&
                    provider.isConnected)
                ) {
                  <button
                    mat-button
                    (click)="syncProvider(provider)"
                    [disabled]="isSyncing(provider.id)"
                  >
                    @if (isSyncing(provider.id)) {
                      <mat-spinner diameter="18"></mat-spinner>
                    } @else {
                      <mat-icon>sync</mat-icon>
                    }
                    Sync
                  </button>
                }
                @if (
                  provider.type === StorageProviderType.Local ||
                  provider.type === StorageProviderType.GooglePhotos ||
                  provider.isConnected
                ) {
                  <button mat-icon-button [matMenuTriggerFor]="providerMenu">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                  <mat-menu #providerMenu="matMenu">
                    <button mat-menu-item (click)="scanProvider(provider)">
                      <mat-icon>search</mat-icon>
                      Scan for New Files
                    </button>
                    @if (provider.type === StorageProviderType.GooglePhotos) {
                      <button
                        mat-menu-item
                        (click)="reconnectProvider(provider)"
                      >
                        <mat-icon>link</mat-icon>
                        Reconnect
                      </button>
                    }
                    @if (provider.type !== StorageProviderType.Local) {
                      <button
                        mat-menu-item
                        (click)="disconnectProvider(provider)"
                      >
                        <mat-icon>link_off</mat-icon>
                        Disconnect
                      </button>
                    }
                    <button mat-menu-item (click)="toggleProvider(provider)">
                      <mat-icon>{{
                        provider.isEnabled ? "visibility_off" : "visibility"
                      }}</mat-icon>
                      {{ provider.isEnabled ? "Disable" : "Enable" }}
                    </button>
                  </mat-menu>
                } @else {
                  <button
                    mat-raised-button
                    color="primary"
                    (click)="connectProvider(provider.type)"
                  >
                    <mat-icon>link</mat-icon>
                    Connect
                  </button>
                }
              </mat-card-actions>
            </mat-card>
          }

          <!-- Add new cloud provider cards -->
          @if (!hasProvider(StorageProviderType.GooglePhotos)) {
            <mat-card class="add-provider">
              <mat-card-header>
                <mat-icon mat-card-avatar class="google">add_to_drive</mat-icon>
                <mat-card-title>Google Photos</mat-card-title>
                <mat-card-subtitle>Not connected</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p>Sync photos from your Google Photos library.</p>
              </mat-card-content>
              <mat-card-actions>
                <button
                  mat-raised-button
                  color="primary"
                  (click)="connectProvider(StorageProviderType.GooglePhotos)"
                >
                  <mat-icon>link</mat-icon>
                  Connect
                </button>
              </mat-card-actions>
            </mat-card>
          }
          @if (!hasProvider(StorageProviderType.GoogleDrive)) {
            <mat-card class="add-provider">
              <mat-card-header>
                <mat-icon mat-card-avatar class="google">cloud</mat-icon>
                <mat-card-title>Google Drive</mat-card-title>
                <mat-card-subtitle>Not connected</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p>Sync photos from Google Drive folders.</p>
              </mat-card-content>
              <mat-card-actions>
                <button
                  mat-raised-button
                  color="primary"
                  (click)="connectProvider(StorageProviderType.GoogleDrive)"
                >
                  <mat-icon>link</mat-icon>
                  Connect
                </button>
              </mat-card-actions>
            </mat-card>
          }
          @if (!hasProvider(StorageProviderType.OneDrive)) {
            <mat-card class="add-provider">
              <mat-card-header>
                <mat-icon mat-card-avatar class="microsoft"
                  >cloud_queue</mat-icon
                >
                <mat-card-title>OneDrive</mat-card-title>
                <mat-card-subtitle>Not connected</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <p>Sync photos from Microsoft OneDrive.</p>
              </mat-card-content>
              <mat-card-actions>
                <button
                  mat-raised-button
                  color="primary"
                  (click)="connectProvider(StorageProviderType.OneDrive)"
                >
                  <mat-icon>link</mat-icon>
                  Connect
                </button>
              </mat-card-actions>
            </mat-card>
          }
        </div>

        <!-- Cache Management Section -->
        <div class="cache-section">
          <h2>Cache Management</h2>

          @if (cacheStatus()) {
            <mat-card>
              <mat-card-header>
                <mat-icon mat-card-avatar class="cache-icon">storage</mat-icon>
                <mat-card-title>Server Cache</mat-card-title>
                <mat-card-subtitle>
                  Files cached from cloud providers
                </mat-card-subtitle>
              </mat-card-header>

              <mat-card-content>
                <div class="cache-stats-grid">
                  <div class="cache-stat">
                    <div class="stat-label">Cache Usage</div>
                    <div class="stat-value">
                      {{ formatBytes(cacheStatus()!.totalSizeBytes) }} /
                      {{ formatBytes(cacheStatus()!.maxSizeBytes) }}
                    </div>
                    <mat-progress-bar
                      mode="determinate"
                      [value]="cacheStatus()!.usagePercent"
                      [color]="
                        cacheStatus()!.usagePercent > 90
                          ? 'warn'
                          : cacheStatus()!.usagePercent > 70
                            ? 'accent'
                            : 'primary'
                      "
                    ></mat-progress-bar>
                    <div class="stat-percent">
                      {{ cacheStatus()!.usagePercent.toFixed(1) }}% used
                    </div>
                  </div>

                  <div class="cache-stat">
                    <div class="stat-label">Cached Files</div>
                    <div class="stat-value">
                      {{ cacheStatus()!.fileCount }} files
                    </div>
                  </div>
                </div>
              </mat-card-content>

              <mat-card-actions>
                <button
                  mat-button
                  (click)="triggerEviction()"
                  matTooltip="Remove old files to free up space"
                >
                  <mat-icon>cleaning_services</mat-icon>
                  Evict Old Files
                </button>
                <button
                  mat-button
                  color="warn"
                  (click)="clearCache()"
                  matTooltip="Remove all cached files"
                >
                  <mat-icon>delete_sweep</mat-icon>
                  Clear Cache
                </button>
              </mat-card-actions>
            </mat-card>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .storage-container {
        padding: 24px;
      }

      .header {
        margin-bottom: 24px;
      }

      .loading {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .providers-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
        gap: 16px;
      }

      mat-card {
        transition: box-shadow 0.2s;
      }

      mat-card.syncing {
        border: 2px solid #2196f3;
      }

      mat-card.add-provider {
        border: 2px dashed #e0e0e0;
      }

      mat-icon[mat-card-avatar] {
        font-size: 40px;
        width: 40px;
        height: 40px;
      }

      mat-icon[mat-card-avatar].google {
        color: #4285f4;
      }

      mat-icon[mat-card-avatar].microsoft {
        color: #00a4ef;
      }

      mat-icon[mat-card-avatar].local {
        color: #4caf50;
      }

      .status {
        display: inline-flex;
        align-items: center;
        gap: 4px;
      }

      .status-chip {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 4px 12px;
        border-radius: 16px;
        font-size: 12px;
        font-weight: 500;
      }

      .status-chip mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
      }

      .local-status {
        background-color: #e8f5e9;
        color: #2e7d32;
      }

      .connected-status {
        background-color: #e8f5e9;
        color: #2e7d32;
      }

      .disconnected-status {
        background-color: #ffebee;
        color: #c62828;
      }

      .unknown-status {
        background-color: #fff3e0;
        color: #e65100;
      }

      .status.connected {
        color: #4caf50;
      }

      .status.disconnected {
        color: #9e9e9e;
      }

      mat-card-content {
        padding-top: 8px;
      }

      mat-card-content p {
        color: rgba(0, 0, 0, 0.6);
        margin-bottom: 16px;
      }

      .provider-stats {
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-bottom: 12px;
      }

      .stat {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 13px;
        color: rgba(0, 0, 0, 0.6);
      }

      .stat mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }

      .status-message {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.54);
        margin-bottom: 12px;
      }

      .sync-progress {
        margin: 16px 0;
      }

      .progress-text {
        display: block;
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
        margin-top: 4px;
      }

      .provider-badges {
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
      }

      .badge {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 12px;
        font-size: 11px;
        background-color: #e3f2fd;
        color: #1976d2;
      }

      .badge.disabled {
        background-color: #f5f5f5;
        color: #9e9e9e;
      }

      mat-card-actions {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      mat-card-actions button mat-spinner {
        display: inline-block;
        margin-right: 8px;
      }

      .cache-section {
        margin-top: 32px;
      }

      .cache-section h2 {
        margin-bottom: 16px;
      }

      .cache-icon {
        color: #9c27b0 !important;
      }

      .cache-stats-grid {
        display: grid;
        grid-template-columns: 2fr 1fr;
        gap: 24px;
      }

      .cache-stat {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .stat-label {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
        font-weight: 500;
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }

      .stat-value {
        font-size: 20px;
        font-weight: 500;
        color: rgba(0, 0, 0, 0.87);
      }

      .stat-percent {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
        margin-top: 4px;
      }
    `,
  ],
})
export class StorageComponent implements OnInit, OnDestroy {
  private readonly storageService = inject(StorageService);
  private readonly cacheService = inject(CacheService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroy$ = new Subject<void>();

  providers = signal<StorageProviderDto[]>([]);
  syncStatuses = signal<Map<number, SyncStatus>>(new Map());
  cacheStatus = signal<CacheStatus | null>(null);
  isLoading = signal(true);

  // Expose enum to template
  StorageProviderType = StorageProviderType;

  ngOnInit(): void {
    this.loadProviders();
    this.loadCacheStatus();
    this.startSyncStatusPolling();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadProviders(): void {
    this.isLoading.set(true);
    this.storageService.getProviders().subscribe({
      next: (providers) => {
        this.providers.set(providers);
        this.isLoading.set(false);
        // Check sync status for each provider
        providers.forEach((p) => this.refreshSyncStatus(p.id));
      },
      error: (error) => {
        console.error("Failed to load providers:", error);
        this.isLoading.set(false);
        this.snackBar.open("Failed to load storage providers", "Close", {
          duration: 3000,
        });
      },
    });
  }

  loadCacheStatus(): void {
    this.cacheService.getCacheStatus().subscribe({
      next: (status) => this.cacheStatus.set(status),
      error: (error) => console.error("Failed to load cache status:", error),
    });
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return "0 B";
    const k = 1024;
    const sizes = ["B", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return (
      Number.parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i]
    );
  }

  triggerEviction(): void {
    this.cacheService.triggerEviction().subscribe({
      next: (result) => {
        this.snackBar.open(
          `Evicted ${result.filesEvicted} files from cache`,
          "Close",
          { duration: 3000 },
        );
        this.loadCacheStatus();
      },
      error: (error) => {
        console.error("Cache eviction failed:", error);
        this.snackBar.open("Cache eviction failed", "Close", {
          duration: 3000,
        });
      },
    });
  }

  clearCache(): void {
    if (confirm("Clear all cached files? This cannot be undone.")) {
      this.cacheService.clearCache().subscribe({
        next: () => {
          this.snackBar.open("Cache cleared successfully", "Close", {
            duration: 3000,
          });
          this.loadCacheStatus();
        },
        error: (error) => {
          console.error("Failed to clear cache:", error);
          this.snackBar.open("Failed to clear cache", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }

  private startSyncStatusPolling(): void {
    interval(5000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        // Only poll for providers that are syncing
        this.providers().forEach((p) => {
          if (this.isSyncing(p.id)) {
            this.refreshSyncStatus(p.id);
          }
        });
      });
  }

  private refreshSyncStatus(providerId: number): void {
    this.storageService.getSyncStatus(providerId).subscribe({
      next: (status) => {
        this.syncStatuses.update((map) => {
          const newMap = new Map(map);
          newMap.set(providerId, status);
          return newMap;
        });
      },
    });
  }

  getProviderIcon(type: StorageProviderType): string {
    switch (type) {
      case StorageProviderType.Local:
        return "folder";
      case StorageProviderType.GooglePhotos:
        return "add_to_drive";
      case StorageProviderType.GoogleDrive:
        return "cloud";
      case StorageProviderType.OneDrive:
        return "cloud_queue";
      default:
        return "storage";
    }
  }

  getProviderIconClass(type: StorageProviderType): string {
    switch (type) {
      case StorageProviderType.Local:
        return "local";
      case StorageProviderType.GooglePhotos:
      case StorageProviderType.GoogleDrive:
        return "google";
      case StorageProviderType.OneDrive:
        return "microsoft";
      default:
        return "";
    }
  }

  getProviderDescription(type: StorageProviderType): string {
    switch (type) {
      case StorageProviderType.Local:
        return "Store photos directly on the device.";
      case StorageProviderType.GooglePhotos:
        return "Select photos from Google Photos and import them into LibraFoto.";
      case StorageProviderType.GoogleDrive:
        return "Sync photos from Google Drive folders.";
      case StorageProviderType.OneDrive:
        return "Sync photos from Microsoft OneDrive.";
      default:
        return "Storage provider";
    }
  }

  hasProvider(type: StorageProviderType): boolean {
    return this.providers().some((p) => p.type === type);
  }

  isSyncing(providerId: number): boolean {
    return this.syncStatuses().get(providerId)?.isInProgress ?? false;
  }

  getSyncProgress(providerId: number): number {
    return this.syncStatuses().get(providerId)?.progressPercent ?? 0;
  }

  getSyncStatusText(providerId: number): string {
    const status = this.syncStatuses().get(providerId);
    if (!status) return "";

    if (status.totalFiles) {
      return `${status.filesProcessed} / ${status.totalFiles} files`;
    }
    if (status.currentOperation) {
      return status.currentOperation;
    }
    return "Syncing...";
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return (
      date.toLocaleDateString() +
      " " +
      date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    );
  }

  syncProvider(provider: StorageProviderDto): void {
    this.storageService.syncProvider(provider.id).subscribe({
      next: (result) => {
        if (result.success) {
          this.snackBar.open(
            `Sync complete: ${result.filesAdded} added, ${result.filesUpdated} updated`,
            "Close",
            { duration: 5000 },
          );
        } else {
          this.snackBar.open(`Sync failed: ${result.errorMessage}`, "Close", {
            duration: 5000,
          });
        }
        this.loadProviders();
      },
      error: (error) => {
        console.error("Sync failed:", error);
        this.snackBar.open("Sync failed", "Close", { duration: 3000 });
      },
    });
    this.refreshSyncStatus(provider.id);
  }

  scanProvider(provider: StorageProviderDto): void {
    this.storageService.scanProvider(provider.id).subscribe({
      next: (result) => {
        if (result.success) {
          this.snackBar.open(
            `Scan complete: Found ${result.newFilesCount} new files (${result.totalFilesFound} total)`,
            "Close",
            { duration: 5000 },
          );
        } else {
          this.snackBar.open(`Scan failed: ${result.errorMessage}`, "Close", {
            duration: 5000,
          });
        }
      },
      error: (error) => {
        console.error("Scan failed:", error);
        this.snackBar.open("Scan failed", "Close", { duration: 3000 });
      },
    });
  }

  toggleProvider(provider: StorageProviderDto): void {
    this.storageService
      .updateProvider(provider.id, { isEnabled: !provider.isEnabled })
      .subscribe({
        next: () => {
          this.snackBar.open(
            provider.isEnabled ? "Provider disabled" : "Provider enabled",
            "Close",
            { duration: 3000 },
          );
          this.loadProviders();
        },
        error: (error) => {
          console.error("Failed to update provider:", error);
          this.snackBar.open("Failed to update provider", "Close", {
            duration: 3000,
          });
        },
      });
  }

  connectProvider(type: StorageProviderType): void {
    if (type === StorageProviderType.GooglePhotos) {
      this.connectGooglePhotos();
    } else {
      this.snackBar.open(
        "This provider integration is coming soon! Use Google Photos or local storage for now.",
        "Close",
        { duration: 5000 },
      );
    }
  }

  private connectGooglePhotos(): void {
    // Check if a Google Photos provider already exists
    const existingProvider = this.providers().find(
      (p) => p.type === StorageProviderType.GooglePhotos,
    );

    if (existingProvider) {
      // Use existing provider
      this.initiateGooglePhotosOAuth(existingProvider.id);
      return;
    }

    // Create a new Google Photos provider
    this.storageService
      .createProvider({
        type: StorageProviderType.GooglePhotos,
        name: "My Google Photos",
        isEnabled: true,
      })
      .subscribe({
        next: (provider) => {
          // Now initiate OAuth flow
          this.initiateGooglePhotosOAuth(provider.id);
        },
        error: (error) => {
          console.error("Failed to create Google Photos provider:", error);
          this.snackBar.open(
            "Failed to create Google Photos provider",
            "Close",
            { duration: 3000 },
          );
        },
      });
  }

  private initiateGooglePhotosOAuth(providerId: number): void {
    this.storageService.getGooglePhotosAuthUrl(providerId).subscribe({
      next: (response: { authorizationUrl: string }) => {
        // Open OAuth URL in a popup window
        const width = 600;
        const height = 700;
        const left = window.screen.width / 2 - width / 2;
        const top = window.screen.height / 2 - height / 2;

        const popup = window.open(
          response.authorizationUrl,
          "GooglePhotosAuth",
          `width=${width},height=${height},top=${top},left=${left},popup=1`,
        );

        if (!popup) {
          this.snackBar.open(
            "Please allow popups to connect to Google Photos",
            "Close",
            { duration: 5000 },
          );
          return;
        }

        // Poll for OAuth completion
        this.pollForOAuthCompletion(providerId, popup);
      },
      error: (error) => {
        console.error("Failed to get Google Photos auth URL:", error);
        this.snackBar.open(
          "Failed to start Google Photos authentication",
          "Close",
          { duration: 3000 },
        );
      },
    });
  }

  private pollForOAuthCompletion(providerId: number, popup: Window): void {
    const pollInterval = setInterval(() => {
      // Check if popup is closed
      if (popup.closed) {
        clearInterval(pollInterval);

        // Refresh provider to check connection status
        setTimeout(() => {
          this.loadProviders();
          this.snackBar.open(
            "Google Photos connection complete! Syncing photos...",
            "Close",
            { duration: 3000 },
          );

          // Trigger initial sync
          this.syncProvider({ id: providerId } as StorageProviderDto);
        }, 1000);
      }
    }, 500);

    // Stop polling after 5 minutes
    setTimeout(
      () => {
        clearInterval(pollInterval);
        if (!popup.closed) {
          popup.close();
        }
      },
      5 * 60 * 1000,
    );
  }

  disconnectProvider(provider: StorageProviderDto): void {
    const deletePhotos = confirm(
      `Disconnect ${provider.name}?\n\nDo you also want to delete all photos from this provider?\n\nClick OK to delete photos, or Cancel to keep photos (they will become orphaned).`,
    );

    const confirmDisconnect = confirm(
      deletePhotos
        ? `Delete ${provider.name} and all its photos? This cannot be undone.`
        : `Disconnect ${provider.name}? Photos will be kept but orphaned.`,
    );

    if (!confirmDisconnect) {
      return;
    }

    this.storageService.deleteProvider(provider.id, deletePhotos).subscribe({
      next: () => {
        this.snackBar.open(
          `${provider.name} disconnected${deletePhotos ? " and photos deleted" : ""}`,
          "Close",
          { duration: 3000 },
        );
        this.loadProviders();
      },
      error: (error) => {
        console.error("Failed to disconnect provider:", error);
        this.snackBar.open("Failed to disconnect provider", "Close", {
          duration: 3000,
        });
      },
    });
  }

  reconnectProvider(provider: StorageProviderDto): void {
    const confirmReconnect = confirm(
      `Reconnect ${provider.name}? This will clear stored OAuth tokens and start a new sign-in.`,
    );

    if (!confirmReconnect) {
      return;
    }

    this.storageService.disconnectProvider(provider.id).subscribe({
      next: () => {
        this.snackBar.open("Provider disconnected. Reconnecting...", "Close", {
          duration: 3000,
        });
        this.initiateGooglePhotosOAuth(provider.id);
      },
      error: (error) => {
        console.error("Failed to disconnect provider:", error);
        this.snackBar.open("Failed to reconnect provider", "Close", {
          duration: 3000,
        });
      },
    });
  }
}
