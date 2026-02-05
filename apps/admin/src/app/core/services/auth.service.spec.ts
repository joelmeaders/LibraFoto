import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { Router } from "@angular/router";
import { AuthService } from "./auth.service";
import {
  LoginResponse,
  UserDto,
  SetupStatusResponse,
  PagedResult,
  GuestLinkDto,
} from "../models";

describe("AuthService", () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  const baseUrl = "";

  const mockUser: UserDto = {
    id: 1,
    email: "test@example.com",
    role: 2, // Admin
    createdAt: "2025-01-01T00:00:00Z",
    lastLoginAt: null,
  };

  const mockLoginResponse: LoginResponse = {
    token: "test-token",
    refreshToken: "test-refresh-token",
    expiresAt: new Date(Date.now() + 3600000).toISOString(), // 1 hour from now
    user: mockUser,
  };

  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();

    routerMock = {
      navigate: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerMock },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  describe("Initial state", () => {
    it("should start with no authenticated user when storage is empty", () => {
      expect(service.currentUser()).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
    });
  });

  describe("checkSetupStatus", () => {
    it("should check if setup is required", () => {
      const status: SetupStatusResponse = {
        isSetupRequired: true,
        message: null,
      };

      service.checkSetupStatus().subscribe((result) => {
        expect(result).toEqual(status);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/setup/status`);
      expect(req.request.method).toBe("GET");
      req.flush(status);
    });
  });

  describe("setup", () => {
    it("should complete setup and save tokens", () => {
      service
        .setup({
          email: "admin@example.com",
          password: "password",
        })
        .subscribe((result) => {
          expect(result).toEqual(mockLoginResponse);
          expect(service.isAuthenticated()).toBe(true);
          expect(service.currentUser()).toEqual(mockUser);
        });

      const req = httpMock.expectOne(`${baseUrl}/api/setup/complete`);
      expect(req.request.method).toBe("POST");
      req.flush(mockLoginResponse);
    });

    it("should set loading state during setup", () => {
      expect(service.isLoading()).toBe(false);

      service
        .setup({
          email: "admin@example.com",
          password: "password",
        })
        .subscribe();
      expect(service.isLoading()).toBe(true);

      const req = httpMock.expectOne(`${baseUrl}/api/setup/complete`);
      req.flush(mockLoginResponse);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("login", () => {
    it("should login and save tokens", () => {
      service
        .login({ email: "test@example.com", password: "password" })
        .subscribe((result) => {
          expect(result).toEqual(mockLoginResponse);
          expect(localStorage.getItem("librafoto_token")).toBe("test-token");
          expect(localStorage.getItem("librafoto_refresh_token")).toBe(
            "test-refresh-token",
          );
          expect(service.isAuthenticated()).toBe(true);
        });

      const req = httpMock.expectOne(`${baseUrl}/api/auth/login`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({
        email: "test@example.com",
        password: "password",
      });
      req.flush(mockLoginResponse);
    });

    it("should handle login error", () => {
      service.login({ email: "wrong@test.com", password: "wrong" }).subscribe({
        error: (error) => {
          expect(error.code).toBe("INVALID_CREDENTIALS");
          expect(service.isLoading()).toBe(false);
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/api/auth/login`);
      req.flush(
        { code: "INVALID_CREDENTIALS", message: "Invalid credentials" },
        { status: 401, statusText: "Unauthorized" },
      );
    });
  });

  describe("logout", () => {
    it("should clear tokens and redirect to login", () => {
      // Setup authenticated state
      localStorage.setItem("librafoto_token", "test-token");
      localStorage.setItem("librafoto_refresh_token", "test-refresh");
      localStorage.setItem("librafoto_user", JSON.stringify(mockUser));

      service.logout();

      expect(localStorage.getItem("librafoto_token")).toBeNull();
      expect(localStorage.getItem("librafoto_refresh_token")).toBeNull();
      expect(localStorage.getItem("librafoto_user")).toBeNull();
      expect(service.currentUser()).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
      expect(routerMock.navigate).toHaveBeenCalledWith(["/login"]);
    });
  });

  describe("refreshToken", () => {
    it("should refresh token when refresh token exists", () => {
      localStorage.setItem("librafoto_refresh_token", "old-refresh-token");

      service.refreshToken().subscribe((result) => {
        expect(result).toEqual(mockLoginResponse);
        expect(localStorage.getItem("librafoto_token")).toBe("test-token");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/auth/refresh`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({ refreshToken: "old-refresh-token" });
      req.flush(mockLoginResponse);
    });

    it("should return null when no refresh token exists", () => {
      service.refreshToken().subscribe((result) => {
        expect(result).toBeNull();
      });
    });

    it("should logout on refresh error", () => {
      localStorage.setItem("librafoto_refresh_token", "invalid-token");

      service.refreshToken().subscribe({
        error: () => {
          expect(routerMock.navigate).toHaveBeenCalledWith(["/login"]);
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/api/auth/refresh`);
      req.flush(
        { code: "INVALID_TOKEN" },
        { status: 401, statusText: "Unauthorized" },
      );
    });
  });

  describe("Token management", () => {
    it("should return token from storage", () => {
      localStorage.setItem("librafoto_token", "stored-token");
      expect(service.getToken()).toBe("stored-token");
    });

    it("should return null when no token exists", () => {
      expect(service.getToken()).toBeNull();
    });

    it("should return refresh token from storage", () => {
      localStorage.setItem("librafoto_refresh_token", "stored-refresh");
      expect(service.getRefreshToken()).toBe("stored-refresh");
    });

    it("should detect expired token", () => {
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() - 1000).toISOString(),
      );
      expect(service.isTokenExpired()).toBe(true);
    });

    it("should detect valid token", () => {
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 3600000).toISOString(),
      );
      expect(service.isTokenExpired()).toBe(false);
    });

    it("should consider token expired if within 30 seconds of expiry", () => {
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 20000).toISOString(),
      ); // 20 seconds
      expect(service.isTokenExpired()).toBe(true);
    });
  });

  describe("Role computations", () => {
    it("should return true for isAdmin when user is admin", () => {
      localStorage.setItem("librafoto_token", "token");
      localStorage.setItem(
        "librafoto_user",
        JSON.stringify({ ...mockUser, role: 2 }),
      );
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 3600000).toISOString(),
      );

      // Reset TestBed to get a fresh service instance that reads from localStorage
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          AuthService,
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: Router, useValue: routerMock },
        ],
      });

      const newService = TestBed.inject(AuthService);
      expect(newService.isAdmin()).toBe(true);
    });

    it("should return false for isAdmin when user is editor", () => {
      localStorage.setItem("librafoto_token", "token");
      localStorage.setItem(
        "librafoto_user",
        JSON.stringify({ ...mockUser, role: 1 }),
      );
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 3600000).toISOString(),
      );

      // Reset TestBed to get a fresh service instance
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          AuthService,
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: Router, useValue: routerMock },
        ],
      });

      const newService = TestBed.inject(AuthService);
      expect(newService.isAdmin()).toBe(false);
    });

    it("should return true for isEditor when user is editor or admin", () => {
      localStorage.setItem("librafoto_token", "token");
      localStorage.setItem(
        "librafoto_user",
        JSON.stringify({ ...mockUser, role: 1 }),
      );
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 3600000).toISOString(),
      );

      // Reset TestBed to get a fresh service instance
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          AuthService,
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: Router, useValue: routerMock },
        ],
      });

      const newService = TestBed.inject(AuthService);
      expect(newService.isEditor()).toBe(true);
    });
  });

  describe("User management", () => {
    it("should get paginated users", () => {
      const pagedResult: PagedResult<UserDto> = {
        data: [mockUser],
        pagination: { page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
      };

      service.getUsers(1, 20).subscribe((result) => {
        expect(result).toEqual(pagedResult);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/users`,
      );
      expect(req.request.params.get("page")).toBe("1");
      expect(req.request.params.get("pageSize")).toBe("20");
      req.flush(pagedResult);
    });

    it("should get a single user", () => {
      service.getUser(1).subscribe((result) => {
        expect(result).toEqual(mockUser);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/users/1`);
      req.flush(mockUser);
    });

    it("should create a user", () => {
      const createRequest = {
        email: "new@example.com",
        password: "pass",
        role: 1,
      };

      service.createUser(createRequest).subscribe((result) => {
        expect(result.email).toBe("new@example.com");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/users`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(createRequest);
      req.flush({ ...mockUser, email: "new@example.com" });
    });

    it("should update a user", () => {
      const updateRequest = { email: "updated@example.com" };

      service.updateUser(1, updateRequest).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/users/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush({ ...mockUser, email: "updated@example.com" });
    });

    it("should delete a user", () => {
      service.deleteUser(1).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/users/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);
    });
  });

  describe("Guest links", () => {
    const mockGuestLink: GuestLinkDto = {
      id: "1",
      name: "Test Link",
      createdAt: "2025-01-01T00:00:00Z",
      expiresAt: "2025-12-31T23:59:59Z",
      maxUploads: 10,
      currentUploads: 0,
      targetAlbumId: 1,
      targetAlbumName: "Test Album",
      createdByUserId: 1,
      createdByUsername: "testuser",
      isActive: true,
    };

    it("should get paginated guest links", () => {
      const pagedResult: PagedResult<GuestLinkDto> = {
        data: [mockGuestLink],
        pagination: { page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
      };

      service.getGuestLinks().subscribe((result) => {
        expect(result).toEqual(pagedResult);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/guest-links`,
      );
      req.flush(pagedResult);
    });

    it("should get a single guest link", () => {
      service.getGuestLink("1").subscribe((result) => {
        expect(result).toEqual(mockGuestLink);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/guest-links/1`);
      req.flush(mockGuestLink);
    });

    it("should create a guest link", () => {
      const createRequest = {
        name: "New Link",
        targetAlbumId: 1,
        maxUploads: 5,
      };

      service.createGuestLink(createRequest).subscribe((result) => {
        expect(result.name).toBe("New Link");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/guest-links`);
      expect(req.request.method).toBe("POST");
      req.flush({ ...mockGuestLink, name: "New Link" });
    });

    it("should delete a guest link", () => {
      service.deleteGuestLink("1").subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/guest-links/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);
    });

    it("should get guest link public info", () => {
      service.getGuestLinkInfo("abc123").subscribe((result) => {
        expect(result.name).toBe("Test Link");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/guest/abc123`);
      req.flush({
        name: "Test Link",
        targetAlbumName: null,
        isActive: true,
        remainingUploads: null,
        statusMessage: null,
      });
    });

    it("should validate a guest link", () => {
      service.validateGuestLink("abc123").subscribe((result) => {
        expect(result.isValid).toBe(true);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/guest/abc123/validate`);
      req.flush({ isValid: true, linkCode: "abc123" });
    });
  });

  describe("Initial state - load from storage", () => {
    it("should load user from storage if valid token exists", () => {
      // Clear and set storage before creating new TestBed
      localStorage.clear();
      localStorage.setItem("librafoto_token", "valid-token");
      localStorage.setItem("librafoto_user", JSON.stringify(mockUser));
      localStorage.setItem(
        "librafoto_token_expiry",
        new Date(Date.now() + 3600000).toISOString(),
      );

      // Reset TestBed to get a fresh service instance
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          AuthService,
          provideHttpClient(),
          provideHttpClientTesting(),
          { provide: Router, useValue: routerMock },
        ],
      });

      const newService = TestBed.inject(AuthService);
      expect(newService.currentUser()).toEqual(mockUser);
      expect(newService.isAuthenticated()).toBe(true);
    });
  });
});
