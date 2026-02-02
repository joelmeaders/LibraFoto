import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { PhotoService } from "./photo.service";
import { environment } from "@environments/environment";
import {
  PhotoListDto,
  PhotoDetailDto,
  PagedResult,
  MediaType,
} from "../models";

describe("PhotoService", () => {
  let service: PhotoService;
  let httpMock: HttpTestingController;
  const baseUrl = environment.apiBaseUrl;

  const mockPhotoList: PhotoListDto = {
    id: 1,
    filename: "test.jpg",
    thumbnailPath: "thumb/test.jpg",
    mediaType: MediaType.Photo,
    dateTaken: "2025-01-01T12:00:00Z",
    dateAdded: "2025-01-01T12:00:00Z",
    width: 1920,
    height: 1080,
    location: null,
    albumCount: 1,
    tagCount: 1,
  };

  const mockPhotoDetail: PhotoDetailDto = {
    id: 1,
    filename: "test.jpg",
    originalFilename: "IMG_001.jpg",
    filePath: "photos/2025/01/test.jpg",
    thumbnailPath: "thumb/test.jpg",
    width: 1920,
    height: 1080,
    fileSize: 1024000,
    mediaType: MediaType.Photo,
    duration: null,
    dateTaken: "2025-01-01T12:00:00Z",
    dateAdded: "2025-01-01T12:00:00Z",
    location: "Test Location",
    latitude: null,
    longitude: null,
    providerId: null,
    providerName: null,
    albums: [{ id: 1, name: "Test Album" }],
    tags: [{ id: 1, name: "nature", color: "#4CAF50" }],
  };

  const mockPagedResult: PagedResult<PhotoListDto> = {
    data: [mockPhotoList],
    pagination: { page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PhotoService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(PhotoService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Initial state", () => {
    it("should start with empty photos array", () => {
      expect(service.photos()).toEqual([]);
    });

    it("should start with no selected photo", () => {
      expect(service.selectedPhoto()).toBeNull();
    });

    it("should start with no pagination", () => {
      expect(service.pagination()).toBeNull();
    });

    it("should start with isLoading false", () => {
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getPhotos", () => {
    it("should fetch photos without filters", () => {
      service.getPhotos().subscribe((result) => {
        expect(result).toEqual(mockPagedResult);
        expect(service.photos()).toEqual([mockPhotoList]);
        expect(service.pagination()).toEqual(mockPagedResult.pagination);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos`);
      expect(req.request.method).toBe("GET");
      req.flush(mockPagedResult);
    });

    it("should fetch photos with filters", () => {
      const filter = { page: 2, pageSize: 10, albumId: 1, search: "test" };

      service.getPhotos(filter).subscribe();

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/photos`
      );
      expect(req.request.params.get("page")).toBe("2");
      expect(req.request.params.get("pageSize")).toBe("10");
      expect(req.request.params.get("albumId")).toBe("1");
      expect(req.request.params.get("search")).toBe("test");
      req.flush(mockPagedResult);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getPhotos().subscribe();
      expect(service.isLoading()).toBe(true);

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos`);
      req.flush(mockPagedResult);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getPhoto", () => {
    it("should fetch a single photo and set selectedPhoto", () => {
      service.getPhoto(1).subscribe((result) => {
        expect(result).toEqual(mockPhotoDetail);
        expect(service.selectedPhoto()).toEqual(mockPhotoDetail);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos/1`);
      expect(req.request.method).toBe("GET");
      req.flush(mockPhotoDetail);
    });
  });

  describe("updatePhoto", () => {
    it("should update photo and set selectedPhoto", () => {
      const updateRequest = { location: "Updated location" };
      const updatedPhoto = { ...mockPhotoDetail, location: "Updated location" };

      service.updatePhoto(1, updateRequest).subscribe((result) => {
        expect(result).toEqual(updatedPhoto);
        expect(service.selectedPhoto()).toEqual(updatedPhoto);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush(updatedPhoto);
    });
  });

  describe("deletePhoto", () => {
    it("should delete photo and remove from photos list", () => {
      // First load photos
      service.getPhotos().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/photos`).flush(mockPagedResult);

      expect(service.photos().length).toBe(1);

      // Delete the photo
      service.deletePhoto(1).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);

      expect(service.photos().length).toBe(0);
    });

    it("should clear selectedPhoto if deleted photo was selected", () => {
      // First select a photo
      service.getPhoto(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/photos/1`)
        .flush(mockPhotoDetail);

      expect(service.selectedPhoto()).not.toBeNull();

      // Delete the selected photo
      service.deletePhoto(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/photos/1`).flush(null);

      expect(service.selectedPhoto()).toBeNull();
    });
  });

  describe("deletePhotos", () => {
    it("should delete multiple photos", () => {
      const photoIds = [1, 2, 3];
      const bulkResult = { successCount: 3, failedCount: 0, errors: [] };

      service.deletePhotos(photoIds).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos/bulk/delete`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({ photoIds });
      req.flush(bulkResult);
    });

    it("should remove deleted photos from photos list", () => {
      // First load photos with multiple items
      const multiplePhotos = {
        data: [
          { ...mockPhotoList, id: 1 },
          { ...mockPhotoList, id: 2 },
          { ...mockPhotoList, id: 3 },
        ],
        pagination: { page: 1, pageSize: 20, totalItems: 3, totalPages: 1 },
      };
      service.getPhotos().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/photos`).flush(multiplePhotos);

      expect(service.photos().length).toBe(3);

      // Delete photos 1 and 2
      service.deletePhotos([1, 2]).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/photos/bulk/delete`)
        .flush({ successCount: 2, failedCount: 0 });

      expect(service.photos().length).toBe(1);
      expect(service.photos()[0].id).toBe(3);
    });
  });

  describe("addTagsToPhotos", () => {
    it("should add tags to photos", () => {
      const request = { photoIds: [1, 2], tagIds: [1, 2] };
      const bulkResult = { successCount: 2, failedCount: 0, errors: [] };

      service.addTagsToPhotos(request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/photos/bulk/add-tags`
      );
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("removeTagsFromPhotos", () => {
    it("should remove tags from photos", () => {
      const request = { photoIds: [1, 2], tagIds: [1] };
      const bulkResult = { successCount: 2, failedCount: 0, errors: [] };

      service.removeTagsFromPhotos(request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/photos/bulk/remove-tags`
      );
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("getPhotoCount", () => {
    it("should get photo count", () => {
      service.getPhotoCount().subscribe((result) => {
        expect(result.count).toBe(42);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/photos/count`);
      expect(req.request.method).toBe("GET");
      req.flush({ count: 42 });
    });
  });

  describe("clearSelectedPhoto", () => {
    it("should clear the selected photo", () => {
      // First select a photo
      service.getPhoto(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/admin/photos/1`)
        .flush(mockPhotoDetail);

      expect(service.selectedPhoto()).not.toBeNull();

      service.clearSelectedPhoto();
      expect(service.selectedPhoto()).toBeNull();
    });
  });

  describe("URL helpers", () => {
    it("should generate thumbnail URL when thumbnailPath exists", () => {
      const url = service.getThumbnailUrl(mockPhotoList);
      expect(url).toBe(`${baseUrl}/api/media/thumbnails/${mockPhotoList.id}`);
    });

    it("should return placeholder when photo has no ID", () => {
      const photoWithoutId: PhotoListDto = {
        ...mockPhotoList,
        id: 0,
      };
      const url = service.getThumbnailUrl(photoWithoutId);
      expect(url).toBe("/assets/placeholder.png");
    });

    it("should generate photo URL from filePath", () => {
      const url = service.getPhotoUrl(mockPhotoDetail);
      expect(url).toBe(`${baseUrl}/api/media/photos/photos/2025/01/test.jpg`);
    });
  });

  describe("refreshThumbnails", () => {
    it("should refresh thumbnails for specified photos", () => {
      const photoIds = [1, 2, 3];
      const mockResult = {
        succeeded: 3,
        failed: 0,
        errors: [],
      };

      service.refreshThumbnails(photoIds).subscribe((result) => {
        expect(result.succeeded).toBe(3);
        expect(result.failed).toBe(0);
        expect(result.errors).toEqual([]);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/media/thumbnails/refresh`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({ photoIds });
      req.flush(mockResult);
    });

    it("should handle partial failures", () => {
      const photoIds = [1, 2, 3];
      const mockResult = {
        succeeded: 2,
        failed: 1,
        errors: ["Photo 3: Source file not found"],
      };

      service.refreshThumbnails(photoIds).subscribe((result) => {
        expect(result.succeeded).toBe(2);
        expect(result.failed).toBe(1);
        expect(result.errors).toContain("Photo 3: Source file not found");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/media/thumbnails/refresh`);
      req.flush(mockResult);
    });
  });
});
