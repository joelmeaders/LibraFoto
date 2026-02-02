import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { CacheService, CacheStatus, CachedFileDto } from "./cache.service";
import { environment } from "@environments/environment";

describe("CacheService", () => {
  let service: CacheService;
  let httpMock: HttpTestingController;
  const baseUrl = environment.apiBaseUrl;

  const mockCacheStatus: CacheStatus = {
    totalSizeBytes: 1048576, // 1MB
    fileCount: 10,
    maxSizeBytes: 104857600, // 100MB
    usagePercent: 1.0,
  };

  const mockCachedFile: CachedFileDto = {
    fileHash: "abc123hash",
    originalUrl: "https://example.com/photo.jpg",
    providerId: 1,
    providerName: "OneDrive",
    fileSizeBytes: 102400,
    contentType: "image/jpeg",
    cachedDate: "2025-01-01T00:00:00Z",
    lastAccessedDate: "2025-01-10T00:00:00Z",
    accessCount: 5,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CacheService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(CacheService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("getCacheStatus", () => {
    it("should get cache status", () => {
      service.getCacheStatus().subscribe((result) => {
        expect(result).toEqual(mockCacheStatus);
        expect(result.usagePercent).toBe(1.0);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/status`);
      expect(req.request.method).toBe("GET");
      req.flush(mockCacheStatus);
    });

    it("should handle empty cache status", () => {
      const emptyStatus: CacheStatus = {
        totalSizeBytes: 0,
        fileCount: 0,
        maxSizeBytes: 104857600,
        usagePercent: 0,
      };

      service.getCacheStatus().subscribe((result) => {
        expect(result.fileCount).toBe(0);
        expect(result.totalSizeBytes).toBe(0);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/status`);
      req.flush(emptyStatus);
    });
  });

  describe("getCachedFiles", () => {
    it("should get paginated cached files with default params", () => {
      const pagedResult = {
        data: [mockCachedFile],
        pagination: {
          page: 1,
          pageSize: 50,
          totalItems: 1,
          totalPages: 1,
        },
      };

      service.getCachedFiles().subscribe((result) => {
        expect(result.data).toHaveLength(1);
        expect(result.data[0]).toEqual(mockCachedFile);
        expect(result.pagination.pageSize).toBe(50);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/cache/files`
      );
      expect(req.request.params.get("page")).toBe("1");
      expect(req.request.params.get("pageSize")).toBe("50");
      req.flush(pagedResult);
    });

    it("should get paginated cached files with custom params", () => {
      const pagedResult = {
        data: [mockCachedFile],
        pagination: {
          page: 2,
          pageSize: 25,
          totalItems: 50,
          totalPages: 2,
        },
      };

      service.getCachedFiles(2, 25).subscribe((result) => {
        expect(result.pagination.page).toBe(2);
        expect(result.pagination.pageSize).toBe(25);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/cache/files`
      );
      expect(req.request.params.get("page")).toBe("2");
      expect(req.request.params.get("pageSize")).toBe("25");
      req.flush(pagedResult);
    });

    it("should handle empty cached files list", () => {
      const emptyResult = {
        data: [],
        pagination: {
          page: 1,
          pageSize: 50,
          totalItems: 0,
          totalPages: 0,
        },
      };

      service.getCachedFiles().subscribe((result) => {
        expect(result.data).toHaveLength(0);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/cache/files`
      );
      req.flush(emptyResult);
    });
  });

  describe("clearCache", () => {
    it("should clear cache", () => {
      const response = { message: "Cache cleared successfully" };

      service.clearCache().subscribe((result) => {
        expect(result.message).toBe("Cache cleared successfully");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/clear`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toBeNull();
      req.flush(response);
    });

    it("should handle clear cache error", () => {
      service.clearCache().subscribe({
        error: (error) => {
          expect(error.code).toBe("CACHE_ERROR");
          expect(error.message).toBe("Failed to clear cache");
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/clear`);
      req.flush(
        { code: "CACHE_ERROR", message: "Failed to clear cache" },
        { status: 500, statusText: "Internal Server Error" }
      );
    });
  });

  describe("triggerEviction", () => {
    it("should trigger cache eviction", () => {
      const response = { filesEvicted: 5 };

      service.triggerEviction().subscribe((result) => {
        expect(result.filesEvicted).toBe(5);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/evict`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toBeNull();
      req.flush(response);
    });

    it("should handle zero files evicted", () => {
      const response = { filesEvicted: 0 };

      service.triggerEviction().subscribe((result) => {
        expect(result.filesEvicted).toBe(0);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/cache/evict`);
      req.flush(response);
    });
  });

  describe("deleteCachedFile", () => {
    it("should delete cached file by hash", () => {
      const fileHash = "abc123hash";

      service.deleteCachedFile(fileHash).subscribe();

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/cache/files/${fileHash}`
      );
      expect(req.request.method).toBe("DELETE");
      req.flush(null);
    });

    it("should handle delete cached file not found", () => {
      const fileHash = "nonexistent";

      service.deleteCachedFile(fileHash).subscribe({
        error: (error) => {
          expect(error.code).toBe("NOT_FOUND");
          expect(error.message).toBe("Cached file not found");
        },
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/cache/files/${fileHash}`
      );
      req.flush(
        { code: "NOT_FOUND", message: "Cached file not found" },
        { status: 404, statusText: "Not Found" }
      );
    });

    it("should handle delete cached file with special characters", () => {
      const fileHash = "abc/123+hash=";

      service.deleteCachedFile(fileHash).subscribe();

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/cache/files/${fileHash}`
      );
      expect(req.request.method).toBe("DELETE");
      req.flush(null);
    });
  });
});
