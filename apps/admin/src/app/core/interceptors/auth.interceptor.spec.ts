import { describe, it, expect, beforeEach, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
  HttpErrorResponse,
} from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { of, throwError } from "rxjs";
import { authInterceptor } from "./auth.interceptor";
import { AuthService } from "../services/auth.service";

describe("AuthInterceptor", () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: {
    getToken: ReturnType<typeof vi.fn>;
    getRefreshToken: ReturnType<typeof vi.fn>;
    refreshToken: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    authService = {
      getToken: vi.fn(),
      getRefreshToken: vi.fn(),
      refreshToken: vi.fn(),
      logout: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Token Attachment", () => {
    it("should attach authorization header when token is available", () => {
      const token = "test-jwt-token";
      authService.getToken.mockReturnValue(token);

      httpClient.get("/api/admin/users").subscribe();

      const req = httpMock.expectOne("/api/admin/users");
      expect(req.request.headers.get("Authorization")).toBe(`Bearer ${token}`);
      req.flush({});
    });

    it("should not attach authorization header when token is not available", () => {
      authService.getToken.mockReturnValue(null);

      httpClient.get("/api/admin/users").subscribe();

      const req = httpMock.expectOne("/api/admin/users");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });
  });

  describe("Skip URLs", () => {
    it("should not attach token for login endpoint", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient
        .post("/api/auth/login", { email: "test@test.com", password: "pass" })
        .subscribe();

      const req = httpMock.expectOne("/api/auth/login");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });

    it("should not attach token for setup complete endpoint", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient
        .post("/api/setup/complete", { email: "admin@test.com" })
        .subscribe();

      const req = httpMock.expectOne("/api/setup/complete");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });

    it("should not attach token for setup status endpoint", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient.get("/api/setup/status").subscribe();

      const req = httpMock.expectOne("/api/setup/status");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });

    it("should not attach token for refresh endpoint", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient
        .post("/api/auth/refresh", { refreshToken: "refresh-token" })
        .subscribe();

      const req = httpMock.expectOne("/api/auth/refresh");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });

    it("should not attach token for guest endpoints", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient.get("/api/guest/abc123").subscribe();

      const req = httpMock.expectOne("/api/guest/abc123");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });

    it("should not attach token for guest upload endpoints", () => {
      authService.getToken.mockReturnValue("test-token");

      httpClient.post("/api/guest/upload/abc123", new FormData()).subscribe();

      const req = httpMock.expectOne("/api/guest/upload/abc123");
      expect(req.request.headers.has("Authorization")).toBe(false);
      req.flush({});
    });
  });

  describe("Token Refresh on 401", () => {
    it("should attempt token refresh on 401 error", () => {
      const oldToken = "old-token";
      const newToken = "new-token";
      const refreshToken = "refresh-token";

      authService.getToken.mockReturnValue(oldToken);
      authService.getRefreshToken.mockReturnValue(refreshToken);
      authService.refreshToken.mockReturnValue(
        of({
          token: newToken,
          refreshToken: "new-refresh-token",
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
          user: { id: 1, email: "test@test.com", role: 2 },
        })
      );

      httpClient.get("/api/admin/users").subscribe({
        next: (data) => {
          expect(data).toEqual({ users: [] });
        },
      });

      // First request fails with 401
      const req1 = httpMock.expectOne("/api/admin/users");
      expect(req1.request.headers.get("Authorization")).toBe(
        `Bearer ${oldToken}`
      );
      req1.flush(
        { code: "UNAUTHORIZED" },
        { status: 401, statusText: "Unauthorized" }
      );

      // Retry with new token
      const req2 = httpMock.expectOne("/api/admin/users");
      expect(req2.request.headers.get("Authorization")).toBe(
        `Bearer ${newToken}`
      );
      req2.flush({ users: [] });

      expect(authService.refreshToken).toHaveBeenCalled();
    });

    it("should logout if refresh token is not available", () => {
      authService.getToken.mockReturnValue("old-token");
      authService.getRefreshToken.mockReturnValue(null);

      httpClient.get("/api/admin/users").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(401);
        },
      });

      const req = httpMock.expectOne("/api/admin/users");
      req.flush(
        { code: "UNAUTHORIZED" },
        { status: 401, statusText: "Unauthorized" }
      );

      expect(authService.logout).toHaveBeenCalled();
    });

    it("should logout if token refresh fails", () => {
      authService.getToken.mockReturnValue("old-token");
      authService.getRefreshToken.mockReturnValue("refresh-token");
      authService.refreshToken.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 401 }))
      );

      httpClient.get("/api/admin/users").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(401);
        },
      });

      const req = httpMock.expectOne("/api/admin/users");
      req.flush(
        { code: "UNAUTHORIZED" },
        { status: 401, statusText: "Unauthorized" }
      );

      expect(authService.logout).toHaveBeenCalled();
    });

    it("should logout if refresh returns null response", () => {
      authService.getToken.mockReturnValue("old-token");
      authService.getRefreshToken.mockReturnValue("refresh-token");
      authService.refreshToken.mockReturnValue(of(null as any));

      httpClient.get("/api/admin/users").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(401);
        },
      });

      const req = httpMock.expectOne("/api/admin/users");
      req.flush(
        { code: "UNAUTHORIZED" },
        { status: 401, statusText: "Unauthorized" }
      );

      expect(authService.logout).toHaveBeenCalled();
    });

    it("should not attempt refresh for refresh endpoint 401", () => {
      authService.getToken.mockReturnValue(null);

      httpClient
        .post("/api/auth/refresh", { refreshToken: "invalid" })
        .subscribe({
          error: (error: HttpErrorResponse) => {
            expect(error.status).toBe(401);
          },
        });

      const req = httpMock.expectOne("/api/auth/refresh");
      req.flush(
        { code: "INVALID_TOKEN" },
        { status: 401, statusText: "Unauthorized" }
      );

      expect(authService.refreshToken).not.toHaveBeenCalled();
    });
  });

  describe("Non-401 Errors", () => {
    it("should not attempt refresh for 403 error", () => {
      authService.getToken.mockReturnValue("valid-token");

      httpClient.get("/api/admin/users").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(403);
        },
      });

      const req = httpMock.expectOne("/api/admin/users");
      req.flush(
        { code: "FORBIDDEN" },
        { status: 403, statusText: "Forbidden" }
      );

      expect(authService.refreshToken).not.toHaveBeenCalled();
      expect(authService.logout).not.toHaveBeenCalled();
    });

    it("should not attempt refresh for 404 error", () => {
      authService.getToken.mockReturnValue("valid-token");

      httpClient.get("/api/admin/users/999").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(404);
        },
      });

      const req = httpMock.expectOne("/api/admin/users/999");
      req.flush(
        { code: "NOT_FOUND" },
        { status: 404, statusText: "Not Found" }
      );

      expect(authService.refreshToken).not.toHaveBeenCalled();
    });

    it("should not attempt refresh for 500 error", () => {
      authService.getToken.mockReturnValue("valid-token");

      httpClient.get("/api/admin/users").subscribe({
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(500);
        },
      });

      const req = httpMock.expectOne("/api/admin/users");
      req.flush(
        { code: "INTERNAL_ERROR" },
        { status: 500, statusText: "Internal Server Error" }
      );

      expect(authService.refreshToken).not.toHaveBeenCalled();
    });
  });

  describe("Successful Requests", () => {
    it("should pass through successful requests unchanged", () => {
      const mockData = { users: [{ id: 1, email: "test@test.com" }] };
      authService.getToken.mockReturnValue("valid-token");

      httpClient.get("/api/admin/users").subscribe((data) => {
        expect(data).toEqual(mockData);
      });

      const req = httpMock.expectOne("/api/admin/users");
      expect(req.request.headers.get("Authorization")).toBe(
        "Bearer valid-token"
      );
      req.flush(mockData);

      expect(authService.refreshToken).not.toHaveBeenCalled();
    });
  });
});
