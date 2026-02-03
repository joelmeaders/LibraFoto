import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect } from "vitest";
import { DisplaySettingsComponent } from "./display-settings.component";
import { DisplaySettingsService } from "../../core/services/display-settings.service";
import { AlbumService } from "../../core/services/album.service";
import { TagService } from "../../core/services/tag.service";
import {
  MatSnackBar,
  MatSnackBarRef,
  TextOnlySnackBar,
} from "@angular/material/snack-bar";
import {
  DisplaySettingsDto,
  TransitionType,
  SourceType,
  ImageFit,
  AlbumDto,
  TagDto,
} from "../../core/models";

describe("DisplaySettingsComponent", () => {
  const mockSettings: DisplaySettingsDto = {
    id: 1,
    name: "Default",
    slideDuration: 10,
    transition: TransitionType.Fade,
    transitionDuration: 500,
    shuffle: false,
    sourceType: SourceType.All,
    sourceId: null,
    imageFit: ImageFit.Contain,
  };

  const displaySettingsServiceStub = {
    getSettings: () => of(mockSettings),
    updateSettings: () => of(mockSettings),
    resetToDefaults: () => of(mockSettings),
  } as Partial<DisplaySettingsService>;

  const albumServiceStub = {
    getAlbums: () => of([] as AlbumDto[]),
  } as Partial<AlbumService>;

  const tagServiceStub = {
    getTags: () => of([] as TagDto[]),
  } as Partial<TagService>;

  const snackBarStub = {
    open: () => ({}) as MatSnackBarRef<TextOnlySnackBar>,
  } as Partial<MatSnackBar>;

  it("shows Save Settings when not saving", () => {
    TestBed.configureTestingModule({
      imports: [DisplaySettingsComponent],
      providers: [
        {
          provide: DisplaySettingsService,
          useValue: displaySettingsServiceStub,
        },
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: TagService, useValue: tagServiceStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(DisplaySettingsComponent);
    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll("button"),
    );
    const saveButton = buttons.find((button) =>
      (button as HTMLElement).textContent?.includes("Save Settings"),
    ) as HTMLElement | undefined;

    expect(saveButton).toBeTruthy();
    expect(saveButton?.textContent).toContain("Save Settings");
  });

  it("shows Saving... when saving", () => {
    TestBed.configureTestingModule({
      imports: [DisplaySettingsComponent],
      providers: [
        {
          provide: DisplaySettingsService,
          useValue: displaySettingsServiceStub,
        },
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: TagService, useValue: tagServiceStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(DisplaySettingsComponent);
    fixture.detectChanges();

    fixture.componentInstance.isSaving.set(true);
    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll("button"),
    );
    const saveButton = buttons.find((button) =>
      (button as HTMLElement).textContent?.includes("Saving..."),
    ) as HTMLElement | undefined;

    expect(saveButton).toBeTruthy();
    expect(saveButton?.textContent).toContain("Saving...");
  });
});
