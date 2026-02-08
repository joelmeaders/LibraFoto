import {
  Component,
  inject,
  signal,
  OnInit,
  ViewChild,
  ElementRef,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatMenuModule } from "@angular/material/menu";
import { MatChipsModule } from "@angular/material/chips";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatSelectModule } from "@angular/material/select";
import { MatFormFieldModule } from "@angular/material/form-field";
import { PhotoService } from "../../core/services/photo.service";
import { StorageService } from "../../core/services/storage.service";
import { PhotoListDto, MediaType } from "../../core/models";
import { PhotoDetailDialogComponent } from "./photo-detail-dialog.component";

interface UploadProgress {
  file: File;
  progress: number;
  status: "pending" | "uploading" | "success" | "error";
  error?: string;
}

@Component({
  selector: "app-photos",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatCheckboxModule,
    MatMenuModule,
    MatChipsModule,
    MatPaginatorModule,
    MatDialogModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatSelectModule,
    MatFormFieldModule,
  ],
  template: `
    <div class="photos-container">
      <!-- Header -->
      <div class="header">
        <h1>Photos</h1>
        <div class="header-actions">
          @if (selectedPhotos().length > 0) {
            <button mat-stroked-button color="warn" (click)="deleteSelected()">
              <mat-icon>delete</mat-icon>
              Delete ({{ selectedPhotos().length }})
            </button>
            <button mat-stroked-button [matMenuTriggerFor]="bulkMenu">
              <mat-icon>more_vert</mat-icon>
              More Actions
            </button>
            <mat-menu #bulkMenu="matMenu">
              <button mat-menu-item>
                <mat-icon>collections</mat-icon>
                Add to Album
              </button>
              <button mat-menu-item>
                <mat-icon>label</mat-icon>
                Add Tags
              </button>
              <button mat-menu-item (click)="regenerateThumbnails()">
                <mat-icon>refresh</mat-icon>
                Regenerate Thumbnails
              </button>
            </mat-menu>
          }
          <button mat-raised-button color="primary" (click)="fileInput.click()">
            <mat-icon>upload</mat-icon>
            Upload Photos
          </button>
          <input
            #fileInput
            type="file"
            hidden
            multiple
            accept="image/*,video/*"
            (change)="onFilesSelected($event)"
          />
        </div>
      </div>

      <!-- Upload Progress -->
      @if (uploadQueue().length > 0) {
        <mat-card class="upload-card">
          <mat-card-header>
            <mat-card-title
              >Uploading {{ uploadQueue().length }} file(s)</mat-card-title
            >
          </mat-card-header>
          <mat-card-content>
            @for (upload of uploadQueue(); track upload.file.name) {
              <div class="upload-item">
                <div class="upload-info">
                  <span class="filename">{{ upload.file.name }}</span>
                  <span class="status" [class]="upload.status">
                    @switch (upload.status) {
                      @case ("pending") {
                        Waiting...
                      }
                      @case ("uploading") {
                        {{ upload.progress }}%
                      }
                      @case ("success") {
                        Complete
                      }
                      @case ("error") {
                        Failed: {{ upload.error }}
                      }
                    }
                  </span>
                </div>
                <mat-progress-bar
                  [mode]="
                    upload.status === 'uploading'
                      ? 'determinate'
                      : 'indeterminate'
                  "
                  [value]="upload.progress"
                  [color]="upload.status === 'error' ? 'warn' : 'primary'"
                >
                </mat-progress-bar>
              </div>
            }
          </mat-card-content>
        </mat-card>
      }

      <!-- Drop Zone -->
      <div
        class="drop-zone"
        [class.active]="isDragOver()"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)"
      >
        @if (isLoading()) {
          <div class="loading-container">
            <mat-spinner diameter="40"></mat-spinner>
            <p>Loading photos...</p>
          </div>
        } @else if (photos().length === 0) {
          <!-- Empty State -->
          <div class="empty-state">
            <mat-icon>photo_library</mat-icon>
            <h2>No photos yet</h2>
            <p>
              Drag and drop photos here, or click the upload button to get
              started.
            </p>
            <button
              mat-raised-button
              color="primary"
              (click)="fileInput.click()"
            >
              <mat-icon>upload</mat-icon>
              Upload Photos
            </button>
          </div>
        } @else {
          <!-- Photo Grid -->
          <div class="photo-grid">
            @for (photo of photos(); track photo.id) {
              <div
                class="photo-card"
                [class.selected]="isSelected(photo.id)"
                (click)="toggleSelection(photo.id)"
              >
                <div class="photo-thumbnail">
                  <img
                    [src]="getThumbnailUrl(photo)"
                    [alt]="photo.filename"
                    loading="lazy"
                  />
                  @if (photo.mediaType === MediaType.Video) {
                    <div class="video-indicator">
                      <mat-icon>videocam</mat-icon>
                    </div>
                  }
                  <div class="photo-overlay">
                    <mat-checkbox
                      [checked]="isSelected(photo.id)"
                      (click)="$event.stopPropagation()"
                      (change)="toggleSelection(photo.id)"
                    >
                    </mat-checkbox>
                    <button
                      mat-icon-button
                      (click)="openDetail(photo); $event.stopPropagation()"
                      matTooltip="View Details"
                    >
                      <mat-icon>fullscreen</mat-icon>
                    </button>
                  </div>
                </div>
                <div class="photo-info">
                  <span class="photo-name" [title]="photo.filename">{{
                    photo.filename
                  }}</span>
                  @if (photo.dateTaken) {
                    <span class="photo-date">{{
                      formatDate(photo.dateTaken)
                    }}</span>
                  }
                </div>
              </div>
            }
          </div>

          <!-- Pagination -->
          <mat-paginator
            [length]="totalPhotos()"
            [pageSize]="pageSize()"
            [pageIndex]="currentPage()"
            [pageSizeOptions]="[24, 48, 96]"
            (page)="onPageChange($event)"
            showFirstLastButtons
          >
          </mat-paginator>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .photos-container {
        padding: 24px;
      }

      @media (max-width: 600px) {
        .photos-container {
          padding: 12px;
        }
      }

      .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
        flex-wrap: wrap;
        gap: 16px;
      }

      .header-actions {
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
      }

      .upload-card {
        margin-bottom: 24px;
      }

      .upload-item {
        margin: 12px 0;
      }

      .upload-info {
        display: flex;
        justify-content: space-between;
        margin-bottom: 4px;
      }

      .filename {
        font-weight: 500;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        max-width: 300px;
      }

      .status {
        font-size: 12px;
      }

      .status.success {
        color: #4caf50;
      }
      .status.error {
        color: #f44336;
      }
      .status.uploading {
        color: #2196f3;
      }

      .drop-zone {
        min-height: 400px;
        border: 2px dashed transparent;
        border-radius: 8px;
        transition: all 0.2s;
      }

      .drop-zone.active {
        border-color: #667eea;
        background-color: rgba(102, 126, 234, 0.05);
      }

      .loading-container {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 48px;
        gap: 16px;
      }

      .empty-state {
        text-align: center;
        padding: 48px;
      }

      .empty-state mat-icon {
        font-size: 64px;
        width: 64px;
        height: 64px;
        color: rgba(0, 0, 0, 0.3);
      }

      .empty-state h2 {
        margin: 16px 0 8px;
      }

      .empty-state p {
        color: rgba(0, 0, 0, 0.6);
        margin-bottom: 24px;
      }

      .photo-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
        gap: 16px;
        margin-bottom: 24px;
      }

      @media (max-width: 600px) {
        .photo-grid {
          gap: 8px;
        }
      }

      .photo-card {
        background: white;
        border-radius: 8px;
        overflow: hidden;
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        cursor: pointer;
        transition: all 0.2s;
        position: relative;
      }

      .photo-card:hover {
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        transform: translateY(-2px);
      }

      .photo-card.selected {
        outline: 3px solid #667eea;
      }

      .photo-thumbnail {
        position: relative;
        aspect-ratio: 1;
        background: #f5f5f5;
      }

      .photo-thumbnail img {
        width: 100%;
        height: 100%;
        object-fit: cover;
      }

      .video-indicator {
        position: absolute;
        bottom: 8px;
        left: 8px;
        background: rgba(0, 0, 0, 0.7);
        color: white;
        border-radius: 4px;
        padding: 4px;
        display: flex;
        align-items: center;
      }

      .video-indicator mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }

      .photo-overlay {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        display: flex;
        justify-content: space-between;
        padding: 8px;
        background: linear-gradient(rgba(0, 0, 0, 0.4), transparent);
        opacity: 0;
        transition: opacity 0.2s;
      }

      .photo-card:hover .photo-overlay,
      .photo-card.selected .photo-overlay {
        opacity: 1;
      }

      @media (hover: none) {
        .photo-overlay {
          opacity: 1;
          background: linear-gradient(
            to bottom,
            rgba(0, 0, 0, 0.6) 0%,
            rgba(0, 0, 0, 0) 40%
          );
        }

        .video-indicator {
          padding: 2px;
          bottom: 4px;
          left: 4px;
        }

        .video-indicator mat-icon {
          font-size: 16px;
          height: 16px;
          width: 16px;
        }
      }

      .photo-info {
        padding: 8px 12px;
      }

      .photo-name {
        display: block;
        font-size: 13px;
        font-weight: 500;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .photo-date {
        display: block;
        font-size: 11px;
        color: rgba(0, 0, 0, 0.5);
        margin-top: 2px;
      }

      mat-paginator {
        background: transparent;
      }
    `,
  ],
})
export class PhotosComponent implements OnInit {
  @ViewChild("fileInput") fileInput!: ElementRef<HTMLInputElement>;

