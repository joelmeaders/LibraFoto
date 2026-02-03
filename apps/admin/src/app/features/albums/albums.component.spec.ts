import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { AlbumsComponent } from "./albums.component";
import { AlbumService } from "../../core/services/album.service";
import { MatDialog, MatDialogRef } from "@angular/material/dialog";
import {
  MatSnackBar,
  MatSnackBarRef,
  TextOnlySnackBar,
} from "@angular/material/snack-bar";

describe("AlbumsComponent", () => {
  it("shows empty state when there are no albums", () => {
    const albumServiceStub = {
      getAlbums: vi.fn(() => of([])),
      getCoverThumbnailUrl: vi.fn(() => null),
    } as Partial<AlbumService>;

    const dialogStub = {
      open: vi.fn(
        () => ({ afterClosed: () => of(false) }) as MatDialogRef<unknown>,
      ),
    } as unknown as MatDialog;

    const snackBarStub = {
      open: () => ({}) as MatSnackBarRef<TextOnlySnackBar>,
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [AlbumsComponent],
      providers: [
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: MatDialog, useValue: dialogStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(AlbumsComponent);
    fixture.detectChanges();

    expect(albumServiceStub.getAlbums).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain("No albums yet");
  });
});
