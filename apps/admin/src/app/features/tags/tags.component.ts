import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatChipsModule } from "@angular/material/chips";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatMenuModule } from "@angular/material/menu";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { RouterLink } from "@angular/router";
import { TagService } from "../../core/services/tag.service";
import { TagDto } from "../../core/models";
import { CreateTagDialogComponent } from "./create-tag-dialog.component";

@Component({
  selector: "app-tags",
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDialogModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="tags-container">
      <div class="header">
        <h1>Tags</h1>
        <button mat-raised-button color="primary" (click)="openCreateDialog()">
          <mat-icon>add</mat-icon>
          Create Tag
        </button>
      </div>

      @if (isLoading()) {
      <div class="loading">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
      } @else if (tags().length === 0) {
      <mat-card class="empty-state">
        <mat-card-content>
          <mat-icon>label</mat-icon>
          <h2>No tags yet</h2>
          <p>Create tags to categorize your photos.</p>
          <button
            mat-raised-button
            color="primary"
            (click)="openCreateDialog()"
          >
            <mat-icon>add</mat-icon>
            Create Tag
          </button>
        </mat-card-content>
      </mat-card>
      } @else {
      <mat-card>
        <mat-card-content>
          <div class="tags-list">
            @for (tag of tags(); track tag.id) {
            <div class="tag-item">
              <div class="tag-info">
                <span
                  class="tag-color"
                  [style.background-color]="tag.color || '#9e9e9e'"
                ></span>
                <span class="tag-name">{{ tag.name }}</span>
                <span class="tag-count"
                  >{{ tag.photoCount }} photo{{
                    tag.photoCount !== 1 ? "s" : ""
                  }}</span
                >
              </div>
              <div class="tag-actions">
                <button
                  mat-button
                  routerLink="/photos"
                  [queryParams]="{ tagId: tag.id }"
                >
                  View Photos
                </button>
                <button mat-icon-button [matMenuTriggerFor]="tagMenu">
                  <mat-icon>more_vert</mat-icon>
                </button>
                <mat-menu #tagMenu="matMenu">
                  <button mat-menu-item (click)="openEditDialog(tag)">
                    <mat-icon>edit</mat-icon>
                    Edit
                  </button>
                  <button
                    mat-menu-item
                    (click)="deleteTag(tag)"
                    class="delete-action"
                  >
                    <mat-icon>delete</mat-icon>
                    Delete
                  </button>
                </mat-menu>
              </div>
            </div>
            }
          </div>
        </mat-card-content>
      </mat-card>

      <div class="tag-chips">
        <h3>Quick View</h3>
        <div class="chips-container">
          @for (tag of tags(); track tag.id) {
          <a
            routerLink="/photos"
            [queryParams]="{ tagId: tag.id }"
            class="tag-chip"
            [style.background-color]="tag.color || '#9e9e9e'"
          >
            {{ tag.name }}
            <span class="chip-count">{{ tag.photoCount }}</span>
          </a>
          }
        </div>
      </div>
      }
    </div>
  `,
  styles: [
    `
      .tags-container {
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

      .tags-list {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .tag-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 12px 16px;
        border-radius: 8px;
        transition: background-color 0.2s;
      }

      .tag-item:hover {
        background-color: rgba(0, 0, 0, 0.04);
      }

      .tag-info {
        display: flex;
        align-items: center;
        gap: 12px;
      }

      .tag-color {
        width: 16px;
        height: 16px;
        border-radius: 50%;
        flex-shrink: 0;
      }

      .tag-name {
        font-weight: 500;
      }

      .tag-count {
        color: rgba(0, 0, 0, 0.5);
        font-size: 13px;
      }

      .tag-actions {
        display: flex;
        align-items: center;
        gap: 4px;
      }

      .delete-action {
        color: #f44336;
      }

      .tag-chips {
        margin-top: 32px;
      }

      .tag-chips h3 {
        margin-bottom: 16px;
        color: rgba(0, 0, 0, 0.7);
      }

      .chips-container {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }

      .tag-chip {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        padding: 6px 12px;
        border-radius: 16px;
        color: white;
        text-decoration: none;
        font-size: 14px;
        transition: opacity 0.2s, transform 0.2s;
      }

      .tag-chip:hover {
        opacity: 0.9;
        transform: scale(1.02);
      }

      .chip-count {
        background-color: rgba(255, 255, 255, 0.3);
        padding: 2px 8px;
        border-radius: 10px;
        font-size: 12px;
      }
    `,
  ],
})
export class TagsComponent implements OnInit {
  private readonly tagService = inject(TagService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  tags = signal<TagDto[]>([]);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadTags();
  }

  loadTags(): void {
    this.isLoading.set(true);
    this.tagService.getTags().subscribe({
      next: (tags) => {
        this.tags.set(tags);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("Failed to load tags:", error);
        this.isLoading.set(false);
        this.snackBar.open("Failed to load tags", "Close", { duration: 3000 });
      },
    });
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(CreateTagDialogComponent, {
      width: "400px",
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadTags();
      }
    });
  }

  openEditDialog(tag: TagDto): void {
    const dialogRef = this.dialog.open(CreateTagDialogComponent, {
      width: "400px",
      data: { tag },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadTags();
      }
    });
  }

  deleteTag(tag: TagDto): void {
    if (confirm(`Delete tag "${tag.name}"? Photos will not be deleted.`)) {
      this.tagService.deleteTag(tag.id).subscribe({
        next: () => {
          this.snackBar.open("Tag deleted", "Close", { duration: 3000 });
          this.loadTags();
        },
        error: (error) => {
          console.error("Failed to delete tag:", error);
          this.snackBar.open("Failed to delete tag", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }
}
