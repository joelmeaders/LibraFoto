import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { RouterLink } from "@angular/router";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { forkJoin } from "rxjs";
import { PhotoService } from "../../core/services/photo.service";
import { AlbumService } from "../../core/services/album.service";
import { TagService } from "../../core/services/tag.service";
import { StorageService } from "../../core/services/storage.service";
import { SystemCardComponent } from "./system-card/system-card.component";

interface DashboardStats {
  photoCount: number;
  albumCount: number;
  tagCount: number;
  providerCount: number;
}

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    SystemCardComponent,
  ],
  template: `
    <div class="dashboard-container">
      <h1>Dashboard</h1>

      @if (isLoading()) {
      <div class="loading">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
      } @else {
      <div class="stats-grid">
        <mat-card class="stat-card">
          <mat-card-header>
            <div class="icon-wrapper photos-icon">
              <mat-icon>photo_library</mat-icon>
            </div>
          </mat-card-header>
          <mat-card-content>
            <p class="stat-value">{{ stats().photoCount }}</p>
            <p class="stat-label">Photos</p>
          </mat-card-content>
          <mat-card-actions>
            <a mat-button routerLink="/photos">View All</a>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-header>
            <div class="icon-wrapper albums-icon">
              <mat-icon>collections</mat-icon>
            </div>
          </mat-card-header>
          <mat-card-content>
            <p class="stat-value">{{ stats().albumCount }}</p>
            <p class="stat-label">Albums</p>
          </mat-card-content>
          <mat-card-actions>
            <a mat-button routerLink="/albums">Manage</a>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-header>
            <div class="icon-wrapper tags-icon">
              <mat-icon>label</mat-icon>
            </div>
          </mat-card-header>
          <mat-card-content>
            <p class="stat-value">{{ stats().tagCount }}</p>
            <p class="stat-label">Tags</p>
          </mat-card-content>
          <mat-card-actions>
            <a mat-button routerLink="/tags">Manage</a>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card">
          <mat-card-header>
            <div class="icon-wrapper storage-icon">
              <mat-icon>cloud</mat-icon>
            </div>
          </mat-card-header>
          <mat-card-content>
            <p class="stat-value">{{ stats().providerCount }}</p>
            <p class="stat-label">Storage Sources</p>
          </mat-card-content>
          <mat-card-actions>
            <a mat-button routerLink="/storage">Configure</a>
          </mat-card-actions>
        </mat-card>
      </div>

      <!-- System Status Card -->
      <div class="system-section">
        <h2>System</h2>
        <app-system-card></app-system-card>
      </div>

      <!-- Quick Actions -->
      <div class="quick-actions">
        <h2>Quick Actions</h2>
        <div class="actions-grid">
          <button mat-raised-button color="primary" routerLink="/photos">
            <mat-icon>upload</mat-icon>
            Upload Photos
          </button>
          <button mat-stroked-button routerLink="/albums">
            <mat-icon>add</mat-icon>
            Create Album
          </button>
          <button mat-stroked-button routerLink="/display">
            <mat-icon>settings</mat-icon>
            Display Settings
          </button>
        </div>
      </div>

      <!-- Getting Started (show if empty) -->
      @if (stats().photoCount === 0) {
      <mat-card class="getting-started">
        <mat-card-header>
          <mat-icon mat-card-avatar>help_outline</mat-icon>
          <mat-card-title>Getting Started</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <ol>
            <li>
              <strong>Upload your photos</strong> - Go to the Photos section and
              upload your favorite memories.
            </li>
            <li>
              <strong>Organize into albums</strong> - Create albums to group
              photos for your slideshow.
            </li>
            <li>
              <strong>Configure the display</strong> - Set up transition
              effects, timing, and overlay options.
            </li>
            <li>
              <strong>Enjoy!</strong> - Open the display URL on your picture
              frame device.
            </li>
          </ol>
        </mat-card-content>
        <mat-card-actions>
          <button mat-raised-button color="primary" routerLink="/photos">
            <mat-icon>upload</mat-icon>
            Start Uploading
          </button>
        </mat-card-actions>
      </mat-card>
      } }
    </div>
  `,
  styles: [
    `
      .dashboard-container {
        padding: 24px;
      }

      h1 {
        margin-bottom: 24px;
      }

      .loading {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .stats-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
        gap: 16px;
        margin-bottom: 32px;
      }

      .stat-card {
        text-align: center;
      }

      .icon-wrapper {
        width: 56px;
        height: 56px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        margin: 0 auto;
      }

      .icon-wrapper mat-icon {
        font-size: 28px;
        width: 28px;
        height: 28px;
        color: white;
      }

      .photos-icon {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      }
      .albums-icon {
        background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
      }
      .tags-icon {
        background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
      }
      .storage-icon {
        background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
      }

      .stat-value {
        font-size: 2.5rem;
        font-weight: 500;
        margin: 16px 0 4px;
      }

      .stat-label {
        color: rgba(0, 0, 0, 0.6);
        margin: 0;
      }

      .quick-actions {
        margin-bottom: 32px;
      }

      .quick-actions h2 {
        font-size: 18px;
        margin-bottom: 16px;
      }

      .system-section {
        margin-bottom: 32px;
      }

      .system-section h2 {
        font-size: 18px;
        margin-bottom: 16px;
      }

      .actions-grid {
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
      }

      .getting-started {
        max-width: 600px;
      }

      .getting-started ol {
        padding-left: 20px;
        margin: 16px 0;
      }

      .getting-started li {
        margin: 12px 0;
        line-height: 1.5;
      }

      mat-card-actions {
        padding: 8px 16px 16px;
      }
    `,
  ],
})
export class DashboardComponent implements OnInit {
  private readonly photoService = inject(PhotoService);
  private readonly albumService = inject(AlbumService);
  private readonly tagService = inject(TagService);
  private readonly storageService = inject(StorageService);

  isLoading = signal(true);
  stats = signal<DashboardStats>({
    photoCount: 0,
    albumCount: 0,
    tagCount: 0,
    providerCount: 0,
  });

  ngOnInit(): void {
    this.loadStats();
  }

  private loadStats(): void {
    this.isLoading.set(true);

    forkJoin({
      photos: this.photoService.getPhotoCount(),
      albums: this.albumService.getAlbums(),
      tags: this.tagService.getTags(),
      providers: this.storageService.getProviders(),
    }).subscribe({
      next: (results) => {
        this.stats.set({
          photoCount: results.photos.count,
          albumCount: results.albums.length,
          tagCount: results.tags.length,
          providerCount: results.providers.length,
        });
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("Failed to load dashboard stats:", error);
        this.isLoading.set(false);
      },
    });
  }
}
