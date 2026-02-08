import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TagService } from "./tag.service";
import { TagDto, PagedResult, PhotoListDto, MediaType } from "../models";

describe("TagService", () => {
  let service: TagService;
  let httpMock: HttpTestingController;
  const baseUrl = "";

  const mockTag: TagDto = {
    id: 1,
    name: "nature",
    color: "#4CAF50",
    photoCount: 15,
  };

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
    albumCount: 0,
    tagCount: 1,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TagService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TagService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Initial state", () => {
    it("should start with empty tags array", () => {
      expect(service.tags()).toEqual([]);
    });

    it("should start with no selected tag", () => {
      expect(service.selectedTag()).toBeNull();
    });

    it("should start with isLoading false", () => {
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getTags", () => {
    it("should fetch all tags", () => {
      const tags = [mockTag];

      service.getTags().subscribe((result) => {
        expect(result).toEqual(tags);
        expect(service.tags()).toEqual(tags);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags`);
      expect(req.request.method).toBe("GET");
      req.flush(tags);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getTags().subscribe();
      expect(service.isLoading()).toBe(true);

      httpMock.expectOne(`${baseUrl}/api/admin/tags`).flush([]);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getTag", () => {
    it("should fetch a single tag and set selectedTag", () => {
      service.getTag(1).subscribe((result) => {
        expect(result).toEqual(mockTag);
        expect(service.selectedTag()).toEqual(mockTag);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags/1`);
      expect(req.request.method).toBe("GET");
      req.flush(mockTag);
    });
  });

  describe("createTag", () => {
    it("should create tag and add to tags list", () => {
      const createRequest = { name: "travel", color: "#2196F3" };
      const createdTag = {
        ...mockTag,
        id: 2,
        name: "travel",
        color: "#2196F3",
      };

      service.createTag(createRequest).subscribe((result) => {
        expect(result).toEqual(createdTag);
        expect(service.tags()).toContainEqual(createdTag);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(createRequest);
      req.flush(createdTag);
    });
  });

  describe("updateTag", () => {
    it("should update tag and update tags list", () => {
      // First load tags
      service.getTags().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags`).flush([mockTag]);

      const updateRequest = { name: "landscape", color: "#8BC34A" };
      const updatedTag = { ...mockTag, name: "landscape", color: "#8BC34A" };

      service.updateTag(1, updateRequest).subscribe((result) => {
        expect(result).toEqual(updatedTag);
        expect(service.tags().find((t) => t.id === 1)?.name).toBe("landscape");
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush(updatedTag);
    });

    it("should update selectedTag if it matches", () => {
      // First select the tag
      service.getTag(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags/1`).flush(mockTag);

      const updateRequest = { name: "landscape" };
      const updatedTag = { ...mockTag, name: "landscape" };

      service.updateTag(1, updateRequest).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags/1`).flush(updatedTag);

      expect(service.selectedTag()?.name).toBe("landscape");
    });
  });

  describe("deleteTag", () => {
    it("should delete tag and remove from tags list", () => {
      // First load tags
      service.getTags().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags`).flush([mockTag]);

      expect(service.tags().length).toBe(1);

      service.deleteTag(1).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);

      expect(service.tags().length).toBe(0);
    });

    it("should clear selectedTag if deleted tag was selected", () => {
      // First select a tag
      service.getTag(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags/1`).flush(mockTag);

      expect(service.selectedTag()).not.toBeNull();

      service.deleteTag(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags/1`).flush(null);

      expect(service.selectedTag()).toBeNull();
    });
  });

  describe("addPhotosToTag", () => {
    it("should add photos to tag", () => {
      const request = { photoIds: [1, 2, 3] };
      const bulkResult = { successCount: 3, failedCount: 0, errors: [] };

      service.addPhotosToTag(1, request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/tags/1/photos`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("removePhotosFromTag", () => {
    it("should remove photos from tag", () => {
      const request = { photoIds: [1, 2] };
      const bulkResult = { successCount: 2, failedCount: 0, errors: [] };

      service.removePhotosFromTag(1, request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/tags/1/photos/remove`,
      );
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("getTagPhotos", () => {
    it("should fetch paginated photos for tag", () => {
      const pagedResult: PagedResult<PhotoListDto> = {
        data: [mockPhotoList],
        pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 },
      };

      service.getTagPhotos(1).subscribe((result) => {
        expect(result).toEqual(pagedResult);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/tags/1/photos`,
      );
      expect(req.request.params.get("page")).toBe("1");
      expect(req.request.params.get("pageSize")).toBe("50");
      req.flush(pagedResult);
    });

    it("should accept custom page and pageSize", () => {
      service.getTagPhotos(1, 3, 30).subscribe();

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/tags/1/photos`,
      );
      expect(req.request.params.get("page")).toBe("3");
      expect(req.request.params.get("pageSize")).toBe("30");
      req.flush({ data: [], pagination: {} });
    });
  });

  describe("clearSelectedTag", () => {
    it("should clear the selected tag", () => {
      // First select a tag
      service.getTag(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/tags/1`).flush(mockTag);

      expect(service.selectedTag()).not.toBeNull();

      service.clearSelectedTag();
      expect(service.selectedTag()).toBeNull();
    });
  });
});
