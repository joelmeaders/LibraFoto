import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialogRef } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { CreateGuestLinkDialogComponent } from "./create-guest-link-dialog.component";
import { GuestLinkDto } from "../../core/models";
import { AuthService } from "../../core/services/auth.service";
import { AlbumService } from "../../core/services/album.service";

describe("CreateGuestLinkDialogComponent", () => {
  it("creates a guest link when form is valid", () => {
    const dialogRefStub = {
      close: vi.fn(),
    } as Partial<MatDialogRef<CreateGuestLinkDialogComponent>>;

    const mockGuestLink: GuestLinkDto = {
      id: "abc123",
      name: "Family Upload",
      createdAt: "2025-01-01T00:00:00Z",
      expiresAt: null,
      maxUploads: null,
      currentUploads: 0,
      targetAlbumId: null,
      targetAlbumName: null,
      createdByUserId: 1,
      createdByUsername: "admin",
      isActive: true,
    };

    const authServiceStub = {
      createGuestLink: vi.fn(() => of(mockGuestLink)),
    } as Partial<AuthService>;

    const albumServiceStub = {
      getAlbums: vi.fn(() => of([])),
    } as Partial<AlbumService>;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [CreateGuestLinkDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefStub },
        { provide: AuthService, useValue: authServiceStub },
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(CreateGuestLinkDialogComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.setValue({
      name: "Family Upload",
      targetAlbumId: null,
      hasExpiry: false,
      expiresAt: null,
      hasMaxUploads: false,
      maxUploads: null,
    });
    component.save();

    expect(authServiceStub.createGuestLink).toHaveBeenCalled();
    expect(dialogRefStub.close).toHaveBeenCalledWith(true);
  });
});
