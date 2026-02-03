import { Component, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  MatDialogModule,
  MAT_DIALOG_DATA,
  MatDialogRef,
} from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";

export interface UpdateConfirmDialogData {
  latestVersion: string | null;
  commitsBehind: number | undefined;
  changelog: string[] | null;
}

@Component({
  selector: "app-update-confirm-dialog",
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>
      <mat-icon class="warning-icon">warning</mat-icon>
      Confirm Update
    </h2>
    <mat-dialog-content>
      <p class="warning-message">
        This will update the application and restart the server. The application
        will be unavailable for a short time.
      </p>
      @if (data.latestVersion) {
        <p><strong>New version:</strong> {{ data.latestVersion }}</p>
      }
      @if (data.commitsBehind && data.commitsBehind > 0) {
        <p>
          <strong>Changes:</strong> {{ data.commitsBehind }} commit{{
            data.commitsBehind > 1 ? "s" : ""
          }}
        </p>
      }
      @if (data.changelog && data.changelog.length > 0) {
        <div class="changelog">
          <strong>Recent changes:</strong>
          <ul>
            @for (entry of data.changelog.slice(0, 5); track entry) {
              <li>{{ entry }}</li>
            }
          </ul>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button mat-raised-button color="warn" (click)="onConfirm()">
        <mat-icon>system_update</mat-icon>
        Update & Restart
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      h2 {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .warning-icon {
        color: #ff9800;
      }

      .warning-message {
        background-color: #fff3e0;
        padding: 12px;
        border-radius: 4px;
        border-left: 4px solid #ff9800;
        margin-bottom: 16px;
      }

      .changelog {
        margin-top: 16px;

        ul {
          margin: 8px 0 0 0;
          padding-left: 20px;
          font-size: 13px;

          li {
            margin: 4px 0;
          }
        }
      }

      mat-dialog-actions button {
        display: flex;
        align-items: center;
        gap: 4px;
      }
    `,
  ],
})
export class UpdateConfirmDialogComponent {
  readonly data = inject<UpdateConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(
    MatDialogRef<UpdateConfirmDialogComponent>,
  );

  onCancel(): void {
    this.dialogRef.close(false);
  }

  onConfirm(): void {
    this.dialogRef.close(true);
  }
}
