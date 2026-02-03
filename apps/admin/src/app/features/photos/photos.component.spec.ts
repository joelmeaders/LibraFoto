import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialog } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { PhotosComponent } from "./photos.component";
import {
  BulkOperationResult,
  RefreshThumbnailsResult,
  UploadResult,
} from "../../core/models";
import { PhotoService } from "../../core/services/photo.service";
import { StorageService } from "../../core/services/storage.service";

describe("PhotosComponent", () => {
  it("loads photos and shows empty state when none exist", () => {
    const bulkResult: BulkOperationResult = {
      successCount: 0,
      failedCount: 0,
      errors: [],
    };

    const refreshResult: RefreshThumbnailsResult = {
      succeeded: 0,
      failed: 0,
      errors: [],
    };

    const photoServiceStub = {
      getPhotos: vi.fn(() =>
        of({
          data: [],
          pagination: { page: 1, pageSize: 24, totalItems: 0, totalPages: 0 },
        }),
      ),
      getThumbnailUrl: vi.fn(() => ""),
      deletePhotos: vi.fn(() => of(bulkResult)),
      refreshThumbnails: vi.fn(() => of(refreshResult)),
    } as Partial<PhotoService>;

    const uploadResult: UploadResult = {
      success: true,
      errorMessage: null,
      photoId: 1,
      fileId: "file",
      fileName: "photo.jpg",
      filePath: "/photos/photo.jpg",
      fileSize: 100,
      contentType: "image/jpeg",
      fileUrl: "http://example.com/photo.jpg",
      thumbnailUrl: "http://example.com/thumb.jpg",
    };

    const storageServiceStub = {
      uploadFile: vi.fn(() => of(uploadResult)),
    } as Partial<StorageService>;

    const dialogStub = {
      open: vi.fn(),
    } as Partial<MatDialog>;

    const snackBarStub = {
      open: vi.fn(),
      dismiss: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [PhotosComponent],
      providers: [
        { provide: PhotoService, useValue: photoServiceStub },
        { provide: StorageService, useValue: storageServiceStub },
        { provide: MatDialog, useValue: dialogStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(PhotosComponent);
    fixture.detectChanges();

    expect(photoServiceStub.getPhotos).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain("No photos yet");
  });
});
