import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect } from "vitest";
import { StorageComponent } from "./storage.component";
import { StorageService } from "../../core/services/storage.service";
import { CacheService, CacheStatus } from "../../core/services/cache.service";
import {
  StorageProviderDto,
  StorageProviderType,
  SyncStatus,
} from "../../core/models";
import {
  MatSnackBar,
  MatSnackBarRef,
  TextOnlySnackBar,
} from "@angular/material/snack-bar";

const disconnectedGooglePhotos: StorageProviderDto = {
  id: 6,
  type: StorageProviderType.GooglePhotos,
  name: "My Google Photos",
  isEnabled: true,
  supportsUpload: false,
  supportsWatch: false,
  lastSyncDate: null,
  photoCount: 0,
  isConnected: false,
  statusMessage: null,
};

const emptySyncStatus: SyncStatus = {
  providerId: 6,
  isInProgress: false,
  progressPercent: 0,
  currentOperation: null,
  filesProcessed: 0,
  totalFiles: 0,
  startTime: null,
  lastSyncResult: null,
};

describe("StorageComponent", () => {
  it("renders provider menu trigger for disconnected Google Photos", () => {
    const storageServiceStub = {
      getProviders: () => of([disconnectedGooglePhotos]),
      getSyncStatus: () => of(emptySyncStatus),
    } as Partial<StorageService>;

    const cacheServiceStub = {
      getCacheStatus: () => of(null as CacheStatus | null),
    } as Partial<CacheService>;

    const snackBarStub = {
      open: () => ({}) as MatSnackBarRef<TextOnlySnackBar>,
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [StorageComponent],
      providers: [
        { provide: StorageService, useValue: storageServiceStub },
        { provide: CacheService, useValue: cacheServiceStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(StorageComponent);
    fixture.detectChanges();

    const icons = Array.from(
      fixture.nativeElement.querySelectorAll("mat-icon"),
    );
    const hasMenuTriggerIcon = icons.some(
      (icon) => (icon as HTMLElement).textContent?.trim() === "more_vert",
    );

    expect(hasMenuTriggerIcon).toBe(true);
  });
});
