import { Component, inject, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from "@angular/forms";
import { MatDialogModule, MatDialogRef } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSelectModule } from "@angular/material/select";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatNativeDateModule } from "@angular/material/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { AuthService } from "../../core/services/auth.service";
import { AlbumService } from "../../core/services/album.service";
import { AlbumDto } from "../../core/models";

@Component({
  selector: "app-create-guest-link-dialog",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <h2 mat-dialog-title>Create Guest Link</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Link Name</mat-label>
          <input
            matInput
            formControlName="name"
            placeholder="e.g., Family Photos Upload"
          />
          <mat-hint>A descriptive name to identify this link</mat-hint>
          @if (form.get('name')?.hasError('required') &&
          form.get('name')?.touched) {
          <mat-error>Name is required</mat-error>
          } @if (form.get('name')?.hasError('maxlength')) {
          <mat-error>Name cannot exceed 100 characters</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Target Album (optional)</mat-label>
          <mat-select formControlName="targetAlbumId">
            <mat-option [value]="null">No specific album</mat-option>
            @for (album of albums(); track album.id) {
            <mat-option [value]="album.id">{{ album.name }}</mat-option>
            }
          </mat-select>
          <mat-hint
            >Photos uploaded via this link will be added to this album</mat-hint
          >
        </mat-form-field>

        <div class="toggle-row">
          <mat-slide-toggle
            formControlName="hasExpiry"
            (change)="onExpiryToggle()"
          >
            Set expiration date
          </mat-slide-toggle>
        </div>

        @if (form.get('hasExpiry')?.value) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Expires On</mat-label>
          <input
            matInput
            [matDatepicker]="picker"
            formControlName="expiresAt"
            [min]="minDate"
          />
          <mat-datepicker-toggle
            matIconSuffix
            [for]="picker"
          ></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>
        }

        <div class="toggle-row">
          <mat-slide-toggle
            formControlName="hasMaxUploads"
            (change)="onMaxUploadsToggle()"
          >
            Limit number of uploads
          </mat-slide-toggle>
        </div>

        @if (form.get('hasMaxUploads')?.value) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Maximum Uploads</mat-label>
          <input
            matInput
            type="number"
            formControlName="maxUploads"
            min="1"
            max="1000"
          />
          @if (form.get('maxUploads')?.hasError('min')) {
          <mat-error>Must be at least 1</mat-error>
          } @if (form.get('maxUploads')?.hasError('max')) {
          <mat-error>Cannot exceed 1000</mat-error>
          }
        </mat-form-field>
        }
      </form>

      @if (createdLink()) {
      <div class="success-message">
        <mat-icon>check_circle</mat-icon>
        <p>Guest link created successfully!</p>
        <div class="link-display">
          <code>{{ getGuestLinkUrl(createdLink()!) }}</code>
          <button mat-icon-button (click)="copyLink()">
            <mat-icon>content_copy</mat-icon>
          </button>
        </div>
      </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (createdLink()) {
      <button mat-button (click)="copyLink()">
        <mat-icon>content_copy</mat-icon>
        Copy Link
      </button>
      <button mat-raised-button color="primary" mat-dialog-close>Done</button>
      } @else {
      <button mat-button mat-dialog-close [disabled]="isSaving()">
        Cancel
      </button>
      <button
        mat-raised-button
        color="primary"
        (click)="save()"
        [disabled]="form.invalid || isSaving()"
      >
        @if (isSaving()) {
        <mat-spinner diameter="20"></mat-spinner>
        } @else { Create }
      </button>
      }
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        min-width: 400px;
      }

      .full-width {
        width: 100%;
        margin-bottom: 8px;
      }

      .toggle-row {
        margin: 16px 0;
      }

      .success-message {
        background-color: #e8f5e9;
        border-radius: 8px;
        padding: 16px;
        text-align: center;
        margin-top: 16px;
      }

      .success-message mat-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        color: #4caf50;
      }

      .success-message p {
        color: #2e7d32;
        font-weight: 500;
        margin: 8px 0 16px;
      }

      .link-display {
        display: flex;
        align-items: center;
        gap: 8px;
        background-color: white;
        padding: 8px 12px;
        border-radius: 4px;
        border: 1px solid #c8e6c9;
      }

      .link-display code {
        flex: 1;
        font-size: 12px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      mat-dialog-actions button mat-spinner {
        display: inline-block;
      }
    `,
  ],
})
export class CreateGuestLinkDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(
    MatDialogRef<CreateGuestLinkDialogComponent>
  );
  private readonly authService = inject(AuthService);
  private readonly albumService = inject(AlbumService);
  private readonly snackBar = inject(MatSnackBar);

  form!: FormGroup;
  albums = signal<AlbumDto[]>([]);
  isSaving = signal(false);
  createdLink = signal<string | null>(null);
  minDate = new Date();

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ["", [Validators.required, Validators.maxLength(100)]],
      targetAlbumId: [null],
      hasExpiry: [false],
      expiresAt: [null],
      hasMaxUploads: [false],
      maxUploads: [10, [Validators.min(1), Validators.max(1000)]],
    });

    this.loadAlbums();
  }

  private loadAlbums(): void {
    this.albumService.getAlbums().subscribe({
      next: (albums) => this.albums.set(albums),
      error: (error) => console.error("Failed to load albums:", error),
    });
  }

  onExpiryToggle(): void {
    if (!this.form.get("hasExpiry")?.value) {
      this.form.patchValue({ expiresAt: null });
    } else {
      // Default to 7 days from now
      const defaultExpiry = new Date();
      defaultExpiry.setDate(defaultExpiry.getDate() + 7);
      this.form.patchValue({ expiresAt: defaultExpiry });
    }
  }

  onMaxUploadsToggle(): void {
    if (!this.form.get("hasMaxUploads")?.value) {
      this.form.patchValue({ maxUploads: null });
    } else {
      this.form.patchValue({ maxUploads: 10 });
    }
  }

  getGuestLinkUrl(linkCode: string): string {
    return `${window.location.origin}/guest/${linkCode}`;
  }

  copyLink(): void {
    const linkCode = this.createdLink();
    if (linkCode) {
      const url = this.getGuestLinkUrl(linkCode);
      navigator.clipboard.writeText(url).then(() => {
        this.snackBar.open("Link copied to clipboard", "Close", {
          duration: 2000,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving.set(true);
    const {
      name,
      targetAlbumId,
      hasExpiry,
      expiresAt,
      hasMaxUploads,
      maxUploads,
    } = this.form.value;

    const request = {
      name,
      targetAlbumId: targetAlbumId || null,
      expiresAt:
        hasExpiry && expiresAt ? new Date(expiresAt).toISOString() : null,
      maxUploads: hasMaxUploads ? maxUploads : null,
    };

    this.authService.createGuestLink(request).subscribe({
      next: (link) => {
        this.createdLink.set(link.id);
        this.isSaving.set(false);
        this.dialogRef.close(true);
      },
      error: (error) => {
        console.error("Failed to create guest link:", error);
        this.isSaving.set(false);
        this.snackBar.open("Failed to create guest link", "Close", {
          duration: 3000,
        });
      },
    });
  }
}
