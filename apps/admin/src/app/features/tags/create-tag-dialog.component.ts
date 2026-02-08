import { Component, inject, OnInit, signal } from "@angular/core";
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
import { TagService } from "../../core/services/tag.service";
import { TagDto } from "../../core/models";

interface DialogData {
  tag?: TagDto;
}

const PRESET_COLORS = [
  "#f44336", // Red
  "#e91e63", // Pink
  "#9c27b0", // Purple
  "#673ab7", // Deep Purple
  "#3f51b5", // Indigo
  "#2196f3", // Blue
  "#03a9f4", // Light Blue
  "#00bcd4", // Cyan
  "#009688", // Teal
  "#4caf50", // Green
  "#8bc34a", // Light Green
  "#cddc39", // Lime
  "#ffeb3b", // Yellow
  "#ffc107", // Amber
  "#ff9800", // Orange
  "#ff5722", // Deep Orange
  "#795548", // Brown
  "#9e9e9e", // Grey
  "#607d8b", // Blue Grey
];

@Component({
  selector: "app-create-tag-dialog",
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
    <h2 mat-dialog-title>{{ isEditMode ? "Edit Tag" : "Create Tag" }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Tag Name</mat-label>
          <input matInput formControlName="name" placeholder="Enter tag name" />
          @if (form.get('name')?.hasError('required') &&
          form.get('name')?.touched) {
          <mat-error>Tag name is required</mat-error>
          } @if (form.get('name')?.hasError('maxlength')) {
          <mat-error>Name cannot exceed 50 characters</mat-error>
          }
        </mat-form-field>

        <div class="color-section">
          <label>Color</label>
          <div class="color-preview">
            <span
              class="preview-dot"
              [style.background-color]="selectedColor()"
            ></span>
            <span>{{ selectedColor() }}</span>
          </div>
          <div class="color-grid">
            @for (color of presetColors; track color) {
            <button
              type="button"
              class="color-button"
              [class.selected]="selectedColor() === color"
              [style.background-color]="color"
              (click)="selectColor(color)"
            ></button>
            }
          </div>
          <mat-form-field appearance="outline" class="full-width custom-color">
            <mat-label>Custom Color (hex)</mat-label>
            <input matInput formControlName="color" placeholder="#RRGGBB" />
          </mat-form-field>
        </div>
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

      .color-section {
        margin-top: 16px;
      }

      .color-section label {
        display: block;
        font-size: 14px;
        color: rgba(0, 0, 0, 0.6);
        margin-bottom: 8px;
      }

      .color-preview {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 12px;
      }

      .preview-dot {
        width: 24px;
        height: 24px;
        border-radius: 50%;
        border: 2px solid white;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
      }

      .color-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(32px, 1fr));
        gap: 8px;
        margin-bottom: 16px;
      }

      .color-button {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        border: 2px solid transparent;
        cursor: pointer;
        transition: transform 0.2s, border-color 0.2s;
      }

      .color-button:hover {
        transform: scale(1.1);
      }

      .color-button.selected {
        border-color: #333;
        transform: scale(1.1);
      }

      .custom-color {
        margin-top: 8px;
      }

      mat-dialog-actions button mat-spinner {
        display: inline-block;
      }
    `,
  ],
})
export class CreateTagDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<CreateTagDialogComponent>);
  private readonly tagService = inject(TagService);
  private readonly data = inject<DialogData | null>(MAT_DIALOG_DATA, {
    optional: true,
  });

  form!: FormGroup;
  isSaving = false;
  isEditMode = false;
  selectedColor = signal("#2196f3");
  presetColors = PRESET_COLORS;

  ngOnInit(): void {
    this.isEditMode = !!this.data?.tag;

    const initialColor = this.data?.tag?.color ?? "#2196f3";
    this.selectedColor.set(initialColor);

    this.form = this.fb.group({
      name: [
        this.data?.tag?.name ?? "",
        [Validators.required, Validators.maxLength(50)],
      ],
      color: [initialColor, [Validators.pattern(/^#[0-9A-Fa-f]{6}$/)]],
    });

    this.form.get("color")?.valueChanges.subscribe((value) => {
      if (value && /^#[0-9A-Fa-f]{6}$/.test(value)) {
        this.selectedColor.set(value);
      }
    });
  }

  selectColor(color: string): void {
    this.selectedColor.set(color);
    this.form.patchValue({ color });
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving = true;
    const { name, color } = this.form.value;

    if (this.isEditMode && this.data?.tag) {
      this.tagService.updateTag(this.data.tag.id, { name, color }).subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error("Failed to update tag:", error);
          this.isSaving = false;
        },
      });
    } else {
      this.tagService.createTag({ name, color }).subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error("Failed to create tag:", error);
          this.isSaving = false;
        },
      });
    }
  }
}
