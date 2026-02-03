import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { PhotoDetailDialogComponent } from "./photo-detail-dialog.component";
import { PhotoService } from "../../core/services/photo.service";
import { MediaType, PhotoDetailDto } from "../../core/models";

describe("PhotoDetailDialogComponent", () => {
  it("loads the photo details on init", () => {
    const mockPhoto: PhotoDetailDto = {
      id: 1,
      filename: "photo.jpg",
      originalFilename: "photo.jpg",
      filePath: "photos/photo.jpg",
      thumbnailPath: "thumb/photo.jpg",
      width: 800,
      height: 600,
      fileSize: 1200,
      mediaType: MediaType.Photo,
      duration: null,
      dateTaken: "2025-01-01T00:00:00Z",
      dateAdded: "2025-01-01T00:00:00Z",
      location: null,
      latitude: null,
      longitude: null,
      providerId: null,
      providerName: null,
      albums: [],
      tags: [],
    };

    const photoServiceStub = {
      getPhoto: vi.fn(() => of(mockPhoto)),
      getPhotoUrl: vi.fn(() => "http://example.com/photo.jpg"),
      updatePhoto: vi.fn(() => of(mockPhoto)),
      deletePhoto: vi.fn(() => of(void 0)),
    } as Partial<PhotoService>;

    const dialogRefStub = {
      close: vi.fn(),
    } as Partial<MatDialogRef<PhotoDetailDialogComponent>>;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [PhotoDetailDialogComponent],
      providers: [
        { provide: PhotoService, useValue: photoServiceStub },
        { provide: MatDialogRef, useValue: dialogRefStub },
        { provide: MAT_DIALOG_DATA, useValue: { photoId: 1 } },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(PhotoDetailDialogComponent);
    fixture.detectChanges();

    expect(photoServiceStub.getPhoto).toHaveBeenCalledWith(1);
    expect(fixture.componentInstance.photo()).toEqual(mockPhoto);
  });
});
