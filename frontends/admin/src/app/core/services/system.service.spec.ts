import { TestBed } from "@angular/core/testing";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { provideHttpClient } from "@angular/common/http";
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { SystemService } from "./system.service";
import { environment } from "@environments/environment";

describe("SystemService", () => {
  let service: SystemService;
  let httpMock: HttpTestingController;
  const baseUrl = environment.apiBaseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), SystemService],
    });

    service = TestBed.inject(SystemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("getSystemInfo", () => {
    it("should make GET request to /api/admin/system/info", () => {
      const mockResponse = {
        version: "1.0.0",
        commitHash: "abc1234",
        updateAvailable: false,
        latestVersion: null,
        commitsBehind: 0,
        changelog: null,
        uptime: "01:00:00",
        isDocker: false,
        environment: "Development",
      };

      service.getSystemInfo().subscribe((result) => {
        expect(result).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
      expect(req.request.method).toBe("GET");
      req.flush(mockResponse);
    });
  });

  describe("checkForUpdates", () => {
    it("should make GET request to /api/admin/system/updates", () => {
      const mockResponse = {
        updateAvailable: true,
        currentVersion: "1.0.0",
        latestVersion: "1.1.0",
        commitsBehind: 5,
        changelog: ["New feature"],
        error: null,
        checkedAt: "2026-01-31T12:00:00Z",
      };

      service.checkForUpdates().subscribe((result) => {
        expect(result).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/system/updates`);
      expect(req.request.method).toBe("GET");
      req.flush(mockResponse);
    });
  });

  describe("forceUpdateCheck", () => {
    it("should make POST request to /api/admin/system/updates/check", () => {
      const mockResponse = {
        updateAvailable: false,
        currentVersion: "1.0.0",
        latestVersion: "1.0.0",
        commitsBehind: 0,
        changelog: [],
        error: null,
        checkedAt: "2026-01-31T12:00:00Z",
      };

      service.forceUpdateCheck().subscribe((result) => {
        expect(result).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/system/updates/check`);
      expect(req.request.method).toBe("POST");
      req.flush(mockResponse);
    });
  });

  describe("applyUpdate", () => {
    it("should make POST request to /api/admin/system/update", () => {
      const mockResponse = {
        message: "Update triggered successfully",
        estimatedDowntimeSeconds: 30,
      };

      service.applyUpdate().subscribe((result) => {
        expect(result).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/system/update`);
      expect(req.request.method).toBe("POST");
      req.flush(mockResponse);
    });
  });
});
