import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { Router } from "@angular/router";
import { MatSnackBar } from "@angular/material/snack-bar";
import { LoginComponent } from "./login.component";
import { LoginResponse, UserDto } from "../../core/models";
import { AuthService } from "../../core/services/auth.service";
import { UserRole } from "../../core/models/enums.model";

describe("LoginComponent", () => {
  it("submits credentials and navigates to dashboard", () => {
    const mockUser: UserDto = {
      id: 1,
      email: "test@example.com",
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
      checkSetupStatus: vi.fn(() =>
        of({ isSetupRequired: false, message: null }),
      ),
      login: vi.fn(() => of(mockLogin)),
    } as Partial<AuthService>;

    const routerStub = {
      navigate: vi.fn(),
    } as Partial<Router>;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authServiceStub },
        { provide: Router, useValue: routerStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.loginForm.setValue({
      email: "test@example.com",
      password: "password123",
    });

    component.onSubmit();

    expect(authServiceStub.login).toHaveBeenCalled();
    expect(routerStub.navigate).toHaveBeenCalledWith(["/dashboard"]);
  });
});
