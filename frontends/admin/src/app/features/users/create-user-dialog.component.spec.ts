import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { CreateUserDialogComponent } from "./create-user-dialog.component";
import { UserDto } from "../../core/models";
import { AuthService } from "../../core/services/auth.service";
import { UserRole } from "../../core/models/enums.model";

describe("CreateUserDialogComponent", () => {
  it("creates a user when form is valid", () => {
    const dialogRefStub = {
      close: vi.fn(),
    } as Partial<MatDialogRef<CreateUserDialogComponent>>;

    const mockUser: UserDto = {
      id: 1,
      email: "new@example.com",
      role: UserRole.Editor,
      createdAt: "2025-01-01T00:00:00Z",
      lastLoginAt: null,
    };

    const authServiceStub = {
      createUser: vi.fn(() => of(mockUser)),
      updateUser: vi.fn(() => of(mockUser)),
    } as Partial<AuthService>;

    TestBed.configureTestingModule({
      imports: [CreateUserDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefStub },
        { provide: AuthService, useValue: authServiceStub },
        { provide: MAT_DIALOG_DATA, useValue: null },
      ],
    });

    const fixture = TestBed.createComponent(CreateUserDialogComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.setValue({
      email: "new@example.com",
      password: "password123",
      role: UserRole.Editor,
    });
    component.save();

    expect(authServiceStub.createUser).toHaveBeenCalled();
    expect(dialogRefStub.close).toHaveBeenCalledWith(true);
  });
});
