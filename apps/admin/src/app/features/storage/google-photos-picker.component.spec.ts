import { TestBed } from "@angular/core/testing";
import { describe, it, expect, vi } from "vitest";
import { MatSnackBar } from "@angular/material/snack-bar";
import { GooglePhotosPickerComponent } from "./google-photos-picker.component";
import { StorageService } from "../../core/services/storage.service";

describe("GooglePhotosPickerComponent", () => {
  it("starts with no active session", () => {
    const storageServiceStub = {
      startGooglePhotosPickerSession: vi.fn(),
      getGooglePhotosPickerSession: vi.fn(),
      getGooglePhotosPickerItems: vi.fn(),
      importGooglePhotosPickerItems: vi.fn(),
      deleteGooglePhotosPickerSession: vi.fn(),
    } as Partial<StorageService>;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [GooglePhotosPickerComponent],
      providers: [
        { provide: StorageService, useValue: storageServiceStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(GooglePhotosPickerComponent);
    fixture.componentInstance.providerId = 1;
    fixture.detectChanges();

    expect(fixture.componentInstance.session()).toBeNull();
  });
});
