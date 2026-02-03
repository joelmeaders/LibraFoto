import { Component, inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from "@angular/forms";
import {
  MatDialogModule,
  MatDialogRef,
  MAT_DIALOG_DATA,
} from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { AlbumService } from "../../core/services/album.service";
import { AlbumDto } from "../../core/models";

interface DialogData {
  album?: AlbumDto;
}

@Component({
  selector: "app-create-album-dialog",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEditMode ? "Edit Album" : "Create Album" }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Album Name</mat-label>
          <input
            matInput
            formControlName="name"
            placeholder="Enter album name"
          />
          @if (form.get('name')?.hasError('required') &&
          form.get('name')?.touched) {
          <mat-error>Album name is required</mat-error>
          } @if (form.get('name')?.hasError('maxlength')) {
          <mat-error>Name cannot exceed 100 characters</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description (optional)</mat-label>
          <textarea
            matInput
            formControlName="description"
            placeholder="Enter description"
            rows="3"
          ></textarea>
          @if (form.get('description')?.hasError('maxlength')) {
          <mat-error>Description cannot exceed 500 characters</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="isSaving">Cancel</button>
      <button
        mat-raised-button
        color="primary"
        (click)="save()"
        [disabled]="form.invalid || isSaving"
      >
        @if (isSaving) {
        <mat-spinner diameter="20"></mat-spinner>
        } @else {
        {{ isEditMode ? "Save" : "Create" }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        min-width: 350px;
      }

      .full-width {
        width: 100%;
        margin-bottom: 8px;
      }

      mat-dialog-actions button mat-spinner {
        display: inline-block;
      }
    `,
  ],
})
export class CreateAlbumDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<CreateAlbumDialogComponent>);
  private readonly albumService = inject(AlbumService);
  private readonly data = inject<DialogData | null>(MAT_DIALOG_DATA, {
    optional: true,
  });

  form!: FormGroup;
  isSaving = false;
  isEditMode = false;

  ngOnInit(): void {
    this.isEditMode = !!this.data?.album;

    this.form = this.fb.group({
      name: [
        this.data?.album?.name ?? "",
        [Validators.required, Validators.maxLength(100)],
      ],
      description: [
        this.data?.album?.description ?? "",
        [Validators.maxLength(500)],
      ],
    });
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving = true;
    const { name, description } = this.form.value;

    if (this.isEditMode && this.data?.album) {
      this.albumService
        .updateAlbum(this.data.album.id, { name, description })
        .subscribe({
          next: () => {
            this.dialogRef.close(true);
          },
          error: (error) => {
            console.error("Failed to update album:", error);
            this.isSaving = false;
          },
        });
    } else {
      this.albumService.createAlbum({ name, description }).subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error("Failed to create album:", error);
          this.isSaving = false;
        },
      });
    }
  }
}
