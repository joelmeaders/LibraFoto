import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { CreateAlbumDialogComponent } from "./create-album-dialog.component";
import { AlbumDto } from "../../core/models";
import { AlbumService } from "../../core/services/album.service";

describe("CreateAlbumDialogComponent", () => {
  it("creates a new album when form is valid", () => {
    const dialogRefStub = {
      close: vi.fn(),
    } as Partial<MatDialogRef<CreateAlbumDialogComponent>>;

    const mockAlbum: AlbumDto = {
      id: 1,
      name: "Holiday",
      description: "Trip",
      coverPhotoId: null,
      coverPhotoThumbnail: null,
      dateCreated: "2025-01-01T00:00:00Z",
      sortOrder: 0,
      photoCount: 0,
    };

    const albumServiceStub = {
      createAlbum: vi.fn(() => of(mockAlbum)),
      updateAlbum: vi.fn(() => of(mockAlbum)),
    } as Partial<AlbumService>;

    TestBed.configureTestingModule({
      imports: [CreateAlbumDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefStub },
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: MAT_DIALOG_DATA, useValue: null },
      ],
    });

    const fixture = TestBed.createComponent(CreateAlbumDialogComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.setValue({ name: "Holiday", description: "Trip" });
    component.save();

    expect(albumServiceStub.createAlbum).toHaveBeenCalled();
    expect(dialogRefStub.close).toHaveBeenCalledWith(true);
  });
});