  private readonly photoService = inject(PhotoService);
  private readonly storageService = inject(StorageService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly MediaType = MediaType;

  photos = signal<PhotoListDto[]>([]);
  totalPhotos = signal(0);
  currentPage = signal(0);
  pageSize = signal(24);
  isLoading = signal(false);
  isDragOver = signal(false);
  selectedPhotos = signal<number[]>([]);
  uploadQueue = signal<UploadProgress[]>([]);

  ngOnInit(): void {
    this.loadPhotos();
  }

  loadPhotos(): void {
    this.isLoading.set(true);

    this.photoService
      .getPhotos({
        page: this.currentPage() + 1,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.photos.set(result.data);
          this.totalPhotos.set(result.pagination.totalItems);
          this.isLoading.set(false);
        },
        error: (error) => {
          console.error("Failed to load photos:", error);
          this.isLoading.set(false);
          this.snackBar.open("Failed to load photos", "Close", {
            duration: 3000,
          });
        },
      });
  }

  getThumbnailUrl(photo: PhotoListDto): string {
    return this.photoService.getThumbnailUrl(photo);
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  isSelected(id: number): boolean {
    return this.selectedPhotos().includes(id);
  }

  toggleSelection(id: number): void {
    const current = this.selectedPhotos();
    if (current.includes(id)) {
      this.selectedPhotos.set(current.filter((p) => p !== id));
    } else {
      this.selectedPhotos.set([...current, id]);
    }
  }

  deleteSelected(): void {
    const ids = this.selectedPhotos();
    if (ids.length === 0) return;

    if (confirm(`Delete ${ids.length} photo(s)? This cannot be undone.`)) {
      this.photoService.deletePhotos(ids).subscribe({
        next: () => {
          this.snackBar.open(`Deleted ${ids.length} photo(s)`, "Close", {
            duration: 3000,
          });
          this.selectedPhotos.set([]);
          this.loadPhotos();
        },
        error: (error) => {
          console.error("Failed to delete photos:", error);
          this.snackBar.open("Failed to delete photos", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }

  regenerateThumbnails(): void {
    const ids = this.selectedPhotos();
    if (ids.length === 0) return;

    this.snackBar.open(
      `Regenerating ${ids.length} thumbnail(s)...`,
      undefined,
      {
        duration: 0,
      },
    );

    this.photoService.refreshThumbnails(ids).subscribe({
      next: (result) => {
        this.snackBar.dismiss();
        if (result.failed > 0) {
          this.snackBar.open(
            `Regenerated ${result.succeeded} thumbnail(s), ${result.failed} failed`,
            "Close",
            { duration: 5000 },
          );
        } else {
          this.snackBar.open(
            `Regenerated ${result.succeeded} thumbnail(s)`,
            "Close",
            { duration: 3000 },
          );
        }
        this.selectedPhotos.set([]);
        this.loadPhotos();
      },
      error: (error) => {
        console.error("Failed to regenerate thumbnails:", error);
        this.snackBar.dismiss();
        this.snackBar.open("Failed to regenerate thumbnails", "Close", {
          duration: 3000,
        });
      },
    });
  }

  openDetail(photo: PhotoListDto): void {
    this.dialog.open(PhotoDetailDialogComponent, {
      data: { photoId: photo.id },
      width: "900px",
      maxWidth: "95vw",
      maxHeight: "95vh",
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadPhotos();
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.uploadFiles(Array.from(input.files));
      input.value = "";
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);

    if (event.dataTransfer?.files) {
      const files = Array.from(event.dataTransfer.files).filter(
        (file) =>
          file.type.startsWith("image/") || file.type.startsWith("video/"),
      );
      if (files.length > 0) {
        this.uploadFiles(files);
      }
    }
  }

  private async uploadFiles(files: File[]): Promise<void> {
    const queue: UploadProgress[] = files.map((file) => ({
      file,
      progress: 0,
      status: "pending" as const,
    }));

    this.uploadQueue.set(queue);

    for (let i = 0; i < queue.length; i++) {
      queue[i].status = "uploading";
      this.uploadQueue.set([...queue]);

      try {
        await this.uploadFile(queue[i]);
        queue[i].status = "success";
        queue[i].progress = 100;
      } catch (error) {
        queue[i].status = "error";
        queue[i].error =
          error instanceof Error ? error.message : "Upload failed";
      }

      this.uploadQueue.set([...queue]);
    }

    // Reload photos after upload
    setTimeout(() => {
      this.uploadQueue.set([]);
      this.loadPhotos();
    }, 2000);
  }

  private uploadFile(upload: UploadProgress): Promise<void> {
    return new Promise((resolve, reject) => {
      this.storageService.uploadFile(upload.file).subscribe({
        next: () => resolve(),
        error: (error) => reject(error),
      });
    });
  }
}
