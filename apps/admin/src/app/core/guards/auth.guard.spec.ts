import { describe, it, expect, beforeEach, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import {
  Router,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
} from "@angular/router";
import { of } from "rxjs";
import {
  authGuard,
  adminGuard,
  editorGuard,
  setupGuard,
  setupPageGuard,
  noAuthGuard,
  roleGuard,
} from "./auth.guard";
import { AuthService } from "../services/auth.service";
import { UserRole } from "../models";

describe("Auth Guards", () => {
  let authService: {
    isAuthenticated: ReturnType<typeof vi.fn>;
    isAdmin: ReturnType<typeof vi.fn>;
    isEditor: ReturnType<typeof vi.fn>;
    currentUser: ReturnType<typeof vi.fn>;
    checkSetupStatus: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };
  let mockRoute: ActivatedRouteSnapshot;
  let mockState: RouterStateSnapshot;

  beforeEach(() => {
    authService = {
      isAuthenticated: vi.fn(),
      isAdmin: vi.fn(),
      isEditor: vi.fn(),
      currentUser: vi.fn(),
      checkSetupStatus: vi.fn(),
    };

    router = {
      navigate: vi.fn(),
    };

    mockRoute = {} as ActivatedRouteSnapshot;
    mockState = { url: "/test" } as RouterStateSnapshot;

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });
  });

  describe("authGuard", () => {
    it("should allow access if user is authenticated", () => {
      authService.isAuthenticated.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        authGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to login if user is not authenticated", () => {
      authService.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        authGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });
  });

  describe("adminGuard", () => {
    it("should allow access if user is authenticated and is admin", () => {
      authService.isAuthenticated.mockReturnValue(true);
      authService.isAdmin.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to login if user is not authenticated", () => {
      authService.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });

    it("should redirect to dashboard if user is authenticated but not admin", () => {
      authService.isAuthenticated.mockReturnValue(true);
      authService.isAdmin.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/dashboard"]);
    });
  });

  describe("editorGuard", () => {
    it("should allow access if user is authenticated and has editor role", () => {
      authService.isAuthenticated.mockReturnValue(true);
      authService.isEditor.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        editorGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to login if user is not authenticated", () => {
      authService.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        editorGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });

    it("should redirect to dashboard if user is authenticated but not editor", () => {
      authService.isAuthenticated.mockReturnValue(true);
      authService.isEditor.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        editorGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/dashboard"]);
    });
  });

  describe("setupGuard", () => {
    it("should allow access if setup is not required", async () => {
      authService.checkSetupStatus.mockReturnValue(
        of({ isSetupRequired: false, message: null })
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to setup if setup is required", async () => {
      authService.checkSetupStatus.mockReturnValue(
        of({ isSetupRequired: true, message: "Setup required" })
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/setup"]);
    });

    it("should allow access if setup status check fails", async () => {
      authService.checkSetupStatus.mockReturnValue(
        new Promise((_, reject) => reject(new Error("Network error")))
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe("setupPageGuard", () => {
    it("should allow access to setup page if setup is required", async () => {
      authService.checkSetupStatus.mockReturnValue(
        of({ isSetupRequired: true, message: "Setup required" })
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupPageGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to login if setup is not required", async () => {
      authService.checkSetupStatus.mockReturnValue(
        of({ isSetupRequired: false, message: null })
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupPageGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });

    it("should redirect to login if setup status check fails", async () => {
      authService.checkSetupStatus.mockReturnValue(
        new Promise((_, reject) => reject(new Error("Network error")))
      );

      const result = await TestBed.runInInjectionContext(() =>
        setupPageGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });
  });

  describe("noAuthGuard", () => {
    it("should allow access if user is not authenticated", () => {
      authService.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        noAuthGuard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to dashboard if user is authenticated", () => {
      authService.isAuthenticated.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        noAuthGuard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/dashboard"]);
    });
  });

  describe("roleGuard", () => {
    it("should allow access if user has required role", () => {
      const mockUser = { id: 1, email: "admin@test.com", role: UserRole.Admin };
      authService.isAuthenticated.mockReturnValue(true);
      authService.currentUser.mockReturnValue(mockUser);

      const guard = roleGuard(UserRole.Admin);
      const result = TestBed.runInInjectionContext(() =>
        guard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should allow access if user has higher role than required", () => {
      const mockUser = { id: 1, email: "admin@test.com", role: UserRole.Admin };
      authService.isAuthenticated.mockReturnValue(true);
      authService.currentUser.mockReturnValue(mockUser);

      const guard = roleGuard(UserRole.Editor);
      const result = TestBed.runInInjectionContext(() =>
        guard(mockRoute, mockState)
      );

      expect(result).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it("should redirect to login if user is not authenticated", () => {
      authService.isAuthenticated.mockReturnValue(false);

      const guard = roleGuard(UserRole.Editor);
      const result = TestBed.runInInjectionContext(() =>
        guard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/login"]);
    });

    it("should redirect to dashboard if user has insufficient role", () => {
      const mockUser = {
        id: 1,
        email: "guest@test.com",
        role: UserRole.Guest,
      };
      authService.isAuthenticated.mockReturnValue(true);
      authService.currentUser.mockReturnValue(mockUser);

      const guard = roleGuard(UserRole.Editor);
      const result = TestBed.runInInjectionContext(() =>
        guard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/dashboard"]);
    });

    it("should redirect to dashboard if current user is null", () => {
      authService.isAuthenticated.mockReturnValue(true);
      authService.currentUser.mockReturnValue(null);

      const guard = roleGuard(UserRole.Editor);
      const result = TestBed.runInInjectionContext(() =>
        guard(mockRoute, mockState)
      );

      expect(result).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(["/dashboard"]);
    });
  });
});
