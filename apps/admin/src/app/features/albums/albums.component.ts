import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatMenuModule } from "@angular/material/menu";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { RouterLink } from "@angular/router";
import { AlbumService } from "../../core/services/album.service";
import { AlbumDto } from "../../core/models";
import { CreateAlbumDialogComponent } from "./create-album-dialog.component";

@Component({
  selector: "app-albums",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="albums-container">
      <div class="header">
        <h1>Albums</h1>
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon>
          Create Album
        </button>
      </div>

      @if (isLoading()) {
      <div class="loading">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
      } @else if (albums().length === 0) {
      <mat-card class="empty-state">
        <mat-card-content>
          <mat-icon>collections</mat-icon>
          <h2>No albums yet</h2>
          <p>Create albums to organize your photos for the slideshow.</p>
          <button
            mat-raised-button
            color="primary"
            (click)="openCreateDialog()"
          >
            <mat-icon>add</mat-icon>
            Create Album
          </button>
        </mat-card-content>
      </mat-card>
      } @else {
      <div class="albums-grid">
        @for (album of albums(); track album.id) {
        <mat-card class="album-card">
          <div
            class="album-cover"
            [style.background-image]="getCoverImage(album)"
          >
            @if (!album.coverPhotoId) {
            <mat-icon>collections</mat-icon>
            }
          </div>
          <mat-card-content>
            <h3>{{ album.name }}</h3>
            @if (album.description) {
            <p class="description">{{ album.description }}</p>
            }
            <p class="photo-count">
              {{ album.photoCount }} photo{{
                album.photoCount !== 1 ? "s" : ""
              }}
            </p>
          </mat-card-content>
          <mat-card-actions>
            <button
              mat-button
              routerLink="/photos"
              [queryParams]="{ albumId: album.id }"
            >
              View Photos
            </button>
            <button mat-icon-button [matMenuTriggerFor]="albumMenu">
              <mat-icon>more_vert</mat-icon>
            </button>
            <mat-menu #albumMenu="matMenu">
              <button mat-menu-item (click)="openEditDialog(album)">
                <mat-icon>edit</mat-icon>
                Edit
              </button>
              <button
                mat-menu-item
                (click)="deleteAlbum(album)"
                class="delete-action"
              >
                <mat-icon>delete</mat-icon>
                Delete
              </button>
            </mat-menu>
          </mat-card-actions>
        </mat-card>
        }
      </div>
      }
    </div>
  `,
  styles: [
    `
      .albums-container {
        padding: 24px;
      }

      .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .loading {
        display: flex;
        justify-content: center;
        padding: 48px;
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

      .albums-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
        gap: 24px;
      }

      .album-card {
        overflow: hidden;
      }

      .album-cover {
        height: 160px;
        background-color: #f5f5f5;
        background-size: cover;
        background-position: center;
        display: flex;
        align-items: center;
        justify-content: center;
      }

      .album-cover mat-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        color: rgba(0, 0, 0, 0.2);
      }

      mat-card-content h3 {
        margin: 0 0 8px;
        font-size: 18px;
      }

      .description {
        color: rgba(0, 0, 0, 0.6);
        font-size: 14px;
        margin: 0 0 8px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .photo-count {
        color: rgba(0, 0, 0, 0.5);
        font-size: 13px;
        margin: 0;
      }

      mat-card-actions {
        display: flex;
        justify-content: space-between;
      }

      .delete-action {
        color: #f44336;
      }
    `,
  ],
})
export class AlbumsComponent implements OnInit {
  private readonly albumService = inject(AlbumService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  albums = signal<AlbumDto[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadAlbums();
  }

  loadAlbums(): void {
    this.isLoading.set(true);
    this.albumService.getAlbums().subscribe({
      next: (albums) => {
        this.albums.set(albums);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("Failed to load albums:", error);
        this.isLoading.set(false);
        this.snackBar.open("Failed to load albums", "Close", {
          duration: 3000,
        });
      },
    });
  }

  getCoverImage(album: AlbumDto): string {
    const url = this.albumService.getCoverThumbnailUrl(album, 'medium');
    if (url) {
      return `url(${url})`;
    }
    return "none";
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(CreateAlbumDialogComponent, {
      width: "400px",
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadAlbums();
      }
    });
  }

  openEditDialog(album: AlbumDto): void {
    const dialogRef = this.dialog.open(CreateAlbumDialogComponent, {
      width: "400px",
      data: { album },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadAlbums();
      }
    });
  }

  deleteAlbum(album: AlbumDto): void {
    if (
      confirm(
        `Delete album "${album.name}"? Photos in this album will not be deleted.`
      )
    ) {
      this.albumService.deleteAlbum(album.id).subscribe({
        next: () => {
          this.snackBar.open("Album deleted", "Close", { duration: 3000 });
          this.loadAlbums();
        },
        error: (error) => {
          console.error("Failed to delete album:", error);
          this.snackBar.open("Failed to delete album", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }
}
