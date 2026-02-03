import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { Router } from "@angular/router";
import { MatSnackBar } from "@angular/material/snack-bar";
import { SetupComponent } from "./setup.component";
import { LoginResponse, UserDto } from "../../core/models";
import { AuthService } from "../../core/services/auth.service";
import { UserRole } from "../../core/models/enums.model";

describe("SetupComponent", () => {
  it("initializes the setup form", () => {
    const mockUser: UserDto = {
      id: 1,
      email: "admin@example.com",
      role: UserRole.Admin,
      createdAt: "2025-01-01T00:00:00Z",
      lastLoginAt: null,
    };

    const mockLogin: LoginResponse = {
      token: "token",
      refreshToken: "refresh",
      expiresAt: "2025-01-01T01:00:00Z",
      user: mockUser,
    };

    const authServiceStub = {
      setup: vi.fn(() => of(mockLogin)),
    } as Partial<AuthService>;

    const routerStub = {
      navigate: vi.fn(),
    } as Partial<Router>;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [SetupComponent],
      providers: [
        { provide: AuthService, useValue: authServiceStub },
        { provide: Router, useValue: routerStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(SetupComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.adminForm).toBeTruthy();
    expect(fixture.componentInstance.adminForm.get("email")).toBeTruthy();
  });
});
