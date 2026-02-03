import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTabsModule } from "@angular/material/tabs";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatChipsModule } from "@angular/material/chips";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { PhotoService } from "../../core/services/photo.service";
import { PhotoDetailDto, MediaType } from "../../core/models";

@Component({
  selector: "app-photo-detail-dialog",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="dialog-header">
      <h2 mat-dialog-title>Photo Details</h2>
      <button mat-icon-button mat-dialog-close>
        <mat-icon>close</mat-icon>
      </button>
    </div>

    <mat-dialog-content>
      @if (isLoading()) {
      <div class="loading">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
      } @else if (photo()) {
      <div class="content-layout">
        <!-- Photo Preview -->
        <div class="photo-preview">
          @if (photo()!.mediaType === MediaType.Video) {
          <video [src]="getPhotoUrl()" controls></video>
          } @else {
          <img [src]="getPhotoUrl()" [alt]="photo()!.filename" />
          }
        </div>

        <!-- Details Panel -->
        <div class="details-panel">
          <mat-tab-group>
            <!-- Info Tab -->
            <mat-tab label="Info">
              <div class="tab-content">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Filename</mat-label>
                  <input
                    matInput
                    [(ngModel)]="editedFilename"
                    [disabled]="!isEditing()"
                  />
                </mat-form-field>

                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Location</mat-label>
                  <input
                    matInput
                    [(ngModel)]="editedLocation"
                    [disabled]="!isEditing()"
                  />
                  <mat-icon matSuffix>location_on</mat-icon>
                </mat-form-field>

                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Date Taken</mat-label>
                  <input
                    matInput
                    type="date"
                    [(ngModel)]="editedDateTaken"
                    [disabled]="!isEditing()"
                  />
                </mat-form-field>

                @if (isEditing()) {
                <div class="edit-actions">
                  <button mat-button (click)="cancelEdit()">Cancel</button>
                  <button
                    mat-raised-button
                    color="primary"
                    (click)="saveChanges()"
                  >
                    Save Changes
                  </button>
                </div>
                } @else {
                <button mat-stroked-button (click)="startEdit()">
                  <mat-icon>edit</mat-icon>
                  Edit Details
                </button>
                }
              </div>
            </mat-tab>

            <!-- Metadata Tab -->
            <mat-tab label="Metadata">
              <div class="tab-content">
                <div class="metadata-grid">
                  <div class="metadata-item">
                    <span class="label">Dimensions</span>
                    <span class="value"
                      >{{ photo()!.width }} × {{ photo()!.height }}</span
                    >
                  </div>
                  <div class="metadata-item">
                    <span class="label">File Size</span>
                    <span class="value">{{
                      formatFileSize(photo()!.fileSize)
                    }}</span>
                  </div>
                  <div class="metadata-item">
                    <span class="label">Type</span>
                    <span class="value">{{
                      photo()!.mediaType === MediaType.Video ? "Video" : "Photo"
                    }}</span>
                  </div>
                  @if (photo()!.duration) {
                  <div class="metadata-item">
                    <span class="label">Duration</span>
                    <span class="value">{{
                      formatDuration(photo()!.duration!)
                    }}</span>
                  </div>
                  }
                  <div class="metadata-item">
                    <span class="label">Date Added</span>
                    <span class="value">{{
                      formatDate(photo()!.dateAdded)
                    }}</span>
                  </div>
                  @if (photo()!.latitude && photo()!.longitude) {
                  <div class="metadata-item">
                    <span class="label">GPS</span>
                    <span class="value"
                      >{{ photo()!.latitude }}, {{ photo()!.longitude }}</span
                    >
                  </div>
                  } @if (photo()!.providerName) {
                  <div class="metadata-item">
                    <span class="label">Source</span>
                    <span class="value">{{ photo()!.providerName }}</span>
                  </div>
                  }
                </div>
              </div>
            </mat-tab>

            <!-- Albums & Tags Tab -->
            <mat-tab label="Organization">
              <div class="tab-content">
                <h4>Albums</h4>
                @if (photo()!.albums.length > 0) {
                <mat-chip-set>
                  @for (album of photo()!.albums; track album.id) {
                  <mat-chip>
                    <mat-icon matChipAvatar>collections</mat-icon>
                    {{ album.name }}
                  </mat-chip>
                  }
                </mat-chip-set>
                } @else {
                <p class="empty-text">Not in any albums</p>
                }

                <h4>Tags</h4>
                @if (photo()!.tags.length > 0) {
                <mat-chip-set>
                  @for (tag of photo()!.tags; track tag.id) {
                  <mat-chip [style.background-color]="tag.color || '#e0e0e0'">
                    {{ tag.name }}
                  </mat-chip>
                  }
                </mat-chip-set>
                } @else {
                <p class="empty-text">No tags</p>
                }
              </div>
            </mat-tab>
          </mat-tab-group>
        </div>
      </div>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button color="warn" (click)="deletePhoto()">
        <mat-icon>delete</mat-icon>
        Delete
      </button>
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .dialog-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 16px 24px 0;
      }

      .dialog-header h2 {
        margin: 0;
      }

      mat-dialog-content {
        min-height: 400px;
      }

      .loading {
        display: flex;
        justify-content: center;
        align-items: center;
        height: 300px;
      }

      .content-layout {
        display: grid;
        grid-template-columns: 1fr 350px;
        gap: 24px;
      }

      @media (max-width: 768px) {
        .content-layout {
          grid-template-columns: 1fr;
        }
      }

      .photo-preview {
        background: #000;
        border-radius: 8px;
        overflow: hidden;
        display: flex;
        align-items: center;
        justify-content: center;
        max-height: 500px;
      }

      .photo-preview img,
      .photo-preview video {
        max-width: 100%;
        max-height: 500px;
        object-fit: contain;
      }

      .details-panel {
        min-width: 300px;
      }

      .tab-content {
        padding: 16px 0;
      }

      .full-width {
        width: 100%;
      }

      mat-form-field {
        margin-bottom: 8px;
      }

      .edit-actions {
        display: flex;
        gap: 8px;
        margin-top: 16px;
      }

      .metadata-grid {
        display: grid;
        gap: 12px;
      }

      .metadata-item {
        display: flex;
        flex-direction: column;
        padding: 8px 0;
        border-bottom: 1px solid rgba(0, 0, 0, 0.1);
      }

      .metadata-item .label {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.5);
        margin-bottom: 4px;
      }

      .metadata-item .value {
        font-weight: 500;
      }

      h4 {
        margin: 16px 0 8px;
        font-size: 14px;
        color: rgba(0, 0, 0, 0.6);
      }

      h4:first-child {
        margin-top: 0;
      }

      .empty-text {
        color: rgba(0, 0, 0, 0.5);
        font-style: italic;
        margin: 8px 0;
      }

      mat-chip-set {
        margin: 8px 0;
      }
    `,
  ],
})
export class PhotoDetailDialogComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<PhotoDetailDialogComponent>);
  private readonly data = inject<{ photoId: number }>(MAT_DIALOG_DATA);
  private readonly photoService = inject(PhotoService);
  private readonly snackBar = inject(MatSnackBar);

  readonly MediaType = MediaType;

  photo = signal<PhotoDetailDto | null>(null);
  isLoading = signal(true);
  isEditing = signal(false);

  editedFilename = "";
  editedLocation = "";
  editedDateTaken = "";

  ngOnInit(): void {
    this.loadPhoto();
  }

  private loadPhoto(): void {
    this.isLoading.set(true);
    this.photoService.getPhoto(this.data.photoId).subscribe({
      next: (photo) => {
        this.photo.set(photo);
        this.resetEditFields(photo);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("Failed to load photo:", error);
        this.isLoading.set(false);
        this.snackBar.open("Failed to load photo details", "Close", {
          duration: 3000,
        });
        this.dialogRef.close();
      },
    });
  }

  getPhotoUrl(): string {
    const p = this.photo();
    return p ? this.photoService.getPhotoUrl(p) : "";
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
    if (bytes < 1024 * 1024 * 1024)
      return (bytes / (1024 * 1024)).toFixed(1) + " MB";
    return (bytes / (1024 * 1024 * 1024)).toFixed(1) + " GB";
  }

  formatDuration(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, "0")}`;
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleString();
  }

  startEdit(): void {
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
    const p = this.photo();
    if (p) {
      this.resetEditFields(p);
    }
  }

  private resetEditFields(photo: PhotoDetailDto): void {
    this.editedFilename = photo.filename;
    this.editedLocation = photo.location || "";
    this.editedDateTaken = photo.dateTaken ? photo.dateTaken.split("T")[0] : "";
  }

  saveChanges(): void {
    const p = this.photo();
    if (!p) return;

    this.photoService
      .updatePhoto(p.id, {
        filename: this.editedFilename || null,
        location: this.editedLocation || null,
        dateTaken: this.editedDateTaken
          ? new Date(this.editedDateTaken).toISOString()
          : null,
      })
      .subscribe({
        next: (updated) => {
          this.photo.set(updated);
          this.isEditing.set(false);
          this.snackBar.open("Photo updated", "Close", { duration: 3000 });
        },
        error: (error) => {
          console.error("Failed to update photo:", error);
          this.snackBar.open("Failed to update photo", "Close", {
            duration: 3000,
          });
        },
      });
  }

  deletePhoto(): void {
    const p = this.photo();
    if (!p) return;

    if (confirm("Delete this photo? This cannot be undone.")) {
      this.photoService.deletePhoto(p.id).subscribe({
        next: () => {
          this.snackBar.open("Photo deleted", "Close", { duration: 3000 });
          this.dialogRef.close({ deleted: true });
        },
        error: (error) => {
          console.error("Failed to delete photo:", error);
          this.snackBar.open("Failed to delete photo", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }
}
