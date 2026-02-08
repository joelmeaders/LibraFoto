import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
} from "@angular/forms";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatSliderModule } from "@angular/material/slider";
import { MatSelectModule } from "@angular/material/select";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatDividerModule } from "@angular/material/divider";
import { DisplaySettingsService } from "../../core/services/display-settings.service";
import { AlbumService } from "../../core/services/album.service";
import { TagService } from "../../core/services/tag.service";
import { DisplaySettingsDto, AlbumDto, TagDto } from "../../core/models";
import {
  TransitionType,
  SourceType,
  ImageFit,
} from "../../core/models/enums.model";
import { forkJoin } from "rxjs";

@Component({
  selector: "app-display-settings",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatSliderModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDividerModule,
  ],
  template: `
    <div class="settings-container">
      <div class="header">
        <h1>Display Settings</h1>
        @if (hasChanges()) {
          <span class="unsaved-indicator">Unsaved changes</span>
        }
      </div>

      @if (isLoading()) {
        <div class="loading">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      } @else if (settings()) {
        <form [formGroup]="form">
          <mat-card>
            <mat-card-header>
              <mat-icon mat-card-avatar>slideshow</mat-icon>
              <mat-card-title>Slideshow</mat-card-title>
              <mat-card-subtitle
                >Configure how photos are displayed</mat-card-subtitle
              >
            </mat-card-header>
            <mat-card-content>
              <div class="setting-row">
                <label
                  >Slide Duration:
                  {{ form.get("slideDuration")?.value }} seconds</label
                >
                <mat-slider min="3" max="120" step="1" discrete>
                  <input matSliderThumb formControlName="slideDuration" />
                </mat-slider>
                <p class="hint">
                  How long each photo is displayed before transitioning
                </p>
              </div>

              <mat-divider></mat-divider>

              <div class="setting-row">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Transition Effect</mat-label>
                  <mat-select formControlName="transition">
                    <mat-option [value]="TransitionType.Fade">Fade</mat-option>
                    <mat-option [value]="TransitionType.Slide"
                      >Slide</mat-option
                    >
                    <mat-option [value]="TransitionType.KenBurns"
                      >Ken Burns (pan & zoom)</mat-option
                    >
                  </mat-select>
                </mat-form-field>
              </div>

              <div class="setting-row">
                <label
                  >Transition Duration:
                  {{ form.get("transitionDuration")?.value }}ms</label
                >
                <mat-slider min="200" max="2000" step="100" discrete>
                  <input matSliderThumb formControlName="transitionDuration" />
                </mat-slider>
              </div>

              <mat-divider></mat-divider>

              <div class="setting-row">
                <mat-slide-toggle formControlName="shuffle"
                  >Shuffle Photos</mat-slide-toggle
                >
                <p class="hint">Randomize the order of photos</p>
              </div>

              <mat-divider></mat-divider>

              <div class="setting-row">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Image Fit Mode</mat-label>
                  <mat-select formControlName="imageFit">
                    <mat-option [value]="ImageFit.Contain"
                      >Contain (show full image with blur
                      background)</mat-option
                    >
                    <mat-option [value]="ImageFit.Cover"
                      >Cover (fill screen, may crop)</mat-option
                    >
                  </mat-select>
                </mat-form-field>
                <p class="hint">
                  How images are fitted to the screen. Contain mode shows a
                  blurred, zoomed background.
                </p>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-header>
              <mat-icon mat-card-avatar>filter_alt</mat-icon>
              <mat-card-title>Photo Source</mat-card-title>
              <mat-card-subtitle
                >Choose which photos to display</mat-card-subtitle
              >
            </mat-card-header>
            <mat-card-content>
              <div class="setting-row">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Photo Source</mat-label>
                  <mat-select
                    formControlName="sourceType"
                    (selectionChange)="onSourceTypeChange()"
                  >
                    <mat-option [value]="SourceType.All">All Photos</mat-option>
                    <mat-option [value]="SourceType.Album"
                      >Selected Album</mat-option
                    >
                    <mat-option [value]="SourceType.Tag"
                      >Selected Tag</mat-option
                    >
                  </mat-select>
                </mat-form-field>
              </div>

              @if (form.get("sourceType")?.value === SourceType.Album) {
                <div class="setting-row">
                  <mat-form-field appearance="outline" class="full-width">
                    <mat-label>Album</mat-label>
                    <mat-select formControlName="sourceId">
                      @for (album of albums(); track album.id) {
                        <mat-option [value]="album.id"
                          >{{ album.name }} ({{
                            album.photoCount
                          }}
                          photos)</mat-option
                        >
                      }
                    </mat-select>
                    @if (albums().length === 0) {
                      <mat-hint
                        >No albums available. Create an album first.</mat-hint
                      >
                    }
                  </mat-form-field>
                </div>
              }
              @if (form.get("sourceType")?.value === SourceType.Tag) {
                <div class="setting-row">
                  <mat-form-field appearance="outline" class="full-width">
                    <mat-label>Tag</mat-label>
                    <mat-select formControlName="sourceId">
                      @for (tag of tags(); track tag.id) {
                        <mat-option [value]="tag.id">
                          <span class="tag-option">
                            <span
                              class="tag-color"
                              [style.background-color]="tag.color"
                            ></span>
                            {{ tag.name }} ({{ tag.photoCount }} photos)
                          </span>
                        </mat-option>
                      }
                    </mat-select>
                    @if (tags().length === 0) {
                      <mat-hint
                        >No tags available. Create a tag first.</mat-hint
                      >
                    }
                  </mat-form-field>
                </div>
              }
            </mat-card-content>
          </mat-card>

          <div class="actions">
            <button
              mat-raised-button
              color="primary"
              (click)="saveSettings()"
              [disabled]="isSaving() || !hasChanges()"
            >
              @if (isSaving()) {
                <mat-spinner diameter="20"></mat-spinner>
              } @else {
                <mat-icon>save</mat-icon>
              }
              @if (isSaving()) {
                <span>Saving...</span>
              } @else {
                <span>Save Settings</span>
              }
            </button>
            <button
              mat-button
              (click)="resetToDefaults()"
              [disabled]="isSaving()"
            >
              <mat-icon>restore</mat-icon>
              Reset to Defaults
            </button>
            @if (hasChanges()) {
              <button mat-button (click)="discardChanges()">
                <mat-icon>undo</mat-icon>
                Discard Changes
              </button>
            }
          </div>
        </form>
      }
    </div>
  `,
  styles: [
    `
      .settings-container {
        padding: 24px;
        max-width: 700px;
      }

      .header {
        display: flex;
        align-items: center;
        gap: 16px;
        margin-bottom: 24px;
      }

      .unsaved-indicator {
        background-color: #ff9800;
        color: white;
        padding: 4px 12px;
        border-radius: 16px;
        font-size: 13px;
      }

      .loading {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      mat-card {
        margin-bottom: 16px;
      }

      mat-card-header mat-icon {
        font-size: 24px;
        color: #1976d2;
      }

      .setting-row {
        margin: 16px 0;
      }

      .setting-row label {
        display: block;
        margin-bottom: 8px;
        font-weight: 500;
      }

      .hint {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.54);
        margin-top: 4px;
      }

      mat-slider {
        width: 100%;
      }

      .full-width {
        width: 100%;
      }

      mat-divider {
        margin: 16px 0;
      }

      .toggle-group {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .tag-option {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .tag-color {
        width: 12px;
        height: 12px;
        border-radius: 50%;
      }

      .actions {
        display: flex;
        gap: 8px;
        margin-top: 24px;
        flex-wrap: wrap;
      }

      .actions button mat-spinner {
        display: inline-block;
      }
    `,
  ],
})
export class DisplaySettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly displaySettingsService = inject(DisplaySettingsService);
  private readonly albumService = inject(AlbumService);
  private readonly tagService = inject(TagService);
  private readonly snackBar = inject(MatSnackBar);

  form!: FormGroup;
  settings = signal<DisplaySettingsDto | null>(null);
  albums = signal<AlbumDto[]>([]);
  tags = signal<TagDto[]>([]);
  isLoading = signal(true);
  isSaving = signal(false);
  hasChanges = signal(false);

  // Expose enums to template
  TransitionType = TransitionType;
  SourceType = SourceType;
  ImageFit = ImageFit;

  private originalSettings: DisplaySettingsDto | null = null;

  ngOnInit(): void {
    this.initForm();
    this.loadData();
  }

  private initForm(): void {
    this.form = this.fb.group({
      slideDuration: [10],
      transition: [TransitionType.Fade],
      transitionDuration: [500],
      shuffle: [false],
      sourceType: [SourceType.All],
      sourceId: [null],
      imageFit: [ImageFit.Contain],
    });

    this.form.valueChanges.subscribe(() => {
      this.checkForChanges();
    });
  }

  private loadData(): void {
    this.isLoading.set(true);

    forkJoin({
      settings: this.displaySettingsService.getSettings(),
      albums: this.albumService.getAlbums(),
      tags: this.tagService.getTags(),
    }).subscribe({
      next: ({ settings, albums, tags }) => {
        this.settings.set(settings);
        this.originalSettings = { ...settings };
        this.albums.set(albums);
        this.tags.set(tags);
        this.populateForm(settings);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("Failed to load settings:", error);
        this.isLoading.set(false);
        this.snackBar.open("Failed to load settings", "Close", {
          duration: 3000,
        });
      },
    });
  }

  private populateForm(settings: DisplaySettingsDto): void {
    this.form.patchValue(
      {
        slideDuration: settings.slideDuration,
        transition: settings.transition,
        transitionDuration: settings.transitionDuration,
        shuffle: settings.shuffle,
        sourceType: settings.sourceType,
        sourceId: settings.sourceId,
        imageFit: settings.imageFit,
      },
      { emitEvent: false },
    );
    this.hasChanges.set(false);
  }

  private checkForChanges(): void {
    if (!this.originalSettings) {
      this.hasChanges.set(false);
      return;
    }

    const current = this.form.value;
    const changed =
      current.slideDuration !== this.originalSettings.slideDuration ||
      current.transition !== this.originalSettings.transition ||
      current.transitionDuration !== this.originalSettings.transitionDuration ||
      current.shuffle !== this.originalSettings.shuffle ||
      current.sourceType !== this.originalSettings.sourceType ||
      current.sourceId !== this.originalSettings.sourceId ||
      current.imageFit !== this.originalSettings.imageFit;

    this.hasChanges.set(changed);
  }

  onSourceTypeChange(): void {
    const sourceType = this.form.get("sourceType")?.value;
    if (sourceType === SourceType.All) {
      this.form.patchValue({ sourceId: null });
    }
  }

  saveSettings(): void {
    const settings = this.settings();
    if (!settings) return;

    this.isSaving.set(true);

    this.displaySettingsService
      .updateSettings(settings.id, this.form.value)
      .subscribe({
        next: (updatedSettings) => {
          this.settings.set(updatedSettings);
          this.originalSettings = { ...updatedSettings };
          this.hasChanges.set(false);
          this.isSaving.set(false);
          this.snackBar.open("Settings saved", "Close", { duration: 3000 });
        },
        error: (error) => {
          console.error("Failed to save settings:", error);
          this.isSaving.set(false);
          this.snackBar.open("Failed to save settings", "Close", {
            duration: 3000,
          });
        },
      });
  }

  resetToDefaults(): void {
    const settings = this.settings();
    if (!settings) return;

    if (confirm("Reset all settings to their default values?")) {
      this.isSaving.set(true);

      this.displaySettingsService.resetToDefaults(settings.id).subscribe({
        next: (resetSettings) => {
          this.settings.set(resetSettings);
          this.originalSettings = { ...resetSettings };
          this.populateForm(resetSettings);
          this.isSaving.set(false);
          this.snackBar.open("Settings reset to defaults", "Close", {
            duration: 3000,
          });
        },
        error: (error) => {
          console.error("Failed to reset settings:", error);
          this.isSaving.set(false);
          this.snackBar.open("Failed to reset settings", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }

  discardChanges(): void {
    if (this.originalSettings) {
      this.populateForm(this.originalSettings);
    }
  }
}
