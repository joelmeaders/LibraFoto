import { TestBed } from "@angular/core/testing";
import { signal } from "@angular/core";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialog, MatDialogRef } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { UsersComponent } from "./users.component";
import { AuthService } from "../../core/services/auth.service";
import { UserRole } from "../../core/models/enums.model";

describe("UsersComponent", () => {
  it("loads users and guest links", () => {
    const authServiceStub = {
      currentUser: signal(null),
      getUsers: vi.fn(() =>
        of({
          data: [
            {
              id: 1,
              email: "admin@example.com",
              role: UserRole.Admin,
              createdAt: "2025-01-01T00:00:00Z",
              lastLoginAt: null,
            },
          ],
          pagination: {
            page: 1,
            pageSize: 20,
            totalItems: 1,
            totalPages: 1,
          },
        }),
      ),
      getGuestLinks: vi.fn(() =>
        of({
          data: [],
          pagination: {
            page: 1,
            pageSize: 20,
            totalItems: 0,
            totalPages: 0,
          },
        }),
      ),
    } as Partial<AuthService>;

    const dialogStub = {
      open: vi.fn(
        () => ({ afterClosed: () => of(false) }) as MatDialogRef<unknown>,
      ),
    } as unknown as MatDialog;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [UsersComponent],
      providers: [
        { provide: AuthService, useValue: authServiceStub },
        { provide: MatDialog, useValue: dialogStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(UsersComponent);
    fixture.detectChanges();

    expect(authServiceStub.getUsers).toHaveBeenCalled();
    expect(authServiceStub.getGuestLinks).toHaveBeenCalled();
    expect(fixture.componentInstance.users().length).toBe(1);
  });
});
