import { CommonModule } from "@angular/common";
import {
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  inject,
  signal,
} from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { StorageService } from "../../core/services/storage.service";
import {
  PickerSessionDto,
  PickedMediaItemDto,
} from "../../core/models/storage.model";
import { toDataURL } from "qrcode";

@Component({
  selector: "app-google-photos-picker",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="picker-container">
      <div class="picker-header">
        <h3>Select Photos</h3>
        <p class="subtitle">
          Pick specific photos from Google Photos to import into LibraFoto.
        </p>
      </div>

      @if (!session()) {
        <button
          mat-raised-button
          color="primary"
          (click)="startPicking()"
          [disabled]="isBusy()"
        >
          @if (isBusy()) {
            <mat-spinner diameter="18"></mat-spinner>
          } @else {
            <mat-icon>photo_library</mat-icon>
          }
          Start Picker
        </button>
      } @else {
        <mat-card class="picker-session">
          <mat-card-content>
            <div class="session-grid">
              <div class="session-info">
                <p class="label">Open this link to select photos:</p>
                <a [href]="session()!.pickerUri" target="_blank">
                  Open Google Photos Picker
                </a>

                @if (polling()) {
                  <p class="polling">
                    Waiting for selection...
                    @if (timeRemaining()) {
                      {{ timeRemaining() }}s remaining
                    }
                  </p>
                }
              </div>

              <div class="qr" *ngIf="qrCodeDataUrl()">
                <img [src]="qrCodeDataUrl()!" alt="Picker QR code" />
                <span>Scan to open picker on mobile</span>
              </div>
            </div>
          </mat-card-content>
        </mat-card>

        @if (pickedItems().length > 0) {
          <div class="picked-items">
            <h4>
              @if (isImporting()) {
                Importing {{ pickedItems().length }} items...
              } @else {
                Selected {{ pickedItems().length }} items
              }
            </h4>
            <div class="preview-grid">
              @for (item of pickedItems(); track item.id) {
                <div class="preview-tile">
                  <img
                    *ngIf="item.thumbnailUrl"
                    [src]="item.thumbnailUrl"
                    [alt]="item.filename ?? item.id"
                  />
                  <div class="preview-meta">
                    <span>{{ item.filename ?? item.id }}</span>
                    <span class="type">{{ item.type }}</span>
                  </div>
                </div>
              }
            </div>

            @if (isImporting()) {
              <div class="importing-status">
                <mat-spinner diameter="24"></mat-spinner>
                <span>Importing photos to LibraFoto...</span>
              </div>
            }
          </div>
        }

        <div class="session-actions">
          <button mat-button (click)="resetSession()" [disabled]="isBusy()">
            <mat-icon>close</mat-icon>
            Cancel Session
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .picker-container {
        margin-top: 16px;
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .picker-header h3 {
        margin: 0 0 4px 0;
      }

      .subtitle {
        color: rgba(0, 0, 0, 0.6);
        margin: 0;
      }

      .picker-session {
        background: #f5f5f5;
      }

      .session-grid {
        display: grid;
        grid-template-columns: 1fr auto;
        gap: 16px;
        align-items: center;
      }

      .session-info a {
        color: #1976d2;
        font-weight: 600;
      }

      .polling {
        margin-top: 8px;
        font-size: 13px;
        color: #1565c0;
      }

      .qr {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
      }

      .qr img {
        width: 160px;
        height: 160px;
        border-radius: 8px;
        background: white;
        padding: 8px;
      }

      .qr span {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.6);
      }

      .picked-items h4 {
        margin: 0 0 12px 0;
      }

      .preview-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
        gap: 12px;
        margin-bottom: 16px;
      }

      .preview-tile {
        background: white;
        border-radius: 10px;
        overflow: hidden;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      }

      .preview-tile img {
        width: 100%;
        height: 100px;
        object-fit: cover;
      }

      .preview-meta {
        padding: 8px;
        display: flex;
        flex-direction: column;
        gap: 4px;
        font-size: 12px;
      }

      .preview-meta .type {
        color: rgba(0, 0, 0, 0.6);
      }

      .session-actions {
        display: flex;
        justify-content: flex-end;
      }

      .importing-status {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px 16px;
        background: #e3f2fd;
        border-radius: 8px;
        color: #1565c0;
        font-weight: 500;
      }
    `,
  ],
})
export class GooglePhotosPickerComponent implements OnDestroy {
  private readonly storageService = inject(StorageService);
  private readonly snackBar = inject(MatSnackBar);

  @Input({ required: true }) providerId!: number;
  @Output() imported = new EventEmitter<void>();

  session = signal<PickerSessionDto | null>(null);
  pickedItems = signal<PickedMediaItemDto[]>([]);
  polling = signal(false);
  isBusy = signal(false);
  isImporting = signal(false);
  timeRemaining = signal<number | null>(null);
  qrCodeDataUrl = signal<string | null>(null);

  private pollTimeoutId: ReturnType<typeof setTimeout> | null = null;

  async startPicking(): Promise<void> {
    this.isBusy.set(true);
    this.storageService
      .startGooglePhotosPickerSession(this.providerId)
      .subscribe({
        next: (session) => {
          this.session.set(session);
          this.pickedItems.set([]);
          this.polling.set(false);
          this.timeRemaining.set(null);
          this.isBusy.set(false);
          this.generateQrCode(session.pickerUri)
            .then(() => this.pollSession())
            .catch(() => this.pollSession());
        },
        error: () => {
          this.isBusy.set(false);
          this.snackBar.open("Failed to start Google Photos picker", "Close", {
            duration: 4000,
          });
        },
      });
  }

  private async generateQrCode(uri: string): Promise<void> {
    try {
      const dataUrl = await toDataURL(uri, { margin: 1, width: 180 });
      this.qrCodeDataUrl.set(dataUrl);
    } catch {
      this.qrCodeDataUrl.set(null);
    }
  }

  private pollSession(): void {
    const session = this.session();
    if (!session) return;

    const pollInterval =
      this.parseDuration(session.pollingConfig?.pollInterval) ?? 3000;
    const timeout =
      this.parseDuration(session.pollingConfig?.timeoutIn) ?? 3600000;
    const start = Date.now();

    this.polling.set(true);

    const tick = () => {
      if (!this.polling()) return;

      const elapsed = Date.now() - start;
      const remaining = Math.max(0, Math.round((timeout - elapsed) / 1000));
      this.timeRemaining.set(remaining);

      if (elapsed >= timeout) {
        this.polling.set(false);
        this.snackBar.open("Picker session timed out", "Close", {
          duration: 4000,
        });
        return;
      }

      this.storageService
        .getGooglePhotosPickerSession(this.providerId, session.sessionId)
        .subscribe({
          next: (status) => {
            this.session.set(status);
            if (status.mediaItemsSet) {
              this.polling.set(false);
              this.loadItems();
              return;
            }
            this.pollTimeoutId = globalThis.setTimeout(tick, pollInterval);
          },
          error: () => {
            this.pollTimeoutId = globalThis.setTimeout(tick, pollInterval);
          },
        });
    };

    tick();
  }

  private loadItems(): void {
    const session = this.session();
    if (!session) return;

    this.storageService
      .getGooglePhotosPickerItems(this.providerId, session.sessionId)
      .subscribe({
        next: (items) => {
          this.pickedItems.set(items);
          // Auto-import after loading items
          if (items.length > 0) {
            this.importItems();
          }
        },
        error: () => {
          this.snackBar.open("Failed to load picked items", "Close", {
            duration: 4000,
          });
        },
      });
  }

  private importItems(): void {
    const session = this.session();
    if (!session) return;

    this.isImporting.set(true);
    this.storageService
      .importGooglePhotosPickerItems(this.providerId, session.sessionId)
      .subscribe({
        next: (result) => {
          this.isImporting.set(false);
          const message =
            result.failed > 0
              ? `Imported ${result.imported} items (${result.failed} failed)`
              : `Imported ${result.imported} items`;
          this.snackBar.open(message, "Close", { duration: 5000 });
          this.imported.emit();
          this.resetSession();
        },
        error: () => {
          this.isImporting.set(false);
          this.snackBar.open("Import failed", "Close", { duration: 4000 });
        },
      });
  }

  resetSession(): void {
    const session = this.session();
    this.polling.set(false);

    if (this.pollTimeoutId) {
      globalThis.clearTimeout(this.pollTimeoutId);
      this.pollTimeoutId = null;
    }

    if (session) {
      this.storageService
        .deleteGooglePhotosPickerSession(this.providerId, session.sessionId)
        .subscribe();
    }

    this.session.set(null);
    this.pickedItems.set([]);
    this.timeRemaining.set(null);
    this.qrCodeDataUrl.set(null);
  }

  private parseDuration(value?: string | null): number | null {
    if (!value) return null;
    const trimmed = value.trim();
    if (!trimmed.endsWith("s")) return null;
    const numeric = Number.parseFloat(trimmed.slice(0, -1));
    if (Number.isNaN(numeric)) return null;
    return Math.round(numeric * 1000);
  }

  ngOnDestroy(): void {
    this.resetSession();
  }
}
