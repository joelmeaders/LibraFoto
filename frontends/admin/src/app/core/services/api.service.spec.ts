import { TestBed } from "@angular/core/testing";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { provideHttpClient } from "@angular/common/http";
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { ApiService } from "./api.service";
import { environment } from "@environments/environment";

describe("ApiService", () => {
  let service: ApiService;
  let httpMock: HttpTestingController;
  const baseUrl = environment.apiBaseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ApiService],
    });

    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("GET requests", () => {
    it("should make a GET request to the correct URL", () => {
      const testData = { id: 1, name: "test" };

      service.get<typeof testData>("/test").subscribe((result) => {
        expect(result).toEqual(testData);
      });

      const req = httpMock.expectOne(`${baseUrl}/test`);
      expect(req.request.method).toBe("GET");
      req.flush(testData);
    });

    it("should include query params when provided", () => {
      service.get<unknown>("/test", { page: 1, search: "query" }).subscribe();

      const req = httpMock.expectOne((r) => r.url === `${baseUrl}/test`);
      expect(req.request.params.get("page")).toBe("1");
      expect(req.request.params.get("search")).toBe("query");
      req.flush({});
    });

    it("should handle Date params as ISO strings", () => {
      const testDate = new Date("2025-01-15T12:00:00Z");

      service.get<unknown>("/test", { date: testDate }).subscribe();

      const req = httpMock.expectOne((r) => r.url === `${baseUrl}/test`);
      expect(req.request.params.get("date")).toBe(testDate.toISOString());
      req.flush({});
    });

    it("should handle array params by appending each value", () => {
      service
        .get<unknown>("/test", { tags: ["tag1", "tag2", "tag3"] })
        .subscribe();

      const req = httpMock.expectOne((r) => r.url === `${baseUrl}/test`);
      expect(req.request.params.getAll("tags")).toEqual([
        "tag1",
        "tag2",
        "tag3",
      ]);
      req.flush({});
    });

    it("should filter out null and undefined params", () => {
      service
        .get<unknown>("/test", {
          valid: "value",
          nullVal: null,
          undefinedVal: undefined,
          emptyStr: "",
        })
        .subscribe();

      const req = httpMock.expectOne((r) => r.url === `${baseUrl}/test`);
      expect(req.request.params.get("valid")).toBe("value");
      expect(req.request.params.has("nullVal")).toBe(false);
      expect(req.request.params.has("undefinedVal")).toBe(false);
      expect(req.request.params.has("emptyStr")).toBe(false);
      req.flush({});
    });
  });

  describe("POST requests", () => {
    it("should make a POST request with body", () => {
      const requestBody = { name: "test", value: 123 };
      const responseData = { id: 1, ...requestBody };

      service
        .post<typeof responseData>("/test", requestBody)
        .subscribe((result) => {
          expect(result).toEqual(responseData);
        });

      const req = httpMock.expectOne(`${baseUrl}/test`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(requestBody);
      req.flush(responseData);
    });

    it("should handle POST without body", () => {
      service.post<unknown>("/test").subscribe();

      const req = httpMock.expectOne(`${baseUrl}/test`);
      expect(req.request.method).toBe("POST");
      // HttpClient sends null when body is undefined
      expect(req.request.body).toBeNull();
      req.flush({});
    });
  });

  describe("PUT requests", () => {
    it("should make a PUT request with body", () => {
      const requestBody = { name: "updated" };
      const responseData = { id: 1, name: "updated" };

      service
        .put<typeof responseData>("/test/1", requestBody)
        .subscribe((result) => {
          expect(result).toEqual(responseData);
        });

      const req = httpMock.expectOne(`${baseUrl}/test/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(requestBody);
      req.flush(responseData);
    });
  });

  describe("PATCH requests", () => {
    it("should make a PATCH request with body", () => {
      const requestBody = { name: "patched" };
      const responseData = { id: 1, name: "patched" };

      service
        .patch<typeof responseData>("/test/1", requestBody)
        .subscribe((result) => {
          expect(result).toEqual(responseData);
        });

      const req = httpMock.expectOne(`${baseUrl}/test/1`);
      expect(req.request.method).toBe("PATCH");
      expect(req.request.body).toEqual(requestBody);
      req.flush(responseData);
    });
  });

  describe("DELETE requests", () => {
    it("should make a DELETE request", () => {
      service.delete<void>("/test/1").subscribe();

      const req = httpMock.expectOne(`${baseUrl}/test/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);
    });
  });

  describe("uploadFile", () => {
    it("should upload FormData via POST", () => {
      const formData = new FormData();
      formData.append("file", new Blob(["test"]), "test.txt");
      const responseData = { id: 1, filename: "test.txt" };

      service
        .uploadFile<typeof responseData>("/upload", formData)
        .subscribe((result) => {
          expect(result).toEqual(responseData);
        });

      const req = httpMock.expectOne(`${baseUrl}/upload`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toBe(formData);
      req.flush(responseData);
    });
  });

  describe("Error handling", () => {
    it("should handle server error with ApiError format", () => {
      const apiError = { code: "NOT_FOUND", message: "Resource not found" };

      service.get<unknown>("/test").subscribe({
        error: (error) => {
          expect(error).toEqual(apiError);
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/test`);
      req.flush(apiError, { status: 404, statusText: "Not Found" });
    });

    it("should handle server error without ApiError format", () => {
      service.get<unknown>("/test").subscribe({
        error: (error) => {
          expect(error.code).toBe("HTTP_500");
          expect(error.message).toBeDefined();
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/test`);
      req.flush("Server error", {
        status: 500,
        statusText: "Internal Server Error",
      });
    });

    it("should handle client-side network errors", () => {
      service.get<unknown>("/test").subscribe({
        error: (error) => {
          // Network errors generate HTTP_0 code
          expect(error.code).toBe("HTTP_0");
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/test`);
      req.error(new ProgressEvent("error"), {
        status: 0,
        statusText: "Unknown Error",
      });
    });
  });
});
