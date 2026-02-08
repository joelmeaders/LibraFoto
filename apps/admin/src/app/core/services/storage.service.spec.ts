import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { StorageService } from "./storage.service";
import {
  StorageProviderDto,
  StorageProviderType,
  SyncResult,
  ScanResult,
  SyncStatus,
} from "../models";

describe("StorageService", () => {
  let service: StorageService;
  let httpMock: HttpTestingController;
  const baseUrl = "";

  const mockProvider: StorageProviderDto = {
    id: 1,
    type: StorageProviderType.Local,
    name: "Local Storage",
    isEnabled: true,
    supportsUpload: true,
    supportsWatch: true,
    lastSyncDate: "2025-01-01T00:00:00Z",
    photoCount: 100,
    isConnected: true,
    statusMessage: null,
  };

  const mockSyncResult: SyncResult = {
    providerId: 1,
    providerName: "Local Storage",
    success: true,
    errorMessage: null,
    filesAdded: 10,
    filesUpdated: 5,
    filesRemoved: 2,
    filesSkipped: 0,
    totalFilesProcessed: 17,
    totalFilesFound: 100,
    startTime: "2025-01-01T00:00:00Z",
    endTime: "2025-01-01T00:01:00Z",
    errors: [],
  };

  const mockScanResult: ScanResult = {
    providerId: 1,
    success: true,
    errorMessage: null,
    totalFilesFound: 50,
    newFilesCount: 10,
    existingFilesCount: 40,
    newFilesTotalSize: 1024000,
    sampleNewFiles: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        StorageService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(StorageService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Initial state", () => {
    it("should start with empty providers array", () => {
      expect(service.providers()).toEqual([]);
    });

    it("should start with no selected provider", () => {
      expect(service.selectedProvider()).toBeNull();
    });

    it("should start with isLoading false", () => {
      expect(service.isLoading()).toBe(false);
    });

    it("should start with empty sync status map", () => {
      expect(service.syncStatus().size).toBe(0);
    });
  });

  describe("getProviders", () => {
    it("should fetch all providers", () => {
      const providers = [mockProvider];

      service.getProviders().subscribe((result) => {
        expect(result).toEqual(providers);
        expect(service.providers()).toEqual(providers);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/storage/providers`);
      expect(req.request.method).toBe("GET");
      req.flush(providers);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getProviders().subscribe();
      expect(service.isLoading()).toBe(true);

      httpMock.expectOne(`${baseUrl}/api/admin/storage/providers`).flush([]);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getProvider", () => {
    it("should fetch a single provider and set selectedProvider", () => {
      service.getProvider(1).subscribe((result) => {
        expect(result).toEqual(mockProvider);
        expect(service.selectedProvider()).toEqual(mockProvider);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/storage/providers/1`,
      );
      expect(req.request.method).toBe("GET");
      req.flush(mockProvider);
    });
  });

  describe("createProvider", () => {
    it("should create provider and add to providers list", () => {
      const createRequest = {
        name: "Google Photos",
        type: StorageProviderType.GooglePhotos,
      };
      const createdProvider = {
        ...mockProvider,
        id: 2,
        name: "Google Photos",
        type: StorageProviderType.GooglePhotos,
      };

      service.createProvider(createRequest).subscribe((result) => {
        expect(result).toEqual(createdProvider);
        expect(service.providers()).toContainEqual(createdProvider);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/storage/providers`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(createRequest);
      req.flush(createdProvider);
    });
  });

  describe("updateProvider", () => {
    it("should update provider and update providers list", () => {
      // First load providers
      service.getProviders().subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers`)
        .flush([mockProvider]);

      const updateRequest = { name: "Updated Storage", isEnabled: false };
      const updatedProvider = {
        ...mockProvider,
        name: "Updated Storage",
        isEnabled: false,
      };

      service.updateProvider(1, updateRequest).subscribe((result) => {
        expect(result).toEqual(updatedProvider);
        expect(service.providers().find((p) => p.id === 1)?.name).toBe(
          "Updated Storage",
        );
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/storage/providers/1`,
      );
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush(updatedProvider);
    });

    it("should update selectedProvider if it matches", () => {
      // First select the provider
      service.getProvider(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers/1`)
        .flush(mockProvider);

      const updateRequest = { name: "Updated Storage" };
      const updatedProvider = { ...mockProvider, name: "Updated Storage" };

      service.updateProvider(1, updateRequest).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers/1`)
        .flush(updatedProvider);

      expect(service.selectedProvider()?.name).toBe("Updated Storage");
    });
  });

  describe("deleteProvider", () => {
    it("should delete provider and remove from providers list", () => {
      // First load providers
      service.getProviders().subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers`)
        .flush([mockProvider]);

      expect(service.providers().length).toBe(1);

      service.deleteProvider(1).subscribe();

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/storage/providers/1`,
      );
      expect(req.request.method).toBe("DELETE");
      req.flush(null);

      expect(service.providers().length).toBe(0);
    });

    it("should clear selectedProvider if deleted provider was selected", () => {
      // First select a provider
      service.getProvider(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers/1`)
        .flush(mockProvider);

      expect(service.selectedProvider()).not.toBeNull();

      service.deleteProvider(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers/1`)
        .flush(null);

      expect(service.selectedProvider()).toBeNull();
    });
  });

  describe("syncProvider", () => {
    it("should start sync without request body", () => {
      service.syncProvider(1).subscribe((result) => {
        expect(result).toEqual(mockSyncResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/storage/sync/1`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({});
      req.flush(mockSyncResult);
    });

    it("should start sync with request options", () => {
      const syncRequest = { fullSync: true, deleteRemoved: true };

      service.syncProvider(1, syncRequest).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/storage/sync/1`);
      expect(req.request.body).toEqual(syncRequest);
      req.flush(mockSyncResult);
    });
  });

  describe("getSyncStatus", () => {
    it("should get sync status and cache it", () => {
      const syncStatus: SyncStatus = {
        providerId: 1,
        isInProgress: true,
        progressPercent: 50,
        currentOperation: "Scanning photo.jpg",
        filesProcessed: 25,
        totalFiles: 50,
        startTime: "2025-01-01T00:00:00Z",
        lastSyncResult: null,
      };

      service.getSyncStatus(1).subscribe((result) => {
        expect(result).toEqual(syncStatus);
        expect(service.getCachedSyncStatus(1)).toEqual(syncStatus);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/storage/sync/1/status`,
      );
      expect(req.request.method).toBe("GET");
      req.flush(syncStatus);
    });
  });

  describe("scanProvider", () => {
    it("should scan provider for files", () => {
      service.scanProvider(1).subscribe((result) => {
        expect(result).toEqual(mockScanResult);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/storage/sync/1/scan`,
      );
      expect(req.request.method).toBe("GET");
      req.flush(mockScanResult);
    });
  });

  describe("uploadFile", () => {
    it("should upload a single file", () => {
      const file = new File(["test content"], "test.jpg", {
        type: "image/jpeg",
      });
      const uploadResult = { id: 1, filename: "test.jpg", success: true };

      service.uploadFile(file).subscribe((result) => {
        expect(result).toEqual(uploadResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/upload`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body instanceof FormData).toBe(true);
      req.flush(uploadResult);
    });

    it("should upload file with options", () => {
      const file = new File(["test content"], "test.jpg", {
        type: "image/jpeg",
      });
      const options = {
        albumId: 1,
        tags: ["nature", "landscape"],
        customFilename: "custom.jpg",
        overwrite: true,
      };

      service.uploadFile(file, options).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/upload`);
      const formData = req.request.body as FormData;
      expect(formData.get("albumId")).toBe("1");
      expect(formData.getAll("tags")).toEqual(["nature", "landscape"]);
      expect(formData.get("customFilename")).toBe("custom.jpg");
      expect(formData.get("overwrite")).toBe("true");
      req.flush({ success: true });
    });
  });

  describe("uploadFiles", () => {
    it("should upload multiple files", () => {
      const files = [
        new File(["test1"], "test1.jpg", { type: "image/jpeg" }),
        new File(["test2"], "test2.jpg", { type: "image/jpeg" }),
      ];
      const batchResult = { successCount: 2, failedCount: 0, results: [] };

      service.uploadFiles(files).subscribe((result) => {
        expect(result).toEqual(batchResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/upload/batch`);
      expect(req.request.method).toBe("POST");
      const formData = req.request.body as FormData;
      expect(formData.getAll("files").length).toBe(2);
      req.flush(batchResult);
    });

    it("should upload files with options", () => {
      const files = [new File(["test"], "test.jpg", { type: "image/jpeg" })];
      const options = { albumId: 2, tags: ["vacation"], overwrite: false };

      service.uploadFiles(files, options).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/upload/batch`);
      const formData = req.request.body as FormData;
      expect(formData.get("albumId")).toBe("2");
      expect(formData.getAll("tags")).toEqual(["vacation"]);
      req.flush({ successCount: 1 });
    });
  });

  describe("guestUpload", () => {
    it("should upload file via guest link", () => {
      const file = new File(["test"], "test.jpg", { type: "image/jpeg" });
      const uploadResult = { id: 1, filename: "test.jpg", success: true };

      service.guestUpload(file, "abc123").subscribe((result) => {
        expect(result).toEqual(uploadResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/guest/upload/abc123`);
      expect(req.request.method).toBe("POST");
      const formData = req.request.body as FormData;
      expect(formData.get("linkCode")).toBe("abc123");
      req.flush(uploadResult);
    });

    it("should include message if provided", () => {
      const file = new File(["test"], "test.jpg", { type: "image/jpeg" });

      service.guestUpload(file, "abc123", "Hello from guest!").subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/guest/upload/abc123`);
      const formData = req.request.body as FormData;
      expect(formData.get("message")).toBe("Hello from guest!");
      req.flush({ success: true });
    });
  });

  describe("clearSelectedProvider", () => {
    it("should clear the selected provider", () => {
      // First select a provider
      service.getProvider(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/providers/1`)
        .flush(mockProvider);

      expect(service.selectedProvider()).not.toBeNull();

      service.clearSelectedProvider();
      expect(service.selectedProvider()).toBeNull();
    });
  });

  describe("getCachedSyncStatus", () => {
    it("should return undefined for uncached provider", () => {
      expect(service.getCachedSyncStatus(999)).toBeUndefined();
    });

    it("should return cached sync status", () => {
      const syncStatus: SyncStatus = {
        providerId: 1,
        isInProgress: false,
        progressPercent: 100,
        currentOperation: null,
        filesProcessed: 50,
        totalFiles: 50,
        startTime: null,
        lastSyncResult: null,
      };

      service.getSyncStatus(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/storage/sync/1/status`)
        .flush(syncStatus);

      expect(service.getCachedSyncStatus(1)).toEqual(syncStatus);
    });
  });
});
